using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

public sealed class OrbPickedUpRpcParameters : ExportGroupDescriptor<OrbPickedUpRpcParameters>
{
    public override string Path => "/Game/BaseGameState.BaseGameState_C:OrbPickedUpRPC";
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public uint OrbGatherer { get; set; }
    public uint CollectableOrb { get; set; }

    protected override void Configure()
    {
        AddProperty("Orb Gatherer", x => x.OrbGatherer).ObjectNetGuid();
        AddProperty("Collectable Orb", x => x.CollectableOrb).ObjectNetGuid();
    }
}
