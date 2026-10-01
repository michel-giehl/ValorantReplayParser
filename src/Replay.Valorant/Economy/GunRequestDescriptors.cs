using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Economy;

public enum AresGunRequestState : byte
{
    Empty = 0,
    Open = 1,
}

public interface IGunRequestParameters
{
    uint? RequestedGun { get; }
    AresGunRequestState? RequestState { get; }
}

public abstract class GunRequestParameters<TDescriptor>
    : ExportGroupDescriptor<TDescriptor>, IGunRequestParameters
    where TDescriptor : GunRequestParameters<TDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Economy | ExportCategory.Inventory;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    // FAresGunRequest is flattened into function parameter handles in the replay.
    public uint? RequestedGun { get; set; }
    public AresGunRequestState? RequestState { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.RequestedGun).ObjectNetGuid();
        AddPropertyHandle(1, x => x.RequestState).EnumRemainingBits();
    }
}

public sealed class NetMulticastCancelGunRequestParameters
    : GunRequestParameters<NetMulticastCancelGunRequestParameters>
{
    public const string ExportPath = "/Script/ShooterGame.GunRequestComponent:NetMulticastCancelGunRequest";
    public override string Path => ExportPath;
}

public sealed class NetMulticastFulfillGunRequestParameters
    : GunRequestParameters<NetMulticastFulfillGunRequestParameters>
{
    public const string ExportPath = "/Script/ShooterGame.GunRequestComponent:NetMulticastFulfillGunRequest";
    public override string Path => ExportPath;

    public uint? FulfillingPlayer { get; set; }

    protected override void Configure()
    {
        base.Configure();
        AddPropertyHandle(2, x => x.FulfillingPlayer).ObjectNetGuid();
    }
}

public sealed class NetMulticastMakeGunRequestParameters
    : GunRequestParameters<NetMulticastMakeGunRequestParameters>
{
    public const string ExportPath = "/Script/ShooterGame.GunRequestComponent:NetMulticastMakeGunRequest";
    public override string Path => ExportPath;
}

public sealed class GunRequestComponentClassNetCacheDescriptor
    : ClassNetCacheDescriptor<GunRequestComponentClassNetCacheDescriptor>
{
    public const string ExportPath = "/Script/ShooterGame.GunRequestComponent_ClassNetCache";
    public override string Path => ExportPath;

    protected override void Configure()
    {
        AddFunctionHandle<NetMulticastCancelGunRequestParameters>(
            0, "NetMulticastCancelGunRequest", NetMulticastCancelGunRequestParameters.ExportPath,
            ExportCategory.Economy | ExportCategory.Inventory);
        AddFunctionHandle<NetMulticastFulfillGunRequestParameters>(
            1, "NetMulticastFulfillGunRequest", NetMulticastFulfillGunRequestParameters.ExportPath,
            ExportCategory.Economy | ExportCategory.Inventory);
        AddFunctionHandle<NetMulticastMakeGunRequestParameters>(
            2, "NetMulticastMakeGunRequest", NetMulticastMakeGunRequestParameters.ExportPath,
            ExportCategory.Economy | ExportCategory.Inventory);
    }
}
