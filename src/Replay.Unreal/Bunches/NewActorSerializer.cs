using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Unreal;
using Replay.Unreal.Channels;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Parsing;

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

        ReadDynamicActorSpawnData(payload, channelState);
    }

    private void ReadDynamicActorSpawnData(FBitArchive payload, ActorChannelState channelState)
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
        channelState.SpawnLocation = ArchiveVectorReaders.ReadOptionalQuantizedVector(
            payload,
            new FVector(0, 0, 0),
            scaleFactor: 10);

        if (payload.ReadBit())
        {
            channelState.SpawnRotation = ArchiveVectorReaders.ReadRotationShort(payload);
        }

        channelState.SpawnScale = ArchiveVectorReaders.ReadOptionalQuantizedVector(
            payload,
            new FVector(1, 1, 1),
            scaleFactor: 10);
        channelState.SpawnVelocity = ArchiveVectorReaders.ReadOptionalQuantizedVector(
            payload,
            new FVector(0, 0, 0),
            scaleFactor: 10);
    }
}