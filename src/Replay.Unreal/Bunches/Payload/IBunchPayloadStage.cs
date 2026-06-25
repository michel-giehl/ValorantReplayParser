namespace Replay.Unreal.Bunches.Payload;

internal interface IBunchPayloadStage
{
    BunchStageResult Process(ref BunchPayloadContext context);
}
