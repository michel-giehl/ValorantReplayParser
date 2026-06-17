using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models;

namespace Replay.Unreal;

public class PlaybackPacketReader
{
    private readonly ReplayReaderContext _context;
    private readonly ReplayDataChunkInfo _dataChunk;
    private readonly FBinaryArchive _archive;
    private readonly ILogger<PlaybackPacketReader> _logger;
    private readonly ILoggerFactory? _loggerFactory;

    public PlaybackPacketReader(
        ReplayReaderContext context,
        ReplayDataChunkInfo dataChunk,
        FBinaryArchive archive,
        ILogger<PlaybackPacketReader>? logger = null,
        ILoggerFactory? loggerFactory = null)
    {
        _context = context;
        _dataChunk = dataChunk;
        _archive = archive;
        _logger = logger ?? NullLogger<PlaybackPacketReader>.Instance;
        _loggerFactory = loggerFactory;
    }

    public void Read()
    {
        while (!_archive.AtEnd)
        {
            ReadDemoFrameIntoPlaybackPackets();
        }
    }

    private void ReadDemoFrameIntoPlaybackPackets()
    {
        var currentLevelIndex = _archive.ReadInt32();

        var timeSeconds = _archive.ReadSingle();
        _logger.LogTrace("Read playback frame at {TimeSeconds} seconds.", timeSeconds);

        new ExportDataReader(_archive, _loggerFactory?.CreateLogger<ExportDataReader>()).Read();

        new StreamingLevelFixesReader(_context, _archive, _loggerFactory?.CreateLogger<StreamingLevelFixesReader>()).Read();
        ReadExternalData();
        new GameSpecificFrameDataReader(_context, _archive, _loggerFactory?.CreateLogger<GameSpecificFrameDataReader>()).Read();

        while (true)
        {
            var seenLevelIndex = 0u;
            if (_context.ReplayHeader.Flags.HasFlag(ReplayHeaderFlags.HasStreamingFixes))
            {
                seenLevelIndex = _archive.ReadIntPacked();
            }

            var packetSize = _archive.ReadInt32();
            switch (packetSize)
            {
                case 0:
                    return;
                case < 0:
                    throw new InvalidReplayInfoException($"Replay packet size {packetSize} is negative.");
            }

            const int maxPacketSizeInBytes = Constants.MaxPacketSizeInBits / 8;
            if (packetSize > maxPacketSizeInBytes)
            {
                throw new InvalidReplayInfoException(
                    $"Replay packet size {packetSize} exceeds maximum {maxPacketSizeInBytes}.");
            }

            _context.PlaybackPackets.Add(new PlaybackPacket
            {
                ReplayDataChunkIndex = _dataChunk.ChunkIndex,
                PacketIndex = _context.PlaybackPackets.Count,
                CurrentLevelIndex = currentLevelIndex,
                SeenLevelIndex = seenLevelIndex,
                TimeSeconds = timeSeconds,
                Data = _archive.ReadBytes(packetSize).ToArray(),
            });
        }
    }

    private void ReadExternalData()
    {
        while (true)
        {
            var numBits = _archive.ReadIntPacked();
            if (numBits == 0)
            {
                return;
            }

            var netGuid = _archive.ReadIntPacked();
            var byteCount = checked((int)((numBits + 7) >> 3));

            _logger.LogTrace("Skipping external data for net GUID {NetGuid} with {BitCount} bits.", netGuid, numBits);
            _archive.Skip(byteCount);
        }
    }
}
