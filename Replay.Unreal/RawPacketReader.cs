using Replay.Encoding.Archives;
using Replay.Models;

namespace Replay.Unreal;

public delegate void RawBunchHeaderCallback(ref RawBunchHeader header);

public readonly struct RawPacketReadResult
{
    public int PacketId { get; init; }
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

    public void Reset()
    {
        _partialBunches.Clear();
        _inReliableSequence = 0;
    }

    public RawPacketReadResult ReadPacket(
        ReadOnlyMemory<byte> packetData,
        int packetId,
        RawBunchHeaderCallback callback)
    {
        if (packetData.Length == 0)
        {
            return new RawPacketReadResult { PacketId = packetId };
        }

        var span = packetData.Span;
        var lastByte = span[^1];
        if (lastByte == 0)
        {
            return new RawPacketReadResult { PacketId = packetId, IsMalformed = true };
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
                    PacketId = packetId,
                    BunchCount = bunchCount,
                    IsMalformed = true,
                    PartialErrorCount = partialErrorCount,
                };
            }

            TrackPartialBunch(ref header, ref partialErrorCount);
            callback(ref header);

            if (header.PayloadBitCount > 0)
            {
                reader.SkipBits(header.PayloadBitCount);
            }

            bunchCount++;
        }

        return new RawPacketReadResult
        {
            PacketId = packetId,
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
            if (_partialBunches.TryGetValue(bunch.ChIndex, out var existing))
            {
                if (!existing.IsComplete)
                {
                    partialErrorCount++;
                    bunch.HasPartialError = true;
                }
            }

            _partialBunches[bunch.ChIndex] = new PartialBunchState
            {
                ChSequence = bunch.ChSequence,
                Reliable = bunch.bReliable,
                CumulativePayloadBitCount = bunch.PayloadBitCount,
            };
        }
        else
        {
            if (!_partialBunches.TryGetValue(bunch.ChIndex, out var state) || state.IsComplete)
            {
                partialErrorCount++;
                bunch.HasPartialError = true;
                return;
            }

            bool sequenceMatches;
            if (state.Reliable)
            {
                sequenceMatches = bunch.ChSequence == state.ChSequence + 1;
            }
            else
            {
                sequenceMatches = bunch.ChSequence == state.ChSequence + 1 || bunch.ChSequence == state.ChSequence;
            }

            if (!sequenceMatches || state.Reliable != bunch.bReliable)
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
}
