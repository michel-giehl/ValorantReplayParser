namespace Replay.Unreal.Bunches.Payload;

internal interface IBunchPayloadStage
{
    BunchStageResult Process(BunchPayloadContext context);
}