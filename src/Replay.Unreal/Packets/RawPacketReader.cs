using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Models.Protocol;
using Replay.Unreal.Bunches;

namespace Replay.Unreal.Packets;

public delegate void RawBunchPayloadCallback(ref RawBunchHeader header, FBitArchive payload);

public readonly struct RawPacketReadResult
{
    public int BunchCount { get; init; }
    public bool IsMalformed { get; init; }
    public int PartialErrorCount { get; init; }
}

internal struct PartialBunchState
{
    public int ChSequence { get; set; }
    public bool Reliable { get; init; }
    public int CumulativePayloadBitCount { get; set; }
    public bool IsComplete { get; set; }
}

public sealed class RawPacketReader
{
    private readonly Dictionary<uint, PartialBunchState> _partialBunches = [];
    private int _inReliableSequence;

    public RawPacketReadResult ReadPacket(
        ReadOnlyMemory<byte> packetData,
        int packetId,
        RawBunchPayloadCallback callback)
    {
        if (packetData.Length == 0)
        {
            return new RawPacketReadResult
            {
                BunchCount = 0,
                IsMalformed = false,
                PartialErrorCount = 0,
            };
        }

        var span = packetData.Span;
        var lastByte = span[^1];
        if (lastByte == 0)
        {
            return new RawPacketReadResult { IsMalformed = true };
        }

        var bitSize = ComputeBitSize(span, lastByte);
        var reader = new BitArchiveReader(packetData, bitSize);

        var bunchCount = 0;
        var partialErrorCount = 0;

        while (!reader.AtEnd)
        {
            var header = ParseBunchHeader(reader, packetId);
            if (header.PayloadBitCount > reader.BitsRemaining)
            {
                return new RawPacketReadResult
                {
                    BunchCount = bunchCount,
                    IsMalformed = true,
                    PartialErrorCount = partialErrorCount,
                };
            }

            TrackPartialBunch(ref header, ref partialErrorCount);
            var payloadArchive = reader.ReadSubArchive(header.PayloadBitCount);
            callback(ref header, payloadArchive);

            bunchCount++;
        }

        return new RawPacketReadResult
        {
            BunchCount = bunchCount,
            PartialErrorCount = partialErrorCount,
        };
    }

    private static int ComputeBitSize(ReadOnlySpan<byte> packet, byte lastByte)
    {
        var bitSize = packet.Length * 8 - 1;
        while ((lastByte & 0x80) == 0)
        {
            lastByte <<= 1;
            bitSize--;
        }

        return bitSize;
    }

    private RawBunchHeader ParseBunchHeader(BitArchiveReader reader, int packetId)
    {
        var bunch = new RawBunchHeader
        {
            PacketId = packetId,
        };

        var bControl = reader.ReadBit();
        if (bControl)
        {
            bunch.bOpen = reader.ReadBit();
            bunch.bClose = reader.ReadBit();
        }

        if (bunch.bClose)
        {
            bunch.CloseReason = (ChannelCloseReason)reader.ReadSerializedInt((int)ChannelCloseReason.MAX);
            bunch.bDormant = bunch.CloseReason == ChannelCloseReason.Dormancy;
        }

        bunch.bIsReplicationPaused = reader.ReadBit();
        bunch.bReliable = reader.ReadBit();
        bunch.ChIndex = reader.ReadIntPacked();
        bunch.bHasPackageMapExports = reader.ReadBit();
        bunch.bHasMustBeMappedGUIDs = reader.ReadBit();
        bunch.bPartial = reader.ReadBit();

        if (bunch.bReliable)
        {
            bunch.ChSequence = _inReliableSequence + 1;
        }
        else if (bunch.bPartial)
        {
            bunch.ChSequence = packetId;
        }

        if (bunch.bPartial)
        {
            bunch.bPartialInitial = reader.ReadBit();
            bunch.bPartialFinal = reader.ReadBit();
        }

        _ = reader.ReadBit(); // Valorant specific bit
        if (bunch.bReliable || bunch.bOpen)
        {
            bunch.ChName = reader.ReadFName();
        }

        bunch.PayloadBitCount = (int)reader.ReadSerializedInt(Constants.MaxPacketSizeInBits);
        bunch.PayloadBitOffset = reader.BitPosition;

        if (bunch.bReliable)
        {
            _inReliableSequence = bunch.ChSequence;
        }

        return bunch;
    }

    private void TrackPartialBunch(ref RawBunchHeader bunch, ref int partialErrorCount)
    {
        if (!bunch.bPartial)
        {
            return;
        }

        if (bunch.bPartialInitial)
        {
            _partialBunches.TryGetValue(bunch.ChIndex, out var existing);
            var error = PartialBunchSequenceValidator.ValidateInitial(
                _partialBunches.ContainsKey(bunch.ChIndex),
                existing.IsComplete);
            if (error is not PartialBunchSequenceError.None)
            {
                partialErrorCount++;
                bunch.HasPartialError = true;
            }

            _partialBunches[bunch.ChIndex] = new PartialBunchState
            {
                ChSequence = bunch.ChSequence,
                Reliable = bunch.bReliable,
                CumulativePayloadBitCount = bunch.PayloadBitCount,
            };
            return;
        }

        var hasState = _partialBunches.TryGetValue(bunch.ChIndex, out var state);
        var validationError = PartialBunchSequenceValidator.ValidateContinuation(
            hasState,
            hasState && state.IsComplete,
            hasState ? state.ChSequence : 0,
            hasState && state.Reliable,
            bunch);
        if (validationError is not PartialBunchSequenceError.None)
        {
            partialErrorCount++;
            bunch.HasPartialError = true;
            return;
        }

        state.CumulativePayloadBitCount += bunch.PayloadBitCount;
        state.ChSequence = bunch.ChSequence;

        if (bunch.bPartialFinal)
        {
            state.IsComplete = true;
            bunch.IsPartialCompleted = true;
        }

        _partialBunches[bunch.ChIndex] = state;
    }
}
