using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Valorant.Flashes;
using Replay.Valorant.Flashes.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.Flashes;

public class ValorantFlashEventEnricherTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Emit_BreachExitResult_SurvivesLaterMovementAndStopOrClose(bool closeWithoutStop)
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        enricher.Emit(SpawnFlash(1, 100, FlashPaths.BreachProjectile));
        var exit = new FVector(1481, -8750, 450) { ScaleFactor = 1 };
        var initial = new BreachFlashProjectileDescriptor { ExitLocation = exit };
        initial.MarkDecoded(nameof(initial.ExitLocation));
        enricher.Emit(Export(1, 1, 100, initial));
        var movement = new BreachFlashProjectileDescriptor
        {
            ReplicatedMovement = Movement(new FVector(14.90, -87.91, 4.51) { ScaleFactor = 100 }),
        };
        movement.MarkDecoded(nameof(movement.ReplicatedMovement));
        enricher.Emit(Export(1.1f, 2, 100, movement));
        if (!closeWithoutStop)
            enricher.Emit(StopRpc(1.2f, 3, 100));
        enricher.Emit(new ActorClosed(1.3f, 4, 100, 1, ChannelCloseReason.Destroyed));
        enricher.Complete();

        var explosion = sink.Events.OfType<ValorantFlashExploded>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(explosion.Location, Is.EqualTo(exit));
            Assert.That(explosion.Evidence, Is.EqualTo(closeWithoutStop
                ? ValorantFlashExplosionEvidence.ActorDestroyedFallback
                : ValorantFlashExplosionEvidence.StopProjectileRpc));
            Assert.That(sink.Events.OfType<ValorantFlashCast>().Single().Location,
                Is.EqualTo(new FVector(0, 0, 0)));
            Assert.That(sink.Events.OfType<ValorantFlashPathUpdated>().Last().Location,
                Is.EqualTo(movement.ReplicatedMovement!.Value.Location));
        });
    }

    [Test]
    public void Emit_BreachWithoutExitResult_PreservesMovementFallback()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        enricher.Emit(SpawnFlash(1, 100, FlashPaths.BreachProjectile));
        var location = new FVector(10, 20, 30);
        var movement = new BreachFlashProjectileDescriptor { ReplicatedMovement = Movement(location) };
        movement.MarkDecoded(nameof(movement.ReplicatedMovement));
        enricher.Emit(Export(1.1f, 2, 100, movement));
        enricher.Emit(StopRpc(1.2f, 3, 100));
        Assert.That(sink.Events.OfType<ValorantFlashExploded>().Single().Location, Is.EqualTo(location));
    }

    [Test]
    public void Emit_ProjectileMovementAndStopRpc_EmitsOrderedLifecycleExactlyOnce()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, characterNetGuid: 20, playerStateNetGuid: 30, "subject-1");
        enricher.Emit(SpawnFlash(1, 100, FlashPaths.KayoOverhandProjectile));

        enricher.Emit(Export(1.01f, 20, 100, Projectile(
            instigator: 20,
            new FRepMovement(
                new FVector(4, 5, 6),
                new FVector(7, 8, 9),
                new FVector(1, 2, 3),
                new FRotator(10, 20, 30),
                false,
                false,
                41,
                0))));
        enricher.Emit(Export(1.1f, 21, 100, Projectile(
            instigator: null,
            new FRepMovement(
                new FVector(14, 15, 16),
                null,
                new FVector(11, 12, 13),
                null,
                false,
                false,
                42,
                0))));
        enricher.Emit(StopRpc(1.2f, 22, 100));
        enricher.Emit(StopRpc(1.21f, 23, 100));

        var cast = sink.Events.OfType<ValorantFlashCast>().Single();
        var paths = sink.Events.OfType<ValorantFlashPathUpdated>().ToArray();
        var explosion = sink.Events.OfType<ValorantFlashExploded>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(cast.TimeSeconds, Is.EqualTo(1));
            Assert.That(cast.FlashKind, Is.EqualTo(ValorantFlashKind.KayoFlashDriveOverhand));
            Assert.That(cast.CasterCharacterNetGuid, Is.EqualTo(20));
            Assert.That(cast.CasterPlayerStateNetGuid, Is.EqualTo(30));
            Assert.That(cast.CasterSubject, Is.EqualTo("subject-1"));
            Assert.That(paths.Select(path => path.SampleIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(paths[0].Source, Is.EqualTo(ValorantFlashPathSampleSource.SpawnTransform));
            Assert.That(paths[2].ServerFrame, Is.EqualTo(42));
            Assert.That(explosion.Evidence, Is.EqualTo(ValorantFlashExplosionEvidence.StopProjectileRpc));
            Assert.That(explosion.Location, Is.EqualTo(new FVector(11, 12, 13)));
        });
    }

    [Test]
    public void Emit_SkyeSourceAndRepeatedActiveBlind_UsesAuthoritativeDurationOnce()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "subject-1");
        enricher.Emit(SpawnFlash(1, 100, FlashPaths.SkyeProjectile));
        enricher.Emit(Export(1.01f, 2, 100, Projectile(20, Movement(new FVector(1, 2, 3)))));
        enricher.Emit(new ActorClosed(2, 3, 100, 1, ChannelCloseReason.Destroyed));
        enricher.Emit(SpawnFlash(2.01f, 200, FlashPaths.SkyeFlashSource));
        enricher.Emit(Rpc(
            2.02f,
            4,
            200,
            "Multicast Set Flash Duration",
            new SkyeSetFlashDurationParameters { MaxFlashDuration = 1.75 }));
        enricher.Emit(EffectRpc(2.025f, 5, 20, 200, 9.9f, 98));

        var blind = new ActiveBlindDescriptor
        {
            BlindId = 7,
            EffectId = 99,
            InitialDuration = 1.25f,
            StartNetMovementTime = 2.02f,
            BlindConfig = 300,
            CausingActor = 200,
        };
        var manager = new BlindManagerComponentDescriptor { ActiveBlinds = [blind] };
        manager.MarkDecoded(nameof(BlindManagerComponentDescriptor.ActiveBlinds));
        enricher.Emit(Export(2.03f, 6, 20, manager));
        enricher.Emit(Export(2.04f, 7, 20, manager));
        enricher.Complete();

        var explosion = sink.Events.OfType<ValorantFlashExploded>().Single();
        var hit = sink.Events.OfType<ValorantFlashPlayerHit>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(explosion.Evidence, Is.EqualTo(ValorantFlashExplosionEvidence.SkyeFlashSource));
            Assert.That(explosion.MaxFlashDurationSeconds, Is.EqualTo(1.75));
            Assert.That(hit.FlashActorNetGuid, Is.EqualTo(100));
            Assert.That(hit.TargetCharacterNetGuid, Is.EqualTo(20));
            Assert.That(hit.TargetPlayerStateNetGuid, Is.EqualTo(30));
            Assert.That(hit.TargetSubject, Is.EqualTo("subject-1"));
            Assert.That(hit.InitialDurationSeconds, Is.EqualTo(1.25f));
            Assert.That(hit.Correlation, Is.EqualTo(ValorantFlashHitCorrelation.CausingFlashSource));
            Assert.That(hit.DurationSource, Is.EqualTo(ValorantFlashDurationSource.BlindManagerInitialDuration));
        });
    }

    [Test]
    public void Emit_YoruProjectileAndStopRpc_EmitsYoruLifecycle()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "subject-1");
        enricher.Emit(SpawnFlash(1, 100, FlashPaths.YoruProjectile));
        enricher.Emit(Export(1.01f, 2, 100, YoruProjectile(20, Movement(new FVector(4, 5, 6)))));
        enricher.Emit(StopRpc(1.2f, 3, 100));

        var cast = sink.Events.OfType<ValorantFlashCast>().Single();
        var explosion = sink.Events.OfType<ValorantFlashExploded>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(cast.FlashKind, Is.EqualTo(ValorantFlashKind.YoruBlindside));
            Assert.That(cast.CasterCharacterNetGuid, Is.EqualTo(20));
            Assert.That(explosion.FlashKind, Is.EqualTo(ValorantFlashKind.YoruBlindside));
            Assert.That(explosion.Evidence, Is.EqualTo(ValorantFlashExplosionEvidence.StopProjectileRpc));
        });
    }

    [Test]
    public void Emit_VyseSourceAndRepeatedBlinds_EmitsReusableExplosions()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "subject-1");
        enricher.Emit(SpawnFlash(1, 100, FlashPaths.VyseFlashTrap));
        enricher.Emit(Export(1.01f, 2, 100, new VyseFlashSourceDescriptor
        {
            Instigator = 20,
        }));

        var first = BlindManager(1, 101, 300);
        first.ActiveBlinds![0].CausingActor = 100;
        first.ActiveBlinds![0].InitialDuration = 1.2f;
        enricher.Emit(Export(2, 3, 20, first));

        var second = BlindManager(2, 102, 300);
        second.ActiveBlinds![0].CausingActor = 100;
        second.ActiveBlinds![0].InitialDuration = 0.4f;
        enricher.Emit(Export(3, 4, 20, second));

        var explosions = sink.Events.OfType<ValorantFlashExploded>().ToArray();
        var hits = sink.Events.OfType<ValorantFlashPlayerHit>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(sink.Events.OfType<ValorantFlashCast>().ToArray(), Has.Length.EqualTo(1));
            Assert.That(explosions, Has.Length.EqualTo(2));
            Assert.That(explosions.All(explosion =>
                explosion.FlashActorNetGuid == 100 &&
                explosion.Evidence == ValorantFlashExplosionEvidence.VyseFlashSource), Is.True);
            Assert.That(hits.Select(hit => hit.InitialDurationSeconds), Is.EqualTo(new float?[] { 1.2f, 0.4f }));
            Assert.That(hits.All(hit => hit.FlashKind == ValorantFlashKind.VyseArcRose), Is.True);
        });
    }

    [Test]
    public void Emit_BlindWithoutDurationOrSource_RemainsExplicitlyUnresolved()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "subject-1");
        var manager = new BlindManagerComponentDescriptor
        {
            ActiveBlinds = [new ActiveBlindDescriptor { BlindId = 8, EffectId = 100 }],
        };
        manager.MarkDecoded(nameof(BlindManagerComponentDescriptor.ActiveBlinds));

        enricher.Emit(Export(3, 10, 20, manager));

        var hit = sink.Events.OfType<ValorantFlashPlayerHit>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(hit.FlashActorNetGuid, Is.Null);
            Assert.That(hit.FlashKind, Is.Null);
            Assert.That(hit.InitialDurationSeconds, Is.Null);
            Assert.That(hit.Correlation, Is.EqualTo(ValorantFlashHitCorrelation.Unresolved));
            Assert.That(hit.DurationSource, Is.EqualTo(ValorantFlashDurationSource.Missing));
        });
    }

    [Test]
    public void Complete_EffectTagAfterExplosionAndResolvedContext_EmitsFallbackOnly()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "subject-1");
        enricher.Emit(SpawnFlash(1, 100, FlashPaths.BreachProjectile));
        enricher.Emit(Export(1.01f, 2, 100, Projectile(20, Movement(new FVector(1, 2, 3)))));

        enricher.Emit(EffectRpc(1.5f, 3, 20, 100, 0.7f, 50));
        enricher.Emit(StopRpc(2, 4, 100));
        enricher.Emit(EffectRpc(2.1f, 5, 20, 999, 0.8f, 51));
        enricher.Emit(EffectRpc(2.2f, 6, 20, 100, 1.4f, 52));
        enricher.Complete();

        var hit = sink.Events.OfType<ValorantFlashPlayerHit>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(hit.FlashActorNetGuid, Is.EqualTo(100));
            Assert.That(hit.InitialDurationSeconds, Is.EqualTo(1.4f));
            Assert.That(hit.BlindId, Is.Null);
            Assert.That(hit.EffectId, Is.EqualTo(52));
            Assert.That(hit.CausingActorNetGuid, Is.EqualTo(100));
            Assert.That(hit.DurationSource, Is.EqualTo(ValorantFlashDurationSource.EffectDataGameplayTag));
        });
    }

    [Test]
    public void Complete_ClosedProjectileWithoutStopRpc_UsesDestroyedFallback()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFlashEventEnricher(sink, new NetGuidCache());
        enricher.Emit(SpawnFlash(1, 100, FlashPaths.PhoenixRightProjectile));
        enricher.Emit(new ActorClosed(2, 3, 100, 1, ChannelCloseReason.Destroyed));

        enricher.Complete();

        Assert.That(sink.Events.OfType<ValorantFlashExploded>().Single().Evidence,
            Is.EqualTo(ValorantFlashExplosionEvidence.ActorDestroyedFallback));
    }

    [Test]
    public void Emit_BlindConfigFallback_RequiresOneRecentMatchingExplosion()
    {
        var sink = new CapturingReplayEventSink();
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(900, "BlindConfig_Grenadier_Flash_C");
        var enricher = new ValorantFlashEventEnricher(sink, cache);
        RegisterPlayer(enricher, 20, 30, "subject-1");
        RegisterPlayer(enricher, 21, 31, "subject-2");

        SpawnAndExplodeKayo(enricher, 1, 100);
        enricher.Emit(Export(1.1f, 10, 20, BlindManager(1, 101, 900)));

        SpawnAndExplodeKayo(enricher, 2, 200);
        SpawnAndExplodeKayo(enricher, 2.02f, 201);
        enricher.Emit(Export(2.1f, 20, 21, BlindManager(2, 102, 900)));

        var hits = sink.Events.OfType<ValorantFlashPlayerHit>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(hits, Has.Length.EqualTo(2));
            Assert.That(hits[0].FlashActorNetGuid, Is.EqualTo(100));
            Assert.That(hits[0].Correlation,
                Is.EqualTo(ValorantFlashHitCorrelation.BlindConfigAndUniqueExplosion));
            Assert.That(hits[1].FlashActorNetGuid, Is.Null);
            Assert.That(hits[1].FlashKind, Is.EqualTo(ValorantFlashKind.KayoFlashDriveOverhand));
            Assert.That(hits[1].Correlation, Is.EqualTo(ValorantFlashHitCorrelation.Unresolved));
        });
    }

    private static void RegisterPlayer(
        ValorantFlashEventEnricher enricher,
        uint characterNetGuid,
        uint playerStateNetGuid,
        string subject)
    {
        var playerState = new BombPlayerStateDescriptor
        {
            PossessedCharacter = characterNetGuid,
            Subject = subject,
        };
        playerState.MarkDecoded(nameof(BombPlayerStateDescriptor.PossessedCharacter));
        playerState.MarkDecoded(nameof(BombPlayerStateDescriptor.Subject));
        enricher.Emit(Export(0, 1, playerStateNetGuid, playerState));
    }

    private static void SpawnAndExplodeKayo(
        ValorantFlashEventEnricher enricher,
        float time,
        uint actorNetGuid)
    {
        enricher.Emit(SpawnFlash(time, actorNetGuid, FlashPaths.KayoOverhandProjectile));
        enricher.Emit(Export(time, 1, actorNetGuid, Projectile(20, Movement(new FVector(1, 2, 3)))));
        enricher.Emit(StopRpc(time, 2, actorNetGuid));
    }

    private static BlindManagerComponentDescriptor BlindManager(
        uint blindId,
        ulong effectId,
        uint blindConfig)
    {
        var manager = new BlindManagerComponentDescriptor
        {
            ActiveBlinds =
            [
                new ActiveBlindDescriptor
                {
                    BlindId = blindId,
                    EffectId = effectId,
                    InitialDuration = 1,
                    BlindConfig = blindConfig,
                },
            ],
        };
        manager.MarkDecoded(nameof(BlindManagerComponentDescriptor.ActiveBlinds));
        return manager;
    }

    private static KayoOverhandFlashProjectileDescriptor Projectile(
        uint? instigator,
        FRepMovement movement)
    {
        var projectile = new KayoOverhandFlashProjectileDescriptor
        {
            Instigator = instigator,
            ReplicatedMovement = movement,
        };
        if (instigator is not null)
        {
            projectile.MarkDecoded(nameof(projectile.Instigator));
        }

        projectile.MarkDecoded(nameof(projectile.ReplicatedMovement));
        return projectile;
    }

    private static YoruFlashProjectileDescriptor YoruProjectile(
        uint? instigator,
        FRepMovement movement)
    {
        var projectile = new YoruFlashProjectileDescriptor
        {
            Instigator = instigator,
            ReplicatedMovement = movement,
        };
        if (instigator is not null)
        {
            projectile.MarkDecoded(nameof(projectile.Instigator));
        }

        projectile.MarkDecoded(nameof(projectile.ReplicatedMovement));
        return projectile;
    }

    private static FRepMovement Movement(FVector location) =>
        new(new FVector(1, 2, 3), null, location, null, false, false, 1, 0);

    private static ActorSpawned SpawnFlash(float time, uint actorNetGuid, string path) =>
        new(
            time,
            1,
            actorNetGuid,
            1,
            true,
            null,
            0,
            "Default__" + path[(path.LastIndexOf('.') + 1)..],
            path,
            0,
            new FVector(0, 0, 0),
            new FRotator(0, 0, 0),
            null,
            new FVector(1, 0, 0));

    private static ExportGroupReceived Export(
        float time,
        int packetId,
        uint actorNetGuid,
        ExportGroupDescriptor payload) =>
        new(
            time,
            packetId,
            actorNetGuid,
            actorNetGuid,
            1,
            true,
            false,
            0,
            payload.Path,
            payload.Kind,
            payload.Categories,
            0,
            0,
            null,
            null,
            null,
            0,
            0,
            true,
            payload,
            0,
            []);

    private static RpcReceived StopRpc(float time, int packetId, uint actorNetGuid) =>
        Rpc(time, packetId, actorNetGuid, "MulticastStopProjectile", null);

    private static RpcReceived EffectRpc(
        float time,
        int packetId,
        uint targetCharacterNetGuid,
        uint contextActorNetGuid,
        float duration,
        ulong effectId) =>
        Rpc(
            time,
            packetId,
            targetCharacterNetGuid,
            "MulticastPlayContinuousEffect",
            new EffectManagerPlayContinuousParameters
            {
                EffectId = effectId,
                StartMovementTime = time,
                FunctionFloatValues =
                [
                    new EffectManagerFunctionFloatValue
                    {
                        Name = new FGameplayTag(1, "FXC.TimedStateDuration"),
                        Value = duration,
                    },
                ],
                FunctionObjectValues =
                [
                    new EffectManagerFunctionObjectValue
                    {
                        Name = new FGameplayTag(2, "FXC.EffectContext"),
                        Value = contextActorNetGuid,
                    },
                ],
            });

    private static RpcReceived Rpc(
        float time,
        int packetId,
        uint actorNetGuid,
        string functionName,
        object? payload) =>
        new(
            time,
            packetId,
            actorNetGuid,
            actorNetGuid,
            1,
            "/Game/Test_ClassNetCache",
            functionName,
            "/Game/Test:" + functionName,
            0,
            ExportCategory.Ability | ExportCategory.Effects,
            0,
            0,
            true,
            payload,
            0,
            []);

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
