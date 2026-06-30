using JetBrains.Annotations;
using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents;

public abstract class GenericAgentDescriptor : ExportGroupDescriptor<GenericAgentDescriptor>
{
    public override ExportCategory Categories => ExportCategory.Agent;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public bool ReplicateMovement { get; [UsedImplicitly] set; }
    public uint Role { get; [UsedImplicitly] set; }
    public uint RemoteRole { get; [UsedImplicitly] set; }
    public uint Owner { get; [UsedImplicitly] set; }
    public uint Instigator { get; [UsedImplicitly] set; }
    public uint PlayerState { get; [UsedImplicitly] set; }
    public uint Controller { get; [UsedImplicitly] set; }
    public float ReplayLastTransformUpdateTimeStamp { get; [UsedImplicitly] set; }
    public FVector ReplicatedGravityDirection { get; [UsedImplicitly] set; }
    public uint ReplicatedMovementMode { get; [UsedImplicitly] set; }
    public bool IsPlayerCharacter { get; [UsedImplicitly] set; }
    public bool CrouchHeld { get; [UsedImplicitly] set; }

    protected override void Configure()
    {
        AddProperty(x => RemoteRole).Ignore();
        AddProperty(x => Role).Ignore();

        AddProperty("bReplicateMovement", x => ReplicateMovement).Bool();
        AddProperty(x => Owner).ObjectNetGuid();
        AddProperty(x => Instigator).ObjectNetGuid();
        AddProperty(x => PlayerState).Byte();
        AddProperty(x => Controller).ObjectNetGuid();
        AddProperty(x => ReplayLastTransformUpdateTimeStamp).Ignore();
        AddProperty(x => ReplicatedGravityDirection).FVector();
        AddProperty(x => ReplicatedMovementMode).Byte();
        AddProperty("bIsPlayerCharacter", x => IsPlayerCharacter).Bool();
        AddProperty("bCrouchHeld", x => CrouchHeld).Bool();
    }
}