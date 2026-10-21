using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

public sealed class TimedBombDescriptor : ExportGroupDescriptor<TimedBombDescriptor>
{
    public const string ExportPath = "/Game/GameModes/Bomb/TimedBomb.TimedBomb_C";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public double? TimeRemainingToExplode { get; set; }
    public byte? PlantedAtSite { get; set; }
    public bool? BombHasBeenDefused { get; set; }
    public byte? BombDefuseState { get; set; }
    public NetObjectReference? CurrentDefuser { get; set; }
    public double? DefuseProgress { get; set; }
    public int? CurrentDefuseSection { get; set; }
    public bool? BombInitializeComplete { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.TimeRemainingToExplode).Double();
        AddProperty(x => x.PlantedAtSite).EnumRemainingBits();
        AddProperty(x => x.BombHasBeenDefused).Bool();
        AddProperty(x => x.BombDefuseState).EnumRemainingBits();
        AddProperty(x => x.CurrentDefuser).ObjectNetGuid();
        AddProperty(x => x.DefuseProgress).Double();
        AddProperty(x => x.CurrentDefuseSection).Int32();
        AddProperty(x => x.BombInitializeComplete).Bool();
    }
}
