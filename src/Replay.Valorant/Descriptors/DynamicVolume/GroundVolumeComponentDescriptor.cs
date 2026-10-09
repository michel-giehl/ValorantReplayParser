using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.DynamicVolume;

/// <summary>Wire data only. Fragment lifetimes and geometry belong to the consumer.</summary>
public sealed class GroundVolumeComponentDescriptor : ExportGroupDescriptor<GroundVolumeComponentDescriptor>,
    IRepLayoutCustomDeltaPayload
{
    public const string ExportPath = "/Script/DynamicVolume.GroundVolumeComponent";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.Ability;
    public override ExportGroupKind Kind => ExportGroupKind.Component;
    public bool? IsActive { get; set; }
    public uint? AttachParent { get; set; }
    public uint? VolumeMaxExtentShape { get; set; }
    public int? FinalCount { get; set; }
    public GroundVolumeDelta? FragmentDelta { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(1, "bIsActive", x => x.IsActive).Bool();
        AddPropertyHandle(2, x => x.AttachParent).ObjectNetGuid();
        AddPropertyHandle(19, x => x.VolumeMaxExtentShape).ObjectNetGuid();
        AddPropertyHandle(21, x => x.FinalCount).Int32();
    }

    public void ReadCustomDelta(ref FieldDecodeContext context, FBitArchive payload)
    {
        // One native custom-delta field, framed like a class-net-cache entry (13.06 fixture).
        if (payload.ReadSerializedInt(2) != 0) throw Invalid(payload, "Unknown native delta handle.");
        var bits = payload.ReadIntPacked();
        if (bits > payload.BitsRemaining) throw Invalid(payload, "Native delta exceeds its content block.");
        using var body = payload.ReadSubArchive(checked((int)bits));
        if (!body.ReadBit()) throw Invalid(body, "Unsupported FastArray serialization mode.");
        var key = body.ReadInt32();
        var baseKey = body.ReadInt32();
        var deletedCount = body.ReadInt32();
        var changedCount = body.ReadInt32();
        if (deletedCount is < 0 or > 16384 || changedCount is < 0 or > 16384)
            throw Invalid(body, "Invalid fragment delta count.");
        var deleted = new int[deletedCount];
        for (var i = 0; i < deleted.Length; i++) deleted[i] = body.ReadInt32();
        var updates = new GroundVolumeFragmentDescriptor[changedCount];
        var parser = new FieldPayloadParser();
        for (var i = 0; i < updates.Length; i++)
        {
            var id = body.ReadInt32();
            var result = parser.ParseRepLayoutProperties(body, FragmentBinding, ref context,
                readPropertyChecksum: false);
            updates[i] = (GroundVolumeFragmentDescriptor)result.Payload!;
            updates[i].ReplicationId = id;
        }
        body.EnsureFullyConsumed(nameof(GroundVolumeComponentDescriptor));
        payload.EnsureFullyConsumed(nameof(GroundVolumeComponentDescriptor));
        FragmentDelta = new(key, baseKey, deleted, updates);
        MarkDecoded(nameof(FragmentDelta));
    }

    private static readonly BoundExportGroup FragmentBinding = BindFragment();
    private static BoundExportGroup BindFragment()
    {
        var descriptor = new GroundVolumeFragmentDescriptor();
        var fields = new FieldBinding[44];
        foreach (var field in descriptor.Fields)
            fields[field.Handle!.Value] = new FieldBinding
            {
                Enabled = true, Categories = ExportCategory.Ability, Name = field.PropertyName,
                ExportName = field.ExportName, TargetProperty = field.TargetProperty,
                Decoder = (IFieldDecoder)field.Decoder!,
            };
        return new BoundExportGroup
        {
            SourceDescriptor = descriptor, Categories = ExportCategory.Ability, Enabled = true,
            Grammar = FieldStreamGrammar.RepLayoutProperties, FieldsByHandle = fields,
        };
    }
    private static ArchiveReadException Invalid(FBitArchive archive, string detail) =>
        new(ArchiveErrorCode.InvalidCount, nameof(GroundVolumeComponentDescriptor),
            archive.Position, archive.Length, 0, detail);
}

