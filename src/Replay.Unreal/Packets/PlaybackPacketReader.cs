using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Encoding.PayloadEncryption;
using Replay.Models.Errors;
using Replay.Models.Protocol;
using Replay.Models.Replay;
using Replay.Unreal.Exports;
using Replay.Unreal.Frames;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Packets;

public class PlaybackPacketReader
{
    private static readonly PayloadTransformRegistry PayloadTransforms = PayloadTransformRegistry.CreateDefault();

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
        ValidatePayloadTransformSupport(_context.ReplayVersion.Branch);

        while (!_archive.AtEnd)
        {
            ReadDemoFrame();
        }
    }

    private static void ValidatePayloadTransformSupport(string replayVersion)
    {
        if (string.IsNullOrWhiteSpace(replayVersion))
        {
            return;
        }

        try
        {
            _ = PayloadTransforms.GetRequired(replayVersion);
        }
        catch (UnsupportedPayloadTransformVersionException)
        {
            throw new InvalidReplayInfoException(
                $"Unsupported VALORANT property payload transform for replay version '{replayVersion}'.");
        }
    }

    private void ReadDemoFrame()
    {
        // currentLevelIndex
        _ = _archive.ReadInt32();

        var timeSeconds = _archive.ReadSingle();
        _context.CurrentTimeSeconds = timeSeconds;
        _logger.LogTrace("Read playback frame at {TimeSeconds} seconds.", timeSeconds);

        new ExportDataReader(
            _archive,
            _context.NetGuidCache,
            _loggerFactory?.CreateLogger<ExportDataReader>(),
            _context.ExportBindingRegistry.OnExportGroupChanged).Read();

        new StreamingLevelFixesReader(_context, _archive, _loggerFactory?.CreateLogger<StreamingLevelFixesReader>()).Read();
        ReadExternalData();
        new GameSpecificFrameDataReader(_context, _archive, _loggerFactory?.CreateLogger<GameSpecificFrameDataReader>()).Read();

        while (true)
        {
            if (_context.ReplayHeader.Flags.HasFlag(ReplayHeaderFlags.HasStreamingFixes))
            {
                // seenLevelIndex
                _ = _archive.ReadIntPacked();
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
            var result = _context.RawPacketReader.ReadPacket(packetData, packetIndex, _context.BunchPayloadPipeline.HandleBunchPayload);
            _context.PacketStats.RecordPacket(packetSize, timeSeconds, result);
            _context.BunchPayloadStats.PacketCount++;
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
