using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Patches;

/// <summary>The flattened NewGrid parameter of CellularPatch.NetMulticastUpdateNodeGrid.</summary>
public sealed class CellularPatchNodeGridParameters : ExportGroupDescriptor<CellularPatchNodeGridParameters>
{
    public const string ExportPath = "/Script/ShooterGame.CellularPatch:NetMulticastUpdateNodeGrid";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public int? MinX { get; set; }
    public int? MinY { get; set; }
    public int? MaxX { get; set; }
    public int? MaxY { get; set; }
    public float? LowestNode { get; set; }
    public float? HighestNode { get; set; }
    public float? MaxDistance { get; set; }
    public CellularPatchNode[]? Nodes { get; set; }
    public bool? IsValid { get; set; }
    public int? LineSize { get; set; }
    protected override void Configure()
    {
        AddPropertyHandle(0, "X", x => x.MinX).Int32();
        AddPropertyHandle(1, "Y", x => x.MinY).Int32();
        AddPropertyHandle(2, "X", x => x.MaxX).Int32();
        AddPropertyHandle(3, "Y", x => x.MaxY).Int32();
        AddPropertyHandle(4, "LowestNode", x => x.LowestNode).Float();
        AddPropertyHandle(5, "HeighestNode", x => x.HighestNode).Float();
        AddPropertyHandle(6, "MaxDistance", x => x.MaxDistance).Float();
        AddPropertyHandle(7, "NodeArray", x => x.Nodes).RepLayoutDynamicArray<CellularPatchNode>();
        AddPropertyHandle(15, "bPatchIsValid", x => x.IsValid).Bool();
        AddPropertyHandle(16, "LineSize", x => x.LineSize).Int32();
    }
}

/// <summary>PatchNode fields retain the exported handles within the containing function's array.</summary>
public sealed class CellularPatchNode : ExportGroupDescriptor<CellularPatchNode>
{
    public override string Path => "/Script/ShooterGame.PatchNode";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Unknown;
    public float? FloorZ { get; set; }
    public float? CeilingZ { get; set; }
    public float? Distance { get; set; }
    public FVector? Normal { get; set; }
    public bool? EdgePoint { get; set; }
    public uint? Status { get; set; }
    protected override void Configure()
    {
        AddPropertyHandle(8, "FloorZ", x => x.FloorZ).Float();
        AddPropertyHandle(9, "CeilingZ", x => x.CeilingZ).Float();
        AddPropertyHandle(10, "Distance", x => x.Distance).Float();
        AddPropertyHandle(11, "Normal", x => x.Normal).FVectorNetQuantizeNormal();
        AddPropertyHandle(12, "bEdgePoint", x => x.EdgePoint).Bool();
        AddPropertyHandle(13, "Status", x => x.Status).EnumRemainingBits();
    }
}
