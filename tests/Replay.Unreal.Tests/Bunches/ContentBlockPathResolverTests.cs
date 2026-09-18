using Replay.Encoding.Net;
using Replay.Models.Net;
using Replay.Unreal.Bunches;
using Replay.Unreal.Channels;

namespace Replay.Unreal.Tests.Bunches;

public class ContentBlockPathResolverTests
{
    [Test]
    public void ResolveClassPath_StableDamageHandlerComponent_ReturnsDamageableComponent()
    {
        var netGuidCache = new NetGuidCache();
        netGuidCache.SetNetGuidPath(17, "DamageHandlerComponent");
        var resolver = new ContentBlockPathResolver(netGuidCache);
        var header = new ContentBlockHeader { ObjectNetGuid = new NetworkGuid(17) };

        var classPath = resolver.ResolveClassPath(header, new ActorChannelState());

        Assert.That(classPath, Is.EqualTo("/Script/ShooterGame.DamageableComponent"));
    }

    [Test]
    public void ResolveClassPath_StableBlindManager_ReturnsBlindManagerComponent()
    {
        var netGuidCache = new NetGuidCache();
        netGuidCache.SetNetGuidPath(18, "BlindManagerComponent");
        var resolver = new ContentBlockPathResolver(netGuidCache);
        var header = new ContentBlockHeader { ObjectNetGuid = new NetworkGuid(18) };

        var classPath = resolver.ResolveClassPath(header, new ActorChannelState());
        var exportGroupPath = resolver.ResolveExportGroupPath(header, new ActorChannelState());

        Assert.That(classPath, Is.EqualTo("/Script/ShooterGame.BlindManagerComponent"));
        Assert.That(exportGroupPath, Is.EqualTo("/Script/ShooterGame.BlindManagerComponent"));
    }
}
