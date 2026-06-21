using Replay.Encoding.Archives;
using Replay.Models;

namespace Replay.Unreal;

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
        _contentBlockFramer = new ContentBlockFramer(_packageMapReader);
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
                    };

                    SerializeNewActor(payload, channelState, header.bClose);

                    _context.ChannelStates[header.ChIndex] = channelState;
                    _context.ActorChannelOpens.Add(channelState);
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

        if (header.bClose)
        {
            if (_context.ChannelStates.TryGetValue(header.ChIndex, out var closingChannel))
            {
                closingChannel.IsOpen = false;
            }
        }

        _context.ChannelStates.TryGetValue(header.ChIndex, out var channel);

        if (openedDynamicActor && !payload.AtEnd)
        {
            // Dynamic actor opens can include game-specific OnSerializeNewActor data before content blocks.
            stats.DynamicOpenPayloadBunchCount++;
            stats.DynamicOpenPayloadBitsSkipped += payload.BitsRemaining;
            payload.SkipRemaining();
            return;
        }

        if (!payload.AtEnd)
        {
            if (channel is null)
            {
                payload.SkipRemaining();
                return;
            }

            _contentBlockFramer.FrameContentBlocks(payload, channel, stats);
        }

        if (payload.AtEnd) return;
        var unconsumed = payload.BitsRemaining;
        payload.SkipBits(unconsumed);
        stats.MalformedPayloadCount++;
        stats.TrailingPayloadCount++;
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
        _context.ChannelStates.Clear();
        _context.ActorChannelOpens.Clear();
    }
}
