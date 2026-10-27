using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class TimedBombDescriptorTests
{
    [Test]
    public void SpikeAndAttemptRpcsAreRegistered()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        Assert.That(catalog.ExportGroupDescriptors.Single(d => d.Path == TimedBombDescriptor.ExportPath),
            Is.TypeOf<TimedBombDescriptor>());
        var cache = catalog.ClassNetCacheDescriptors.OfType<BombObjectiveClassNetCacheDescriptor>().Single();
        Assert.That(cache.FunctionFields.Select(f => f.Name), Is.SupersetOf(new[]
        {
            "BombPlantStartRPC", "BombPlantCancelRPC", "BombDefuseStartRPC", "BombDefuseStopRPC",
            "BombDefuseCheckpointReachedRPC", "BombCarrierKilledRPC", "BombDefuserKilledRPC", "BombPlanterKilledRPC",
        }));
        Assert.That(cache.FunctionFields.Single(f => f.Name == "BombDefuseCheckpointReachedRPC").ParameterDescriptor,
            Is.TypeOf<BombDefuseCheckpointReachedRpcParameters>());
        Assert.That(cache.FunctionFields.Single(f => f.Name == "BombDefuseStopRPC").ParameterDescriptor, Is.Null);
        Assert.That(cache.FunctionFields.All(f => f.FunctionExportPath!.StartsWith(
            BombObjectiveClassNetCacheDescriptor.ObjectiveRpcPath, StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void SpikePropertiesBindObservedHandlesByName()
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog(), releaseVersion: TestReleases.Current);
        var fields = new NetFieldExport?[24];
        foreach (var (handle, name) in new[]
                 {
                     (15, "TimeRemainingToExplode"), (16, "PlantedAtSite"), (17, "BombHasBeenDefused"),
                     (18, "BombDefuseState"), (19, "CurrentDefuser"), (20, "DefuseProgress"),
                     (22, "CurrentDefuseSection"), (23, "BombInitializeComplete"),
                 })
        {
            fields[handle] = new() { Handle = (uint)handle, Name = name, CompatibleChecksum = 0 };
        }

        registry.OnExportGroupAdded(new() { PathName = TimedBombDescriptor.ExportPath, PathNameIndex = 1, NetFieldExports = fields });
        var bound = registry.GetBoundGroup(TimedBombDescriptor.ExportPath)!;
        Assert.That(bound.FieldsByHandle[15].TargetProperty!.Name, Is.EqualTo("TimeRemainingToExplode"));
        Assert.That(bound.FieldsByHandle[19].TargetProperty!.Name, Is.EqualTo("CurrentDefuser"));
        Assert.That(bound.FieldsByHandle[20].TargetProperty!.Name, Is.EqualTo("DefuseProgress"));
        Assert.That(bound.FieldsByHandle[22].TargetProperty!.Name, Is.EqualTo("CurrentDefuseSection"));
        Assert.That(bound.FieldsByHandle[23].TargetProperty!.Name, Is.EqualTo("BombInitializeComplete"));
    }

    [Test]
    public void TimerAndProgressAreDoublesSectionIsInt32AndSiteIsTwoBits()
    {
        var spike = new TimedBombDescriptor();
        Assert.That(Decode(spike, "TimeRemainingToExplode", BitConverter.GetBytes(44.25d)).DoubleValue, Is.EqualTo(44.25d));
        Assert.That(Decode(spike, "DefuseProgress", BitConverter.GetBytes(3.5d)).DoubleValue, Is.EqualTo(3.5d));
        Assert.That(Decode(spike, "CurrentDefuseSection", BitConverter.GetBytes(2)).Int32Value, Is.EqualTo(2));
        Assert.That(Decode(spike, "PlantedAtSite", [2], 2).UInt32Value, Is.EqualTo(2));
        Assert.That(Decode(spike, "BombDefuseState", [1], 2).UInt32Value, Is.EqualTo(1));
        Assert.That(Decode(spike, "CurrentDefuser", [0x54]).ObjectReferenceValue.NetGuid, Is.EqualTo(42));
        Assert.That(Decode(spike, "BombHasBeenDefused", [1], 1).BoolValue, Is.True);
        Assert.That(spike.DefuseProgress, Is.Null);
        Assert.That(spike.BombHasBeenDefused, Is.Null);
    }

    [Test]
    public void AttemptParametersDecodeReferencesSitesAndCheckpointSeconds()
    {
        var checkpoint = new BombDefuseCheckpointReachedRpcParameters();
        Assert.That(Decode(checkpoint, "Defuser", [0x54]).ObjectReferenceValue.NetGuid, Is.EqualTo(42));
        Assert.That(Decode(checkpoint, "CheckpointNumber", BitConverter.GetBytes(1)).Int32Value, Is.EqualTo(1));
        Assert.That(Decode(checkpoint, "Progress", BitConverter.GetBytes(3.4665d)).DoubleValue, Is.EqualTo(3.4665d));
        Assert.That(Decode(new BombPlantStartRpcParameters(), "Planter", [0x54]).ObjectReferenceValue.NetGuid, Is.EqualTo(42));
        Assert.That(Decode(new BombPlantStartRpcParameters(), "PlantSite", [1], 2).UInt32Value, Is.EqualTo(1));
        Assert.That(Decode(new BombPlantCancelRpcParameters(), "PlantSite", [0], 2).UInt32Value, Is.Zero);
        Assert.That(Decode(new BombCarrierKilledRpcParameters(), "OldCarrier", [0x54]).ObjectReferenceValue.NetGuid, Is.EqualTo(42));
        Assert.That(Decode(new BombDefuserKilledRpcParameters(), "ShooterCharacter", [0]).ObjectReferenceValue.NetGuid, Is.Zero);
        Assert.That(Decode(new BombPlanterKilledRpcParameters(), "Planter", [0x54]).ObjectReferenceValue.NetGuid, Is.EqualTo(42));
        Assert.Throws<ArchiveReadException>(() => Decode(checkpoint, "Progress", BitConverter.GetBytes(3.4665d), 63));
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
