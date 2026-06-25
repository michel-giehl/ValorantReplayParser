using Replay.Unreal.PackageMap;

namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class PackageMapExportBunchStage : IBunchPayloadStage
{
    private readonly PackageMapReader _packageMapReader;

    public PackageMapExportBunchStage(PackageMapReader packageMapReader)
    {
        _packageMapReader = packageMapReader;
    }

    public BunchStageResult Process(ref BunchPayloadContext context)
    {
        if (!context.Header.bHasPackageMapExports)
        {
            return BunchStageResult.Continue;
        }

        context.Stats.PackageMapExportBunchCount++;
        _packageMapReader.ReceiveNetGUIDBunch(context.Payload, context.Stats);
        return BunchStageResult.Continue;
    }
}
