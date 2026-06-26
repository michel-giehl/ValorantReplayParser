using Replay.Encoding.Archives;
using Replay.Models.Replay;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Frames;

public class GameSpecificFrameDataReader
{
    private readonly ReplayReaderContext _context;
    private readonly FBinaryArchive _archive;

    public GameSpecificFrameDataReader(
        ReplayReaderContext context,
        FBinaryArchive archive)
    {
        _context = context;
        _archive = archive;
    }

    public void Read()
    {
        if (!_context.ReplayHeader.Flags.HasFlag(ReplayHeaderFlags.GameSpecificFrameData))
        {
            return;
        }

        var skipExternalOffset = _archive.ReadUInt64();
        if (skipExternalOffset <= 0)
        {
            return;
        }

        _archive.Skip(checked((long)skipExternalOffset));
    }
}
