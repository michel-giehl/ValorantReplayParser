using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Replay;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors.Agents.Terra;

namespace Replay.Valorant.Descriptors.Agents;

/// <summary>Reusable sparse actor fields. Agent registrations select only fields present in their wire exports.</summary>
public sealed class AbilityActorDescriptor(
    string path,
    IReadOnlySet<string> exportedFields,
    ERotatorQuantization rotatorQuantization)
    : ExportGroupDescriptor<AbilityActorDescriptor>
{
    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public override object CreatePayloadInstance() => new AbilityActorDescriptor(path, exportedFields, rotatorQuantization);
    public NetObjectReference? Owner { get; set; }
    public NetObjectReference? Instigator { get; set; }
    public NetObjectReference? CreatedByCharacter { get; set; }
    public NetObjectReference? AttachParent { get; set; }
    public NetObjectReference? AttachComponent { get; set; }
    public FVector? RelativeScale3D { get; set; }
    public int? CosmeticRandomSeed { get; set; }
    public NetObjectReference? DeployedActor { get; set; }
    public NetObjectReference? FlashTrap { get; set; }
    public NetObjectReference? Target { get; set; }
    public bool? IsBurrowed { get; set; }
    public bool? IsAlive { get; set; }
    public bool? InPersistentData { get; set; }
    public bool? Hidden { get; set; }
    public NetObjectReference? MotherNode { get; set; }
    public NetObjectReference? ActiveTether { get; set; }
    public NetObjectReference? Cocoon { get; set; }
    public NetObjectReference? GameObjectSpline { get; set; }
    public NetObjectReference? SplineObject { get; set; }
    public NetObjectReference? Projectile { get; set; }
    public NetObjectReference? SpawnedBlast { get; set; }
    public FVector? SelectedPoint { get; set; }
    public ReplicatedObjectReference?[]? Proxies { get; set; }
    public NetObjectReference? WarningActor { get; set; }
    public NetObjectReference? AssignedPlayspace { get; set; }
    public NetObjectReference? OwningPrimaryDataAsset { get; set; }
    public NetObjectReference? CurrentUsableTarget { get; set; }
    public NetObjectReference? SpawnedDeployable { get; set; }
    public NetObjectReference? SeekingMissileActor { get; set; }
    public bool? IsOwnerAlive { get; set; }
    public bool? IsPlacedInWorld { get; set; }
    public bool? IsRecording { get; set; }
    public bool? IsRewinding { get; set; }
    public FVector? Position { get; set; }
    public FVector? RewindEndPointLocation { get; set; }
    public WaylayRewindPathPointDescriptor[]? RewindPath { get; set; }
    public FVector? EndingStrikeLocation { get; set; }
    public FRepMovement? ReplicatedMovement { get; set; }
    protected override void Configure()
    {
        if (exportedFields.Contains("Owner")) AddProperty("Owner", x => x.Owner).ObjectNetGuid();
        if (exportedFields.Contains("Instigator")) AddProperty("Instigator", x => x.Instigator).ObjectNetGuid();
        if (exportedFields.Contains("CreatedByCharacter")) AddProperty("CreatedByCharacter", x => x.CreatedByCharacter).ObjectNetGuid();
        if (exportedFields.Contains("AttachParent")) AddProperty("AttachParent", x => x.AttachParent).ObjectNetGuid();
        if (exportedFields.Contains("AttachComponent")) AddProperty("AttachComponent", x => x.AttachComponent).ObjectNetGuid();
        if (exportedFields.Contains("RelativeScale3D")) AddProperty("RelativeScale3D", x => x.RelativeScale3D).FVectorNetQuantize100();
        if (exportedFields.Contains("CosmeticRandomSeed")) AddProperty("CosmeticRandomSeed", x => x.CosmeticRandomSeed).Int32();
        if (exportedFields.Contains("DeployedActor")) AddProperty("DeployedActor", x => x.DeployedActor).ObjectNetGuid();
        if (exportedFields.Contains("FlashTrap")) AddProperty("FlashTrap", x => x.FlashTrap).ObjectNetGuid();
        if (exportedFields.Contains("Target")) AddProperty("Target", x => x.Target).ObjectNetGuid();
        if (exportedFields.Contains("IsBurrowed")) AddProperty("IsBurrowed", x => x.IsBurrowed).Bool();
        if (exportedFields.Contains("IsAlive")) AddProperty("IsAlive", x => x.IsAlive).Bool();
        if (exportedFields.Contains("bInPersistentData")) AddProperty("bInPersistentData", x => x.InPersistentData).Bool();
        if (exportedFields.Contains("bHidden")) AddProperty("bHidden", x => x.Hidden).Bool();
        if (exportedFields.Contains("Mother Node")) AddProperty("Mother Node", x => x.MotherNode).ObjectNetGuid();
        if (exportedFields.Contains("Active Tether")) AddProperty("Active Tether", x => x.ActiveTether).ObjectNetGuid();
        if (exportedFields.Contains("Cocoon ")) AddProperty("Cocoon ", x => x.Cocoon).ObjectNetGuid();
        if (exportedFields.Contains("Game Object Spline")) AddProperty("Game Object Spline", x => x.GameObjectSpline).ObjectNetGuid();
        if (exportedFields.Contains("Spline Object")) AddProperty("Spline Object", x => x.SplineObject).ObjectNetGuid();
        if (exportedFields.Contains("Projectile")) AddProperty("Projectile", x => x.Projectile).ObjectNetGuid();
        if (exportedFields.Contains("Spawned Blast")) AddProperty("Spawned Blast", x => x.SpawnedBlast).ObjectNetGuid();
        if (exportedFields.Contains("Selected Point")) AddProperty("Selected Point", x => x.SelectedPoint).FVector();
        if (exportedFields.Contains("Proxies"))
            AddProperty("Proxies", x => x.Proxies).RepLayoutDynamicArray(
                new VersionedDefinition<ReplicatedObjectReference>(new(15, "Proxies")));
        if (exportedFields.Contains("WarningActor")) AddProperty("WarningActor", x => x.WarningActor).ObjectNetGuid();
        if (exportedFields.Contains("AssignedPlayspace")) AddProperty("AssignedPlayspace", x => x.AssignedPlayspace).ObjectNetGuid();
        if (exportedFields.Contains("OwningPrimaryDataAsset")) AddProperty("OwningPrimaryDataAsset", x => x.OwningPrimaryDataAsset).ObjectNetGuid();
        if (exportedFields.Contains("Current Usable Target")) AddProperty("Current Usable Target", x => x.CurrentUsableTarget).ObjectNetGuid();
        if (exportedFields.Contains("Spawned Deployable")) AddProperty("Spawned Deployable", x => x.SpawnedDeployable).ObjectNetGuid();
        if (exportedFields.Contains("Seeking Missile Actor")) AddProperty("Seeking Missile Actor", x => x.SeekingMissileActor).ObjectNetGuid();
        if (exportedFields.Contains("IsOwnerAlive")) AddProperty("IsOwnerAlive", x => x.IsOwnerAlive).Bool();
        if (exportedFields.Contains("Is Placed In World")) AddProperty("Is Placed In World", x => x.IsPlacedInWorld).Bool();
        if (exportedFields.Contains("bIsRecording")) AddProperty("bIsRecording", x => x.IsRecording).Bool();
        if (exportedFields.Contains("bIsRewinding")) AddProperty("bIsRewinding", x => x.IsRewinding).Bool();
        if (exportedFields.Contains("Position")) AddProperty("Position", x => x.Position).FVector();
        if (exportedFields.Contains("RewindEndPointLocation")) AddProperty("RewindEndPointLocation", x => x.RewindEndPointLocation).FVector();
        if (exportedFields.Contains("RewindPath"))
            AddProperty("RewindPath", x => x.RewindPath).RepLayoutDynamicArray<WaylayRewindPathPointDescriptor>();
        if (exportedFields.Contains("EndingStrikeLocation")) AddProperty("EndingStrikeLocation", x => x.EndingStrikeLocation).FVector();
        if (exportedFields.Contains("ReplicatedMovement"))
            AddProperty(x => x.ReplicatedMovement, ExportCategory.Movement).ReplicatedMovement(rotatorQuantization);
    }
}
