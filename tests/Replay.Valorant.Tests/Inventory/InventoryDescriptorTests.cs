using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.GameState;
using Replay.Valorant.Inventory;

namespace Replay.Valorant.Tests.Inventory;

public sealed class InventoryDescriptorTests
{
    [Test]
    public void StaticSlotFieldsDecodeAllSixteenRepeatedExportNames()
    {
        var descriptor = new AresInventoryDescriptor();
        var exports = new NetFieldExport?[32];
        for (uint handle = 2; handle <= 17; handle++)
            exports[handle] = new() { Handle = handle, Name = "ItemSlots", CompatibleChecksum = 0 };
        var bound = Bind(descriptor, exports);
        var payload = new List<byte>();
        for (uint handle = 2; handle <= 17; handle++)
        {
            AppendPacked(payload, handle + 1);
            AppendPacked(payload, 8);
            AppendPacked(payload, handle == 2 ? 0 : handle + 40);
        }
        AppendPacked(payload, 0);
        using var archive = new BitArchiveReader(payload.ToArray(), payload.Count * 8);
        var context = new FieldDecodeContext();
        var decoded = (AresInventoryDescriptor)new FieldPayloadParser()
            .ParseRepLayoutProperties(archive, bound, ref context, readPropertyChecksum: false).Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(decoded.GetDecodedItemSlots().Count(), Is.EqualTo(16));
            Assert.That(decoded.GetItemSlot(EAresItemSlot.Primary), Is.Zero);
            Assert.That(decoded.GetItemSlot(EAresItemSlot.SecondaryStorage), Is.EqualTo(57));
            Assert.That(decoded.ItemSlots, Has.Count.EqualTo(16));
            Assert.That(decoded.HasDecoded(nameof(AresInventoryDescriptor.PrimarySlot)), Is.True);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void SparseSlotReferencesDistinguishAbsentFromExplicitRemoval()
    {
        var descriptor = new AresInventoryDescriptor { PrimarySlot = 0, SecondarySlot = 42 };
        descriptor.MarkDecoded(nameof(AresInventoryDescriptor.PrimarySlot));
        descriptor.MarkDecoded(nameof(AresInventoryDescriptor.SecondarySlot));
        Assert.That(descriptor.GetDecodedItemSlots(), Is.EqualTo(new[]
        {
            (EAresItemSlot.Primary, 0u), (EAresItemSlot.Secondary, 42u),
        }));
        Assert.That(descriptor.GetItemSlot(EAresItemSlot.Armor), Is.Null);
    }

    [Test]
    public void SlotChangesIgnoreValuesWithoutDecodedPropertyEvidence()
    {
        var descriptor = new AresInventoryDescriptor { PrimarySlot = 42, SecondarySlot = 43 };
        Assert.That(descriptor.GetDecodedItemSlots(), Is.Empty);
        descriptor.MarkDecoded(nameof(AresInventoryDescriptor.PrimarySlot));
        Assert.That(descriptor.GetDecodedItemSlots(), Is.EqualTo(new[] { (EAresItemSlot.Primary, 42u) }));
    }

    [TestCase(EAresItemSlot.Count)]
    [TestCase(EAresItemSlot.Any)]
    [TestCase(EAresItemSlot.Invalid)]
    public void SlotAccessorRejectsNonSlotEnumValues(EAresItemSlot slot) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new AresInventoryDescriptor().GetItemSlot(slot));

    [TestCase("02020620CD080000", 614u)]
    [TestCase("02020620910A0000", 712u)]
    [TestCase("02020620550C0000", 810u)]
    public void MultiContentsDecodesCapturedReplayPayloads(string hex, uint contents)
    {
        var update = DecodeContents(Convert.FromHexString(hex));
        Assert.Multiple(() =>
        {
            Assert.That(update.Count, Is.EqualTo(1));
            Assert.That(update.Updates, Is.EqualTo(new[] { new ItemSlotContentUpdate(0, contents) }));
        });
    }

    [Test]
    public void MultiContentsPreservesSparseIndicesAndExplicitZero()
    {
        var bytes = new List<byte>();
        AppendPacked(bytes, 3);
        AppendPacked(bytes, 3);
        AppendPacked(bytes, 3);
        AppendPacked(bytes, 8);
        AppendPacked(bytes, 0);
        AppendPacked(bytes, 0);
        AppendPacked(bytes, 0);
        var update = DecodeContents(bytes.ToArray());
        Assert.That(update.Count, Is.EqualTo(3));
        Assert.That(update.Updates, Is.EqualTo(new[] { new ItemSlotContentUpdate(2, 0) }));
    }

