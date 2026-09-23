using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Flashes.Descriptors;

// Breach replicates ExitResults as an FHitResult, independently of projectile movement.
internal sealed class BreachFlashExitLocationDecoder : IFieldDecoder
{
    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        var flags = archive.ReadByte();
        _ = archive.ReadSingle(); // Trace time.
        var location = PrimitiveDecoders.VectorNetQuantize.Decode(ref context, archive);
        _ = PrimitiveDecoders.VectorNetQuantizeNormal.Decode(ref context, archive);
        if ((flags & 4) == 0) // ImpactPoint differs from Location.
            _ = PrimitiveDecoders.VectorNetQuantize.Decode(ref context, archive);
        if ((flags & 8) == 0) // ImpactNormal differs from Normal.
            _ = PrimitiveDecoders.VectorNetQuantizeNormal.Decode(ref context, archive);
        _ = PrimitiveDecoders.VectorNetQuantize.Decode(ref context, archive); // TraceStart.
        _ = PrimitiveDecoders.VectorNetQuantize.Decode(ref context, archive); // TraceEnd.
        if ((flags & 64) == 0)
            _ = archive.ReadSingle(); // PenetrationDepth.
        if ((flags & 16) == 0)
            _ = archive.ReadInt32(); // Item.
        _ = archive.ReadIntPacked(); // PhysMaterial.
        _ = archive.ReadIntPacked(); // HitObjectHandle.Actor.
        _ = archive.ReadIntPacked(); // HitObjectHandle.Manager.
        _ = archive.ReadInt32(); // HitObjectHandle.InstanceIndex.
        _ = archive.ReadIntPacked(); // Component.
        _ = archive.ReadFName(); // BoneName.
        if ((flags & 32) == 0)
            _ = archive.ReadInt32(); // FaceIndex.
        if ((flags & 128) == 0)
            _ = archive.ReadByte(); // ElementIndex.
        return location;
    }
}
