using Replay.Encoding.Archives;
using Replay.Models.Replay;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Frames;

public class StreamingLevelFixesReader
{
    private readonly ReplayReaderContext _context;
    private readonly FBinaryArchive _archive;

    public StreamingLevelFixesReader(
        ReplayReaderContext context,
        FBinaryArchive archive)
    {
        _context = context;
        _archive = archive;
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
