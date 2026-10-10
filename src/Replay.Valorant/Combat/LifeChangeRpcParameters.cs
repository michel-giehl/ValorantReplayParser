using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Combat;

public sealed class HealLifeChangeEvent : ExportGroupDescriptor<HealLifeChangeEvent>
{
    public override string Path => "/Script/ShooterGame.LifeChangeEvent:Heal";
    public override ExportCategory Categories => ExportCategory.Gunplay;
    public uint ChangedComponent { get; set; }
    public float LifeResult { get; set; }
    public float DeltaLife { get; set; }
    public bool AliveAfterChange { get; set; }
    protected override void Configure()
    {
        AddPropertyHandle(2, x => x.ChangedComponent).ObjectNetGuid();
        AddPropertyHandle(3, x => x.LifeResult).Float();
        AddPropertyHandle(4, x => x.DeltaLife).Float();
        AddPropertyHandle(5, "bAliveAfterChange", x => x.AliveAfterChange).Bool();
    }
}

public abstract class HealParameters<T> : ExportGroupDescriptor<T> where T : HealParameters<T>
{
    public override ExportCategory Categories => ExportCategory.Gunplay;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public float Amount { get; set; }
    public HealLifeChangeEvent?[]? LifeChanges { get; set; }
    public uint EventInstigator { get; set; }
    public uint EventInstigatorPawn { get; set; }
    public uint Causer { get; set; }
    protected void AddSharedFields(string amountName, string causerName)
    {
        AddPropertyHandle(0, amountName, x => x.Amount).Float();
        AddPropertyHandle(1, "LifeChangeBySection", x => x.LifeChanges).RepLayoutDynamicArray<HealLifeChangeEvent>();
        AddPropertyHandle(7, x => x.EventInstigator).ObjectNetGuid();
        AddPropertyHandle(8, x => x.EventInstigatorPawn).ObjectNetGuid();
        AddPropertyHandle(9, causerName, x => x.Causer).ObjectNetGuid();
    }
}

public sealed class MulticastNotifyHealParameters : HealParameters<MulticastNotifyHealParameters>
{
    public override string Path => "/Script/ShooterGame.DamageableComponent:MulticastNotifyHeal";
    protected override void Configure() => AddSharedFields("HealTaken", "HealCauser");
}
public sealed class MulticastNotifyOverhealDecayParameters : HealParameters<MulticastNotifyOverhealDecayParameters>
{
    public override string Path => "/Script/ShooterGame.DamageableComponent:MulticastNotifyOverhealDecay";
    protected override void Configure() => AddSharedFields("DecayApplied", "DecayCauser");
}

public sealed class SectionLifeChangeEvent : ExportGroupDescriptor<SectionLifeChangeEvent>
{
    public override string Path => "/Script/ShooterGame.LifeChangeEvent:SectionLifeChange";
    public override ExportCategory Categories => ExportCategory.Gunplay;
    public uint ChangedComponent { get; set; }
    public float LifeResult { get; set; }
    public float DeltaLife { get; set; }
    public bool AliveAfterChange { get; set; }
    protected override void Configure()
    {
        AddPropertyHandle(1, x => x.ChangedComponent).ObjectNetGuid();
        AddPropertyHandle(2, x => x.LifeResult).Float();
        AddPropertyHandle(3, x => x.DeltaLife).Float();
        AddPropertyHandle(4, "bAliveAfterChange", x => x.AliveAfterChange).Bool();
    }
}
public sealed class MulticastSectionLifeChangeParameters : ExportGroupDescriptor<MulticastSectionLifeChangeParameters>
{
    public override string Path => "/Script/ShooterGame.DamageableComponent:MulticastSectionLifeChange";
    public override ExportCategory Categories => ExportCategory.Gunplay;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public SectionLifeChangeEvent?[]? LifeChanges { get; set; }
    public bool AliveOnServer { get; set; }
    public uint Character { get; set; }
    public float NetTimestamp { get; set; }
    public int RespawnNumber { get; set; }
    public int LifeChangeEventIndex { get; set; }
    protected override void Configure()
    {
        AddPropertyHandle(0, "LifeChangeEvents", x => x.LifeChanges).RepLayoutDynamicArray<SectionLifeChangeEvent>();
        AddPropertyHandle(6, "bAliveOnServer", x => x.AliveOnServer).Bool();
        AddPropertyHandle(7, x => x.Character).ObjectNetGuid();
        AddPropertyHandle(8, x => x.NetTimestamp).Float();
        AddPropertyHandle(9, x => x.RespawnNumber).Int32();
        AddPropertyHandle(10, x => x.LifeChangeEventIndex).Int32();
    }
}
