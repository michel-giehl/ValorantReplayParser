using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Bunches;
using Replay.Unreal.Channels;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Tests.Bunches;

public class ContentBlockPathResolverTests
{
    [TestCase("Component")]
    [TestCase("/Game/Test.Actor:Component")]
    public void ResolveStableSubobject_UsesCatalogMapping(string objectPath)
    {
        var catalog = new DescriptorCatalog();
        catalog.AddSubobjectClassPath("Component", "/Script/Test.ComponentClass");
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(17, objectPath);
        var resolver = new ContentBlockPathResolver(cache, new ExportBindingRegistry(catalog));
        var header = new ContentBlockHeader { ObjectNetGuid = new NetworkGuid(17) };

        Assert.That(resolver.ResolveClassPath(header, new ActorChannelState()),
            Is.EqualTo("/Script/Test.ComponentClass"));
        Assert.That(resolver.ResolveExportGroupPath(header, new ActorChannelState()),
            Is.EqualTo("/Script/Test.ComponentClass"));
    }

    [Test]
    public void ResolveStableSubobject_WithoutMapping_DoesNotInferClass()
    {
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(17, "DamageHandlerComponent");
        var resolver = new ContentBlockPathResolver(cache, new ExportBindingRegistry());
        var header = new ContentBlockHeader { ObjectNetGuid = new NetworkGuid(17) };

        Assert.That(resolver.ResolveClassPath(header, new ActorChannelState()), Is.Null);
        Assert.That(resolver.ResolveExportGroupPath(header, new ActorChannelState()), Is.Null);
    }

    [Test]
    public void ResolveStableSubobject_ClassGuidTakesPrecedenceOverMapping()
    {
        var catalog = new DescriptorCatalog();
        catalog.AddSubobjectClassPath("Component", "/Script/Test.Fallback");
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(17, "Component");
        cache.SetNetGuidPath(19, "/Script/Test.ActualClass");
        var resolver = new ContentBlockPathResolver(cache, new ExportBindingRegistry(catalog));
        var header = new ContentBlockHeader
        {
            ObjectNetGuid = new NetworkGuid(17),
            ClassNetGuid = new NetworkGuid(19),
        };

        Assert.That(resolver.ResolveClassPath(header, new ActorChannelState()),
            Is.EqualTo("/Script/Test.ActualClass"));
    }

    [Test]
    public void ResolveStableSubobject_ReplacingCatalogRemovesOldMapping()
    {
        var catalog = new DescriptorCatalog();
        catalog.AddSubobjectClassPath("Component", "/Script/Test.ComponentClass");
        var registry = new ExportBindingRegistry(catalog);
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(17, "Component");
        var resolver = new ContentBlockPathResolver(cache, registry);
        var header = new ContentBlockHeader { ObjectNetGuid = new NetworkGuid(17) };
        Assert.That(resolver.ResolveClassPath(header, new ActorChannelState()), Is.Not.Null);

        registry.SetCatalog(new DescriptorCatalog());

        Assert.That(resolver.ResolveClassPath(header, new ActorChannelState()), Is.Null);
    }

    [Test]
    public void ResolveExportGroupPath_UsesCatalogAliasProviderAndInvalidatesCache()
    {
        var catalog = new DescriptorCatalog
        {
            PathAliasProvider = new TestPathAliasProvider(),
        };
        var registry = new ExportBindingRegistry(catalog);
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(19, "/Package/Actor.Test_C");
        cache.AddExportGroup(new NetFieldExportGroup
        {
            PathName = "/Package/_Variant/Actor.Test_C",
            PathNameIndex = 1,
            NetFieldExports = [],
        });
        var resolver = new ContentBlockPathResolver(cache, registry);
        var header = new ContentBlockHeader
        {
            ObjectNetGuid = new NetworkGuid(17),
            ClassNetGuid = new NetworkGuid(19),
        };

        Assert.That(resolver.ResolveExportGroupPath(header, new ActorChannelState()),
            Is.EqualTo("/Package/_Variant/Actor.Test_C"));

        registry.SetCatalog(new DescriptorCatalog());

        Assert.That(resolver.ResolveExportGroupPath(header, new ActorChannelState()), Is.Null);
    }

    private sealed class TestPathAliasProvider : IReplayPathAliasProvider
    {
        public string? GetAlternatePath(string path) => path == "/Package/Actor.Test_C"
            ? "/Package/_Variant/Actor.Test_C"
            : null;
    }
}
