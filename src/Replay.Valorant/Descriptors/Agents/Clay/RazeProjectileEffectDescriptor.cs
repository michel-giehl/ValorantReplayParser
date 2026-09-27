using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Clay;

public sealed class RazeProjectileEffectDescriptor : ClassNetCacheDescriptor<RazeProjectileEffectDescriptor>
{
    public const string ClassPath = "/Game/Characters/Global/Projectiles/Cosmetics/Comp_Projectile_CosmeticPlayEffectUntilTrigger.Comp_Projectile_CosmeticPlayEffectUntilTrigger_C";
    public override string Path => ClassPath + "_ClassNetCache";
    protected override void Configure() => AddFunctionHandle(0, "Multicast Event Triggered",
        ClassPath + ":Multicast Event Triggered", ExportCategory.Ability | ExportCategory.Effects)
        .Decode(ValorantPayloadDecoders.NoParametersRpc);
}
