using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Replay;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Tests.Combat;

public sealed class DamageRpcLayoutTests
{
    [Test]
    public void Release1310_PointDamageDecodesRecordedLifeChangesAndLaterFields()
    {
        // df095e3a-4bcf-49e8-a91b-00835f61e0dd, packet 16157, channel 62.
        const string hex =
            "04800000E085088000002C860C04200854002913600089617030A4BA51D059B77FEF0704480C8304040005C83190C59880800006C89A8A06100000000007100000BCB08740000011900C0612084C13080014880215080016A0FFFFBF7F17A0FFFFFF7F18A0000000001920000000001A01360270409E81742402F008F001020000FC01020B000000040014E26A32A8961E5688C0FEFF010001008D88921716C8A20202000000B00202000000C08200D08200E08200F0120000";
        var release = new ReplayReleaseVersion(13, 10);
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog(), releaseVersion: release);
        var group = registry.GetBoundCache("/Script/ShooterGame.DamageableComponent_ClassNetCache")!
            .FunctionsByHandle[1].FunctionGroup!;
        using var archive = new BitArchiveReader(Convert.FromHexString(hex), 1476);
        var context = new FieldDecodeContext { ReplayReleaseVersion = release };

        var result = new FieldPayloadParser().ParseRepLayoutProperties(archive, group, ref context);
        var damage = (MulticastNotifyDamagePointParameters)result.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(archive.AtEnd, Is.True);
            Assert.That(damage.DamageTaken, Is.EqualTo(120));
            Assert.That(damage.DamageDealt, Is.EqualTo(150));
            Assert.That(damage.DamageIcon?.NetGuid, Is.EqualTo(4195));
            Assert.That(damage.LifeChangeEvents, Has.Length.EqualTo(1));
            Assert.That(damage.LifeChangeEvents![0]!.ChangedComponent.NetGuid, Is.EqualTo(2741));
            Assert.That(damage.LifeChangeEvents[0]!.LifeResult, Is.Zero);
            Assert.That(damage.LifeChangeEvents[0]!.DeltaLife, Is.EqualTo(-120));
            Assert.That(damage.DamagerPlayerState.NetGuid, Is.EqualTo(76));
            Assert.That(damage.FalloffMultiplier, Is.EqualTo(1));
            Assert.That(damage.HasDecoded(nameof(damage.DeathMontageEffectOverrideIsQueued)), Is.True);
        });
    }

    [TestCase("/Script/ShooterGame.DamageableComponent_ClassNetCache", "MulticastNotifyDamage_Base", 0u, "KillsForKiller", 33u)]
    [TestCase("/Script/ShooterGame.DamageableComponent_ClassNetCache", "MulticastNotifyDamage_Point", 0u, "KillsForKiller", 41u)]
    [TestCase("/Game/GameModes/Bomb/BombGameState.BombGameState_C_ClassNetCache", "MulticastReceivePlayerTemporaryDeathEvent_Base", 2u, "RecoversInstantly", 30u)]
    [TestCase("/Game/GameModes/Bomb/BombGameState.BombGameState_C_ClassNetCache", "MulticastReceivePlayerTemporaryDeathEvent_Point", 2u, "RecoversInstantly", 38u)]
    public void Release1310_DamageResponseLayoutIsSelectedAtReleaseBoundary(
        string cachePath, string functionName, uint offset, string finalField, uint finalHandle)
    {
        var function = ValorantDescriptors.CreateCatalog().ClassNetCacheDescriptors
            .Single(cache => cache.Path == cachePath).FunctionFields.Single(rpc => rpc.Name == functionName);
        var previous = function.ParameterDescriptorDefinition!.Resolve(new ReplayReleaseVersion(13, 9));
        var current = function.ParameterDescriptorDefinition.Resolve(new ReplayReleaseVersion(13, 10));

        Assert.Multiple(() =>
        {
            Assert.That(previous.Fields.Any(field => field.PropertyName == "DamageIcon"), Is.False);
            Assert.That(Handle(current, "DamageIcon"), Is.EqualTo(9 + offset));
            Assert.That(Handle(previous, "LifeChangeEvents"), Is.EqualTo(9 + offset));
            Assert.That(Handle(current, "LifeChangeEvents"), Is.EqualTo(10 + offset));
            Assert.That(Handle(current, "KillCreditPlayerState"), Is.EqualTo(18 + offset));
            Assert.That(Handle(previous, finalField), Is.EqualTo(finalHandle - 1));
            Assert.That(Handle(current, finalField), Is.EqualTo(finalHandle));
        });
    }

    private static uint? Handle(ExportGroupDescriptor descriptor, string property) =>
        descriptor.Fields.Single(field => field.PropertyName == property).Handle;
}
