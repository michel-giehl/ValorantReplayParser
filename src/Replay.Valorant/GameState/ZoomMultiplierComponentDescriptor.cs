using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

public sealed class ZoomMultiplierComponentDescriptor : ExportGroupDescriptor<ZoomMultiplierComponentDescriptor>
{
    public const string ExportPath = "/Script/ShooterGame.ZoomMultiplierComponent";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.Gunplay;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public int? SourceZoomLevel { get; set; }
    public float? TargetFov { get; set; }
    public int? TargetZoomLevel { get; set; }
    public float? TotalTransitionTimeDuration { get; set; }
    public byte? TransitionState { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.SourceZoomLevel).Int32();
        AddProperty(x => x.TargetFov).Float();
        AddProperty(x => x.TargetZoomLevel).Int32();
        AddProperty(x => x.TotalTransitionTimeDuration).Float();
        AddProperty(x => x.TransitionState).EnumRemainingBits();
    }
}
