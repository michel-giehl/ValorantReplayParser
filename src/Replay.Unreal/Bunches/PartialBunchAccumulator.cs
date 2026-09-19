using System.Buffers;
using System.Runtime.ExceptionServices;
using Replay.Encoding.Archives;
using Replay.Models.Diagnostics;
using Replay.Models.Errors;
using Replay.Models.Net;

namespace Replay.Unreal.Bunches;

/// <summary>
/// Retains fragments for incomplete bunches and transfers completed payload ownership to the caller.
/// </summary>
internal sealed class PartialBunchAccumulator : IPartialBunchAccumulator
{
    private readonly Dictionary<uint, AccumulatorState> _fragments = [];
    private readonly PartialBunchLimits _limits;
    private readonly MemoryPool<byte> _memoryPool;
    private readonly Action<ReplayDiagnostic>? _diagnosticCallback;

    private long _retainedCapacityBytes;
    private bool _isDisposed;

    internal long RetainedCapacityBytes => _retainedCapacityBytes;

    internal int PendingAssemblyCount => _fragments.Count;

    public PartialBunchAccumulator(
        PartialBunchLimits? limits = null,
        MemoryPool<byte>? memoryPool = null,
        Action<ReplayDiagnostic>? diagnosticCallback = null)
    {
        _limits = limits ?? PartialBunchLimits.Default;
        _memoryPool = memoryPool ?? MemoryPool<byte>.Shared;
        _diagnosticCallback = diagnosticCallback;
    }

    public PartialBunchResult AddFragment(
        uint chIndex,
        RawBunchHeader header,
        FBitArchive payload,
        BunchPayloadStats stats)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var fragmentBitCount = payload.BitsRemaining;
        if (fragmentBitCount < 0)
        {
            throw CreateInvalidBitCount(
                payload,
                fragmentBitCount,
                header,
                "Partial payload has a negative remaining bit count.");
        }

        if (header.bPartialInitial)
        {
            return AddInitialFragment(chIndex, header, payload, fragmentBitCount, stats);
        }

