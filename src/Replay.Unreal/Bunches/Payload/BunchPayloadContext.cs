using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Unreal.Channels;
using Replay.Unreal.Readers;

namespace Replay.Unreal.Bunches.Payload;

internal sealed class BunchPayloadContext : IDisposable
{
    private FBitArchive? _ownedPayload;

    public BunchPayloadContext(ReplayReaderContext readerContext, RawBunchHeader header, FBitArchive payload)
    {
        ReaderContext = readerContext;
        Header = header;
        Payload = payload;
    }

    public ReplayReaderContext ReaderContext { get; }

    public RawBunchHeader Header { get; set; }

    public FBitArchive Payload { get; private set; }

    public BunchPayloadStats Stats => ReaderContext.BunchPayloadStats;

    public ActorChannelState? Channel { get; set; }

    public bool OpenedDynamicActor { get; set; }

    public void UseOwnedPayload(FBitArchive payload)
    {
        _ownedPayload?.Dispose();
        _ownedPayload = payload;
        Payload = payload;
    }

    public void Dispose()
    {
        _ownedPayload?.Dispose();
    }
}
