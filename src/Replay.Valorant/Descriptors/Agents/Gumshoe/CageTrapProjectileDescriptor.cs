using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

internal sealed class CageTrapProjectileDescriptor : ExportGroupDescriptor<CageTrapProjectileDescriptor>
{
    public override string Path => "/Game/Characters/Gumshoe/S0/Ability_4/Projectile_Gumshoe_4_CageTrap.Projectile_Gumshoe_4_CageTrap_C";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint Owner { get; set; }
    public uint RemoteRole { get; set; }
    public uint Role { get; set; }
    public uint Instigator { get; set; }
    public uint ReplicatedMovement { get; set; }
    public bool HasStopped { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.RemoteRole).Ignore();
        AddProperty(x => x.Role).Ignore();
        AddProperty(x => x.ReplicatedMovement).Ignore();
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
        AddProperty(x => x.HasStopped).Bool();
    }
}