using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents;

/// <summary>Reusable sparse actor fields. Agent registrations select only fields present in their wire exports.</summary>
public sealed class AbilityActorDescriptor(string path, IReadOnlySet<string> exportedFields)
    : ExportGroupDescriptor<AbilityActorDescriptor>
{
    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.Ability | ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public override object CreatePayloadInstance() => new AbilityActorDescriptor(path, exportedFields);
    public uint? Owner { get; set; }
    public uint? Instigator { get; set; }
    public uint? CreatedByCharacter { get; set; }
    public uint? AttachParent { get; set; }
    public uint? DeployedActor { get; set; }
    public uint? FlashTrap { get; set; }
    public uint? Target { get; set; }
    public bool? IsBurrowed { get; set; }
    public bool? IsAlive { get; set; }
    public bool? InPersistentData { get; set; }
    public bool? Hidden { get; set; }
    public uint? MotherNode { get; set; }
    public uint? ActiveTether { get; set; }
    public uint? Cocoon { get; set; }
    public uint? GameObjectSpline { get; set; }
    public uint? SplineObject { get; set; }
    public FRepMovement? ReplicatedMovement { get; set; }
    protected override void Configure()
    {
        if (exportedFields.Contains("Owner")) AddProperty("Owner", x => x.Owner).ObjectNetGuid();
        if (exportedFields.Contains("Instigator")) AddProperty("Instigator", x => x.Instigator).ObjectNetGuid();
        if (exportedFields.Contains("CreatedByCharacter")) AddProperty("CreatedByCharacter", x => x.CreatedByCharacter).ObjectNetGuid();
        if (exportedFields.Contains("AttachParent")) AddProperty("AttachParent", x => x.AttachParent).ObjectNetGuid();
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
        if (exportedFields.Contains("ReplicatedMovement"))
            AddProperty(x => x.ReplicatedMovement, ExportCategory.Movement).ReplicatedMovement(
                path.Contains("/Pawn_", StringComparison.Ordinal) ? ERotatorQuantization.ShortComponents : ERotatorQuantization.ByteComponents);
    }
}
