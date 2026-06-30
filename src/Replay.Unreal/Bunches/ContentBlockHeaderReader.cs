using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Unreal.Channels;
using Replay.Unreal.PackageMap;

namespace Replay.Unreal.Bunches;

internal sealed class ContentBlockHeaderReader
{
    private readonly PackageMapReader _packageMapReader;

    public ContentBlockHeaderReader(PackageMapReader packageMapReader)
    {
        _packageMapReader = packageMapReader;
    }

    public ContentBlockHeader Read(FBitArchive payload, ActorChannelState channel)
    {
        var hasRepLayout = payload.ReadBit();
        if (payload.ReadBit())
        {
            return new ContentBlockHeader
            {
                HasRepLayout = hasRepLayout,
                IsActor = true,
                OuterNetGuid = channel.ActorNetGuid,
            };
        }

        return ReadSubobject(payload, channel.ActorNetGuid, hasRepLayout);
    }

    private ContentBlockHeader ReadSubobject(
        FBitArchive payload,
        NetworkGuid actorNetGuid,
        bool hasRepLayout)
    {
        var objectNetGuid = LoadObject(payload);
        var isStablyNamed = payload.ReadBit();
        var outerNetGuid = actorNetGuid;

        if (isStablyNamed)
        {
            return CreateSubobjectHeader(hasRepLayout, objectNetGuid, default, outerNetGuid, isStablyNamed);
        }

        if (payload.ReadBit())
        {
            return CreateDeletedHeader(hasRepLayout, objectNetGuid, outerNetGuid, payload.ReadByte());
        }

        var classNetGuid = LoadObject(payload);
        if (!classNetGuid.IsValid)
        {
            return CreateDeletedHeader(hasRepLayout, objectNetGuid, outerNetGuid, deleteFlags: 0);
        }

        if (!payload.ReadBit())
        {
            outerNetGuid = LoadObject(payload);
        }

        return CreateSubobjectHeader(hasRepLayout, objectNetGuid, classNetGuid, outerNetGuid, isStablyNamed);
    }

    private NetworkGuid LoadObject(FBitArchive payload) =>
        _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);

    private static ContentBlockHeader CreateSubobjectHeader(
        bool hasRepLayout,
        NetworkGuid objectNetGuid,
        NetworkGuid classNetGuid,
        NetworkGuid outerNetGuid,
        bool isStablyNamed) =>
        new()
        {
            HasRepLayout = hasRepLayout,
            ObjectNetGuid = objectNetGuid,
            ClassNetGuid = classNetGuid,
            OuterNetGuid = outerNetGuid,
            IsStablyNamed = isStablyNamed,
        };

    private static ContentBlockHeader CreateDeletedHeader(
        bool hasRepLayout,
        NetworkGuid objectNetGuid,
        NetworkGuid outerNetGuid,
        byte deleteFlags) =>
        new()
        {
            HasRepLayout = hasRepLayout,
            ObjectNetGuid = objectNetGuid,
            OuterNetGuid = outerNetGuid,
            DeleteFlags = deleteFlags,
            IsDeleted = true,
        };
}