using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class UltimateObjectiveDescriptorTests
{
    [Test]
    public void OrbPickupBindsInheritedRpcAndSpacedParameterNames()
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        var exports = new NetFieldExport?[10];
        exports[9] = new() { Handle = 9, Name = "OrbPickedUpRPC", CompatibleChecksum = 0 };
        const string cache = "/Game/GameModes/Bomb/BombGameState.BombGameState_C_ClassNetCache";
        registry.OnExportGroupAdded(new() { PathName = cache, PathNameIndex = 1, NetFieldExports = exports });
        registry.OnExportGroupAdded(new()
        {
            PathName = new OrbPickedUpRpcParameters().Path, PathNameIndex = 2,
            NetFieldExports = [new() { Handle = 0, Name = "Orb Gatherer", CompatibleChecksum = 0 },
                new() { Handle = 1, Name = "Collectable Orb", CompatibleChecksum = 0 }],
        });
        var function = registry.GetBoundCache(cache)!.FunctionsByHandle[9];
        Assert.That(function.FunctionGroup!.SourceDescriptor, Is.TypeOf<OrbPickedUpRpcParameters>());
        Assert.That(function.FunctionGroup.FieldsByHandle[0].TargetProperty!.Name, Is.EqualTo("OrbGatherer"));
        Assert.That(function.FunctionGroup.FieldsByHandle[1].TargetProperty!.Name, Is.EqualTo("CollectableOrb"));
    }

    [TestCase("OrbGatherer")]
    [TestCase("CollectableOrb")]
    public void OrbObjectReferencesDecodePackedNetGuids(string property)
    {
        var field = new OrbPickedUpRpcParameters().Fields.Single(f => f.PropertyName == property);
        using var archive = new BitArchiveReader(new byte[] { 0x54 }, 8);
        var context = new FieldDecodeContext();
        var decoded = ((IFieldDecoder)field.Decoder!).Decode(ref context, archive);
        Assert.That(decoded.NetGuidValue, Is.EqualTo(42));
        Assert.That(archive.AtEnd, Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void PlantSiteDecodesObservedTwoBitEnum(byte site)
    {
        var field = new BombPlantedRpcParameters().Fields.Single(f => f.PropertyName == "PlantSite");
        using var archive = new BitArchiveReader(new[] { site }, 2);
        var context = new FieldDecodeContext();
        var decoded = ((IFieldDecoder)field.Decoder!).Decode(ref context, archive);
        Assert.That(decoded.UInt32Value, Is.EqualTo(site));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void ObjectiveComponentAndCompletionRpcsAreRegistered()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        Assert.That(catalog.SubobjectClassPaths["Comp_BombEvents"],
            Is.EqualTo("/Game/GameModes/Components/Comp_BombEvents.Comp_BombEvents_C"));
        var cache = catalog.ClassNetCacheDescriptors.OfType<BombObjectiveClassNetCacheDescriptor>().Single();
        Assert.That(cache.FunctionFields.Single(f => f.Name == "BombPlantedRPC").ParameterDescriptor,
            Is.TypeOf<BombPlantedRpcParameters>());
        Assert.That(cache.FunctionFields.Single(f => f.Name == "BombDefusedRPC").ParameterDescriptor,
            Is.TypeOf<BombDefusedRpcParameters>());
    }
}
