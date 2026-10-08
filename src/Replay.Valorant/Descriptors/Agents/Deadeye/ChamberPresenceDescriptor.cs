using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Deadeye;

/// <summary>Observed Owner (11) and Instigator (13) on placed Trademark and Rendezvous actors.</summary>
public sealed class ChamberPresenceDescriptor(string path) : ExportGroupDescriptor<ChamberPresenceDescriptor>
{
    public const string Trademark = "/Game/Characters/Deadeye/S0/Ability_4/GameObject_Deadeye_E_Trap.GameObject_Deadeye_E_Trap_C";
    public const string Rendezvous = "/Game/Characters/Deadeye/S0/Ability_E/GameObject_Deadeye_E_Teleporter_Tether.GameObject_Deadeye_E_Teleporter_Tether_C";
    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public override object CreatePayloadInstance() => new ChamberPresenceDescriptor(path);
    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
    }
}
