using Replay.Encoding.Archives;

namespace Replay.Unreal.Bunches.Payload;

internal interface IPropertyPayloadDecoder
{
    FBitArchive Decode(FBitArchive payload, uint actorNetGuid, string replayVersion);
}
