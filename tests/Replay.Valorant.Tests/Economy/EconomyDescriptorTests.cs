using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Economy;

namespace Replay.Valorant.Tests.Economy;

public class EconomyDescriptorTests
{
    [TestCase(2u, nameof(MoneyManagementComponentDescriptor.Money))]
    [TestCase(3u, nameof(MoneyManagementComponentDescriptor.StartOfRoundMoney))]
    [TestCase(4u, nameof(MoneyManagementComponentDescriptor.TotalMoneyGranted))]
    public void MoneyPropertiesBindDumpVerifiedHandlesAndDecodeSignedValues(uint handle, string name)
    {
        var catalog = new DescriptorCatalog();
        catalog.Add(new MoneyManagementComponentDescriptor());
        var registry = new ExportBindingRegistry(catalog,
            new ParseProfile { EnabledCategories = ExportCategory.Economy });
        var exports = new NetFieldExport?[5];
        exports[handle] = Export(handle, name);
        registry.OnExportGroupAdded(new()
        {
            PathName = MoneyManagementComponentDescriptor.ExportPath,
            PathNameIndex = 1,
            NetFieldExports = exports,
        });
        var group = registry.GetBoundGroup(MoneyManagementComponentDescriptor.ExportPath)!;
        var field = group.FieldsByHandle[handle];
        using var archive = new BitArchiveReader(BitConverter.GetBytes(-500), 32);
        var context = new FieldDecodeContext();

        var decoded = field.Decoder!.Decode(ref context, archive);

        Assert.Multiple(() =>
        {
            Assert.That(group.Enabled, Is.True);
            Assert.That(field.Enabled, Is.True);
            Assert.That(field.TargetProperty!.PropertyType, Is.EqualTo(typeof(int?)));
            Assert.That(decoded.Int32Value, Is.EqualTo(-500));
            Assert.That(archive.AtEnd, Is.True);
        });
        using var truncated = new BitArchiveReader(new byte[3], 24);
        Assert.Throws<ArchiveReadException>(() => field.Decoder.Decode(ref context, truncated));
    }

