using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Cable;

/// <summary>Deadlock's named wall endpoints, spline points and pull-state flag.</summary>
public sealed class CableGeometryParameters(string path, string kind) : ExportGroupDescriptor<CableGeometryParameters>
{
    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public override object CreatePayloadInstance() => new CableGeometryParameters(path, kind);
    public FVector? Start { get; set; }
    public FVector? End { get; set; }
    public FVector? Direction { get; set; }
    public CableSplinePoint[]? Points { get; set; }
    public bool? Pulling { get; set; }
    protected override void Configure()
    {
        if (kind == "wall")
        {
            AddPropertyHandle(0, "Destination", x => x.End).FVector();
            AddPropertyHandle(1, "RootLocation", x => x.Start).FVector();
        }
        if (kind == "segment")
        {
            AddPropertyHandle(0, "SegmentLocation_5_FF06CDF14F98D9DF52A415ABFFED5C1B", x => x.Start).FVector();
            AddPropertyHandle(1, "SegmentEndLocation_27_F527C723412B04D1B7B70DBCA8DB9274", x => x.End).FVector();
            AddPropertyHandle(2, "SegmentForwardVector_6_90F49F23446D7962D7884A81F1216F08", x => x.Direction).FVector();
        }
        if (kind == "pulling") AddPropertyHandle(0, "Is Pulling", x => x.Pulling).Bool();
        if (kind == "spline") AddPropertyHandle(1, "PlayerTrajectoryPoints", x => x.Points).RepLayoutDynamicArray<CableSplinePoint>();
    }
}

public sealed class CableSplinePoint : ExportGroupDescriptor<CableSplinePoint>
{
    public override string Path => "/Game/Characters/Cable/S0/Ability_X/GameObject_Spline.GameObject_Spline_C:PlayerTrajectoryPoints";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Unknown;
    public FVector? Position { get; set; }
    protected override void Configure() => AddPropertyHandle(2, "PlayerTrajectoryPoints", x => x.Position).FVector();
}
