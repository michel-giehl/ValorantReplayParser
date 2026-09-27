using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Clay;

public sealed class ClayBoomBotClassNetCacheDescriptor : ClassNetCacheDescriptor<ClayBoomBotClassNetCacheDescriptor>
{
    public override string Path => ClayPaths.BoomBotPawn + "_ClassNetCache";
    protected override void Configure() => AddFunctionHandle<ClayResetRemoteMovementPredictionParameters>(6,
        "ClientResetRemoteMovementPrediction", ClayPaths.BoomBotPawn + ":ClientResetRemoteMovementPrediction", ExportCategory.Ability);
}

public sealed class ClayResetRemoteMovementPredictionParameters : ExportGroupDescriptor<ClayResetRemoteMovementPredictionParameters>
{
    public override string Path => ClayPaths.BoomBotPawn + ":ClientResetRemoteMovementPrediction";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public bool? IsPossess { get; set; }
    protected override void Configure() => AddPropertyHandle(0, "isPossess", x => x.IsPossess).Bool();
}