        return AddContinuationFragment(chIndex, header, payload, fragmentBitCount, stats);
    }

    public bool TryComplete(
        uint chIndex,
        out IMemoryOwner<byte> buffer,
        out int bitCount,
        out RawBunchHeader storedHeader)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_fragments.TryGetValue(chIndex, out var state) || !state.IsComplete)
        {
            buffer = null!;
            bitCount = 0;
            storedHeader = default;
            return false;
        }

        var assemblyHasOwner = state.BufferOwner is not null;
        var transferredOwner = state.BufferOwner ?? _memoryPool.Rent(0);
        var transferredBuffer = transferredOwner.Memory;
        if (transferredBuffer.Length < RequiredByteCount(state.BitCount))
        {
            if (!assemblyHasOwner)
            {
                transferredOwner.Dispose();
            }

            throw new InvalidOperationException("The memory pool returned a buffer smaller than the completed payload.");
        }

        _fragments.Remove(chIndex);
        _retainedCapacityBytes = checked(_retainedCapacityBytes - state.RentedCapacityBytes);

        buffer = transferredOwner;
        bitCount = state.BitCount;
        storedHeader = state.StoredBunchHeader;
        state.BufferOwner = null;
        state.Buffer = default;
        state.RentedCapacityBytes = 0;
        return true;
    }

    /// <summary>
    /// Discards unfinished assemblies and reports them as incomplete. Call only after normal replay traversal.
    /// </summary>
    public int FinalizePending()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var diagnostics = new List<ReplayDiagnostic>();
        foreach (var (channelIndex, state) in _fragments)
        {
            if (state.IsComplete)
            {
                continue;
            }

            diagnostics.Add(new ReplayDiagnostic(
                ReplayDiagnosticCode.IncompletePartialBunch,
                $"Discarded an incomplete partial bunch at end of replay on channel {channelIndex}.",
                state.StoredBunchHeader.PacketId,
                channelIndex));
        }

        ReleaseAllFragments();

        foreach (var diagnostic in diagnostics)
        {
            _diagnosticCallback?.Invoke(diagnostic);
        }

        return diagnostics.Count;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ReleaseAllFragments();
    }

    internal static long RequiredByteCount(long bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }

        return checked((bitCount + 7L) / 8L);
    }

    private PartialBunchResult AddInitialFragment(
        uint channelIndex,
        RawBunchHeader header,
        FBitArchive payload,
        long fragmentBitCount,
        BunchPayloadStats stats)
    {
        var hasExisting = _fragments.TryGetValue(channelIndex, out var existing);
        var sequenceError = PartialBunchSequenceValidator.ValidateInitial(hasExisting, hasExisting && existing!.IsComplete);
        if (sequenceError is PartialBunchSequenceError.None && header.HasPartialError)
        {
            // RawPacketReader also tracks partial sequences. Preserve its overlap signal when its
            // state differs because an earlier accumulator error already discarded that assembly.
            sequenceError = PartialBunchSequenceError.OverlappingInitial;
        }

        if (!hasExisting && _fragments.Count >= _limits.MaxPendingAssemblies)
        {
            throw CreateLimitException(
                "pending assemblies",
                _limits.MaxPendingAssemblies,
                checked((long)_fragments.Count + 1),
                header);
        }

        if (sequenceError is not PartialBunchSequenceError.None)
        {
            stats.PartialErrorCount++;
            DiscardFragment(channelIndex);
            header.HasPartialError = false;
        }
        else if (hasExisting)
        {
            // A complete assembly should normally already have been transferred. Replace it safely if not.
            DiscardFragment(channelIndex);
        }

        var state = new AccumulatorState
        {
            ChSequence = header.ChSequence,
            Reliable = header.bReliable,
            StoredBunchHeader = header,
        };

        _fragments[channelIndex] = state;
        try
        {
            EnsureAssemblySize(0, fragmentBitCount, header);
            if (fragmentBitCount > 0)
            {
                AppendPayloadBits(state, payload, fragmentBitCount, header);
            }
        }
        catch
        {
            DiscardFragment(channelIndex);
            throw;
        }

        if (fragmentBitCount > 0)
        {
            stats.PartialFragmentCount++;
        }

        if (header.bPartialFinal)
        {
            state.IsComplete = true;
            stats.CompletedPartialBunchCount++;
        }

        if (sequenceError is not PartialBunchSequenceError.None)
        {
            ReportSequenceError(sequenceError, channelIndex, header.PacketId);
        }

        return CreateResult(header);
    }

    private PartialBunchResult AddContinuationFragment(
        uint channelIndex,
        RawBunchHeader header,
        FBitArchive payload,
        long fragmentBitCount,
        BunchPayloadStats stats)
    {
        var hasState = _fragments.TryGetValue(channelIndex, out var state);
        var sequenceError = PartialBunchSequenceValidator.ValidateContinuation(
            hasState,
            hasState && state!.IsComplete,
            hasState ? state!.ChSequence : 0,
            hasState && state!.Reliable,
            header);

        if (sequenceError is not PartialBunchSequenceError.None)
        {
            stats.PartialErrorCount++;
            header.HasPartialError = true;
            if (sequenceError is PartialBunchSequenceError.MismatchedContinuation)
            {
                DiscardFragment(channelIndex);
            }

            ReportSequenceError(sequenceError, channelIndex, header.PacketId);
            return CreateResult(header);
        }

        var currentState = state!;
        EnsureAssemblySize(currentState.BitCount, fragmentBitCount, header);

        currentState.ChSequence = header.ChSequence;
        if (fragmentBitCount > 0)
        {
            AppendPayloadBits(currentState, payload, fragmentBitCount, header);
        }
        if (fragmentBitCount > 0)
        {
            stats.PartialFragmentCount++;
        }

        if (header.bPartialFinal)
        {
            currentState.IsComplete = true;
            stats.CompletedPartialBunchCount++;
        }

        return CreateResult(header);
    }

    private void EnsureAssemblySize(long currentBitCount, long fragmentBitCount, RawBunchHeader header)
    {
        try
        {
            var totalBitCount = checked(currentBitCount + fragmentBitCount);
            var requiredBytes = RequiredByteCount(totalBitCount);
            if (requiredBytes > _limits.MaxPayloadBytesPerChannel)
            {
                throw CreateLimitException(
                    "per-channel logical payload bytes",
                    _limits.MaxPayloadBytesPerChannel,
                    requiredBytes,
                    header);
            }

            _ = checked((int)totalBitCount);
        }
        catch (OverflowException exception)
        {
            throw new InvalidReplayDataException(
                $"Partial bunch size overflow on packet {header.PacketId}, channel {header.ChIndex}.",
                exception);
        }
    }

    private void AppendPayloadBits(
        AccumulatorState state,
        FBitArchive payload,
        long fragmentBitCount,
        RawBunchHeader header)
    {
        var totalBitCount = checked((long)state.BitCount + fragmentBitCount);
        var requiredByteCountLong = RequiredByteCount(totalBitCount);
        var requiredByteCount = checked((int)requiredByteCountLong);
        var fragmentBitCountInt = checked((int)fragmentBitCount);
        var previousByteCount = checked((int)RequiredByteCount(state.BitCount));

        if (state.BufferOwner is not null && state.Buffer.Length >= requiredByteCount)
        {
            AppendBits(state.Buffer.Span, state.BitCount, payload, fragmentBitCountInt);
            state.BitCount = checked((int)totalBitCount);
            return;
        }

        var oldCapacity = state.RentedCapacityBytes;
        ValidateRequestedReplacementCapacity(oldCapacity, requiredByteCount, header);

        var newOwner = _memoryPool.Rent(requiredByteCount);
        try
        {
            var newBuffer = newOwner.Memory;
            if (newBuffer.Length < requiredByteCount)
            {
                throw new InvalidOperationException("The memory pool returned a buffer smaller than requested.");
            }

            ValidateActualReplacementCapacity(oldCapacity, newBuffer.Length, header);
            newBuffer.Span[..requiredByteCount].Clear();
            if (previousByteCount > 0 && state.BufferOwner is not null)
            {
                state.Buffer.Span[..previousByteCount].CopyTo(newBuffer.Span);
            }

            AppendBits(newBuffer.Span, state.BitCount, payload, fragmentBitCountInt);

            var projectedRetainedCapacity = checked(_retainedCapacityBytes - oldCapacity + newBuffer.Length);
            if (projectedRetainedCapacity > _limits.MaxRetainedCapacityBytes)
            {
                throw CreateLimitException(
                    "aggregate retained buffer capacity",
                    _limits.MaxRetainedCapacityBytes,
                    projectedRetainedCapacity,
                    header);
            }

            var previousOwner = state.BufferOwner;
            state.BufferOwner = newOwner;
            state.Buffer = newBuffer;
            state.RentedCapacityBytes = newBuffer.Length;
            state.BitCount = checked((int)totalBitCount);
            _retainedCapacityBytes = projectedRetainedCapacity;
            newOwner = null!;

            previousOwner?.Dispose();
        }
        catch
        {
            newOwner?.Dispose();
            throw;
        }
    }

    private void ValidateRequestedReplacementCapacity(long oldCapacity, int requestedCapacity, RawBunchHeader header)
    {
        var projectedRetainedCapacity = checked(_retainedCapacityBytes - oldCapacity + requestedCapacity);
        if (projectedRetainedCapacity > _limits.MaxRetainedCapacityBytes)
        {
            throw CreateLimitException(
                "aggregate retained buffer capacity",
                _limits.MaxRetainedCapacityBytes,
                projectedRetainedCapacity,
                header);
        }

        if (oldCapacity > 0)
        {
            var temporaryCapacity = checked(oldCapacity + requestedCapacity);
            if (temporaryCapacity > _limits.MaxReplacementTransientCapacityBytes)
            {
                throw CreateLimitException(
                    "temporary replacement buffer capacity",
                    _limits.MaxReplacementTransientCapacityBytes,
                    temporaryCapacity,
                    header);
            }
        }
    }

    private void ValidateActualReplacementCapacity(long oldCapacity, int newCapacity, RawBunchHeader header)
    {
        if (oldCapacity > 0)
        {
            var temporaryCapacity = checked(oldCapacity + newCapacity);
            if (temporaryCapacity > _limits.MaxReplacementTransientCapacityBytes)
            {
                throw CreateLimitException(
                    "temporary replacement buffer capacity",
                    _limits.MaxReplacementTransientCapacityBytes,
                    temporaryCapacity,
                    header);
            }
        }

        var projectedRetainedCapacity = checked(_retainedCapacityBytes - oldCapacity + newCapacity);
        if (projectedRetainedCapacity > _limits.MaxRetainedCapacityBytes)
        {
            throw CreateLimitException(
                "aggregate retained buffer capacity",
                _limits.MaxRetainedCapacityBytes,
                projectedRetainedCapacity,
                header);
        }
    }

    private static void AppendBits(Span<byte> destination, int destinationBitOffset, FBitArchive payload, int bitCount)
    {
        // Partial payloads may split at arbitrary bit offsets, so the next fragment starts at the
        // exact cumulative bit count rather than at the next byte boundary.
        for (var i = 0; i < bitCount; i++)
        {
            var bit = payload.ReadBit();
            var destinationBit = destinationBitOffset + i;
            var destinationByteIndex = destinationBit >> 3;
            var destinationBitIndex = destinationBit & 7;
            if (bit)
            {
                destination[destinationByteIndex] |= (byte)(1 << destinationBitIndex);
            }
            else
            {
                destination[destinationByteIndex] &= (byte)~(1 << destinationBitIndex);
            }
        }
    }

    private void DiscardFragment(uint channelIndex)
    {
        if (_fragments.Remove(channelIndex, out var state))
        {
            _retainedCapacityBytes = checked(_retainedCapacityBytes - state.RentedCapacityBytes);
            try
            {
                state.BufferOwner?.Dispose();
            }
            finally
            {
                state.BufferOwner = null;
                state.Buffer = default;
                state.RentedCapacityBytes = 0;
            }
        }
    }

    private void ReleaseAllFragments()
    {
        var states = _fragments.Values.ToArray();
        _fragments.Clear();
        _retainedCapacityBytes = 0;
        ExceptionDispatchInfo? firstDisposalFailure = null;

        foreach (var state in states)
        {
            try
            {
                state.BufferOwner?.Dispose();
            }
            catch (Exception exception)
            {
                // Finish releasing the other owners before allowing a faulty owner to escape cleanup.
                firstDisposalFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                state.BufferOwner = null;
                state.Buffer = default;
                state.RentedCapacityBytes = 0;
            }
        }

        firstDisposalFailure?.Throw();
    }

    private void ReportSequenceError(PartialBunchSequenceError error, uint channelIndex, int packetId)
    {
        _diagnosticCallback?.Invoke(new ReplayDiagnostic(
            ReplayDiagnosticCode.PartialSequenceError,
            $"Discarded an invalid partial bunch sequence ({error}) on channel {channelIndex}.",
            packetId,
            channelIndex));
    }

    private static InvalidReplayDataException CreateInvalidBitCount(
        FBitArchive payload,
        long requested,
        RawBunchHeader header,
        string message)
    {
        var archiveException = new ArchiveReadException(
            ArchiveErrorCode.InvalidBitCount,
            nameof(AddFragment),
            payload.BitPosition,
            payload.BitLength,
            requested,
            message);
        return new InvalidReplayDataException(
            $"Invalid partial bunch framing on packet {header.PacketId}, channel {header.ChIndex}: {message}",
            archiveException);
    }

    private static InvalidReplayDataException CreateLimitException(
        string limitName,
        long allowed,
        long requested,
        RawBunchHeader header) =>
        new(
            $"Partial bunch {limitName} limit exceeded on packet {header.PacketId}, channel {header.ChIndex}: " +
            $"allowed {allowed}, requested {requested}.");

    private static PartialBunchResult CreateResult(RawBunchHeader header) => new()
    {
        Header = header,
        ShouldProcessCompletePayload = header is { bPartialFinal: true, HasPartialError: false },
    };

    private sealed class AccumulatorState
    {
        public int ChSequence { get; set; }
        public bool Reliable { get; set; }
        public bool IsComplete { get; set; }
        public RawBunchHeader StoredBunchHeader { get; set; }
        public IMemoryOwner<byte>? BufferOwner { get; set; }
        public Memory<byte> Buffer { get; set; }
        public long RentedCapacityBytes { get; set; }
        public int BitCount { get; set; }
    }
}
