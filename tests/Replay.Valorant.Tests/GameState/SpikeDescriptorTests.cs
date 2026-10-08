using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents.Deadeye;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class SpikeDescriptorTests
{
    [TestCase("ground", "MyEquippable")]
    [TestCase("ground", "LastOwner")]
    [TestCase("projectile", "MyEquippable")]
    [TestCase("pickup", "NewBombHolder")]
    [TestCase("drop", "OldBombHolder")]
    [TestCase("hotHands", "Owner")]
    [TestCase("hotHands", "Instigator")]
    [TestCase("runItBack", "Owner")]
    [TestCase("runItBack", "Instigator")]
    [TestCase("trademark", "Owner")]
    [TestCase("trademark", "Instigator")]
    [TestCase("rendezvous", "Owner")]
    [TestCase("rendezvous", "Instigator")]
    public void ReferencesConsumeExactlyTheirBoundedPayloadAndRejectTruncation(string kind, string property)
    {
        var field = Descriptor(kind).Fields.Single(f => f.PropertyName == property);
        using var archive = new BitArchiveReader(new byte[] { 0x54 }, 8);
        var context = new FieldDecodeContext();
        var decoder = (IFieldDecoder)field.Decoder!;
        var decoded = decoder.Decode(ref context, archive);
        Assert.That(decoded.NetGuidValue, Is.EqualTo(42));
        Assert.That(archive.AtEnd, Is.True);
        using var truncated = new BitArchiveReader(Array.Empty<byte>(), 0);
        Assert.Throws<ArchiveReadException>(() => decoder.Decode(ref context, truncated));
    }

    [TestCase("ground", 15, "MyEquippable", 16, "LastOwner")]
    [TestCase("projectile", 10, "ReplicatedMovement", 16, "MyEquippable")]
    [TestCase("hotHands", 11, "Owner", 13, "Instigator")]
    [TestCase("runItBack", 11, "Owner", 13, "Instigator")]
    [TestCase("trademark", 11, "Owner", 13, "Instigator")]
    [TestCase("rendezvous", 11, "Owner", 13, "Instigator")]
    public void PickupExportsBindObservedHandlesByName(string kind, int first, string firstName, int second, string secondName)
    {
        var descriptor = Descriptor(kind);
        Assert.That(((ExportGroupDescriptor)descriptor.CreatePayloadInstance()).Path, Is.EqualTo(descriptor.Path));
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        var fields = new NetFieldExport?[17];
        fields[first] = new() { Handle = (uint)first, Name = firstName, CompatibleChecksum = 0 };
        fields[second] = new() { Handle = (uint)second, Name = secondName, CompatibleChecksum = 0 };
        registry.OnExportGroupAdded(new() { PathName = descriptor.Path, PathNameIndex = 1, NetFieldExports = fields });
        var bound = registry.GetBoundGroup(descriptor.Path)!;
        Assert.That(bound.FieldsByHandle[first].TargetProperty!.Name, Is.EqualTo(firstName));
        Assert.That(bound.FieldsByHandle[second].TargetProperty!.Name, Is.EqualTo(secondName));
    }

    [TestCase("BombPickedUpRPC", "pickup", 8)]
    [TestCase("BombDroppedRPC", "drop", 6)]
    public void ObjectiveRpcsBindTheirTypedReferenceParameters(string name, string kind, int handle)
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        var fields = new NetFieldExport?[9];
        fields[handle] = new() { Handle = (uint)handle, Name = name, CompatibleChecksum = 0 };
        var path = new BombObjectiveClassNetCacheDescriptor().Path;
        var descriptor = Descriptor(kind);
        registry.OnExportGroupAdded(new() { PathName = path, PathNameIndex = 1, NetFieldExports = fields });
        registry.OnExportGroupAdded(new() { PathName = descriptor.Path, PathNameIndex = 2,
            NetFieldExports = [new() { Handle = 0, Name = descriptor.Fields.Single().PropertyName!, CompatibleChecksum = 0 }] });
        var bound = registry.GetBoundCache(path)!.FunctionsByHandle[handle].FunctionGroup!;
        Assert.That(bound.SourceDescriptor.GetType(), Is.EqualTo(descriptor.GetType()));
        Assert.That(bound.FieldsByHandle[0].TargetProperty!.Name, Is.EqualTo(descriptor.Fields.Single().PropertyName));
    }

    private static ExportGroupDescriptor Descriptor(string kind) => kind switch
    {
        "ground" => new EquippableGroundPickupDescriptor(),
        "projectile" => new EquippablePickupProjectileDescriptor(),
        "pickup" => new BombPickedUpRpcParameters(),
        "drop" => new BombDroppedRpcParameters(),
        "hotHands" => new PhoenixPresenceDescriptor(PhoenixPresenceDescriptor.HotHands),
        "runItBack" => new PhoenixPresenceDescriptor(PhoenixPresenceDescriptor.RunItBack),
        "trademark" => new ChamberPresenceDescriptor(ChamberPresenceDescriptor.Trademark),
        "rendezvous" => new ChamberPresenceDescriptor(ChamberPresenceDescriptor.Rendezvous),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
