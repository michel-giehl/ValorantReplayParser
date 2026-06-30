using System.Buffers;
using Replay.Encoding.Archives;
using Replay.Models.Net;

namespace Replay.Unreal.Bunches;

internal sealed class PartialBunchAccumulator : IPartialBunchAccumulator
{
    private readonly Dictionary<uint, AccumulatorState> _fragments = [];

    public PartialBunchResult AddFragment(
        uint chIndex,
        RawBunchHeader header,
        FBitArchive payload,
        BunchPayloadStats stats)
    {
        if (!ValidateSequence(chIndex, ref header, stats))
        {
            return CreateResult(header);
        }

        var remainingBits = (int)payload.BitsRemaining;
        var state = _fragments[chIndex];

        if (remainingBits == 0)
        {
            if (!header.bPartialFinal) return CreateResult(header);
            state.IsComplete = true;
            stats.CompletedPartialBunchCount++;
            _fragments[chIndex] = state;

            return CreateResult(header);
        }

        if (!header.bPartialFinal && remainingBits % 8 != 0)
        {
            stats.PartialErrorCount++;
            header.HasPartialError = true;
            DiscardFragment(chIndex);
            return CreateResult(header);
        }

        AppendPayloadBits(ref state, payload, remainingBits);

        stats.PartialFragmentCount++;

        if (header.bPartialFinal)
        {
            state.IsComplete = true;
            stats.CompletedPartialBunchCount++;
        }

        _fragments[chIndex] = state;
        return CreateResult(header);
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

    private bool ValidateSequence(uint chIndex, ref RawBunchHeader header, BunchPayloadStats stats)
    {
        if (header.bPartialInitial)
        {
            var hasExisting = _fragments.TryGetValue(chIndex, out var existing);
            var error = PartialBunchSequenceValidator.ValidateInitial(hasExisting, existing.IsComplete);
            if (error is not PartialBunchSequenceError.None)
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
            return true;
        }

        var hasState = _fragments.TryGetValue(chIndex, out var state);
        var validationError = PartialBunchSequenceValidator.ValidateContinuation(
            hasState,
            hasState && state.IsComplete,
            hasState ? state.ChSequence : 0,
            hasState && state.Reliable,
            header);
        if (validationError is PartialBunchSequenceError.None)
        {
            state.ChSequence = header.ChSequence;
            _fragments[chIndex] = state;
            return true;
        }

        stats.PartialErrorCount++;
        header.HasPartialError = true;
        if (validationError is PartialBunchSequenceError.MismatchedContinuation)
        {
            header.IsPartialCompleted = header.bPartialFinal;
            DiscardFragment(chIndex);
        }

        return false;
    }

    private void DiscardFragment(uint chIndex)
    {
        if (_fragments.Remove(chIndex, out var state))
        {
            state.BufferOwner?.Dispose();
        }
    }

    private static PartialBunchResult CreateResult(RawBunchHeader header) => new()
    {
        Header = header,
        ShouldProcessCompletePayload = header is { bPartialFinal: true, HasPartialError: false },
    };

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

    private struct AccumulatorState
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