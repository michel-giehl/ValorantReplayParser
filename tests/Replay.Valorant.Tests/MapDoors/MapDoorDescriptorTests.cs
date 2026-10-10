using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.MapDoors;

namespace Replay.Valorant.Tests.MapDoors;

public sealed class MapDoorDescriptorTests
{
    [TestCase(MapDoorDescriptor.LotusWall, "Hidden", "bHidden", 1u)]
    [TestCase(MapDoorDescriptor.LotusWall, "IsAlive", "IsAlive", 15u)]
    [TestCase(MapDoorDescriptor.SummitTrigger, "IsAlive", "IsAlive", 15u)]
    [TestCase(MapDoorDescriptor.DoorSwitch, "HasPlayed", "HasPlayed", 16u)]
    [TestCase(MapDoorDescriptor.DoorSwitch, "IsDisabled", "IsDisabled", 17u)]
    [TestCase(MapDoorDescriptor.DoorSwitch, "LeverDown", "LeverDown", 19u)]
    [TestCase(MapDoorDescriptor.LotusSwitch, "HasPlayed", "HasPlayed", 19u)]
    [TestCase(MapDoorDescriptor.LotusDoor, "Rotated", "Rotated", 24u)]
    public void FamilySpecificBoolHandlesPreserveFalse(string path, string property, string exported, uint handle)
    {
        var descriptor = new MapDoorDescriptor(path);
        var field = descriptor.Fields.Single(f => f.PropertyName == property);
        Assert.That(field.Handle, Is.EqualTo(handle));
        Assert.That(field.ExportName, Is.EqualTo(exported));
        var payload = Parse(descriptor, Stream([(handle, 1, new byte[] { 0 })]));
        Assert.That(payload.HasDecoded(property), Is.True);
        Assert.That(payload.GetType().GetProperty(property)!.GetValue(payload), Is.False);
        Assert.Throws<ArchiveReadException>(() => Decode(descriptor, property, [], 0));
    }

    [TestCase(MapDoorDescriptor.SummitDoor, "DoorState", 17u, 3, 2)]
    [TestCase(MapDoorDescriptor.SwitchDoor, "DoorState", 17u, 3, 3)]
    [TestCase(MapDoorDescriptor.LotusDoor, "DoorState", 17u, 3, 1)]
    [TestCase(MapDoorDescriptor.SummitDoor, "Transition", 18u, 2, 1)]
    [TestCase(MapDoorDescriptor.SwitchDoor, "Transition", 18u, 2, 0)]
    [TestCase(MapDoorDescriptor.LotusDoor, "Transition", 18u, 2, 1)]
    public void NativeEnumsConsumeObservedBoundedWidths(string path, string field, uint handle, int bits, byte value)
    {
        var descriptor = new MapDoorDescriptor(path);
        var bound = descriptor.Fields.Single(f => f.PropertyName == field);
        Assert.That(bound.Handle, Is.EqualTo(handle));
        Assert.That(bound.ExportName, Is.EqualTo(field));
        Assert.That(Decode(descriptor, field, [value], bits).UInt32Value, Is.EqualTo(value));
        var payload = Parse(descriptor, Stream([(handle, bits, new byte[] { value })]));
        Assert.That(payload.HasDecoded(field), Is.True);
        Assert.That(payload.GetType().GetProperty(field)!.GetValue(payload), Is.EqualTo(value));
    }

    [TestCase(MapDoorDescriptor.DoorSwitch, "LastUsedTime", 15u)]
    [TestCase(MapDoorDescriptor.DoorSwitch, "GameplayStartTime", 18u)]
    [TestCase(MapDoorDescriptor.LotusSwitch, "LastUsedTime", 17u)]
    public void WorldClocksAreDoublesAndRejectTruncation(string path, string field, uint handle)
    {
        const double time = 3456.123456789;
        var descriptor = new MapDoorDescriptor(path);
        Assert.That(descriptor.Fields.Single(f => f.PropertyName == field).Handle, Is.EqualTo(handle));
        Assert.That(Decode(descriptor, field, BitConverter.GetBytes(time), 64).DoubleValue, Is.EqualTo(time));
        Assert.Throws<ArchiveReadException>(() => Decode(descriptor, field, BitConverter.GetBytes(time), 63));
    }

