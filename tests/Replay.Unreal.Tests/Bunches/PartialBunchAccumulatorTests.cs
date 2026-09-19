using System.Buffers;
using Replay.Encoding.Archives;
using Replay.Models.Diagnostics;
using Replay.Models.Errors;
using Replay.Models.Net;
using Replay.Unreal.Bunches;

namespace Replay.Unreal.Tests.Bunches;

[TestFixture]
internal sealed class PartialBunchAccumulatorTests
{
    [Test]
    public void AddFragment_AllowsExactPerChannelLimit_AndRejectsOneByteOver()
    {
        var pool = new CountingMemoryPool();
        using var accumulator = new PartialBunchAccumulator(
            new PartialBunchLimits(maxPayloadBytesPerChannel: 1),
            pool);

        using (var first = Payload(8, 0xA5))
        {
            accumulator.AddFragment(3, Header(3, 10, initial: true, final: false), first, new BunchPayloadStats());
        }

        using (var final = Payload(0))
        {
            accumulator.AddFragment(3, Header(3, 11, initial: false, final: true, sequence: 1), final, new BunchPayloadStats());
        }

        Assert.That(accumulator.TryComplete(3, out var completedOwner, out var bitCount, out _), Is.True);
        Assert.That(accumulator.RetainedCapacityBytes, Is.Zero);
        Assert.That(accumulator.PendingAssemblyCount, Is.Zero);
        using (completedOwner)
        {
            Assert.That(bitCount, Is.EqualTo(8));
            Assert.That(completedOwner.Memory.Span[0], Is.EqualTo(0xA5));
        }

        var tooLargePool = new CountingMemoryPool();
        using var tooLargeAccumulator = new PartialBunchAccumulator(
            new PartialBunchLimits(maxPayloadBytesPerChannel: 1),
            tooLargePool);
        using (var first = Payload(8, 0x11))
        {
            tooLargeAccumulator.AddFragment(4, Header(4, 20, initial: true, final: false), first, new BunchPayloadStats());
        }

        using (var final = Payload(1, 1))
        {
            var exception = Assert.Throws<InvalidReplayDataException>(() =>
                tooLargeAccumulator.AddFragment(4, Header(4, 21, initial: false, final: true, sequence: 1), final, new BunchPayloadStats()));
            Assert.That(exception!.Message, Does.Contain("allowed 1, requested 2"));
            Assert.That(exception.Message, Does.Contain("channel 4"));
        }

        tooLargeAccumulator.Dispose();
        Assert.That(tooLargePool.Owners.Single().DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void AddFragment_EnforcesPendingCountAndActualAggregatePoolCapacity()
    {
        using var pendingLimitAccumulator = new PartialBunchAccumulator(
            new PartialBunchLimits(maxPendingAssemblies: 1),
            new CountingMemoryPool());
        using (var empty = Payload(0))
        {
            pendingLimitAccumulator.AddFragment(1, Header(1, 1, initial: true, final: false), empty, new BunchPayloadStats());
            var exception = Assert.Throws<InvalidReplayDataException>(() =>
                pendingLimitAccumulator.AddFragment(2, Header(2, 2, initial: true, final: false), empty, new BunchPayloadStats()));
            Assert.That(exception!.Message, Does.Contain("pending assemblies limit exceeded"));
            Assert.That(exception.Message, Does.Contain("allowed 1, requested 2"));
        }

        var pool = new CountingMemoryPool(_ => 2);
        using var aggregateLimitAccumulator = new PartialBunchAccumulator(
            new PartialBunchLimits(maxRetainedCapacityBytes: 3),
            pool);
        using (var first = Payload(8, 0x12))
        {
            aggregateLimitAccumulator.AddFragment(5, Header(5, 3, initial: true, final: false), first, new BunchPayloadStats());
        }

        using (var second = Payload(8, 0x34))
        {
            var exception = Assert.Throws<InvalidReplayDataException>(() =>
                aggregateLimitAccumulator.AddFragment(6, Header(6, 4, initial: true, final: false), second, new BunchPayloadStats()));
            Assert.That(exception!.Message, Does.Contain("aggregate retained buffer capacity limit exceeded"));
            Assert.That(exception.Message, Does.Contain("allowed 3, requested 4"));
        }

        Assert.That(pool.Owners[1].DisposeCount, Is.EqualTo(1), "the over-limit rental is rejected and released");
        Assert.That(aggregateLimitAccumulator.RetainedCapacityBytes, Is.EqualTo(2));
        Assert.That(aggregateLimitAccumulator.PendingAssemblyCount, Is.EqualTo(1));
        aggregateLimitAccumulator.Dispose();
        Assert.That(pool.Owners.Select(owner => owner.DisposeCount), Is.EqualTo(new[] { 1, 1 }));
        Assert.That(aggregateLimitAccumulator.RetainedCapacityBytes, Is.Zero);
        Assert.That(aggregateLimitAccumulator.PendingAssemblyCount, Is.Zero);

        var exactPool = new CountingMemoryPool(_ => 2);
        using var exactAggregateAccumulator = new PartialBunchAccumulator(
            new PartialBunchLimits(maxRetainedCapacityBytes: 4),
            exactPool);
        using (var first = Payload(8, 0x56))
        using (var second = Payload(8, 0x78))
        {
            exactAggregateAccumulator.AddFragment(8, Header(8, 5, initial: true, final: false), first, new BunchPayloadStats());
            exactAggregateAccumulator.AddFragment(9, Header(9, 6, initial: true, final: false), second, new BunchPayloadStats());
        }

        Assert.That(exactAggregateAccumulator.RetainedCapacityBytes, Is.EqualTo(4));
        exactAggregateAccumulator.Dispose();
        Assert.That(exactPool.Owners.Select(owner => owner.DisposeCount), Is.EqualTo(new[] { 1, 1 }));
    }

    [Test]
    public void AddFragment_EnforcesTemporaryReplacementCapacityBeforeRenting()
    {
        var pool = new CountingMemoryPool(minimum => minimum);
        using var accumulator = new PartialBunchAccumulator(
            new PartialBunchLimits(
                maxPayloadBytesPerChannel: 4,
                maxRetainedCapacityBytes: 8,
                maxReplacementTransientCapacityBytes: 2),
            pool);

        using (var initial = Payload(8, 0x01))
        {
            accumulator.AddFragment(7, Header(7, 10, initial: true, final: false), initial, new BunchPayloadStats());
        }

        using (var continuation = Payload(8, 0x02))
        {
            var exception = Assert.Throws<InvalidReplayDataException>(() =>
                accumulator.AddFragment(7, Header(7, 11, initial: false, final: true, sequence: 1), continuation, new BunchPayloadStats()));
            Assert.That(exception!.Message, Does.Contain("temporary replacement buffer capacity limit exceeded"));
            Assert.That(exception.Message, Does.Contain("allowed 2, requested 3"));
        }

        Assert.That(pool.Owners, Has.Count.EqualTo(1), "requested replacement capacity is checked before rent");
        accumulator.Dispose();
        Assert.That(pool.Owners[0].DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void RequiredByteCount_UsesCheckedLongRounding()
    {
        Assert.That(PartialBunchAccumulator.RequiredByteCount(int.MaxValue), Is.EqualTo(268_435_456));
        Assert.Throws<OverflowException>(() => PartialBunchAccumulator.RequiredByteCount(long.MaxValue));
    }

    [Test]
    public void OverlappingInitial_DiscardsOldAssemblyAndAcceptsCleanReplacement()
    {
        var pool = new CountingMemoryPool();
        var diagnostics = new List<ReplayDiagnostic>();
        using var accumulator = new PartialBunchAccumulator(
            new PartialBunchLimits(),
            pool,
            diagnostics.Add);
        var stats = new BunchPayloadStats();

        using (var first = Payload(8, 0x01))
        {
            accumulator.AddFragment(9, Header(9, 30, initial: true, final: false, sequence: 4), first, stats);
        }

        var overlappingHeader = Header(9, 31, initial: true, final: true, sequence: 20);
        overlappingHeader.HasPartialError = true;
        using (var replacement = Payload(8, 0x5A))
        {
            var result = accumulator.AddFragment(9, overlappingHeader, replacement, stats);
            Assert.That(result.ShouldProcessCompletePayload, Is.True);
            Assert.That(result.Header.HasPartialError, Is.False);
        }

        Assert.That(pool.Owners[0].DisposeCount, Is.EqualTo(1));
        Assert.That(accumulator.TryComplete(9, out var owner, out var bitCount, out var storedHeader), Is.True);
        using (owner)
        {
            Assert.That(bitCount, Is.EqualTo(8));
            Assert.That(owner.Memory.Span[0], Is.EqualTo(0x5A));
            Assert.That(storedHeader.HasPartialError, Is.False);
        }

        Assert.Multiple(() =>
        {
            Assert.That(stats.PartialErrorCount, Is.EqualTo(1));
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0].Code, Is.EqualTo(ReplayDiagnosticCode.PartialSequenceError));
            Assert.That(diagnostics[0].PacketId, Is.EqualTo(31));
            Assert.That(diagnostics[0].ChannelIndex, Is.EqualTo(9));
        });
    }

    [Test]
    public void MissingAndMismatchedContinuations_AreReportedAndNeverCompleted()
    {
        var pool = new CountingMemoryPool();
        var diagnostics = new List<ReplayDiagnostic>();
        using var accumulator = new PartialBunchAccumulator(
            new PartialBunchLimits(),
            pool,
            diagnostics.Add);
        var stats = new BunchPayloadStats();

        using (var missing = Payload(1, 0x01))
        {
            var result = accumulator.AddFragment(12, Header(12, 40, initial: false, final: true), missing, stats);
            Assert.That(result.ShouldProcessCompletePayload, Is.False);
        }

        using (var initial = Payload(8, 0x11))
        {
            accumulator.AddFragment(13, Header(13, 41, initial: true, final: false, sequence: 3), initial, stats);
        }

        using (var mismatched = Payload(1, 1))
        {
            var result = accumulator.AddFragment(13, Header(13, 42, initial: false, final: true, sequence: 9), mismatched, stats);
            Assert.That(result.ShouldProcessCompletePayload, Is.False);
        }

        Assert.That(accumulator.TryComplete(12, out _, out _, out _), Is.False);
        Assert.That(accumulator.TryComplete(13, out _, out _, out _), Is.False);
        Assert.That(pool.Owners.Single().DisposeCount, Is.EqualTo(1));
        Assert.That(accumulator.RetainedCapacityBytes, Is.Zero);
        Assert.That(accumulator.PendingAssemblyCount, Is.Zero);
        Assert.Multiple(() =>
        {
            Assert.That(stats.PartialErrorCount, Is.EqualTo(2));
            Assert.That(diagnostics.Select(diagnostic => diagnostic.Code),
                Is.EqualTo(new[] { ReplayDiagnosticCode.PartialSequenceError, ReplayDiagnosticCode.PartialSequenceError }));
            Assert.That(diagnostics[0].Message, Does.Contain("MissingInitial"));
            Assert.That(diagnostics[1].Message, Does.Contain("MismatchedContinuation"));
        });
    }

    [Test]
    public void FinalizePending_ReportsAndReleasesIncompleteAssemblies_WhileDisposeStaysSilent()
    {
        var pool = new CountingMemoryPool();
        var diagnostics = new List<ReplayDiagnostic>();
        var accumulator = new PartialBunchAccumulator(new PartialBunchLimits(), pool, diagnostics.Add);
        using (var payload = Payload(8, 0x22))
        {
            accumulator.AddFragment(15, Header(15, 51, initial: true, final: false), payload, new BunchPayloadStats());
        }

        Assert.That(accumulator.FinalizePending(), Is.EqualTo(1));
        Assert.That(accumulator.FinalizePending(), Is.Zero);
        Assert.That(diagnostics, Has.Count.EqualTo(1));
        Assert.That(diagnostics[0].Code, Is.EqualTo(ReplayDiagnosticCode.IncompletePartialBunch));
        Assert.That(diagnostics[0].PacketId, Is.EqualTo(51));
        Assert.That(diagnostics[0].ChannelIndex, Is.EqualTo(15));
        Assert.That(pool.Owners.Single().DisposeCount, Is.EqualTo(1));

        accumulator.Dispose();
        accumulator.Dispose();
        Assert.That(pool.Owners.Single().DisposeCount, Is.EqualTo(1));
        Assert.Throws<ObjectDisposedException>(() => accumulator.FinalizePending());
        using var empty = Payload(0);
        Assert.Throws<ObjectDisposedException>(() =>
            accumulator.AddFragment(15, Header(15, 52, initial: true, final: true), empty, new BunchPayloadStats()));

        var silentDiagnostics = new List<ReplayDiagnostic>();
        var silentlyDisposed = new PartialBunchAccumulator(new PartialBunchLimits(), diagnosticCallback: silentDiagnostics.Add);
        using (var payload = Payload(8, 0x33))
        {
            silentlyDisposed.AddFragment(16, Header(16, 53, initial: true, final: false), payload, new BunchPayloadStats());
        }

        silentlyDisposed.Dispose();
        Assert.That(silentDiagnostics, Is.Empty);
    }

    [Test]
    public void TryComplete_TransfersOwnershipExactlyOnce_AndZeroLengthFinalIsSupported()
    {
        var pool = new CountingMemoryPool();
        var accumulator = new PartialBunchAccumulator(new PartialBunchLimits(), pool);
        using (var initial = Payload(0))
        {
            accumulator.AddFragment(18, Header(18, 60, initial: true, final: false), initial, new BunchPayloadStats());
        }

        using (var final = Payload(0))
        {
            accumulator.AddFragment(18, Header(18, 61, initial: false, final: true, sequence: 1), final, new BunchPayloadStats());
        }

        Assert.That(accumulator.TryComplete(18, out var transferredOwner, out var bits, out _), Is.True);
        Assert.That(bits, Is.Zero);
        Assert.That(accumulator.TryComplete(18, out _, out _, out _), Is.False);
        accumulator.Dispose();
        Assert.That(pool.Owners.Single().DisposeCount, Is.Zero);
        transferredOwner.Dispose();
        Assert.That(pool.Owners.Single().DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void FragmentsWithNonByteAlignedBoundaries_AreConcatenatedBitwise()
    {
        var pool = new CountingMemoryPool();
        using var accumulator = new PartialBunchAccumulator(new PartialBunchLimits(), pool);
        var stats = new BunchPayloadStats();

        using (var first = Payload(3, 0b0000_0101))
        {
            accumulator.AddFragment(21, Header(21, 70, initial: true, final: false), first, stats);
        }

        using (var second = Payload(2, 0b0000_0010))
        {
            accumulator.AddFragment(21, Header(21, 71, initial: false, final: false, sequence: 1), second, stats);
        }

        using (var final = Payload(3, 0b0000_0011))
        {
            accumulator.AddFragment(21, Header(21, 72, initial: false, final: true, sequence: 2), final, stats);
        }

        Assert.That(accumulator.TryComplete(21, out var owner, out var bitCount, out _), Is.True);
        using (owner)
        {
            Assert.That(bitCount, Is.EqualTo(8));
            Assert.That(owner.Memory.Span[0], Is.EqualTo(0x75));
        }

        Assert.Multiple(() =>
        {
            Assert.That(stats.PartialFragmentCount, Is.EqualTo(3));
            Assert.That(stats.CompletedPartialBunchCount, Is.EqualTo(1));
            Assert.That(stats.PartialErrorCount, Is.Zero);
            Assert.That(accumulator.RetainedCapacityBytes, Is.Zero);
        });
    }

    private static FBitArchive Payload(int bitCount, byte value = 0) =>
        new BitArchiveReader(bitCount == 0 ? ReadOnlyMemory<byte>.Empty : new byte[] { value }, bitCount);

    private static RawBunchHeader Header(
        uint channelIndex,
        int packetId,
        bool initial,
        bool final,
        int sequence = 0) =>
        new()
        {
            ChIndex = channelIndex,
            PacketId = packetId,
            ChSequence = sequence,
            bPartial = true,
            bPartialInitial = initial,
            bPartialFinal = final,
            bReliable = true,
        };

    private sealed class CountingMemoryPool(Func<int, int>? capacitySelector = null) : MemoryPool<byte>
    {
        private readonly Func<int, int> _capacitySelector = capacitySelector ?? (minimum => Math.Max(minimum, 1));

        public List<CountingOwner> Owners { get; } = [];

        public override int MaxBufferSize => int.MaxValue;

        public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
        {
            var capacity = _capacitySelector(Math.Max(minBufferSize, 0));
            var owner = new CountingOwner(new byte[capacity]);
            Owners.Add(owner);
            return owner;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    private sealed class CountingOwner(byte[] buffer) : IMemoryOwner<byte>
    {
        public int DisposeCount { get; private set; }

        public Memory<byte> Memory => buffer;

        public void Dispose() => DisposeCount++;
    }
}
