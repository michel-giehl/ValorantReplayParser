using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class BombPlayerStateUltimateTests
{
    private const string Path = "/Game/GameModes/Bomb/BombPlayerState.BombPlayerState_C";

    [TestCase(200u, "bUltimateActive", nameof(BombPlayerStateDescriptor.UltimateActive), typeof(bool))]
    [TestCase(201u, "NumUltimatePoints", nameof(BombPlayerStateDescriptor.NumUltimatePoints), typeof(int))]
    [TestCase(202u, "TotalAcquiredUltimatePoints", nameof(BombPlayerStateDescriptor.TotalAcquiredUltimatePoints), typeof(int))]
    public void DumpExportsBindToTypedProperties(uint handle, string name, string property, Type type)
    {
        var exports = new NetFieldExport?[203];
        exports[handle] = new NetFieldExport { Handle = handle, Name = name, CompatibleChecksum = 0 };
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        registry.OnExportGroupAdded(new NetFieldExportGroup { PathName = Path, PathNameIndex = 1, NetFieldExports = exports });
        var field = registry.GetBoundGroup(Path)!.FieldsByHandle[handle];
        Assert.Multiple(() =>
        {
            Assert.That(field.Enabled, Is.True);
            Assert.That(field.TargetProperty!.Name, Is.EqualTo(property));
            Assert.That(field.TargetProperty.PropertyType, Is.EqualTo(type));
            Assert.That(field.Decoder, Is.Not.Null);
        });
    }

    [TestCase(nameof(BombPlayerStateDescriptor.NumUltimatePoints), 0)]
    [TestCase(nameof(BombPlayerStateDescriptor.NumUltimatePoints), 8)]
    [TestCase(nameof(BombPlayerStateDescriptor.TotalAcquiredUltimatePoints), 31)]
    public void PointFieldsDecodeInt32IncludingReset(string property, int value)
    {
        var field = new BombPlayerStateDescriptor().Fields.Single(f => f.PropertyName == property);
        using var archive = new BitArchiveReader(BitConverter.GetBytes(value), 32);
        var context = new FieldDecodeContext { FieldName = property };
        var decoded = ((IFieldDecoder)field.Decoder!).Decode(ref context, archive);
        Assert.That(decoded.Int32Value, Is.EqualTo(value));
        Assert.That(archive.AtEnd, Is.True);
    }

    [TestCase(nameof(BombPlayerStateDescriptor.NumUltimatePoints))]
    [TestCase(nameof(BombPlayerStateDescriptor.TotalAcquiredUltimatePoints))]
    public void TruncatedPointsFail(string property)
    {
        var field = new BombPlayerStateDescriptor().Fields.Single(f => f.PropertyName == property);
        using var archive = new BitArchiveReader(new byte[] { 1 }, 8);
        var context = new FieldDecodeContext { FieldName = property };
        Assert.Throws<ArchiveReadException>(() => ((IFieldDecoder)field.Decoder!).Decode(ref context, archive));
    }
}
