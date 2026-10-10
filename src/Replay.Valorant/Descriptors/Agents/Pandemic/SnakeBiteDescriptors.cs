using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;

namespace Replay.Valorant.Descriptors.Agents.Pandemic;

/// <summary>Snake Bite wire exports observed in the 13.06 replay 12438a1c.</summary>
public static class SnakeBiteDescriptors
{
    public const string Equipment = "/Game/Characters/Pandemic/S0/Ability_Q/Ability_Pandemic_Q_AcidGrenade.Ability_Pandemic_Q_AcidGrenade_C";
    public const string Projectile = "/Game/Characters/Pandemic/S0/Ability_Q/Projectile_Pandemic_Q_AcidGrenade.Projectile_Pandemic_Q_AcidGrenade_C";
    public const string Patch = "/Game/Characters/Pandemic/S0/Ability_Q/Patch_Pandemic_AcidMolotov_NewMolotov.Patch_Pandemic_AcidMolotov_NewMolotov_C";

    public static IReadOnlyList<ExportGroupDescriptor> CreateDescriptors() =>
    [
        Actor(Equipment, ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
        Actor(Projectile, ["Owner", "Instigator", "ReplicatedMovement"]),
        new SnakeBitePatchDescriptor(),
    ];

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        Rpc(Equipment, "MulticastOnItemMovedToPersistentData", 0),
        Rpc(Projectile, "MulticastStopProjectile", 3),
        Rpc(Patch, "BeginFadeOut", 0),
    ];
}

public sealed class SnakeBitePatchDescriptor : ExportGroupDescriptor<SnakeBitePatchDescriptor>
{
    public override string Path => SnakeBiteDescriptors.Patch;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }
    // Retain the wire flag without claiming it identifies a damaged/vulnerable target.
    public bool? HasSuccessfullyHit { get; set; }

    protected override void Configure()
    {
        AddProperty("Owner", x => x.Owner).ObjectNetGuid();
        AddProperty("Instigator", x => x.Instigator).ObjectNetGuid();
        AddProperty("Has Succesfully Hit", x => x.HasSuccessfullyHit).Bool();
    }
}