public sealed record GroundVolumeDelta(int ArrayKey, int BaseKey, IReadOnlyList<int> DeletedIds,
    IReadOnlyList<GroundVolumeFragmentDescriptor> Updates);

public sealed class GroundVolumeFragmentDescriptor : ExportGroupDescriptor<GroundVolumeFragmentDescriptor>
{
    public override string Path => GroundVolumeComponentDescriptor.ExportPath + ":Fragment";
    public override ExportCategory Categories => ExportCategory.Ability;
    public int ReplicationId { get; set; }
    public int? ReplicationKey { get; set; }
    public bool? IsActive { get; set; }
    public int? Status { get; set; }
    public GroundVolumeExteriorSegment?[]? ExteriorSegments { get; set; }
    public GroundVolumePoint?[]? ConvexHullPoints { get; set; }
    public GroundVolumeCeiling?[]? ConvexHullCeilings { get; set; }
    public GroundVolumeTravelDistance?[]? ConvexHullTravelDistances { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public float? TravelDistance { get; set; }
    public float? Ceiling { get; set; }
    public float? Floor { get; set; }
    protected override void Configure()
    {
        AddPropertyHandle(23, x => x.ReplicationKey).Int32();
        AddPropertyHandle(24, "bIsActive", x => x.IsActive).Bool();
        AddPropertyHandle(25, x => x.Status).EnumRemainingBits();
        AddPropertyHandle(26, x => x.ExteriorSegments).RepLayoutDynamicArray<GroundVolumeExteriorSegment>();
        AddPropertyHandle(30, x => x.ConvexHullPoints).RepLayoutDynamicArray<GroundVolumePoint>();
        AddPropertyHandle(33, x => x.ConvexHullCeilings).RepLayoutDynamicArray<GroundVolumeCeiling>();
        AddPropertyHandle(36, x => x.ConvexHullTravelDistances).RepLayoutDynamicArray<GroundVolumeTravelDistance>();
        AddPropertyHandle(39, x => x.X).Int32();
        AddPropertyHandle(40, x => x.Y).Int32();
        AddPropertyHandle(41, x => x.TravelDistance).Float();
        AddPropertyHandle(42, x => x.Ceiling).Float();
        AddPropertyHandle(43, x => x.Floor).Float();
    }
}

public sealed class GroundVolumeExteriorSegment : ExportGroupDescriptor<GroundVolumeExteriorSegment>
{
    public override string Path => GroundVolumeComponentDescriptor.ExportPath + ":Exterior";
    public byte? Begin { get; set; }
    public byte? End { get; set; }
    protected override void Configure()
    {
        AddPropertyHandle(27, x => x.Begin).Byte();
        AddPropertyHandle(28, x => x.End).Byte();
    }
}
public sealed class GroundVolumePoint : ExportGroupDescriptor<GroundVolumePoint>
{
    public override string Path => GroundVolumeComponentDescriptor.ExportPath + ":Point";
    public FVector? Point { get; set; }
    protected override void Configure() => AddPropertyHandle(31, x => x.Point).FVector();
}
public sealed class GroundVolumeCeiling : ExportGroupDescriptor<GroundVolumeCeiling>
{
    public override string Path => GroundVolumeComponentDescriptor.ExportPath + ":Ceiling";
    public float? Value { get; set; }
    protected override void Configure() => AddPropertyHandle(34, x => x.Value).Float();
}
public sealed class GroundVolumeTravelDistance : ExportGroupDescriptor<GroundVolumeTravelDistance>
{
    public override string Path => GroundVolumeComponentDescriptor.ExportPath + ":TravelDistance";
    public float? Value { get; set; }
    protected override void Configure() => AddPropertyHandle(37, x => x.Value).Float();
}
