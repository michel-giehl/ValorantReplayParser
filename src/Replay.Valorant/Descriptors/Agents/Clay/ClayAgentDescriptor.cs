using Replay.Unreal.Parsing;
using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Clay;

/// <summary>Raze character, including replicated focus-projectile references.</summary>
public sealed class ClayAgentDescriptor : GenericAgentDescriptor
{
    public override string Path => "/Game/Characters/Clay/Clay_PC.Clay_PC_C";
    public ClayFocusProjectileReference[] FocusProjectiles { get; set; } = [];

    protected override void Configure()
    {
        AddPropertyHandle(0, "bReplicateMovement", x => x.ReplicateMovement).Bool();
        AddPropertyHandle(3, "216", x => x.RemoteRole).EnumRemainingBits();
        AddPropertyHandle(11, x => x.Owner).ObjectNetGuid();
        AddPropertyHandle(12, "215", x => x.Role).EnumRemainingBits();
        AddPropertyHandle(13, x => x.Instigator).ObjectNetGuid();
        AddPropertyHandle(14, x => x.PlayerState).ObjectNetGuid();
        AddPropertyHandle(15, x => x.Controller).ObjectNetGuid();
        AddPropertyHandle(26, x => x.ReplayLastTransformUpdateTimeStamp).Float();
        AddPropertyHandle(27, x => x.ReplicatedGravityDirection).FVectorNetQuantizeNormal();
        AddPropertyHandle(30, x => x.ReplicatedMovementMode).Byte();
        AddPropertyHandle(45, "FocusProjectiles", x => ((ClayAgentDescriptor)x).FocusProjectiles)
            .RepLayoutDynamicArray<ClayFocusProjectileReference>();
        AddPropertyHandle(55, "bIsPlayerCharacter", x => x.IsPlayerCharacter).Bool();
        AddPropertyHandle(58, "bCrouchHeld", x => x.CrouchHeld).Bool();
    }
}

public sealed class ClayFocusProjectileReference : ExportGroupDescriptor<ClayFocusProjectileReference>
{
    public uint? ActorNetGuid { get; set; }
    protected override void Configure() => AddPropertyHandle(46, "FocusProjectiles", x => x.ActorNetGuid).ObjectNetGuid();
}
