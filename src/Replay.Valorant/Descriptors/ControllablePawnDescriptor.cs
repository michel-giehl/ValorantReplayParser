using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Control;

/// <summary>Pawn fields verified in recorded export groups; remote motion uses RemoteCharacterUpdate.</summary>
public sealed class ControllablePawnDescriptor(string path) : ExportGroupDescriptor<ControllablePawnDescriptor>
{
    public const string Skye = "/Game/Characters/Guide/S0/Ability_Q/Pawn_Guide_Q_PossessableScout.Pawn_Guide_Q_PossessableScout_C";
    public const string Sova = "/Game/Characters/Hunter/S0/Ability_E/Drone/Pawn_Hunter_E_Drone.Pawn_Hunter_E_Drone_C";
    public const string Tejo = "/Game/Characters/Cashew/S0/Ability_4/Pawn_Cashew_4_Spider_LockOn.Pawn_Cashew_4_Spider_LockOn_C";
    public const string Gekko = "/Game/Characters/AggroBot/S0/Ability_X/Pawn_Aggrobot_RollyPolly.Pawn_Aggrobot_RollyPolly_C";
    public override string Path => path;
    public override object CreatePayloadInstance() => new ControllablePawnDescriptor(path);
    public override ExportCategory Categories => ExportCategory.Agent;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    public uint PlayerState { get; set; }
    public uint Controller { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.PlayerState).ObjectNetGuid();
        AddProperty(x => x.Controller).ObjectNetGuid();
    }
}

public sealed class PossessableActorComponentDescriptor : ExportGroupDescriptor<PossessableActorComponentDescriptor>
{
    public override string Path => "/Game/Characters/States/PossessableActorComponent.PossessableActorComponent_C";
    public override ExportCategory Categories => ExportCategory.Agent;
    public override ExportGroupKind Kind => ExportGroupKind.Component;
    public bool IsPossessed { get; set; }
    protected override void Configure() => AddProperty(x => x.IsPossessed).Bool();
}
