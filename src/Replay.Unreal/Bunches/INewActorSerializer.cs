using Replay.Encoding.Archives;
using Replay.Unreal.Channels;

namespace Replay.Unreal.Bunches;

internal interface INewActorSerializer
{
    void Serialize(FBitArchive payload, ActorChannelState channelState, bool isClosingChannel);
}