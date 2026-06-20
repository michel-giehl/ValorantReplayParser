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
            ReadDemoFrame();
        }
    }

    private void ReadDemoFrame()
    {
        var currentLevelIndex = _archive.ReadInt32();

        var timeSeconds = _archive.ReadSingle();
        _logger.LogTrace("Read playback frame at {TimeSeconds} seconds.", timeSeconds);

        new ExportDataReader(
            _archive,
            _context.NetGuidCache,
            _loggerFactory?.CreateLogger<ExportDataReader>()).Read();

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

            var packetIndex = _context.PacketStats.PacketCount;
            var packetData = _archive.ReadBytes(packetSize);
            var result = _context.RawPacketReader.ReadPacket(packetData, packetIndex, static (ref RawBunchHeader _) => { });
            _context.PacketStats.RecordPacket(packetSize, timeSeconds, result);
            if (result.IsMalformed)
            {
                throw new InvalidReplayInfoException($"Replay packet {packetIndex} is malformed.");
            }
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
