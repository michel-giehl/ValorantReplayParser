using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class ZoomMultiplierComponentDescriptorTests
{
    [Test]
    public void ZoomMultiplierResolvesByInstanceNameToNativeDescriptor()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        Assert.That(catalog.SubobjectClassPaths["ZoomMultiplier"], Is.EqualTo(ZoomMultiplierComponentDescriptor.ExportPath));
        Assert.That(catalog.ExportGroupDescriptors.Single(d => d.Path == ZoomMultiplierComponentDescriptor.ExportPath),
            Is.TypeOf<ZoomMultiplierComponentDescriptor>());
    }

    [Test]
    public void ZoomFieldsBindObservedHandlesByName()
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        var fields = new NetFieldExport?[11];
        foreach (var (handle, name) in new[]
                 {
                     (4, "SourceZoomLevel"), (5, "TargetFov"), (7, "TargetZoomLevel"),
                     (8, "TotalTransitionTimeDuration"), (10, "TransitionState"),
                 })
        {
            fields[handle] = new() { Handle = (uint)handle, Name = name, CompatibleChecksum = 0 };
        }

        registry.OnExportGroupAdded(new()
            { PathName = ZoomMultiplierComponentDescriptor.ExportPath, PathNameIndex = 1, NetFieldExports = fields });
        var bound = registry.GetBoundGroup(ZoomMultiplierComponentDescriptor.ExportPath)!;
        Assert.That(bound.FieldsByHandle[4].TargetProperty!.Name, Is.EqualTo("SourceZoomLevel"));
        Assert.That(bound.FieldsByHandle[7].TargetProperty!.Name, Is.EqualTo("TargetZoomLevel"));
        Assert.That(bound.FieldsByHandle[10].TargetProperty!.Name, Is.EqualTo("TransitionState"));
    }

    [Test]
    public void ZoomLevelsAreSignedFovIsSingleAndTransitionStateIsTwoBits()
    {
        var zoom = new ZoomMultiplierComponentDescriptor();
        Assert.That(Decode(zoom, "SourceZoomLevel", BitConverter.GetBytes(-1)).Int32Value, Is.EqualTo(-1));
        Assert.That(Decode(zoom, "TargetZoomLevel", BitConverter.GetBytes(1)).Int32Value, Is.EqualTo(1));
        Assert.That(Decode(zoom, "TargetFov", BitConverter.GetBytes(103f)).FloatValue, Is.EqualTo(103f));
        Assert.That(Decode(zoom, "TotalTransitionTimeDuration", BitConverter.GetBytes(0.1f)).FloatValue, Is.EqualTo(0.1f));
        Assert.That(Decode(zoom, "TransitionState", [2], 2).UInt32Value, Is.EqualTo(2));
        Assert.That(zoom.TargetZoomLevel, Is.Null);
        Assert.Throws<ArchiveReadException>(() => Decode(zoom, "TargetFov", BitConverter.GetBytes(103f), 31));
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
