using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

internal sealed class AresAbilitySystemComponentDescriptor : ExportGroupDescriptor<AresAbilitySystemComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.AresAbilitySystemComponent";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    public uint AresAttributeSet { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.AresAttributeSet);
    }
}

internal sealed class AresAttributeSetDescriptor : ExportGroupDescriptor<AresAttributeSetDescriptor>
{
    public override string Path => "/Script/ShooterGame.AresAttributeSet";
    public override ExportCategory Categories => ExportCategory.Combat | ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.AttributeSet;

    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public float Shield { get; set; }
    public float MaxShield { get; set; }
    public float Damage { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Health, ExportCategory.Combat).Float();
        AddProperty(x => x.MaxHealth, ExportCategory.Combat).Float();
        AddProperty(x => x.Shield, ExportCategory.Combat).Float();
        AddProperty(x => x.MaxShield, ExportCategory.Combat).Float();
        AddProperty(x => x.Damage, ExportCategory.Combat).Float();
    }
}

internal sealed class RemoteCharacterUpdateDescriptor : ExportGroupDescriptor<RemoteCharacterUpdateDescriptor>
{
    public override string Path => "/Script/ShooterGame.RemoteCharacterUpdate";
    public override ExportCategory Categories => ExportCategory.Movement;
    public override ExportGroupKind Kind => ExportGroupKind.FastArray;

    public uint ShooterCharacterNetGuidValue { get; set; }
    public uint ShooterCharacterNetGuid { get; set; }
    public uint ComponentDataStream { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.ShooterCharacterNetGuidValue).ObjectNetGuid();
        AddProperty(x => x.ShooterCharacterNetGuid).ObjectNetGuid();
        AddProperty(x => x.ComponentDataStream);
    }
}

internal sealed class BaseReplayPlayerState : ExportGroupDescriptor<BaseReplayPlayerState>
{
    public override string Path => "/Game/GameModes/Common/BaseReplayPlayerState.BaseReplayPlayerState_C";
    public override ExportCategory Categories => ExportCategory.Movement;
    public override ExportGroupKind Kind => ExportGroupKind.Unknown;

    public uint RemoteRole { get; set; }
    public uint Owner { get; set; }
    public bool OnlySpectator { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.RemoteRole).ObjectNetGuid();
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty("bOnlySpectator", x => x.OnlySpectator);
    }
}

internal sealed class BaseReplayControllerDescriptor : ExportGroupDescriptor<BaseReplayControllerDescriptor>
{
    public override string Path => "/Game/Characters/_Core/BaseReplayController.BaseReplayController_C";
    public override ExportCategory Categories => ExportCategory.Movement;
    public override ExportGroupKind Kind => ExportGroupKind.PlayerController;

    public uint RemoteCharacterUpdatesArray { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.RemoteCharacterUpdatesArray);
    }
}

internal sealed class TripWireAbilityDescriptor : ExportGroupDescriptor<TripWireAbilityDescriptor>
{
    public override string Path => "/Game/Characters/Gumshoe/S0/Ability_E/Ability_Gumshoe_E_TripWire.Ability_Gumshoe_E_TripWire_C";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    public uint CreatedByCharacter { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.CreatedByCharacter).ObjectNetGuid();
    }
}

internal sealed class SmokeAbilityDescriptor : ExportGroupDescriptor<SmokeAbilityDescriptor>
{
    public override string Path => "/Game/Characters/Phoenix/S0/Ability_E/Phoenix_E_Smoke.Phoenix_E_Smoke_C";
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint Owner { get; set; }
    public uint Instigator { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Owner, ExportCategory.Ability).ObjectNetGuid();
        AddProperty(x => x.Instigator, ExportCategory.Ability).ObjectNetGuid();
    }
}
