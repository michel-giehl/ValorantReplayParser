using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Combat;

/// <summary>A sparse section-life result inside the damage RPC's RepLayout dynamic array.</summary>
public sealed class DamageLifeChangeEvent : ExportGroupDescriptor<DamageLifeChangeEvent>
{
    public override string Path => "/Script/ShooterGame.LifeChangeEvent";
    public override ExportCategory Categories => ExportCategory.Gunplay;
    public uint ChangedComponent { get; set; }
    public float LifeResult { get; set; }
    public float DeltaLife { get; set; }
    public bool AliveAfterChange { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(10, x => x.ChangedComponent).ObjectNetGuid();
        AddPropertyHandle(11, x => x.LifeResult).Float();
        AddPropertyHandle(12, x => x.DeltaLife).Float();
        AddPropertyHandle(13, "bAliveAfterChange", x => x.AliveAfterChange).Bool();
    }
}

/// <summary>Retains the existing raw damage field contract while exposing bounded typed section results.</summary>
public static class DamageLifeChangeEvents
{
    private static readonly IFieldDecoder Decoder = RepLayoutArrayDecoders.DynamicArray<DamageLifeChangeEvent>();

    public static bool TryDecode(ValorantRawPayload? raw, out DamageLifeChangeEvent?[] entries)
    {
        entries = [];
        if (raw is null || raw.BitCount < 0 || raw.BitCount > 65536 ||
            raw.Data.Length != (raw.BitCount + 7) / 8) return false;
        if (raw.BitCount % 8 != 0 && raw.Data.Span[^1] >> (raw.BitCount % 8) != 0) return false;
        try
        {
            using var archive = new BitArchiveReader(raw.Data, raw.BitCount);
            var context = new FieldDecodeContext { FieldName = "LifeChangeEvents", Categories = ExportCategory.Gunplay };
            var value = Decoder.Decode(ref context, archive);
            if (!archive.AtEnd || value.ObjectValue is not DamageLifeChangeEvent?[] decoded) return false;
            entries = decoded;
            return true;
        }
        catch (ArchiveReadException) { return false; }
        catch (OverflowException) { return false; }
    }
}
