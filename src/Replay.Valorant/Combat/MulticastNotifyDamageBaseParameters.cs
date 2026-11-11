using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Combat;

public sealed class MulticastNotifyDamageBaseParameters : DamageParameters<MulticastNotifyDamageBaseParameters>
{
    public MulticastNotifyDamageBaseParameters()
        : this(hasKillCreditPlayerState: true)
    {
    }

    public MulticastNotifyDamageBaseParameters(bool hasKillCreditPlayerState, bool hasDamageIcon = false)
        : base(hasKillCreditPlayerState, hasDamageIcon)
    {
    }

    public override string Path => "/Script/ShooterGame.DamageableComponent:MulticastNotifyDamage_Base";

    public int KillsForKiller { get; set; }
    public DamageAssist?[]? AssistsList { get; set; }
    public int KillsForVictim { get; set; }
    public NetObjectReference DeathAnimMontage { get; set; }
    public NetObjectReference? DeathMontageEffectOverride { get; set; }
    public NetObjectReference? DeathMontageEffectOverrideContext { get; set; }
    public bool DeathMontageEffectOverrideIsQueued { get; set; }

    protected override void Configure()
    {
        AddSharedFields();
        AddPropertyHandle(LayoutHandle(26), x => x.AssistsList).RepLayoutDynamicArray(
            new Replay.Models.Replay.VersionedDefinition<DamageAssist>(new(LayoutHandle(27))));
        AddDeathFields(LayoutHandle(32));
    }

    private void AddDeathFields(uint firstHandle)
    {
        AddPropertyHandle(firstHandle, x => x.KillsForKiller, ExportCategory.Gunplay).Int32();
        AddPropertyHandle(firstHandle + 1, x => x.KillsForVictim, ExportCategory.Gunplay).Int32();
        AddPropertyHandle(firstHandle + 2, x => x.DeathAnimMontage, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(firstHandle + 3, x => x.DeathMontageEffectOverride).ObjectNetGuid();
        AddPropertyHandle(firstHandle + 4, x => x.DeathMontageEffectOverrideContext).ObjectNetGuid();
        AddPropertyHandle(firstHandle + 5, "bDeathMontageEffectOverrideIsQueued", x => x.DeathMontageEffectOverrideIsQueued, ExportCategory.Gunplay).Bool();
    }
}
