using Replay.Encoding.Archives;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Unreal.Channels;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Readers;
using Replay.Unreal.World;

namespace Replay.Unreal.Bunches;

public sealed class BunchPayloadPipeline
{
    private readonly ReplayReaderContext _context;
    private readonly PackageMapReader _packageMapReader;
    private readonly PartialBunchAccumulator _partialBunchAccumulator;
    private readonly ContentBlockFramer _contentBlockFramer;

    public BunchPayloadPipeline(ReplayReaderContext context)
    {
        _context = context;
        _packageMapReader = new PackageMapReader(context.NetGuidCache);
        _partialBunchAccumulator = new PartialBunchAccumulator(_packageMapReader);
        _contentBlockFramer = new ContentBlockFramer(
            _packageMapReader,
            context.NetGuidCache,
            context.WorldState,
            context.EventSink,
            context.ExportBindingRegistry);
    }

    public void HandleBunchPayload(ref RawBunchHeader header, FBitArchive payload)
    {
        var stats = _context.BunchPayloadStats;
        stats.BunchCount++;

        if (payload.BitLength > 0)
        {
            stats.PayloadBunchCount++;
        }

        if (header.bHasPackageMapExports)
        {
            stats.PackageMapExportBunchCount++;
            _packageMapReader.ReceiveNetGUIDBunch(payload, stats);
        }

        if (header.bPartial)
        {
            _partialBunchAccumulator.AddFragment(header.ChIndex, ref header, payload, stats);

            if (!header.bPartialFinal || header.HasPartialError)
            {
                return;
            }
        }

        if (header is { bPartialFinal: true, HasPartialError: false })
        {
            if (_partialBunchAccumulator.TryComplete(header.ChIndex, out var stitchedBuffer, out var stitchedBitCount, out header))
            {
                using (stitchedBuffer)
                {
                    var stitchedPayload = new BitArchiveReader(stitchedBuffer, stitchedBitCount);
                    ProcessCompletePayload(ref header, stitchedPayload, stats);
                }

                return;
            }
        }

        if (!header.bPartial || header.IsPartialCompleted)
        {
            ProcessCompletePayload(ref header, payload, stats);
        }
    }

    private void ProcessCompletePayload(ref RawBunchHeader header, FBitArchive payload, BunchPayloadStats stats)
    {
        try
        {
            ProcessCompletePayloadCore(ref header, payload, stats);
        }
        catch (ArchiveReadException)
        {
            stats.MalformedPayloadCount++;
            stats.MalformedPayloadExceptionCount++;
        }
    }

    private void ProcessCompletePayloadCore(ref RawBunchHeader header, FBitArchive payload, BunchPayloadStats stats)
    {
        if (header.bHasMustBeMappedGUIDs)
        {
            try
            {
                var count = payload.ReadUInt16();
                for (var i = 0; i < count; i++)
                {
                    // guid
                    _ = payload.ReadIntPacked();
                    stats.MustBeMappedGuidCount++;
                }
            }
            catch (ArchiveReadException)
            {
                stats.MalformedPayloadCount++;
                stats.MalformedMustBeMappedGuidCount++;
                payload.SkipRemaining();
                return;
            }
        }

        var openedDynamicActor = false;
        if (header.bOpen)
        {
            var hasOpenChannel = _context.ChannelStates.TryGetValue(header.ChIndex, out var ch) && ch.IsOpen;
            if (!hasOpenChannel)
            {
                try
                {
                    var channelState = new ActorChannelState
                    {
                        ChannelIndex = header.ChIndex,
                        IsOpen = true,
                        OpenPacketId = header.PacketId,
                        OpenTimeSeconds = _context.CurrentTimeSeconds,
                    };

                    SerializeNewActor(payload, channelState, header.bClose);

                    _context.ChannelStates[header.ChIndex] = channelState;
                    _context.ActorChannelOpens.Add(channelState);
                    OpenActor(channelState, stats);
                    openedDynamicActor = channelState.ActorNetGuid.IsDynamic;
                    stats.ActorChannelOpenCount++;
                    stats.ActorSerializeNewActorCount++;
                }
                catch (ArchiveReadException)
                {
                    stats.MalformedPayloadCount++;
                    stats.MalformedActorOpenCount++;
                    payload.SkipRemaining();
                    return;
                }
            }
        }

        _context.ChannelStates.TryGetValue(header.ChIndex, out var channel);

        if (openedDynamicActor && !payload.AtEnd)
        {
            // Dynamic actor opens can include game-specific OnSerializeNewActor data before content blocks.
            stats.DynamicOpenPayloadBunchCount++;
            stats.DynamicOpenPayloadBitsSkipped += payload.BitsRemaining;
            payload.SkipRemaining();
        }
        else if (!payload.AtEnd)
        {
            if (channel is null)
            {
                payload.SkipRemaining();
            }
            else
            {
                _contentBlockFramer.FrameContentBlocks(
                    payload,
                    channel,
                    stats,
                    _context.CurrentTimeSeconds,
                    header.PacketId);
            }
        }

        if (header.bClose && channel is not null)
        {
            CloseActorChannel(channel, header, stats);
        }

        if (payload.AtEnd) return;
        var unconsumed = payload.BitsRemaining;
        payload.SkipBits(unconsumed);
        stats.MalformedPayloadCount++;
        stats.TrailingPayloadCount++;
    }

