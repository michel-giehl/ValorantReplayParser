using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.Descriptors.Agents.Pandemic;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class SnakeBiteDescriptorTests
{
    [Test]
    public void CatalogRegistersAllThreeActorsAndTheirRecordedFunctionsExactlyOnce()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        foreach (var actor in SnakeBiteDescriptors.CreateDescriptors())
        {
            Assert.That(catalog.ExportGroupDescriptors.Count(d => d.Path == actor.Path), Is.EqualTo(1));
            var fresh = (ExportGroupDescriptor)actor.CreatePayloadInstance();
            Assert.That(fresh.Fields.Select(f => f.ExportName), Is.EqualTo(actor.Fields.Select(f => f.ExportName)));
            Assert.That(fresh.DecodedProperties, Is.Empty);
        }
        foreach (var cache in SnakeBiteDescriptors.CreateClassNetCacheDescriptors())
        {
            var registered = catalog.ClassNetCacheDescriptors.Single(c => c.Path == cache.Path);
            Assert.That(registered.FunctionFields.Single().Handle, Is.EqualTo(cache.FunctionFields.Single().Handle));
            var decoder = (IRpcDecoder)registered.FunctionFields.Single().Decoder!;
            var context = new FieldDecodeContext();
            using var payload = new BitArchiveReader([], 0);
            decoder.Decode(ref context, payload);
            Assert.That(payload.AtEnd, Is.True);
        }
    }

    [Test]
    public void RecordedExportNamesBindToTheirHandlesIncludingTheMisspelledPatchFlag()
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog());
        var fields = new NetFieldExport?[32];
        foreach (var (handle, name) in new[] { (11, "Owner"), (13, "Instigator"), (31, "Has Succesfully Hit") })
            fields[handle] = new() { Handle = (uint)handle, CompatibleChecksum = 0, Name = name };
        registry.OnExportGroupAdded(new() { PathName = SnakeBiteDescriptors.Patch, PathNameIndex = 1, NetFieldExports = fields });
        var bound = registry.GetBoundGroup(SnakeBiteDescriptors.Patch)!;
        Assert.That(bound.FieldsByHandle[11].Name, Is.EqualTo(nameof(SnakeBitePatchDescriptor.Owner)));
        Assert.That(bound.FieldsByHandle[13].Name, Is.EqualTo(nameof(SnakeBitePatchDescriptor.Instigator)));
        Assert.That(bound.FieldsByHandle[31].Name, Is.EqualTo(nameof(SnakeBitePatchDescriptor.HasSuccessfullyHit)));
    }

    [Test]
    public void RecordedReferencesAndFlagConsumeTheirBoundedPayloadsAndPreserveAbsence()
    {
        // 12438a1c, patch 4574, packet 16198; projectile 4528, packet 16093.
        var patch = new SnakeBitePatchDescriptor();
        Assert.That(patch.Owner, Is.Null);
        Assert.That(patch.HasSuccessfullyHit, Is.Null);
        Assert.That(Decode(patch, nameof(patch.Owner), "7D14", 16).NetGuidValue, Is.EqualTo(1342));
        Assert.That(Decode(patch, nameof(patch.Instigator), "5D14", 16).NetGuidValue, Is.EqualTo(1326));
        Assert.That(Decode(patch, nameof(patch.HasSuccessfullyHit), "01", 1).BoolValue, Is.True);
        Assert.That(Decode(patch, nameof(patch.HasSuccessfullyHit), "00", 1).BoolValue, Is.False);
        Assert.Throws<ArchiveReadException>(() => Decode(patch, nameof(patch.Owner), "7D14", 8));
        Assert.Throws<ArchiveReadException>(() => Decode(patch, nameof(patch.HasSuccessfullyHit), "", 0));
        Assert.That(patch.HasDecoded(nameof(patch.HasSuccessfullyHit)), Is.False);
    }

    [Test]
    public void RecordedProjectileMovementConsumesExactBoundaryAndRejectsTruncation()
    {
        var projectile = SnakeBiteDescriptors.CreateDescriptors().OfType<AbilityActorDescriptor>()
            .Single(d => d.Path == SnakeBiteDescriptors.Projectile);
        const string raw = "F0DCE34CB31402F7C76A3A70147BE801";
        var movement = Decode(projectile, nameof(projectile.ReplicatedMovement), raw, 121).RepMovementValue;
        Assert.That(movement.Location, Is.Not.Null);
        Assert.That(movement.Location!.Value.ScaleFactor, Is.EqualTo(100));
        Assert.Throws<ArchiveReadException>(() => Decode(projectile, nameof(projectile.ReplicatedMovement), raw, 120));
    }

    private static DecodedFieldValue Decode(ExportGroupDescriptor descriptor, string property, string hex, int bits)
    {
        using var archive = new BitArchiveReader(Convert.FromHexString(hex), bits);
        var context = new FieldDecodeContext();
        var result = ((IFieldDecoder)descriptor.Fields.Single(f => f.PropertyName == property).Decoder!).Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True);
        return result;
    }
}
