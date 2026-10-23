using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;
using Replay.Valorant.Inventory;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class AgentPickupAndCommDescriptorTests
{
    [Test]
    public void EveryAgentCacheBindsPickupAndCommFunctionsByInheritedPath()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var caches = catalog.ClassNetCacheDescriptors
            .Where(d => d.Path.EndsWith("_PC_C_ClassNetCache", StringComparison.Ordinal)).ToArray();
        Assert.That(caches, Is.Not.Empty);
        foreach (var cache in caches)
        {
            var pickup = cache.FunctionFields.Single(f => f.Name == "MulticastItemPickedUp");
            var halos = cache.FunctionFields.Single(f => f.Name == "UpdateHalosEvent");
            Assert.That(pickup.FunctionExportPath, Is.EqualTo(MulticastItemPickedUpParameters.ExportPath));
            Assert.That(pickup.ParameterDescriptor, Is.TypeOf<MulticastItemPickedUpParameters>());
            Assert.That(halos.FunctionExportPath, Is.EqualTo(UpdateHalosEventParameters.ExportPath));
            Assert.That(halos.ParameterDescriptor, Is.TypeOf<UpdateHalosEventParameters>());
        }
    }

    [Test]
    public void PickupDecodesPackedItemAndDroppedFlag()
    {
        var pickup = new MulticastItemPickedUpParameters();
        Assert.That(Decode(pickup, "Item", [0x54]).ObjectReferenceValue.NetGuid, Is.EqualTo(42));
        Assert.That(Decode(pickup, "CurrentWasDropped", [1], 1).BoolValue, Is.True);
        Assert.That(Decode(pickup, "CurrentWasDropped", [0], 1).BoolValue, Is.False);
        Assert.That(pickup.Item, Is.Null);
        Assert.That(pickup.CurrentWasDropped, Is.Null);
    }

    [TestCase(7)]
    [TestCase(41)]
    public void CommActionConsumesTheRecordedEnumWidth(byte action)
    {
        var halos = new UpdateHalosEventParameters();
        Assert.That(Decode(halos, "CommActionEnum", [action], 6).UInt32Value, Is.EqualTo(action));
        Assert.That(halos.CommActionEnum, Is.Null);
    }

    private static DecodedFieldValue Decode(ExportGroupDescriptor descriptor, string property, byte[] bytes, int? bits = null)
    {
        using var archive = new BitArchiveReader(bytes, bits ?? bytes.Length * 8);
        var context = new FieldDecodeContext();
        var decoder = (IFieldDecoder)descriptor.Fields.Single(f => f.PropertyName == property).Decoder!;
        var result = decoder.Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True);
        return result;
    }
}