    [TestCase("DoorOpenStartTime", 19u)]
    [TestCase("DoorCloseStartTime", 20u)]
    public void LotusMotionClocksPreserveFloatInvalidSentinel(string field, uint handle)
    {
        var descriptor = new MapDoorDescriptor(MapDoorDescriptor.LotusDoor);
        Assert.That(descriptor.Fields.Single(f => f.PropertyName == field).Handle, Is.EqualTo(handle));
        Assert.That(Decode(descriptor, field, Convert.FromHexString("FFFF7FFF"), 32).FloatValue, Is.EqualTo(-float.MaxValue));
        Assert.Throws<ArchiveReadException>(() => Decode(descriptor, field, Convert.FromHexString("FFFF7FFF"), 31));
    }

    [Test]
    public void SparseReplicationDistinguishesAbsentAndExplicitZeroAndFreshPayloads()
    {
        var descriptor = new MapDoorDescriptor(MapDoorDescriptor.SwitchDoor);
        var absent = Parse(descriptor, Stream([]));
        var open = Parse(descriptor, Stream([(17, 3, new byte[] { 0 }), (18, 2, new byte[] { 1 })]));
        var closed = Parse(descriptor, Stream([(17, 3, new byte[] { 3 })]));
        Assert.That(absent.HasDecoded(nameof(MapDoorDescriptor.DoorState)), Is.False);
        Assert.That(open.HasDecoded(nameof(MapDoorDescriptor.DoorState)), Is.True);
        Assert.That(((MapDoorDescriptor)open).DoorState, Is.Zero);
        Assert.That(((MapDoorDescriptor)open).Transition, Is.EqualTo(1));
        Assert.That(((MapDoorDescriptor)closed).DoorState, Is.EqualTo(3));
        Assert.That(closed.HasDecoded(nameof(MapDoorDescriptor.Transition)), Is.False);
        Assert.That(descriptor.DecodedProperties, Is.Empty);
        var stream = Stream([(17, 3, new byte[] { 2 })]);
        Assert.Throws<InvalidReplayDataException>(() => Parse(descriptor, (stream.Data, stream.Bits - 10)));
    }

    [Test]
    public void CatalogKeepsExactSwitchNamesHandlesResetMarkersAndInheritedDeath()
    {
        var catalog = new DescriptorCatalog();
        MapDoorDescriptors.AddTo(catalog);
        Assert.That(catalog.ExportGroupDescriptors.OfType<MapDoorDescriptor>().Count(), Is.EqualTo(7));
        Assert.That(catalog.ClassNetCacheDescriptors.Count, Is.EqualTo(5));
        foreach (var descriptor in catalog.ExportGroupDescriptors)
            Assert.That(((ExportGroupDescriptor)descriptor.CreatePayloadInstance()).Path, Is.EqualTo(descriptor.Path));
        foreach (var (path, off, on) in new[] { (MapDoorDescriptor.DoorSwitch, 2u, 3u), (MapDoorDescriptor.LotusSwitch, 4u, 5u) })
        {
            var cache = catalog.ClassNetCacheDescriptors.Single(c => c.Path == path + "_ClassNetCache");
            Assert.That(cache.FunctionFields.Single(f => f.Handle == 0).Name, Is.EqualTo(" MulticastPlayAnimation"));
            Assert.That(cache.FunctionFields.Single(f => f.Handle == 1).Name, Is.EqualTo("MulticastResetAnimation"));
            Assert.That(cache.FunctionFields.Single(f => f.Handle == off).Name, Is.EqualTo("ToggleLightOff"));
            Assert.That(cache.FunctionFields.Single(f => f.Handle == on).Name, Is.EqualTo("ToggleLightOn"));
        }
        foreach (var path in new[] { MapDoorDescriptor.SummitTrigger, MapDoorDescriptor.LotusWall })
        {
            var cache = catalog.ClassNetCacheDescriptors.Single(c => c.Path == path + "_ClassNetCache");
            var death = cache.FunctionFields.Single(f => f.Name == "OnDie");
            Assert.That(death.FunctionExportPath, Is.EqualTo(MapDoorRpcParameters.DeathPath));
            Assert.That(death.ParameterDescriptor, Is.TypeOf<MapDoorRpcParameters>());
            Assert.That(cache.FunctionFields.Single(f => f.Name == "RoundBeginBroadcast").Decoder, Is.Not.Null);
        }
        var registry = new ExportBindingRegistry(catalog);
        registry.OnExportGroupAdded(new() { PathName = MapDoorDescriptor.LotusSwitch + "_ClassNetCache", PathNameIndex = 1,
            NetFieldExports = [new() { Handle = 0, Name = " MulticastPlayAnimation", CompatibleChecksum = 0 }] });
        Assert.That(registry.GetBoundCache(MapDoorDescriptor.LotusSwitch + "_ClassNetCache")!.FunctionsByHandle[0].Name,
            Is.EqualTo(" MulticastPlayAnimation"));
    }