    private void OpenActor(ActorChannelState channel, BunchPayloadStats stats)
    {
        var actorNetGuid = channel.ActorNetGuid;
        if (!actorNetGuid.IsValid)
        {
            return;
        }

        var worldState = _context.WorldState;
        var isNew = !worldState.ActorsByNetGuid.TryGetValue(actorNetGuid.Value, out var actor);
        if (actor?.LifecycleStatus == ActorLifecycleStatus.Destroyed)
        {
            throw new InvalidReplayInfoException(
                $"Actor net GUID {actorNetGuid.Value} was reopened after destruction.");
        }

        if (isNew)
        {
            actor = new ActorState
            {
                NetGuid = actorNetGuid,
                ChannelIndex = channel.ChannelIndex,
                IsDynamic = actorNetGuid.IsDynamic,
                LifecycleStatus = ActorLifecycleStatus.Open,
                ActorPath = channel.ActorPath,
                ArchetypeNetGuid = channel.ArchetypeNetGuid,
                ArchetypePath = channel.ArchetypePath,
                LevelNetGuid = channel.LevelGuid,
                FirstObservedTimeSeconds = channel.OpenTimeSeconds,
                FirstObservedPacketId = channel.OpenPacketId,
                OpenTimeSeconds = channel.OpenTimeSeconds,
                OpenPacketId = channel.OpenPacketId,
                OpenCount = 1,
                SpawnTimeSeconds = actorNetGuid.IsDynamic ? channel.OpenTimeSeconds : null,
                SpawnPacketId = actorNetGuid.IsDynamic ? channel.OpenPacketId : null,
                SpawnLocation = channel.SpawnLocation,
                SpawnRotation = channel.SpawnRotation,
                SpawnScale = channel.SpawnScale,
                SpawnVelocity = channel.SpawnVelocity,
                Location = channel.SpawnLocation,
                Rotation = channel.SpawnRotation,
                Scale = channel.SpawnScale,
                Velocity = channel.SpawnVelocity,
            };
            worldState.ActorsByNetGuid.Add(actorNetGuid.Value, actor);
            stats.ActorCreatedCount++;
        }
        else
        {
            actor!.ChannelIndex = channel.ChannelIndex;
            actor.LifecycleStatus = ActorLifecycleStatus.Open;
            actor.ActorPath ??= channel.ActorPath;
            actor.ArchetypeNetGuid = channel.ArchetypeNetGuid.IsValid
                ? channel.ArchetypeNetGuid
                : actor.ArchetypeNetGuid;
            actor.ArchetypePath ??= channel.ArchetypePath;
            actor.LevelNetGuid = channel.LevelGuid.IsValid ? channel.LevelGuid : actor.LevelNetGuid;
            actor.OpenTimeSeconds = channel.OpenTimeSeconds;
            actor.OpenPacketId = channel.OpenPacketId;
            actor.OpenCount++;
            actor.CloseTimeSeconds = null;
            actor.ClosePacketId = null;
            actor.CloseReason = null;
        }

        _context.EventSink.Emit(new ActorOpened(
            channel.OpenTimeSeconds,
            channel.OpenPacketId,
            actorNetGuid.Value,
            channel.ChannelIndex,
            actorNetGuid.IsDynamic,
            actor.ActorPath,
            actor.ArchetypePath));

        if (!isNew || !actorNetGuid.IsDynamic)
        {
            return;
        }

        _context.EventSink.Emit(new ActorSpawned(
            channel.OpenTimeSeconds,
            channel.OpenPacketId,
            actorNetGuid.Value,
            channel.ChannelIndex,
            actor.ArchetypePath,
            actor.SpawnLocation,
            actor.SpawnRotation,
            actor.SpawnScale,
            actor.SpawnVelocity));
    }

