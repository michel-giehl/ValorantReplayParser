using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Effects.Locational;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class LocationalEffectDescriptorTests
{
    [Test]
    public void CatalogResolvesLocationalEffectManagerAndRecordedFunctionHandle()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        Assert.That(catalog.SubobjectClassPaths["LocationalEffectManager"],
            Is.EqualTo(LocationalEffectManagerClassNetCacheDescriptor.ComponentPath));
        var cache = catalog.ClassNetCacheDescriptors.Single(d =>
            d.Path == LocationalEffectManagerClassNetCacheDescriptor.ComponentPath + "_ClassNetCache");
        var function = cache.FunctionFields.Single();
        Assert.That((function.Name, function.Handle), Is.EqualTo(("ClientPlayOneShotEffectAtLocation", (uint?)2)));
        Assert.That(function.FunctionExportPath, Is.EqualTo(ClientPlayOneShotEffectAtLocationParameters.ExportPath));
        Assert.That(function.ParameterDescriptor, Is.TypeOf<ClientPlayOneShotEffectAtLocationParameters>());
        Assert.That(function.ParameterDescriptor!.Grammar, Is.EqualTo(FieldStreamGrammar.FunctionParameters));
    }

    [Test]
    public void LocationIsNativeUnrealCentimetersAndRotationIsCompressedShort()
    {
        var location = Decode(new ClientPlayOneShotEffectAtLocationParameters(), "Location", Vector(100, -200, 300));
        Assert.That((location.VectorValue.X, location.VectorValue.Y, location.VectorValue.Z), Is.EqualTo((100d, -200d, 300d)));
        var bits = new Bits(); bits.Angle(16384); bits.Angle(32768); bits.Bit(false);
        var rotation = Decode(new ClientPlayOneShotEffectAtLocationParameters(), "Rotation", bits.Bytes(), bits.Count);
        Assert.That((rotation.RotatorValue.Pitch, rotation.RotatorValue.Yaw, rotation.RotatorValue.Roll),
            Is.EqualTo((90f, 180f, 0f)));
        Assert.That(new ClientPlayOneShotEffectAtLocationParameters().Location, Is.Null);
        Assert.That(new ClientPlayOneShotEffectAtLocationParameters().Rotation, Is.Null);
    }

    [Test]
    public void FloatAndObjectValuesUseEffectManagerElementHandles()
    {
        var floats = new Bits(); floats.Packed(1); floats.Packed(1);
        floats.Field(3, b => b.Packed(491)); floats.Field(4, b => b.Float(1)); floats.Packed(0); floats.Packed(0);
        var parameters = new ClientPlayOneShotEffectAtLocationParameters();
        var floatValues = (EffectManagerFunctionFloatValue[])Decode(parameters, "FunctionFloatValues",
            floats.Bytes(), floats.Count).ObjectValue!;
        Assert.That(floatValues.Single().Name!.Value.TagIndex, Is.EqualTo(491));
        Assert.That(floatValues.Single().Value, Is.EqualTo(1f));

        var objects = new Bits(); objects.Packed(1); objects.Packed(1);
        objects.Field(11, b => b.Packed(466)); objects.Field(12, b => b.Packed(850)); objects.Packed(0); objects.Packed(0);
        var objectValues = (EffectManagerFunctionObjectValue[])Decode(parameters, "FunctionObjectValues",
            objects.Bytes(), objects.Count).ObjectValue!;
        Assert.That(objectValues.Single().Name!.Value.TagIndex, Is.EqualTo(466));
        Assert.That(objectValues.Single().Value!.Value.NetGuid, Is.EqualTo(850));
        Assert.That(parameters.FloatValues, Is.Empty);
        Assert.That(parameters.ObjectValues, Is.Empty);
    }

    [Test]
    public void StartMovementTimeIsSingleAndTruncatedLocationIsRejected()
    {
        var time = Decode(new ClientPlayOneShotEffectAtLocationParameters(), "StartMovementTime", BitConverter.GetBytes(12.5f));
        Assert.That(time.FloatValue, Is.EqualTo(12.5f));
        Assert.That(new ClientPlayOneShotEffectAtLocationParameters().StartMovementTime, Is.Null);
        Assert.Throws<ArchiveReadException>(() =>
            Decode(new ClientPlayOneShotEffectAtLocationParameters(), "Location", Vector(100, 200, 300), 191));
    }

    private static byte[] Vector(double x, double y, double z) =>
        [.. BitConverter.GetBytes(x), .. BitConverter.GetBytes(y), .. BitConverter.GetBytes(z)];

    private static DecodedFieldValue Decode(ExportGroupDescriptor descriptor, string property, byte[] bytes, int? bits = null)
    {
        using var archive = new BitArchiveReader(bytes, bits ?? bytes.Length * 8);
        var context = new FieldDecodeContext();
        var decoder = (IFieldDecoder)descriptor.Fields.Single(f => f.PropertyName == property).Decoder!;
        var result = decoder.Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True);
        return result;
    }

    private sealed class Bits
    {
        private readonly List<bool> _bits = [];
        public int Count => _bits.Count;
        public void Bit(bool value) => _bits.Add(value);
        public void Packed(uint value)
        {
            do { var b = (byte)((value & 127) << 1); value >>= 7; if (value != 0) b |= 1; Byte(b); } while (value != 0);
        }
        public void Field(uint handle, Action<Bits> payload)
        { var p = new Bits(); payload(p); Packed(handle + 1); Packed((uint)p._bits.Count); _bits.AddRange(p._bits); }
        public void Float(float value) { foreach (var b in BitConverter.GetBytes(value)) Byte(b); }
        public void Angle(ushort value) { Bit(true); foreach (var b in BitConverter.GetBytes(value)) Byte(b); }
        public byte[] Bytes()
        { var bytes = new byte[(_bits.Count + 7) / 8]; for (var i = 0; i < _bits.Count; i++) if (_bits[i]) bytes[i / 8] |= (byte)(1 << (i % 8));
            return bytes; }
        private void Byte(byte value) { for (var i = 0; i < 8; i++) Bit((value & (1 << i)) != 0); }
    }
}