    [Test]
    public void MultiContentsDecodesArrayShrinkWithoutInventingUpdates()
    {
        var update = DecodeContents(new byte[] { 0, 0 });
        Assert.That(update.Count, Is.Zero);
        Assert.That(update.Updates, Is.Empty);
    }

    [TestCase("02040610540000", ArchiveErrorCode.InvalidCount)]
    [TestCase("02020810540000", ArchiveErrorCode.InvalidCount)]
    [TestCase("02020620CD", ArchiveErrorCode.InvalidBitCount)]
    [TestCase("0202062054000000", ArchiveErrorCode.UnexpectedTrailingData)]
    [TestCase("02020620CD0800", ArchiveErrorCode.EndOfArchive)]
    [TestCase("02020620CD08000000", ArchiveErrorCode.UnexpectedTrailingData)]
    public void MultiContentsRejectsMalformedWireData(string hex, ArchiveErrorCode code)
    {
        var exception = Assert.Throws<ArchiveReadException>(() => DecodeContents(Convert.FromHexString(hex)));
        Assert.That(exception!.ErrorCode, Is.EqualTo(code));
    }

    [Test]
    public void MultiContentsRejectsOversizedAllocation()
    {
        var bytes = new List<byte>();
        AppendPacked(bytes, 65537);
        var exception = Assert.Throws<ArchiveReadException>(() => DecodeContents(bytes.ToArray()));
        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.InvalidCount));
    }

    [Test]
    public void ItemContentsUsesPackedObjectReference()
    {
        var descriptor = new ItemSlotDescriptor();
        using var archive = new BitArchiveReader(new byte[] { 0x54 }, 8);
        var context = new FieldDecodeContext();
        var value = ((IFieldDecoder)descriptor.Fields.Single().Decoder!).Decode(ref context, archive);
        Assert.That(value.NetGuidValue, Is.EqualTo(42));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void PurchasedItemFieldsBindDumpVerifiedNamesAndTypes()
    {
        var descriptor = new PurchasedItemComponentDescriptor();
        var exports = new NetFieldExport?[6];
        string[] names = ["Purchaseable", "bIsCurrentSessionPurchase", "PurchasingPlayerState", "PurchasableTransactionSource"];
        for (uint handle = 2; handle <= 5; handle++)
            exports[handle] = new() { Handle = handle, Name = names[handle - 2], CompatibleChecksum = 0 };
        var bound = Bind(descriptor, exports);
        Assert.Multiple(() =>
        {
            Assert.That(bound.FieldsByHandle.Skip(2).All(field => field.Enabled), Is.True);
            Assert.That(bound.FieldsByHandle[2].TargetProperty!.PropertyType, Is.EqualTo(typeof(uint?)));
            Assert.That(bound.FieldsByHandle[3].TargetProperty!.PropertyType, Is.EqualTo(typeof(bool?)));
            Assert.That(bound.FieldsByHandle[4].TargetProperty!.PropertyType, Is.EqualTo(typeof(uint?)));
            Assert.That(bound.FieldsByHandle[5].TargetProperty!.PropertyType, Is.EqualTo(typeof(EInventoryTransaction?)));
        });
    }

    [Test]
    public void PurchaseSourceDecodesBitWidthSelectedByReplay()
    {
        var field = new PurchasedItemComponentDescriptor().Fields.Single(field => field.Handle == 5);
        using var archive = new BitArchiveReader(new byte[] { 4 }, 4);
        var context = new FieldDecodeContext();
        Assert.That(((IFieldDecoder)field.Decoder!).Decode(ref context, archive).UInt32Value,
            Is.EqualTo((uint)EInventoryTransaction.Drop));
        Assert.That(archive.AtEnd, Is.True);
    }

    private static BoundExportGroup Bind(ExportGroupDescriptor descriptor, NetFieldExport?[] exports)
    {
        var catalog = new DescriptorCatalog();
        catalog.Add(descriptor);
        var registry = new ExportBindingRegistry(catalog);
        registry.OnExportGroupAdded(new()
        {
            PathName = descriptor.Path, PathNameIndex = 1, NetFieldExports = exports,
        });
        return registry.GetBoundGroup(descriptor.Path)!;
    }

    private static ItemSlotContentsUpdate DecodeContents(byte[] bytes)
    {
        var field = new MultiItemSlotDescriptor().Fields.Single();
        using var archive = new BitArchiveReader(bytes, bytes.Length * 8);
        var context = new FieldDecodeContext();
        var value = ((IFieldDecoder)field.Decoder!).Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True);
        return (ItemSlotContentsUpdate)value.ObjectValue!;
    }

    private static void AppendPacked(List<byte> bytes, uint value)
    {
        do
        {
            var next = (byte)((value & 0x7f) << 1);
            value >>= 7;
            if (value != 0) next |= 1;
            bytes.Add(next);
        } while (value != 0);
    }
}
