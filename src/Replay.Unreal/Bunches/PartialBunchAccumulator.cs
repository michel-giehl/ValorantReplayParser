using System.Buffers;
using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Unreal.PackageMap;

namespace Replay.Unreal.Bunches;

public sealed class PartialBunchAccumulator
{
    private readonly Dictionary<uint, AccumulatorState> _fragments = [];
    private readonly PackageMapReader _packageMapReader;

    public PartialBunchAccumulator(PackageMapReader packageMapReader)
    {
        _packageMapReader = packageMapReader;
    }

    public void AddFragment(
        uint chIndex,
        ref RawBunchHeader header,
        FBitArchive payload,
        BunchPayloadStats stats)
    {
        if (header.bPartialInitial)
        {
            if (_fragments.TryGetValue(chIndex, out var existing) && !existing.IsComplete)
            {
                stats.PartialErrorCount++;
                header.HasPartialError = true;
                existing.BufferOwner?.Dispose();
            }

            _fragments[chIndex] = new AccumulatorState
            {
                ChSequence = header.ChSequence,
                Reliable = header.bReliable,
                StoredBunchHeader = header,
            };
        }
        else
        {
            if (!_fragments.TryGetValue(chIndex, out var state) || state.IsComplete)
            {
                stats.PartialErrorCount++;
                header.HasPartialError = true;
                return;
            }

            bool sequenceMatches;
            if (state.Reliable)
            {
                sequenceMatches = header.ChSequence == state.ChSequence + 1;
            }
            else
            {
                sequenceMatches = header.ChSequence == state.ChSequence + 1 || header.ChSequence == state.ChSequence;
            }

            if (!sequenceMatches || state.Reliable != header.bReliable)
            {
                stats.PartialErrorCount++;
                header.HasPartialError = true;
                header.IsPartialCompleted = header.bPartialFinal;
                DiscardFragment(chIndex);
                return;
            }

            state.ChSequence = header.ChSequence;
        }

        var remainingBits = (int)payload.BitsRemaining;
        var state2 = _fragments[chIndex];

        if (remainingBits == 0)
        {
            if (header.bPartialFinal)
            {
                state2.IsComplete = true;
                stats.CompletedPartialBunchCount++;
                _fragments[chIndex] = state2;
            }

            return;
        }

        if (!header.bPartialFinal && remainingBits % 8 != 0)
        {
            stats.PartialErrorCount++;
            header.HasPartialError = true;
            DiscardFragment(chIndex);
            return;
        }

        AppendPayloadBits(ref state2, payload, remainingBits);

        stats.PartialFragmentCount++;

        if (header.bPartialFinal)
        {
            state2.IsComplete = true;
            stats.CompletedPartialBunchCount++;
        }

        _fragments[chIndex] = state2;
    }

    public bool TryComplete(uint chIndex, out IMemoryOwner<byte> buffer, out int bitCount, out RawBunchHeader storedHeader)
    {
        if (_fragments.TryGetValue(chIndex, out var state) && state.IsComplete)
        {
            buffer = state.BufferOwner ?? MemoryPool<byte>.Shared.Rent(0);
            bitCount = state.BitCount;
            storedHeader = state.StoredBunchHeader;
            _fragments.Remove(chIndex);
            return true;
        }

        buffer = null!;
        bitCount = 0;
        storedHeader = default;
        return false;
    }

    public void Reset()
    {
        foreach (var state in _fragments.Values)
        {
            state.BufferOwner?.Dispose();
        }

        _fragments.Clear();
    }

    internal int PendingFragmentCount => _fragments.Count;

    private void DiscardFragment(uint chIndex)
    {
        if (_fragments.Remove(chIndex, out var state))
        {
            state.BufferOwner?.Dispose();
        }
    }

    private static void AppendPayloadBits(ref AccumulatorState state, FBitArchive payload, int bitCount)
    {
        var newTotalBits = state.BitCount + bitCount;
        var newByteCount = (newTotalBits + 7) / 8;
        var destBitOffset = state.BitCount;

        if (state.BufferOwner is null)
        {
            var newOwner = MemoryPool<byte>.Shared.Rent(newByteCount);
            var newBuffer = newOwner.Memory;
            newBuffer.Span[..newByteCount].Clear();
            state.BufferOwner = newOwner;
            state.Buffer = newBuffer;
        }
        else if (state.Buffer.Length < newByteCount)
        {
            var oldOwner = state.BufferOwner;
            var oldSpan = state.Buffer.Span[..((state.BitCount + 7) / 8)];

            var newOwner = MemoryPool<byte>.Shared.Rent(newByteCount);
            var newBuffer = newOwner.Memory;
            newBuffer.Span[..newByteCount].Clear();
            oldSpan.CopyTo(newBuffer.Span);

            oldOwner.Dispose();
            state.BufferOwner = newOwner;
            state.Buffer = newBuffer;
        }

        var destSpan = state.Buffer.Span;
        for (var i = 0; i < bitCount; i++)
        {
            var bit = payload.ReadBit();
            var destBit = destBitOffset + i;
            var destByteIdx = destBit >> 3;
            var destBitIdx = destBit & 7;
            if (bit)
            {
                destSpan[destByteIdx] |= (byte)(1 << destBitIdx);
            }
            else
            {
                destSpan[destByteIdx] &= (byte)~(1 << destBitIdx);
            }
        }

        state.BitCount = newTotalBits;
    }

    internal struct AccumulatorState
    {
        public int ChSequence;
        public bool Reliable;
        public bool IsComplete;
        public RawBunchHeader StoredBunchHeader;
        public IMemoryOwner<byte>? BufferOwner;
        public Memory<byte> Buffer;
        public int BitCount;
    }
}