    private void CloseActorChannel(
        ActorChannelState channel,
        RawBunchHeader header,
        BunchPayloadStats stats)
    {
        if (!channel.IsOpen)
        {
            return;
        }

        channel.IsOpen = false;
        channel.IsDormant = header.bDormant;
        channel.ClosePacketId = header.PacketId;
        channel.CloseTimeSeconds = _context.CurrentTimeSeconds;
        channel.CloseReason = header.CloseReason;
        stats.ActorChannelCloseCount++;

        if (!_context.WorldState.ActorsByNetGuid.TryGetValue(channel.ActorNetGuid.Value, out var actor))
        {
            return;
        }

        actor.ClosePacketId = header.PacketId;
        actor.CloseTimeSeconds = _context.CurrentTimeSeconds;
        actor.CloseReason = header.CloseReason;
        actor.LifecycleStatus = header.CloseReason switch
        {
            ChannelCloseReason.Destroyed => ActorLifecycleStatus.Destroyed,
            ChannelCloseReason.Dormancy => ActorLifecycleStatus.Dormant,
            _ => ActorLifecycleStatus.Closed,
        };

        _context.EventSink.Emit(new ActorClosed(
            _context.CurrentTimeSeconds,
            header.PacketId,
            actor.NetGuid.Value,
            channel.ChannelIndex,
            header.CloseReason));

        if (header.CloseReason == ChannelCloseReason.Dormancy)
        {
            stats.ActorDormantCount++;
        }

        if (header.CloseReason != ChannelCloseReason.Destroyed)
        {
            return;
        }

        actor.DestroyTimeSeconds = _context.CurrentTimeSeconds;
        actor.DestroyPacketId = header.PacketId;
        stats.ActorDestroyedCount++;

        foreach (var objectNetGuid in actor.SubobjectNetGuids)
        {
            if (!_context.WorldState.ObjectsByNetGuid.TryGetValue(objectNetGuid, out var objectState) ||
                !objectState.IsActive)
            {
                continue;
            }

            objectState.IsActive = false;
            objectState.DestroyTimeSeconds = _context.CurrentTimeSeconds;
            objectState.DestroyPacketId = header.PacketId;
            objectState.DeleteFlags = 0;
            stats.SubobjectDestroyedCount++;
            _context.EventSink.Emit(new SubobjectDestroyed(
                _context.CurrentTimeSeconds,
                header.PacketId,
                objectState.NetGuid.Value,
                actor.NetGuid.Value,
                channel.ChannelIndex,
                DeleteFlags: 0,
                DestroyedWithActor: true));
        }

        _context.EventSink.Emit(new ActorDestroyed(
            _context.CurrentTimeSeconds,
            header.PacketId,
            actor.NetGuid.Value,
            channel.ChannelIndex));
    }

    private void SerializeNewActor(
        FBitArchive payload,
        ActorChannelState channelState,
        bool isClosingChannel)
    {
        var actorNetGuid = _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);
        channelState.ActorNetGuid = actorNetGuid;
        if (_context.NetGuidCache.TryGetPath(actorNetGuid.Value, out var actorPath))
        {
            channelState.ActorPath = actorPath;
        }

