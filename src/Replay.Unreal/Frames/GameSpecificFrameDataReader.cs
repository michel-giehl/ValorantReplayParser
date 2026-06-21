using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models;

namespace Replay.Unreal;

public class GameSpecificFrameDataReader
{
    private readonly ReplayReaderContext _context;
    private readonly FBinaryArchive _archive;
    private readonly ILogger<GameSpecificFrameDataReader> _logger;

    public GameSpecificFrameDataReader(
        ReplayReaderContext context,
        FBinaryArchive archive,
        ILogger<GameSpecificFrameDataReader>? logger = null)
    {
        _context = context;
        _archive = archive;
        _logger = logger ?? NullLogger<GameSpecificFrameDataReader>.Instance;
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

        _logger.LogTrace("Skipping {ByteCount} bytes of game-specific frame data.", skipExternalOffset);
        _archive.Skip(checked((long)skipExternalOffset));
    }
}
