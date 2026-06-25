using Replay.Models.Net;
using Replay.Unreal.Channels;

namespace Replay.Unreal.Bunches;

internal interface IActorChannelLifecycleService
{
    void OpenActor(ActorChannelState channel, BunchPayloadStats stats);

    void CloseActorChannel(ActorChannelState channel, RawBunchHeader header, BunchPayloadStats stats);
}