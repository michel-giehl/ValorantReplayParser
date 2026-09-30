using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.GameState;

public class WingmanDescriptorTests
{
    [TestCase(11u, "Owner")]
    [TestCase(13u, "Instigator")]
    public void DumpVerifiedOwnershipReferencesBindAndDecode(uint handle, string name)
    {
        var fields = new NetFieldExport?[14];
        fields[handle] = new() { Handle = handle, Name = name, CompatibleChecksum = 0 };
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        registry.OnExportGroupAdded(new() { PathName = WingmanDescriptor.ExportPath, PathNameIndex = 1, NetFieldExports = fields });
        var field = registry.GetBoundGroup(WingmanDescriptor.ExportPath)!.FieldsByHandle[handle];
        Assert.That(field.Enabled, Is.True);
        Assert.That(field.TargetProperty!.Name, Is.EqualTo(name));
        Assert.That(field.TargetProperty.PropertyType, Is.EqualTo(typeof(uint)));
        using var archive = new BitArchiveReader(new byte[] { 0x54 }, 8);
        var context = new FieldDecodeContext();
        Assert.That(field.Decoder!.Decode(ref context, archive).NetGuidValue, Is.EqualTo(42));
        Assert.That(archive.AtEnd, Is.True);
        using var truncated = new BitArchiveReader(new byte[] { 0x01 }, 8);
        Assert.Throws<ArchiveReadException>(() => field.Decoder!.Decode(ref context, truncated));
    }
}
