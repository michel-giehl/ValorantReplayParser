using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

/// <summary>64-bit fuel and 1-bit drain flag verified in d62adeea (Neon/Skye).</summary>
public sealed class AbilityFuelComponentDescriptor : ExportGroupDescriptor<AbilityFuelComponentDescriptor>
{
    public override string Path => "/Game/Characters/Components/Comp_AbilityFuelSystem.Comp_AbilityFuelSystem_C";
    public override ExportCategory Categories => ExportCategory.Inventory | ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;
    public double CurrentFuel { get; set; }
    public bool IsFuelDraining { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.CurrentFuel).Double();
        AddProperty(x => x.IsFuelDraining).Bool();
    }
}
public sealed class AbilityGunAmmoResourceDescriptor : ResourceComponentDescriptor<AbilityGunAmmoResourceDescriptor>
{
    public override string Path => "/Game/Characters/Components/Comp_Ability_GunAmmoResourceComponent.Comp_Ability_GunAmmoResourceComponent_C";
}
public sealed class ExternalResourceComponentDescriptor : ExportGroupDescriptor<ExternalResourceComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.ExternalResourceComponent";
    public override ExportCategory Categories => ExportCategory.Inventory;
    public override ExportGroupKind Kind => ExportGroupKind.Component;
    public byte ExternalSlot { get; set; }
    protected override void Configure() => AddProperty(x => x.ExternalSlot).Byte();
}
public sealed class BlueprintResourceVisualizationDescriptor : ExportGroupDescriptor<BlueprintResourceVisualizationDescriptor>
{
    public override string Path => "/Script/ShooterGame.BlueprintResourceVisualizationComponent";
    public override ExportCategory Categories => ExportCategory.Inventory;
    public override ExportGroupKind Kind => ExportGroupKind.Component;
    public int Ammo { get; set; }
    public int MaxAmmo { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.Ammo).Int32();
        AddProperty(x => x.MaxAmmo).Int32();
    }
}
public sealed class AbilityRadiusComponentDescriptor : ExportGroupDescriptor<AbilityRadiusComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.AbilityRadiusComponent";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;
    public float RuntimeRadius { get; set; }
    protected override void Configure() => AddProperty(x => x.RuntimeRadius).Float();
}
