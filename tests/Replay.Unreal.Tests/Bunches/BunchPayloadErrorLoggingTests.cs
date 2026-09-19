using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Models.Errors;
using Replay.Models.Net;
using Replay.Unreal.Bunches;
using Replay.Unreal.Bunches.Payload;
using Replay.Unreal.Bunches.Payload.Stages;
using Replay.Unreal.Channels;
using Replay.Unreal.PackageMap;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Tests.Bunches;

public class BunchPayloadErrorLoggingTests
{
    [Test]
    public void MustBeMappedGuidsFailure_ThrowsContextualDataExceptionWithoutErrorLog()
    {
        var loggerFactory = new CapturingLoggerFactory();
        var readerContext = CreateReaderContext(loggerFactory);
        var payload = new BitArchiveReader(ReadOnlyMemory<byte>.Empty, bitCount: 0);
        var context = new BunchPayloadContext(
            readerContext,
            new RawBunchHeader { PacketId = 12, ChIndex = 3, bHasMustBeMappedGUIDs = true },
            payload);

        var exception = Assert.Throws<InvalidReplayDataException>(() =>
            new MustBeMappedGuidsBunchStage().Process(ref context));

        AssertContextualFailure(loggerFactory, exception!, "must-be-mapped GUIDs", packetId: 12, channelIndex: 3);
        Assert.That(readerContext.BunchPayloadStats.MalformedMustBeMappedGuidCount, Is.Zero);
    }

    [Test]
    public void ActorChannelOpenFailure_ThrowsContextualDataExceptionWithoutErrorLog()
    {
        var loggerFactory = new CapturingLoggerFactory();
        var readerContext = CreateReaderContext(loggerFactory);
        var payload = new BitArchiveReader(ReadOnlyMemory<byte>.Empty, bitCount: 0);
        var context = new BunchPayloadContext(
            readerContext,
            new RawBunchHeader { PacketId = 21, ChIndex = 5, bOpen = true },
            payload);
        var stage = new ActorChannelOpenBunchStage(
            new ThrowingNewActorSerializer(),
            new NoOpActorChannelLifecycleService());

        var exception = Assert.Throws<InvalidReplayDataException>(() => stage.Process(ref context));

        AssertContextualFailure(loggerFactory, exception!, "actor-channel open", packetId: 21, channelIndex: 5);
        Assert.That(readerContext.BunchPayloadStats.MalformedActorOpenCount, Is.Zero);
    }

    [Test]
    public void ContentBlocksFailure_ThrowsContextualDataExceptionWithoutErrorLog()
    {
        var loggerFactory = new CapturingLoggerFactory();
        var readerContext = CreateReaderContext(loggerFactory);
        var payload = new BitArchiveReader(new byte[] { 0 }, bitCount: 1);
        var context = new BunchPayloadContext(
            readerContext,
            new RawBunchHeader { PacketId = 34, ChIndex = 8 },
            payload)
        {
            Channel = new ActorChannelState { ChannelIndex = 8 },
        };
        var framer = new ContentBlockFramer(new PackageMapReader(readerContext.NetGuidCache), readerContext);

        var exception = Assert.Throws<InvalidReplayDataException>(() =>
            new ContentBlocksBunchStage(framer).Process(ref context));

        AssertContextualFailure(loggerFactory, exception!, "content blocks", packetId: 34, channelIndex: 8);
        Assert.That(readerContext.BunchPayloadStats.MalformedPayloadExceptionCount, Is.Zero);
    }

    private static ReplayReaderContext CreateReaderContext(ILoggerFactory loggerFactory) =>
        new(new FBinaryArchive(ReadOnlyMemory<byte>.Empty), loggerFactory: loggerFactory);

    private static void AssertContextualFailure(
        CapturingLoggerFactory loggerFactory,
        InvalidReplayDataException exception,
        string operation,
        int packetId,
        uint channelIndex)
    {
        Assert.Multiple(() =>
        {
            Assert.That(loggerFactory.Entries, Is.Empty);
            Assert.That(exception.InnerException, Is.TypeOf<ArchiveReadException>());
            Assert.That(exception.Message, Does.Contain(operation));
            Assert.That(exception.Message, Does.Contain($"packet {packetId}"));
            Assert.That(exception.Message, Does.Contain($"channel {channelIndex}"));
            Assert.That(exception.Message, Does.Contain("payload position"));
        });
    }

    private sealed class ThrowingNewActorSerializer : INewActorSerializer
    {
        public void Serialize(FBitArchive payload, ActorChannelState channelState, bool isClosingChannel) =>
            throw CreateArchiveException(nameof(ThrowingNewActorSerializer));
    }

    private sealed class NoOpActorChannelLifecycleService : IActorChannelLifecycleService
    {
        public void OpenActor(ActorChannelState channel, BunchPayloadStats stats)
        {
        }

        public void CloseActorChannel(ActorChannelState channel, RawBunchHeader header, BunchPayloadStats stats)
        {
        }
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

    private static ArchiveReadException CreateArchiveException(string operation) =>
        new(ArchiveErrorCode.EndOfArchive, operation, position: 0, length: 0, requested: 1);
}
