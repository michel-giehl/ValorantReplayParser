using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models;

namespace Replay.Unreal;

public class ExportDataReader
{
    private readonly FBinaryArchive _archive;
    private readonly ILogger<ExportDataReader> _logger;

    public ExportDataReader(FBinaryArchive archive, ILogger<ExportDataReader>? logger = null)
    {
        _archive = archive;
        _logger = logger ?? NullLogger<ExportDataReader>.Instance;
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

        for (var i = 0; i < numLayoutCmdExports; i++)
        {
            var pathNameIndex = _archive.ReadIntPacked();
            var isExported = _archive.ReadIntPacked() == 1;

            _logger.LogTrace(
                "Read net-field export for path index {PathNameIndex}; exported group: {IsExported}.",
                pathNameIndex,
                isExported);
            if (isExported)
            {
                ReadExportedGroup();
            }

            ReadNetFieldExport();
        }
    }

    private void ReadExportedGroup()
    {
        var pathName = _archive.ReadFString();
        var numExports = _archive.ReadIntPacked();
        _logger.LogDebug("Read exported net-field group {PathName} with {ExportCount} exports.", pathName, numExports);
    }

    private void ReadNetFieldExport()
    {
        var isExported = _archive.ReadBoolean();
        if (!isExported)
        {
            return;
        }

        var handle = _archive.ReadIntPacked();
        _ = _archive.ReadUInt32();
        var name = _archive.ReadFName();
        _logger.LogTrace("Read net-field export {Name} with handle {Handle}.", name, handle);
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

            _archive.Skip(size);
        }
    }
}
