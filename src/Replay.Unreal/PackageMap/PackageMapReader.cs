using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Errors;
using Replay.Models.Protocol;
using Replay.Unreal.Bunches;

namespace Replay.Unreal.PackageMap;

public class PackageMapReader
{
    private const int MaxNetGuidRecursionDepth = 16;

    private readonly NetGuidCache _netGuidCache;

    public PackageMapReader(NetGuidCache netGuidCache)
    {
        _netGuidCache = netGuidCache;
    }

    public void ReceiveNetGUIDBunch(FBitArchive payload, BunchPayloadStats stats)
    {
        var bHasRepLayoutExport = payload.ReadBit();
        if (bHasRepLayoutExport)
        {
            throw new InvalidReplayInfoException(
                "Package-map export with bHasRepLayoutExport is not supported in this parser version.");
        }

        var numGUIDs = payload.ReadInt32();
        if (numGUIDs < 0 || numGUIDs > Constants.MaxGuidCount)
        {
            throw new InvalidReplayInfoException(
                $"Package-map export GUID count {numGUIDs} exceeds maximum {Constants.MaxGuidCount}.");
        }

        for (var i = 0; i < numGUIDs; i++)
        {
            InternalLoadObject(payload, isExportingNetGuidBunch: true, recursionDepth: 0);
            stats.ExportedNetGuidCount++;
        }
    }

    public NetworkGuid InternalLoadObject(
        FBitArchive archive,
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

        ExportFlags exportFlags = ExportFlags.None;
        if (netGuid.IsDefault || isExportingNetGuidBunch)
        {
            exportFlags = (ExportFlags)archive.ReadByte();
        }

        if (!exportFlags.HasFlag(ExportFlags.HasPath))
        {
            return netGuid;
        }

        _ = InternalLoadObject(archive, isExportingNetGuidBunch, recursionDepth + 1);
        var pathName = archive.ReadFString();

        if (exportFlags.HasFlag(ExportFlags.HasNetworkChecksum))
        {
            _ = archive.ReadUInt32();
        }

        _netGuidCache.SetNetGuidPath(netGuid.Value, pathName);
        return netGuid;
    }
}
