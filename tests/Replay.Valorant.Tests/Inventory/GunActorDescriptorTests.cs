using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Inventory;

namespace Replay.Valorant.Tests.Inventory;

public class GunActorDescriptorTests
{
    [Test]
    public void EveryGunClassIsRegistered()
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        foreach (var path in GunActorDescriptor.ClassPaths)
        {
            registry.OnExportGroupAdded(new() { PathName = path, PathNameIndex = 1, NetFieldExports = [] });
            Assert.That(registry.GetBoundGroup(path)?.SourceDescriptor, Is.TypeOf<GunActorDescriptor>(), path);
        }
    }

    // Handles observed in the 3f0a3366 export table for /Game/Equippables/Guns/Sidearms/BasePistol/BasePistol.BasePistol_C.
    [TestCase(16, "OwningPrimaryDataAsset")]
    [TestCase(17, "SkinDataAsset")]
    [TestCase(18, "SkinLevelDataAsset")]
    [TestCase(22, "ChromaDataAsset")]
    [TestCase(23, "CharmDataAsset")]
    [TestCase(24, "CharmLevelDataAsset")]
    public void CosmeticAssetsBindByNameAndDecodeAsObjectReferences(int handle, string name)
    {
        const string path = "/Game/Equippables/Guns/Sidearms/BasePistol/BasePistol.BasePistol_C";
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        var fields = new NetFieldExport?[handle + 1];
        fields[handle] = new() { Handle = (uint)handle, Name = name, CompatibleChecksum = 0 };
        registry.OnExportGroupAdded(new() { PathName = path, PathNameIndex = 1, NetFieldExports = fields });
        var field = registry.GetBoundGroup(path)!.FieldsByHandle[handle];
        Assert.That(field.TargetProperty!.Name, Is.EqualTo(name));

        using var archive = new BitArchiveReader(new byte[] { 0x54 }, 8);
        var context = new FieldDecodeContext();
        var decoded = field.Decoder!.Decode(ref context, archive);
        Assert.That(decoded.ObjectReferenceValue.NetGuid, Is.EqualTo(42));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void PayloadInstancesKeepTheirClassPath()
    {
        var descriptor = new GunActorDescriptor(GunActorDescriptor.ClassPaths[0]);
        Assert.That(((GunActorDescriptor)descriptor.CreatePayloadInstance()).Path, Is.EqualTo(descriptor.Path));
    }
}
