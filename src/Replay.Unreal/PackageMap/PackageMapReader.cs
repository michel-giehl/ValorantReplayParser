using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Errors;
using Replay.Models.Net;
using Replay.Models.Protocol;
using Replay.Unreal.Bunches;

namespace Replay.Unreal.PackageMap;

public class PackageMapReader
{
    private readonly NetGuidObjectReader _objectReader;

    public PackageMapReader(NetGuidCache netGuidCache)
    {
        _objectReader = new NetGuidObjectReader(netGuidCache);
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
        int recursionDepth) =>
        _objectReader.InternalLoadObject(archive, isExportingNetGuidBunch, recursionDepth);
}