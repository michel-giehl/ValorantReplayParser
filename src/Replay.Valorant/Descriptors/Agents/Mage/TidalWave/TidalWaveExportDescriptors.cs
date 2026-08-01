using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Mage.TidalWave;

public sealed class TidalWaveChunkDescriptor : ExportGroupDescriptor<TidalWaveChunkDescriptor>
{
    public override string Path => TidalWavePaths.Chunk;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(11, x => x.Owner).ObjectNetGuid();
        AddPropertyHandle(13, x => x.Instigator).ObjectNetGuid();
    }
}

public sealed class SplineMovementComponentDescriptor
    : ExportGroupDescriptor<SplineMovementComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.SplineMovementComponent";
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Movement;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public bool? IsActive { get; set; }
    public ValorantRawPayload? Trajectory { get; set; }
    public ValorantRawPayload? SecondaryTrajectory { get; set; }
    public float? SpeedAlongSpline { get; set; }
    public float? SpeedUnit { get; set; }
    public float? ServerPosition { get; set; }
    public float? ServerMovementTime { get; set; }
    public float? ServerTeleportTime { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(1, "bIsActive", x => x.IsActive).Bool();
        AddPropertyHandle(2, "Trajectory", x => x.Trajectory)
            .Decode(ValorantPayloadDecoders.RawPayload("Trajectory"));
        AddPropertyHandle(3, "Trajectory", x => x.SecondaryTrajectory)
            .Decode(ValorantPayloadDecoders.RawPayload("Trajectory"));
        AddPropertyHandle(5, x => x.SpeedAlongSpline).Float();
        AddPropertyHandle(6, x => x.SpeedUnit).Float();
        AddPropertyHandle(7, x => x.ServerPosition).Float();
        AddPropertyHandle(8, x => x.ServerMovementTime).Float();
        AddPropertyHandle(9, x => x.ServerTeleportTime).Float();
    }
}

public sealed class ActorListTransitionContextDescriptor
    : ExportGroupDescriptor<ActorListTransitionContextDescriptor>
{
    public override string Path => "/Script/ShooterGame.ActorListTransitionContext";
    public override ExportCategory Categories => ExportCategory.Ability;

    public ValorantRawPayload? Actors { get; set; }
    public ValorantRawPayload? SecondaryActors { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(1, "Actors", x => x.Actors)
            .Decode(ValorantPayloadDecoders.RawPayload("Actors"));
        AddPropertyHandle(2, "Actors", x => x.SecondaryActors)
            .Decode(ValorantPayloadDecoders.RawPayload("Actors"));
    }
}

public sealed class TransformTransitionContextDescriptor
    : ExportGroupDescriptor<TransformTransitionContextDescriptor>
{
    public override string Path => "/Script/ShooterGame.TransformTransitionContext";
    public override ExportCategory Categories => ExportCategory.Ability;

    public ValorantRawPayload? Rotation { get; set; }
    public FVector? Translation { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(1, "249", x => x.Rotation)
            .Decode(ValorantPayloadDecoders.RawPayload("Rotation"));
        AddPropertyHandle(2, x => x.Translation).FVector();
    }
}

public sealed class ReadyingStateComponentDescriptor
    : ExportGroupDescriptor<ReadyingStateComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.ReadyingStateComponent";
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public float? AuthEquipSpeed { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(2, x => x.AuthEquipSpeed).Float();
    }
}

public sealed class ActivatableActorComponentDescriptor
    : ExportGroupDescriptor<ActivatableActorComponentDescriptor>
{
    public override string Path =>
        "/Game/Characters/States/ActivatableActorComponent.ActivatableActorComponent_C";

    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public bool? IsActivated { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(2, x => x.IsActivated).Bool();
    }
}
