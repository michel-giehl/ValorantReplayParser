using Replay.Models.Descriptors;
using Replay.Models.Replay;

namespace Replay.Valorant.Combat;

public sealed class DamageableComponentClassNetCacheDescriptor
    : ClassNetCacheDescriptor<DamageableComponentClassNetCacheDescriptor>
{
    public override string Path => "/Script/ShooterGame.DamageableComponent_ClassNetCache";
    protected override void Configure()
    {
        AddFunctionHandle(
            0, "MulticastNotifyDamage_Base",
            "/Script/ShooterGame.DamageableComponent:MulticastNotifyDamage_Base",
            new VersionedDefinition<MulticastNotifyDamageBaseParameters>(new(hasKillCreditPlayerState: false))
                .From(DamageResponseLayout.KillCreditPlayerStateRelease, new())
                .From(DamageResponseLayout.DamageIconRelease, new(hasKillCreditPlayerState: true, hasDamageIcon: true)),
            ExportCategory.Gunplay);
        AddFunctionHandle(
            1, "MulticastNotifyDamage_Point",
            "/Script/ShooterGame.DamageableComponent:MulticastNotifyDamage_Point",
            new VersionedDefinition<MulticastNotifyDamagePointParameters>(new(hasKillCreditPlayerState: false))
                .From(DamageResponseLayout.KillCreditPlayerStateRelease, new())
                .From(DamageResponseLayout.DamageIconRelease, new(hasKillCreditPlayerState: true, hasDamageIcon: true)),
            ExportCategory.Gunplay);
        AddFunctionHandle<MulticastNotifyHealParameters>(
            6, "MulticastNotifyHeal", "/Script/ShooterGame.DamageableComponent:MulticastNotifyHeal", ExportCategory.Gunplay);
        AddFunctionHandle<MulticastNotifyOverhealDecayParameters>(
            7, "MulticastNotifyOverhealDecay", "/Script/ShooterGame.DamageableComponent:MulticastNotifyOverhealDecay", ExportCategory.Gunplay);
        AddFunctionHandle<MulticastSectionLifeChangeParameters>(
            8, "MulticastSectionLifeChange", "/Script/ShooterGame.DamageableComponent:MulticastSectionLifeChange", ExportCategory.Gunplay);
    }
}
