using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Paprika;

/// <summary>Hush's character component for the ranged usable interaction.</summary>
public sealed class PaprikaRangedUsableComponentDescriptor
    : ExportGroupDescriptor<PaprikaRangedUsableComponentDescriptor>
{
    public const string InstanceName = "Paprika_RangedUsableComponent";
    public const string ExportPath = "/Game/Characters/Paprika/Paprika_RangedUsableComponent.Paprika_RangedUsableComponent_C";

    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public bool? IsActive { get; set; }
    public NetObjectReference? CooldownTimer { get; set; }

    protected override void Configure()
    {
        AddProperty("bIsActive", x => x.IsActive).Bool();
        AddProperty(x => x.CooldownTimer).ObjectNetGuid();
    }
}

/// <summary>Links an ultimate-affected actor to Hush's force-proxy manager.</summary>
public sealed class PaprikaForceProxyManagerUserDescriptor
    : ExportGroupDescriptor<PaprikaForceProxyManagerUserDescriptor>
{
    public const string InstanceName = "Comp_ForceProxyManagerUser";
    public const string ExportPath = "/Game/Characters/Paprika/S0/Ability_X/Comp_ForceProxyManagerUser.Comp_ForceProxyManagerUser_C";

    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public NetObjectReference? ProxyManager { get; set; }

    protected override void Configure() => AddProperty(x => x.ProxyManager).ObjectNetGuid();
}