    [Test]
    public void GunRequestRpcHandlesBindFlattenedStructAndFulfillerReference()
    {
        var catalog = new DescriptorCatalog();
        catalog.Add(new GunRequestComponentClassNetCacheDescriptor());
        var registry = new ExportBindingRegistry(catalog,
            new ParseProfile { EnabledCategories = ExportCategory.Economy });
        registry.OnExportGroupAdded(new()
        {
            PathName = GunRequestComponentClassNetCacheDescriptor.ExportPath,
            PathNameIndex = 1,
            NetFieldExports =
            [
                Export(0, "NetMulticastCancelGunRequest"),
                Export(1, "NetMulticastFulfillGunRequest"),
                Export(2, "NetMulticastMakeGunRequest"),
            ],
        });
        AddParameters(registry, NetMulticastCancelGunRequestParameters.ExportPath, 2, false);
        AddParameters(registry, NetMulticastFulfillGunRequestParameters.ExportPath, 3, true);
        AddParameters(registry, NetMulticastMakeGunRequestParameters.ExportPath, 4, false);

        var functions = registry.GetBoundCache(GunRequestComponentClassNetCacheDescriptor.ExportPath)!
            .FunctionsByHandle;

        Assert.Multiple(() =>
        {
            Assert.That(functions[0].FunctionGroup!.SourceDescriptor,
                Is.TypeOf<NetMulticastCancelGunRequestParameters>());
            Assert.That(functions[1].FunctionGroup!.SourceDescriptor,
                Is.TypeOf<NetMulticastFulfillGunRequestParameters>());
            Assert.That(functions[2].FunctionGroup!.SourceDescriptor,
                Is.TypeOf<NetMulticastMakeGunRequestParameters>());
        });
        foreach (var function in functions)
        {
            var group = function.FunctionGroup!;
            Assert.That(group.FieldsByHandle[0].TargetProperty!.Name, Is.EqualTo("RequestedGun"));
            Assert.That(group.FieldsByHandle[1].TargetProperty!.PropertyType,
                Is.EqualTo(typeof(AresGunRequestState?)));
            Assert.That(group.FieldsByHandle[0].Enabled, Is.True);
            Assert.That(group.FieldsByHandle[1].Enabled, Is.True);
        }
        Assert.That(functions[1].FunctionGroup!.FieldsByHandle[2].TargetProperty!.Name,
            Is.EqualTo("FulfillingPlayer"));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void GunRequestStateDecodesBoundedEnum(byte state)
    {
        var descriptor = new NetMulticastMakeGunRequestParameters();
        var field = descriptor.Fields.Single(value => value.PropertyName == "RequestState");
        using var archive = new BitArchiveReader(new[] { state }, 1);
        var context = new FieldDecodeContext();

        var decoded = ((IFieldDecoder)field.Decoder!).Decode(ref context, archive);

        Assert.That(decoded.UInt32Value, Is.EqualTo(state));
        Assert.That(archive.AtEnd, Is.True);
    }

    [TestCase("RequestedGun")]
    [TestCase("FulfillingPlayer")]
    public void GunRequestObjectReferencesDecodePackedNetGuidsAndRejectTruncation(string property)
    {
        var field = new NetMulticastFulfillGunRequestParameters().Fields
            .Single(value => value.PropertyName == property);
        using var archive = new BitArchiveReader(new byte[] { 0x54 }, 8);
        var context = new FieldDecodeContext();

        var decoded = ((IFieldDecoder)field.Decoder!).Decode(ref context, archive);

        Assert.That(decoded.NetGuidValue, Is.EqualTo(42));
        Assert.That(archive.AtEnd, Is.True);
        using var truncated = new BitArchiveReader(new byte[] { 0x01 }, 8);
        Assert.Throws<ArchiveReadException>(() => ((IFieldDecoder)field.Decoder).Decode(ref context, truncated));
    }

    [Test]
    public void OwnerExclusiveEconomyIdentityAndRoundFieldsBindWithEconomyOnlyProfile()
    {
        var descriptor = new OwnerExclusivePlayerInfoDescriptor();
        var catalog = new DescriptorCatalog();
        catalog.Add(descriptor);
        var registry = new ExportBindingRegistry(catalog,
            new ParseProfile { EnabledCategories = ExportCategory.Economy });
        var exports = new NetFieldExport?[40];
        exports[11] = Export(11, "Owner");
        exports[14] = Export(14, "AresController");
        exports[34] = Export(34, "EndOfRoundBeforeRewardsMoney");
        exports[35] = Export(35, "bLoadoutFinalized");
        exports[39] = Export(39, "RoundInfos");
        registry.OnExportGroupAdded(new()
        {
            PathName = descriptor.Path,
            PathNameIndex = 1,
            NetFieldExports = exports,
        });
        var group = registry.GetBoundGroup(descriptor.Path)!;

        Assert.That(group.Enabled, Is.True);
        Assert.That(group.SourceDescriptor.Kind, Is.EqualTo(ExportGroupKind.Actor));
        foreach (var handle in new uint[] { 11, 14, 34, 35, 39 })
            Assert.That(group.FieldsByHandle[handle].Enabled, Is.True);
    }

    private static NetFieldExport Export(uint handle, string name) =>
        new() { Handle = handle, Name = name, CompatibleChecksum = 0 };

    private static void AddParameters(ExportBindingRegistry registry, string path, uint index, bool fulfill) =>
        registry.OnExportGroupAdded(new()
        {
            PathName = path,
            PathNameIndex = index,
            NetFieldExports = fulfill
                ? [Export(0, "RequestedGun"), Export(1, "RequestState"), Export(2, "FulfillingPlayer")]
                : [Export(0, "RequestedGun"), Export(1, "RequestState")],
        });
}