    [Test]
    public void RecordedPhaseAndFunctionDefaultZeroRemainTyped()
    {
        var phase = new MapDoorRpcParameters(MapDoorDescriptor.LotusWall + ":OnPhaseChangeEvent");
        var observed = (MapDoorRpcParameters)Parse(phase, (Convert.FromHexString("0420060000"), 33));
        Assert.That(observed.NewPhase, Is.EqualTo(3));
        Assert.That(observed.HasDecoded(nameof(observed.NewPhase)), Is.True);
        var state = new MapDoorRpcParameters(MapDoorDescriptor.SwitchDoor + ":PlayDoorSounds");
        var call = (MapDoorRpcParameters)Parse(state, Stream([(1, 3, new byte[] { 1 })]));
        Assert.That(call.OldState, Is.EqualTo(1));
        Assert.That(call.NewState, Is.Zero);
        Assert.That(call.HasDecoded(nameof(call.NewState)), Is.False);
        Assert.That(state.Fields.Single(f => f.PropertyName == "NewState").ExportName, Is.EqualTo("New State"));
    }

    private static DecodedFieldValue Decode(ExportGroupDescriptor descriptor, string field, byte[] data, int bits)
    {
        using var archive = new BitArchiveReader(data, bits);
        var context = new FieldDecodeContext();
        var result = ((IFieldDecoder)descriptor.Fields.Single(f => f.PropertyName == field).Decoder!).Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True);
        return result;
    }
    private static ExportGroupDescriptor Parse(ExportGroupDescriptor descriptor, (byte[] Data, int Bits) stream)
    {
        var fields = new FieldBinding[descriptor.Fields.Max(f => (int)f.Handle!.Value) + 1];
        foreach (var field in descriptor.Fields)
            fields[field.Handle!.Value] = new() { Enabled = true, Decoder = (IFieldDecoder)field.Decoder!,
                Name = field.PropertyName, TargetProperty = field.TargetProperty, Categories = ExportCategory.GameState };
        var bound = new BoundExportGroup { SourceDescriptor = descriptor, FieldsByHandle = fields,
            Grammar = descriptor.Grammar, Enabled = true };
        using var archive = new BitArchiveReader(stream.Data, stream.Bits);
        var context = new FieldDecodeContext();
        var result = new FieldPayloadParser().ParseRepLayoutProperties(archive, bound, ref context);
        Assert.That(archive.AtEnd, Is.True);
        return (ExportGroupDescriptor)result.Payload!;
    }
    private static (byte[] Data, int Bits) Stream((uint Handle, int Bits, byte[] Data)[] fields)
    {
        var bits = new List<bool> { false };
        foreach (var field in fields)
        {
            Packed(field.Handle + 1);
            Packed((uint)field.Bits);
            for (var i = 0; i < field.Bits; i++) bits.Add((field.Data[i / 8] & (1 << (i % 8))) != 0);
        }
        Packed(0);
        var data = new byte[(bits.Count + 7) / 8];
        for (var i = 0; i < bits.Count; i++) if (bits[i]) data[i / 8] |= (byte)(1 << (i % 8));
        return (data, bits.Count);
        void Packed(uint value)
        {
            do
            {
                var encoded = (byte)((value & 0x7f) << 1);
                value >>= 7;
                if (value > 0) encoded |= 1;
                for (var bit = 0; bit < 8; bit++) bits.Add((encoded & (1 << bit)) != 0);
            } while (value > 0);
        }
    }
}
