namespace Replay.Unreal.Bunches.Payload;

internal interface IBunchPayloadProcessor
{
    void Process(BunchPayloadContext context);

    void Reset();
}
