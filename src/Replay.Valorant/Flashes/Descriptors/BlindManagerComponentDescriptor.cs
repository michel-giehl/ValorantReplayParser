using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Flashes.Descriptors;

public sealed class ActiveBlindDescriptor : ExportGroupDescriptor<ActiveBlindDescriptor>
{
    public uint BlindId { get; set; }
    public ulong? EffectId { get; set; }
    public string? SourceId { get; set; }
    public bool? IsLocalEffect { get; set; }
    public bool? IsTransient { get; set; }
    public float? InitialDuration { get; set; }
    public float? StartNetMovementTime { get; set; }
    public uint? BlindConfig { get; set; }
    public uint? CausingActor { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(3, x => x.BlindId).UInt32();
        AddPropertyHandle(4, "EffectID", x => x.EffectId).UInt64();
        AddPropertyHandle(5, "SourceID", x => x.SourceId).FName();
        AddPropertyHandle(6, "bLocalEffect", x => x.IsLocalEffect).Bool();
        AddPropertyHandle(7, "bTransient", x => x.IsTransient).Bool();
        AddPropertyHandle(8, x => x.InitialDuration).Float();
        AddPropertyHandle(9, x => x.StartNetMovementTime).Float();
        AddPropertyHandle(10, x => x.BlindConfig).ObjectNetGuid();
        AddPropertyHandle(11, x => x.CausingActor).ObjectNetGuid();
    }
}

public sealed class BlindManagerComponentDescriptor
    : ExportGroupDescriptor<BlindManagerComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.BlindManagerComponent";
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public ActiveBlindDescriptor[]? ActiveBlinds { get; set; }
    public float? LongestActiveBlindDuration { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(2, x => x.ActiveBlinds).RepLayoutDynamicArray<ActiveBlindDescriptor>();
        AddPropertyHandle(13, x => x.LongestActiveBlindDuration).Float();
    }
}
