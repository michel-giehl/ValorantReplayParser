using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Errors;

namespace Replay.Unreal.Exports;

public class ExportDataReader
{
    private const int MaxNetGuidRecursionDepth = 16;

    private readonly FBinaryArchive _archive;
    private readonly NetGuidCache _netGuidCache;
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
        _logger = logger ?? NullLogger<ExportDataReader>.Instance;
        _exportGroupChanged = exportGroupChanged;
    }

    public void Read()
    {
        _logger.LogTrace("Reading replay export data at offset {Offset}.", _archive.Position);
        ReadNetFieldExports();
        ReadExportGuids();
    }

    public void ReadNetFieldExports()
    {
        var numLayoutCmdExports = _archive.ReadIntPacked();
        if (numLayoutCmdExports > 0)
        {
            _logger.LogDebug("Reading {ExportCount} net-field layout command exports.", numLayoutCmdExports);
        }
        else
        {
            _logger.LogTrace("Reading 0 net-field layout command exports.");
        }

        Dictionary<uint, NetFieldExportGroup>? changedGroups = null;
        for (var i = 0; i < numLayoutCmdExports; i++)
        {
            var pathNameIndex = _archive.ReadIntPacked();
            var isExported = _archive.ReadIntPacked() == 1;

            _logger.LogTrace(
                "Read net-field export for path index {PathNameIndex}; exported group: {IsExported}.",
                pathNameIndex,
                isExported);
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
        _logger.LogDebug("Read exported net-field group {PathName} with {ExportCount} exports.", pathName, numExports);

        return new NetFieldExportGroup
        {
            PathName = pathName,
            PathNameIndex = pathNameIndex,
            NetFieldExportsLength = numExports,
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
        _logger.LogTrace("Read net-field export {Name} with handle {Handle}.", name, handle);

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
            _logger.LogDebug("Reading {GuidCount} exported net GUID payloads.", numGuids);
        }
        else
        {
            _logger.LogTrace("Reading 0 exported net GUID payloads.");
        }
        for (var i = 0; i < numGuids; i++)
        {
            var size = _archive.ReadInt32();
            if (size < 0)
            {
                throw new InvalidReplayInfoException($"Export GUID payload size {size} is negative.");
            }

            var payloadArchive = new FBinaryArchive(_archive.ReadBytes(size));
            InternalLoadObject(payloadArchive, isExportingNetGuidBunch: true, recursionDepth: 0);
            payloadArchive.EnsureFullyConsumed("ReadExportGuidPayload");
        }
    }

    private NetworkGuid InternalLoadObject(
        FBinaryArchive archive,
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
            exportFlags = archive.ReadByteAsEnum<ExportFlags>();
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
