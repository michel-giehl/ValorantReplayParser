using Replay.Models.Descriptors;
using Replay.Models.Replay;

namespace Replay.Models.Tests.Descriptors;

public class DescriptorCatalogTests
{
    [Test]
    public void AddExportGroup_AddsDescriptor()
    {
        var catalog = new DescriptorCatalog();

        catalog.Add(new TestExportGroupDescriptor());

        Assert.That(catalog.ExportGroupDescriptors, Has.Count.EqualTo(1));
        Assert.That(catalog.ExportGroupDescriptors[0].Path, Is.EqualTo("/Game/Test.Test_C"));
    }

    [Test]
    public void AddClassNetCache_AddsDescriptor()
    {
        var catalog = new DescriptorCatalog();

        catalog.Add(new TestClassNetCacheDescriptor());

        Assert.That(catalog.ClassNetCacheDescriptors, Has.Count.EqualTo(1));
        Assert.That(catalog.ClassNetCacheDescriptors[0].Path, Is.EqualTo("/Game/Test.Test_C_ClassNetCache"));
    }

    [Test]
    public void AddVersionedDescriptors_PreservesBaselineCatalogViewAndDefinitions()
    {
        var exportDefinition = new VersionedDefinition<TestExportGroupDescriptor>(new TestExportGroupDescriptor())
            .From(new ReplayReleaseVersion(13, 5), new TestExportGroupDescriptor());
        var cacheDefinition = new VersionedDefinition<TestClassNetCacheDescriptor>(new TestClassNetCacheDescriptor())
            .From(new ReplayReleaseVersion(13, 5), new TestClassNetCacheDescriptor());
        var catalog = new DescriptorCatalog();

        catalog.AddExportGroup(exportDefinition);
        catalog.AddClassNetCache(cacheDefinition);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.ExportGroupDescriptors, Has.Count.EqualTo(1));
            Assert.That(catalog.ClassNetCacheDescriptors, Has.Count.EqualTo(1));
            Assert.That(catalog.ExportGroupDefinitions.Single().HasVersionBoundaries, Is.True);
            Assert.That(catalog.ClassNetCacheDefinitions.Single().HasVersionBoundaries, Is.True);
        });
    }

    [Test]
    public void ExportGroupDescriptor_ConfigureBuildsFieldsOnce()
    {
        var descriptor = new TestExportGroupDescriptor();

        var fields = descriptor.Fields;

        Assert.Multiple(() =>
        {
            Assert.That(fields, Has.Count.EqualTo(3));
            Assert.That(fields[0].ExportName, Is.EqualTo("BaseValue"));
            Assert.That(fields[0].PropertyName, Is.EqualTo("BaseValue"));
            Assert.That(fields[1].Handle, Is.EqualTo(2));
            Assert.That(fields[1].PropertyName, Is.EqualTo("CurrentValue"));
            Assert.That(fields[2].ExportName, Is.EqualTo("bIsActive"));
            Assert.That(fields[2].PropertyName, Is.EqualTo("IsActive"));
        });
    }

    [Test]
    public void ExportGroupDescriptor_CreatePayloadInstance_ReturnsFreshTrackedDescriptor()
    {
        var descriptor = new TestExportGroupDescriptor();

        var payload = (TestExportGroupDescriptor)descriptor.CreatePayloadInstance();
        payload.BaseValue = 7;
        payload.MarkDecoded(nameof(TestExportGroupDescriptor.BaseValue));

        Assert.Multiple(() =>
        {
            Assert.That(payload, Is.Not.SameAs(descriptor));
            Assert.That(payload.BaseValue, Is.EqualTo(7));
            Assert.That(payload.HasDecoded(nameof(TestExportGroupDescriptor.BaseValue)), Is.True);
            Assert.That(descriptor.HasDecoded(nameof(TestExportGroupDescriptor.BaseValue)), Is.False);
        });
    }

    [Test]
    public void Clear_RemovesAllDescriptors()
    {
        var catalog = new DescriptorCatalog();
        catalog.Add(new TestExportGroupDescriptor());
        catalog.Add(new TestClassNetCacheDescriptor());
        catalog.AddSubobjectClassPath("Component", "/Script/Test.Component");

        catalog.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(catalog.ExportGroupDescriptors, Is.Empty);
            Assert.That(catalog.ClassNetCacheDescriptors, Is.Empty);
            Assert.That(catalog.SubobjectClassPaths, Is.Empty);
        });
    }

    private sealed class TestExportGroupDescriptor : ExportGroupDescriptor<TestExportGroupDescriptor>
    {
        public override string Path => "/Game/Test.Test_C";
        public override ExportCategory Categories => ExportCategory.Ability;
        public override ExportGroupKind Kind => ExportGroupKind.Actor;

        public float BaseValue { get; set; }
        public float CurrentValue { get; set; }
        public bool IsActive { get; set; }

        protected override void Configure()
        {
            AddProperty(nameof(BaseValue), x => x.BaseValue);
            AddPropertyHandle(2, x => x.CurrentValue);
            AddProperty("bIsActive", x => x.IsActive);
        }
    }

    private sealed class TestClassNetCacheDescriptor : ClassNetCacheDescriptor<TestClassNetCacheDescriptor>
    {
        public override string Path => "/Game/Test.Test_C_ClassNetCache";

        protected override void Configure()
        {
            AddFunction("SomeFunction", "/Game/Test.Test_C:SomeFunction", ExportCategory.Ability);
        }
    }
}
