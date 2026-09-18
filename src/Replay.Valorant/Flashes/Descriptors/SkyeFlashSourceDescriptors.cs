using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Flashes.Descriptors;

internal interface IFlashSourcePayload
{
    uint? Owner { get; }
    uint? Instigator { get; }
}

public sealed class SkyeFlashSourceDescriptor : ExportGroupDescriptor<SkyeFlashSourceDescriptor>, IFlashSourcePayload
{
    public override string Path => FlashPaths.SkyeFlashSource;
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(11, x => x.Owner).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator).ObjectNetGuid();
    }
}

public sealed class VyseFlashSourceDescriptor : ExportGroupDescriptor<VyseFlashSourceDescriptor>, IFlashSourcePayload
{
    public override string Path => FlashPaths.VyseFlashTrap;
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(11, x => x.Owner).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator).ObjectNetGuid();
    }
}

public sealed class VyseDeployTrapParameters : ExportGroupDescriptor<VyseDeployTrapParameters>
{
    public override string Path => FlashPaths.VyseFlashTrap + ":MulticastDeployTrap";
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public double? DeployDelay { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, "Deploy Delay", x => x.DeployDelay).Double();
    }
}

public sealed class VyseFlashSourceClassNetCacheDescriptor
    : ClassNetCacheDescriptor<VyseFlashSourceClassNetCacheDescriptor>
{
    public override string Path => FlashPaths.VyseFlashTrap + "_ClassNetCache";

    protected override void Configure()
    {
        AddFunctionHandle<VyseDeployTrapParameters>(
            0,
            "MulticastDeployTrap",
            FlashPaths.VyseFlashTrap + ":MulticastDeployTrap",
            ExportCategory.Ability | ExportCategory.Effects);
    }
}

public sealed class SkyeSetFlashDurationParameters
    : ExportGroupDescriptor<SkyeSetFlashDurationParameters>
{
    public override string Path => FlashPaths.SkyeFlashSource + ":Multicast Set Flash Duration";
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public double? MaxFlashDuration { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.MaxFlashDuration).Double();
    }
}

public sealed class SkyeFlashSourceClassNetCacheDescriptor
    : ClassNetCacheDescriptor<SkyeFlashSourceClassNetCacheDescriptor>
{
    public override string Path => FlashPaths.SkyeFlashSource + "_ClassNetCache";

    protected override void Configure()
    {
        AddFunctionHandle<SkyeSetFlashDurationParameters>(
            0,
            "Multicast Set Flash Duration",
            FlashPaths.SkyeFlashSource + ":Multicast Set Flash Duration",
            ExportCategory.Ability | ExportCategory.Effects);
    }
}
