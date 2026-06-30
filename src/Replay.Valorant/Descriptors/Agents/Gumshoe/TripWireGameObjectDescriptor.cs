using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

internal sealed class TripWireGameObjectDescriptor : ExportGroupDescriptor<TripWireGameObjectDescriptor>
{
    public override string Path => "/Game/Characters/Gumshoe/S0/Ability_E/GameObject_Gumshoe_E_TripWire.GameObject_Gumshoe_E_TripWire_C";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint Role { get; set; }
    public uint RemoteRole { get; set; }
    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    public bool Deployed { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.RemoteRole).Ignore();
        AddProperty(x => x.Role).Ignore();
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.Deployed).Bool();
    }
}

internal sealed class SecondTripWireGameObjectDescriptor : ExportGroupDescriptor<SecondTripWireGameObjectDescriptor>
{
    public override string Path => "/Game/Characters/Gumshoe/S0/Ability_E/GameObject_Gumshoe_E_TripWire_SecondWire.GameObject_Gumshoe_E_TripWire_SecondWire_C";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint Role { get; set; }
    public uint RemoteRole { get; set; }
    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    public bool Deployed { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.RemoteRole).Ignore();
        AddProperty(x => x.Role).Ignore();
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.Deployed).Bool();
    }
}