using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Errors;

namespace Replay.Unreal.PackageMap;

internal sealed class NetGuidObjectReader
{
    private const int MaxNetGuidRecursionDepth = 16;

    private readonly NetGuidCache _netGuidCache;

    public NetGuidObjectReader(NetGuidCache netGuidCache)
    {
        _netGuidCache = netGuidCache;
    }

    public NetworkGuid InternalLoadObject(
        FArchive archive,
        bool isExportingNetGuidBunch,
        int recursionDepth)
    {
        if (recursionDepth >= MaxNetGuidRecursionDepth)
        {
            throw new InvalidReplayInfoException(
                $"Exported net GUID recursion depth exceeded {MaxNetGuidRecursionDepth}.");
        }

        var netGuid = new NetworkGuid(archive.ReadIntPacked());
        if (!netGuid.IsValid)
        {
            return netGuid;
        }

        var exportFlags = ExportFlags.None;
        if (netGuid.IsDefault || isExportingNetGuidBunch)
        {
            exportFlags = (ExportFlags)archive.ReadByte();
        }

        if (!exportFlags.HasFlag(ExportFlags.HasPath))
        {
            return netGuid;
        }

        var outerNetGuid = InternalLoadObject(archive, isExportingNetGuidBunch, recursionDepth + 1);
        var pathName = archive.ReadFString();

        if (exportFlags.HasFlag(ExportFlags.HasNetworkChecksum))
        {
            _ = archive.ReadUInt32();
        }

        _netGuidCache.SetNetGuidPath(netGuid.Value, pathName, outerNetGuid);
        return netGuid;
    }
}