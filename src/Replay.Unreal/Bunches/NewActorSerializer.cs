using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Unreal;
using Replay.Unreal.Channels;
using Replay.Unreal.PackageMap;

namespace Replay.Unreal.Bunches;

internal sealed class NewActorSerializer : INewActorSerializer
{
    private readonly PackageMapReader _packageMapReader;
    private readonly NetGuidCache _netGuidCache;

    public NewActorSerializer(PackageMapReader packageMapReader, NetGuidCache netGuidCache)
    {
        _packageMapReader = packageMapReader;
        _netGuidCache = netGuidCache;
    }

    public void Serialize(FBitArchive payload, ActorChannelState channelState, bool isClosingChannel)
    {
        var actorNetGuid = _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);
        channelState.ActorNetGuid = actorNetGuid;
        if (_netGuidCache.TryGetPath(actorNetGuid.Value, out var actorPath))
        {
            channelState.ActorPath = actorPath;
        }

        if (!actorNetGuid.IsDynamic || payload.AtEnd && isClosingChannel)
        {
            return;
        }

        ReadDynamicActorState(payload, channelState);
    }

    private void ReadDynamicActorState(FBitArchive payload, ActorChannelState channelState)
    {
        var archetype = _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);
        channelState.ArchetypeNetGuid = archetype;
        if (_netGuidCache.TryGetPath(archetype.Value, out var archetypePath))
        {
            channelState.ArchetypePath = archetypePath;
        }

        if (_netGuidCache.TryGetOuterPath(archetype.Value, out var replicationClassPath))
        {
            channelState.ReplicationClassPath = replicationClassPath;
        }

        channelState.LevelGuid = _packageMapReader.InternalLoadObject(
            payload,
            isExportingNetGuidBunch: false,
            recursionDepth: 0);
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
            return ReadPackedQuantizedVector(payload, componentBitCount, extraInfo, scaleFactor);
        }

        return extraInfo == 0
            ? ReadSinglePrecisionVector(payload, scaleFactor)
            : ReadDoublePrecisionVector(payload, scaleFactor);
    }

    private static FVector ReadPackedQuantizedVector(
        FBitArchive payload,
        int componentBitCount,
        uint extraInfo,
        int scaleFactor)
    {
        var x = ReadBitsToLong(payload, componentBitCount);
        var y = ReadBitsToLong(payload, componentBitCount);
        var z = ReadBitsToLong(payload, componentBitCount);
        var signBit = 1UL << componentBitCount - 1;

        double fX = (long)(x ^ signBit) - (long)signBit;
        double fY = (long)(y ^ signBit) - (long)signBit;
        double fZ = (long)(z ^ signBit) - (long)signBit;

        if (extraInfo > 0)
        {
            fX /= scaleFactor;
            fY /= scaleFactor;
            fZ /= scaleFactor;
        }

        return new FVector(fX, fY, fZ)
        {
            Bits = componentBitCount,
            ScaleFactor = scaleFactor,
        };
    }

    private static FVector ReadSinglePrecisionVector(FBitArchive payload, int scaleFactor) =>
        new(payload.ReadSingle(), payload.ReadSingle(), payload.ReadSingle())
        {
            Bits = 32,
            ScaleFactor = scaleFactor,
        };

    private static FVector ReadDoublePrecisionVector(FBitArchive payload, int scaleFactor) =>
        new(payload.ReadDouble(), payload.ReadDouble(), payload.ReadDouble())
        {
            Bits = 64,
            ScaleFactor = scaleFactor,
        };

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
}
