using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents.Mage;
using Replay.Valorant.Descriptors.Agents.Phoenix;
using Replay.Valorant.Descriptors.Agents.Sprinter;
using Replay.Valorant.Nearsights.Descriptors;
using Replay.Valorant.Reveals.Descriptors;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class AbilityProjectileDescriptorTests
{
    [TestCase(typeof(FlameWallDescriptor))]
    [TestCase(typeof(MageWallDescriptor))]
    [TestCase(typeof(NeonTunnelDescriptor))]
    public void NamedProjectileFieldsBindAtRecordedHandlesAndAssignConcretePayload(Type descriptorType)
    {
        var descriptor = (ExportGroupDescriptor)Activator.CreateInstance(descriptorType)!;
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog(), releaseVersion: TestReleases.Current);
        var exports = new NetFieldExport?[24];
        exports[23] = new NetFieldExport { Handle = 23, Name = "Owner", CompatibleChecksum = 0 };
        registry.OnExportGroupAdded(new NetFieldExportGroup
        {
            PathName = descriptor.Path,
            PathNameIndex = 1,
            NetFieldExports = exports,
        });

        var group = registry.GetBoundGroup(descriptor.Path)!;
        var payload = (ExportGroupDescriptor)group.SourceDescriptor.CreatePayloadInstance();
        var binding = group.FieldsByHandle[23];
        var owner = new NetObjectReference(17, "OwnerPath");
        DecodedValueAssigner.Assign(payload, binding, DecodedFieldValue.FromObjectReference(owner));

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetType(), Is.EqualTo(descriptorType));
            Assert.That(payload.HasDecoded("Owner"), Is.True);
            Assert.That(binding.TargetProperty!.GetValue(payload), Is.EqualTo(owner));
            Assert.That(descriptor.Categories, Is.EqualTo(ExportCategory.Ability));
            Assert.That(descriptor.Fields.All(field => field.Handle is null), Is.True);
            Assert.That(descriptor.Fields.All(field => field.Categories == ExportCategory.None), Is.True);
        });
    }

    [TestCase(typeof(OmenNearsightProjectileDescriptor))]
    [TestCase(typeof(ReynaNearsightProjectileDescriptor))]
    [TestCase(typeof(HarborNearsightProjectileDescriptor))]
    [TestCase(typeof(SovaRevealProjectileDescriptor))]
    [TestCase(typeof(FadeRevealProjectileDescriptor))]
    public void SparseProjectileFieldsKeepTheirCategoriesAndAbsentOwnership(Type descriptorType)
    {
        var descriptor = (ExportGroupDescriptor)Activator.CreateInstance(descriptorType)!;
        var payload = descriptor.CreatePayloadInstance();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetType(), Is.EqualTo(descriptorType));
            Assert.That(descriptor.Categories, Is.EqualTo(ExportCategory.Ability | ExportCategory.Effects));
            Assert.That(descriptor.Fields.Select(field => (field.Handle, field.Categories)), Is.EquivalentTo(new[]
            {
                ((uint?)10, ExportCategory.Movement),
                ((uint?)11, ExportCategory.Ability),
                ((uint?)13, ExportCategory.Ability),
            }));
            Assert.That(descriptorType.GetProperty("Owner")!.GetValue(payload), Is.Null);
            Assert.That(descriptorType.GetProperty("Instigator")!.GetValue(payload), Is.Null);
            if (descriptorType.Name.Contains("Nearsight", StringComparison.Ordinal))
                Assert.That(payload, Is.InstanceOf<INearsightProjectilePayload>());
        });
    }
}
