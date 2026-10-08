using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

/// <summary>Named pickup fields observed in the 0853f12c and d62adeea replay exports.</summary>
public sealed class EquippableGroundPickupDescriptor : ExportGroupDescriptor<EquippableGroundPickupDescriptor>
{
    public const string ExportPath = "/Game/Weapons/WeaponPickups/EquippableGroundPickup.EquippableGroundPickup_C";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public uint MyEquippable { get; set; }
    public uint LastOwner { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.MyEquippable).ObjectNetGuid();
        AddProperty(x => x.LastOwner).ObjectNetGuid();
    }
}

public sealed class EquippablePickupProjectileDescriptor : ExportGroupDescriptor<EquippablePickupProjectileDescriptor>
{
    public const string ExportPath = "/Game/Weapons/WeaponPickups/EquippablePickupProjectile.EquippablePickupProjectile_C";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public uint MyEquippable { get; set; }
    public FRepMovement? ReplicatedMovement { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.MyEquippable).ObjectNetGuid();
        AddProperty(x => x.ReplicatedMovement).ReplicatedMovement(ERotatorQuantization.ByteComponents);
    }
}

public sealed class BombPickedUpRpcParameters : ExportGroupDescriptor<BombPickedUpRpcParameters>
{
    public const string ExportPath = "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C:BombPickedUpRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public uint NewBombHolder { get; set; }
    protected override void Configure() => AddProperty(x => x.NewBombHolder).ObjectNetGuid();
}

public sealed class BombDroppedRpcParameters : ExportGroupDescriptor<BombDroppedRpcParameters>
{
    public const string ExportPath = "/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C:BombDroppedRPC";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public uint OldBombHolder { get; set; }
    protected override void Configure() => AddProperty(x => x.OldBombHolder).ObjectNetGuid();
}
