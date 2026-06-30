using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Errors;
using Replay.Unreal.PackageMap;

namespace Replay.Unreal.Exports;

public class ExportDataReader
{
    private readonly FBinaryArchive _archive;
    private readonly NetGuidCache _netGuidCache;
    private readonly NetGuidObjectReader _objectReader;
    private readonly ILogger<ExportDataReader> _logger;
    private readonly Action<NetFieldExportGroup>? _exportGroupChanged;

    public ExportDataReader(
        FBinaryArchive archive,
        NetGuidCache netGuidCache,
        ILogger<ExportDataReader>? logger = null,
        Action<NetFieldExportGroup>? exportGroupChanged = null)
    {
        _archive = archive;
        _netGuidCache = netGuidCache;
        _objectReader = new NetGuidObjectReader(netGuidCache);
        _logger = logger ?? NullLogger<ExportDataReader>.Instance;
        _exportGroupChanged = exportGroupChanged;
    }

    public void Read()
    {
        ReadNetFieldExports();
        ReadExportGuids();
    }

    public void ReadNetFieldExports()
    {
        var numLayoutCmdExports = _archive.ReadIntPacked();
        if (numLayoutCmdExports > 0)
        {
#pragma warning disable CA1873
            _logger.LogDebug("Reading {ExportCount} net-field layout command exports.", numLayoutCmdExports);
#pragma warning restore CA1873
        }

        Dictionary<uint, NetFieldExportGroup>? changedGroups = null;
        for (var i = 0; i < numLayoutCmdExports; i++)
        {
            var pathNameIndex = _archive.ReadIntPacked();
            var isExported = _archive.ReadIntPacked() == 1;

            NetFieldExportGroup group;
            if (isExported)
            {
                group = _netGuidCache.AddExportGroup(ReadExportedGroup(pathNameIndex));
                RecordChangedGroup(ref changedGroups, group);
            }
            else
            {
                if (!_netGuidCache.TryGetExportGroup(pathNameIndex, out var existingGroup))
                {
                    throw new InvalidReplayInfoException(
                        $"Net-field export references unknown path index {pathNameIndex}.");
                }

                group = existingGroup;
            }

            var netFieldExport = ReadNetFieldExport();
            if (netFieldExport is null)
            {
                continue;
            }

            if (netFieldExport.Handle >= group.NetFieldExportsLength)
            {
                _logger.LogWarning(
                    "Ignoring net-field export {Name} with handle {Handle}; group {PathName} has length {ExportCount}.",
                    netFieldExport.Name,
                    netFieldExport.Handle,
                    group.PathName,
                    group.NetFieldExportsLength);
                continue;
            }

            group.NetFieldExports[netFieldExport.Handle] = netFieldExport;
            RecordChangedGroup(ref changedGroups, group);
        }

        if (changedGroups is null)
        {
            return;
        }

        foreach (var changedGroup in changedGroups.Values)
        {
            _exportGroupChanged!(changedGroup);
        }
    }

    private void RecordChangedGroup(
        ref Dictionary<uint, NetFieldExportGroup>? changedGroups,
        NetFieldExportGroup group)
    {
        if (_exportGroupChanged is null)
        {
            return;
        }

        changedGroups ??= [];
        changedGroups[group.PathNameIndex] = group;
    }

    private NetFieldExportGroup ReadExportedGroup(uint pathNameIndex)
    {
        var pathName = _archive.ReadFString();
        var numExports = _archive.ReadIntPacked();

        return new NetFieldExportGroup
        {
            PathName = pathName,
            PathNameIndex = pathNameIndex,
            NetFieldExports = new NetFieldExport?[checked((int)numExports)],
        };
    }

    private NetFieldExport? ReadNetFieldExport()
    {
        var isExported = _archive.ReadBoolean();
        if (!isExported)
        {
            return null;
        }

        var handle = _archive.ReadIntPacked();
        var compatibleChecksum = _archive.ReadUInt32();
        var name = _archive.ReadFName();

        return new NetFieldExport
        {
            Handle = handle,
            CompatibleChecksum = compatibleChecksum,
            Name = name,
        };
    }

    public void ReadExportGuids()
    {
        var numGuids = _archive.ReadIntPacked();
        if (numGuids > 0)
        {
#pragma warning disable CA1873
            _logger.LogDebug("Reading {GuidCount} exported net GUID payloads.", numGuids);
#pragma warning restore CA1873
        }

        for (var i = 0; i < numGuids; i++)
        {
            var size = _archive.ReadInt32();
            if (size < 0)
            {
                throw new InvalidReplayInfoException($"Export GUID payload size {size} is negative.");
            }

            var payloadArchive = new FBinaryArchive(_archive.ReadBytes(size));
            _objectReader.InternalLoadObject(payloadArchive, isExportingNetGuidBunch: true, recursionDepth: 0);
            payloadArchive.EnsureFullyConsumed("ReadExportGuidPayload");
        }
    }
}