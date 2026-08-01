using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;

namespace Replay.Valorant.Descriptors.Agents.Mage.TidalWave;

public sealed class MulticastInitializeParameters
    : ExportGroupDescriptor<MulticastInitializeParameters>
{
    public override string Path => TidalWavePaths.ChunkInitialize;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public int? ChunkIndex { get; set; }
    public int? Generation { get; set; }
    public int? NumChunks { get; set; }
    public float? ChunkSpacing { get; set; }
    public double? VelocityIn { get; set; }
    public double? AnchorSpacingIn { get; set; }
    public int? NumCrossfadeAnchorsIn { get; set; }
    public uint? PreviousChunk { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.ChunkIndex).Int32();
        AddPropertyHandle(1, x => x.Generation).Int32();
        AddPropertyHandle(2, "Num Chunks", x => x.NumChunks).Int32();
        AddPropertyHandle(3, x => x.ChunkSpacing).Float();
        AddPropertyHandle(4, "VelocityIn", x => x.VelocityIn).Double();
        AddPropertyHandle(5, "Anchor Spacing In", x => x.AnchorSpacingIn).Double();
        AddPropertyHandle(6, "Num Crossfade Anchors In", x => x.NumCrossfadeAnchorsIn).Int32();
        AddPropertyHandle(7, x => x.PreviousChunk).ObjectNetGuid();
    }
}

public sealed class MulticastWallStartLingerParameters
    : ExportGroupDescriptor<MulticastWallStartLingerParameters>
{
    public override string Path => TidalWavePaths.ChunkWallStartLinger;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public double? LingerWallStopPosition { get; set; }
    public bool? FinalEndpointReached { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.LingerWallStopPosition).Double();
        AddPropertyHandle(1, x => x.FinalEndpointReached).Bool();
    }
}

public sealed class MulticastStopWaveParameters
    : ExportGroupDescriptor<MulticastStopWaveParameters>
{
    public override string Path => TidalWavePaths.WaveStop;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public bool? FinalEndpointReached { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.FinalEndpointReached).Bool();
    }
}

public sealed class NetMulticastRemoveForceModuleParameters
    : ExportGroupDescriptor<NetMulticastRemoveForceModuleParameters>
{
    public override string Path =>
        "/Script/ShooterGame.ForceModuleManagerComponent:NetMulticastRemoveForceModule";

    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public int? HandleNumber { get; set; }
    public uint? ModuleType { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.HandleNumber).Int32();
        AddPropertyHandle(1, x => x.ModuleType).EnumRemainingBits();
    }
}
