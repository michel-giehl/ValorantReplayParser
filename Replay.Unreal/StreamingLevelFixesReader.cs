using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models;

namespace Replay.Unreal;

public class StreamingLevelFixesReader
{
    private readonly ReplayReaderContext _context;
    private readonly FBinaryArchive _archive;
    private readonly ILogger<StreamingLevelFixesReader> _logger;

    public StreamingLevelFixesReader(
        ReplayReaderContext context,
        FBinaryArchive archive,
        ILogger<StreamingLevelFixesReader>? logger = null)
    {
        _context = context;
        _archive = archive;
        _logger = logger ?? NullLogger<StreamingLevelFixesReader>.Instance;
    }

    public void Read()
    {
        var hasStreamingLevelFixes = _context.ReplayHeader.Flags.HasFlag(ReplayHeaderFlags.HasStreamingFixes);

        if (hasStreamingLevelFixes)
        {
            ReadWithFixes();
        }
        else
        {
            ReadWithoutFixes();
        }
    }

    private void ReadWithFixes()
    {
        var numLevels = _archive.ReadIntPacked();
        for (var i = 0; i < numLevels; i++)
        {
            _ = _archive.ReadFString();
        }

        // externalOffset
        _archive.ReadUInt64();
    }

    private void ReadWithoutFixes()
    {
        var numLevels = _archive.ReadIntPacked();
        for (var i = 0; i < numLevels; i++)
        {
            // packageName
            _ = _archive.ReadFString();
            // packageNameToLoad
            _ = _archive.ReadFString();
            // levelTransform
            _ = _archive.ReadFTransform();
        }
    }
}
