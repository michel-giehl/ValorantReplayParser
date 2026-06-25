using Replay.Encoding.Archives;
using Replay.Unreal.Channels;

namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class ActorChannelOpenBunchStage : IBunchPayloadStage
{
    private readonly INewActorSerializer _newActorSerializer;
    private readonly IActorChannelLifecycleService _lifecycleService;

    public ActorChannelOpenBunchStage(
        INewActorSerializer newActorSerializer,
        IActorChannelLifecycleService lifecycleService)
    {
        _newActorSerializer = newActorSerializer;
        _lifecycleService = lifecycleService;
    }

    public BunchStageResult Process(ref BunchPayloadContext context)
    {
        if (!ShouldOpenChannel(context))
        {
            return BunchStageResult.Continue;
        }

        try
        {
            OpenChannel(ref context);
            return BunchStageResult.Continue;
        }
        catch (ArchiveReadException)
        {
            context.Stats.MalformedPayloadCount++;
            context.Stats.MalformedActorOpenCount++;
            context.Payload.SkipRemaining();
            return BunchStageResult.Stop;
        }
    }

    private static bool ShouldOpenChannel(BunchPayloadContext context) =>
        context.Header.bOpen && !HasOpenChannel(context);

    private static bool HasOpenChannel(BunchPayloadContext context) =>
        context.ReaderContext.ChannelStates.TryGetValue(context.Header.ChIndex, out var channel) && channel.IsOpen;

    private void OpenChannel(ref BunchPayloadContext context)
    {
        var channel = CreateChannelState(context);
        _newActorSerializer.Serialize(context.Payload, channel, context.Header.bClose);

        context.ReaderContext.ChannelStates[context.Header.ChIndex] = channel;
        context.ReaderContext.ActorChannelOpens.Add(channel);
        _lifecycleService.OpenActor(channel, context.Stats);

        context.Channel = channel;
        context.OpenedDynamicActor = channel.ActorNetGuid.IsDynamic;
        context.Stats.ActorChannelOpenCount++;
        context.Stats.ActorSerializeNewActorCount++;
    }

    private static ActorChannelState CreateChannelState(BunchPayloadContext context) => new()
    {
        ChannelIndex = context.Header.ChIndex,
        IsOpen = true,
        OpenPacketId = context.Header.PacketId,
        OpenTimeSeconds = context.ReaderContext.CurrentTimeSeconds,
    };
}
