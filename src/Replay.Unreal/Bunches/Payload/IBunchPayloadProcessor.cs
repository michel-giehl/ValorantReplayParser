namespace Replay.Unreal.Bunches.Payload;

internal interface IBunchPayloadProcessor
{
    void Process(ref BunchPayloadContext context);

    void Reset();
}
