using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Models.Errors;
using Replay.Models.Protocol;
using Replay.Models.Replay;
using Replay.Unreal.Bunches.Payload;
using Replay.Unreal.Exports;
using Replay.Unreal.Frames;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Packets;

public class PlaybackPacketReader
{

    private readonly ReplayReaderContext _context;
    private readonly FBinaryArchive _archive;

    public PlaybackPacketReader(
        ReplayReaderContext context,
        FBinaryArchive archive)
    {
        _context = context;
        _archive = archive;
    }

    public void Read()
    {
        try
        {
            ReadCore();
        }
        catch (ArchiveReadException exception)
        {
            throw new InvalidReplayDataException(
                $"Error while parsing replay-data packet stream: {exception.Message}", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidReplayDataException(
                $"Error while parsing replay-data packet stream: {exception.Message}", exception);
        }
    }

    private void ReadCore()
    {
        PayloadTransformSupport.ValidateSupported(_context.ReplayVersion.Branch);

        while (!_archive.AtEnd)
        {
            ReadDemoFrame();
        }
    }

    private void ReadDemoFrame()
    {
        // currentLevelIndex
        _ = _archive.ReadInt32();

        var timeSeconds = _archive.ReadSingle();
        _context.CurrentTimeSeconds = timeSeconds;

        new ExportDataReader(
            _archive,
            _context.NetGuidCache,
            _context.LoggerFactory?.CreateLogger<ExportDataReader>(),
            _context.ExportBindingRegistry.OnExportGroupChanged).Read();

        new StreamingLevelFixesReader(_context, _archive).Read();
        ReadExternalData();
        new GameSpecificFrameDataReader(_context, _archive).Read();

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

            _ = _archive.ReadIntPacked();
            var byteCount = checked((int)((numBits + 7) >> 3));

            _archive.Skip(byteCount);
        }
    }
}
