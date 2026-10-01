using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Inventory;

/// <summary>A sparse MultiContents delta; omitted indices retain their previous contents.</summary>
public sealed record ItemSlotContentsUpdate(int Count, IReadOnlyList<ItemSlotContentUpdate> Updates);

public sealed record ItemSlotContentUpdate(int Index, uint Contents);

internal sealed class ItemSlotContentsDecoder : IFieldDecoder
{
    private const uint MaxContents = 64 * 1024;
    private const uint ContentsHandle = 2;

    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        var count = archive.ReadIntPacked();
        if (count > MaxContents) throw InvalidCount(archive, count);
        var updates = new List<ItemSlotContentUpdate>();
        while (true)
        {
            var encodedIndex = archive.ReadIntPacked();
            if (encodedIndex == 0) break;
            var index = encodedIndex - 1;
            if (index >= count) throw InvalidCount(archive, index);
            uint? contents = null;
            while (true)
            {
                var encodedHandle = archive.ReadIntPacked();
                if (encodedHandle == 0) break;
                if (encodedHandle - 1 != ContentsHandle || contents.HasValue)
                {
                    throw InvalidCount(archive, encodedHandle - 1,
                        $"Expected a single MultiContents object reference at handle {ContentsHandle}.");
                }
                var bits = archive.ReadIntPacked();
                if (bits > 40 || bits > archive.BitsRemaining)
                {
                    throw new ArchiveReadException(ArchiveErrorCode.InvalidBitCount,
                        nameof(ItemSlotContentsDecoder), archive.Position, archive.Length, bits);
                }
                using var payload = archive.ReadSubArchive(checked((int)bits));
                contents = payload.ReadIntPacked();
                payload.EnsureFullyConsumed(nameof(ItemSlotContentsDecoder));
            }
            if (contents is { } value) updates.Add(new(checked((int)index), value));
        }
        archive.EnsureFullyConsumed(nameof(ItemSlotContentsDecoder));
        return DecodedFieldValue.FromObject(new ItemSlotContentsUpdate(checked((int)count), updates.AsReadOnly()));
    }

    private static ArchiveReadException InvalidCount(FBitArchive archive, uint requested, string? detail = null) =>
        new(ArchiveErrorCode.InvalidCount, nameof(ItemSlotContentsDecoder),
            archive.Position, archive.Length, requested, detail);
}