        if (!actorNetGuid.IsDynamic)
        {
            return;
        }

        if (payload.AtEnd && isClosingChannel)
        {
            return;
        }

        var archetype = _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);
        channelState.ArchetypeNetGuid = archetype;
        if (_context.NetGuidCache.TryGetPath(archetype.Value, out var archetypePath))
        {
            channelState.ArchetypePath = archetypePath;
        }

        var level = _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);
        channelState.LevelGuid = level;

        channelState.SpawnLocation = ConditionallyReadQuantizedVector(payload, new FVector(0, 0, 0));

        if (payload.ReadBit())
        {
            channelState.SpawnRotation = ReadRotationShort(payload);
        }

        channelState.SpawnScale = ConditionallyReadQuantizedVector(payload, new FVector(1, 1, 1));
        channelState.SpawnVelocity = ConditionallyReadQuantizedVector(payload, new FVector(0, 0, 0));
    }

    private static FVector ConditionallyReadQuantizedVector(FBitArchive payload, FVector defaultVector)
    {
        if (!payload.ReadBit())
        {
            return defaultVector;
        }

        var shouldQuantize = payload.ReadBit();
        return shouldQuantize
            ? ReadQuantizedVector(payload, scaleFactor: 10)
            : ReadFVector(payload);
    }

    private static FVector ReadFVector(FBitArchive payload) =>
        new(payload.ReadDouble(), payload.ReadDouble(), payload.ReadDouble())
        {
            Bits = 64,
        };

    private static FVector ReadQuantizedVector(FBitArchive payload, int scaleFactor)
    {
        var componentBitCountAndExtraInfo = payload.ReadSerializedInt(1 << 7);
        var componentBitCount = (int)(componentBitCountAndExtraInfo & 63U);
        var extraInfo = componentBitCountAndExtraInfo >> 6;

        if (componentBitCount > 0U)
        {
            var x = ReadBitsToLong(payload, componentBitCount);
            var y = ReadBitsToLong(payload, componentBitCount);
            var z = ReadBitsToLong(payload, componentBitCount);

            var signBit = 1UL << componentBitCount - 1;

            double fX = (long)(x ^ signBit) - (long)signBit;
            double fY = (long)(y ^ signBit) - (long)signBit;
            double fZ = (long)(z ^ signBit) - (long)signBit;

            if (extraInfo <= 0)
                return new FVector(fX, fY, fZ)
                {
                    Bits = componentBitCount,
                    ScaleFactor = scaleFactor,
                };
            fX /= scaleFactor;
            fY /= scaleFactor;
            fZ /= scaleFactor;

            return new FVector(fX, fY, fZ)
            {
                Bits = componentBitCount,
                ScaleFactor = scaleFactor,
            };
        }

        if (extraInfo == 0)
        {
            double x = payload.ReadSingle();
            double y = payload.ReadSingle();
            double z = payload.ReadSingle();

            return new FVector(x, y, z)
            {
                Bits = 32,
                ScaleFactor = scaleFactor,
            };
        }
        else
        {
            var x = payload.ReadDouble();
            var y = payload.ReadDouble();
            var z = payload.ReadDouble();

            return new FVector(x, y, z)
            {
                Bits = 64,
                ScaleFactor = scaleFactor,
            };
        }
    }

    private static ulong ReadBitsToLong(FBitArchive payload, int bitCount) =>
        payload.ReadBitsToUInt64(bitCount);

    private static FRotator ReadRotationShort(FBitArchive payload) =>
        new(
            ReadCompressedShortRotatorComponent(payload),
            ReadCompressedShortRotatorComponent(payload),
            ReadCompressedShortRotatorComponent(payload));

    private static float ReadCompressedShortRotatorComponent(FBitArchive payload) =>
        payload.ReadBit() ? DecompressShortAngle(payload.ReadUInt16()) : 0.0f;

    private static float DecompressShortAngle(ushort value)
    {
        const float scale = 360.0f / 65536.0f;
        return value * scale;
    }

    internal void Reset()
    {
        _partialBunchAccumulator.Reset();
        _context.WorldState.Reset();
    }
}
