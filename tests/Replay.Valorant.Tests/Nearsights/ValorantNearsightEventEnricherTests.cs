using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Valorant.Flashes.Descriptors;
using Replay.Valorant.GameState;
using Replay.Valorant.Nearsights;
using Replay.Valorant.Nearsights.Descriptors;

namespace Replay.Valorant.Tests.Nearsights;

public class ValorantNearsightEventEnricherTests
{
    [TestCase(412u)] // Cypher camera.
    [TestCase(798u)] // Gekko ult.
    [TestCase(1170u)] // Skye dog.
    [TestCase(1534u)] // Sova drone.
    [TestCase(1884u)] // Tejo drone.
    public void Emit_PossessedAbilityNearsight_OnlyCountsActualAgentAndItsInterval(uint device)
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantNearsightEventEnricher(sink, new NetGuidCache());
        var identity = new BombPlayerStateDescriptor
        {
            Subject = "target", SpawnedCharacter = 20, PossessedCharacter = device,
        };
        identity.MarkDecoded(nameof(identity.Subject));
        identity.MarkDecoded(nameof(identity.SpawnedCharacter));
        identity.MarkDecoded(nameof(identity.PossessedCharacter));
        enricher.Emit(Export(0, 1, 30, identity));
        enricher.Emit(Spawn(1, 100, NearsightPaths.OmenProjectile));
        enricher.Emit(Export(1, 2, 100, Projectile(20, owner: 60)));
        enricher.Emit(EffectStarted(1.1f, 3, device, 100, 11, 2));
        enricher.Emit(EffectStarted(1.2f, 4, 20, 100, 12, 2));
        var released = new BombPlayerStateDescriptor { PossessedCharacter = 20 };
        released.MarkDecoded(nameof(released.PossessedCharacter));
        enricher.Emit(Export(1.3f, 5, 30, released));
        enricher.Emit(EffectStarted(1.4f, 6, device, 100, 13, 2));
        enricher.Emit(EffectStopped(3.1f, 7, device, 11, 3.1f));
        enricher.Emit(EffectStopped(3.2f, 8, 20, 12, 3.2f));
        enricher.Emit(EffectStopped(3.4f, 9, device, 13, 3.4f));

        var hits = sink.Events.OfType<ValorantNearsightPlayerHit>().ToArray();
        var intervals = sink.Events.OfType<ValorantNearsightPlayerEffectEnded>().ToArray();
        Assert.That(hits, Has.Length.EqualTo(1));
        Assert.That(intervals, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(hits[0].TargetCharacterNetGuid, Is.EqualTo(20));
            Assert.That(hits[0].TargetSubject, Is.EqualTo("target"));
            Assert.That(intervals[0].TargetCharacterNetGuid, Is.EqualTo(20));
            Assert.That(intervals[0].ObservedDurationSeconds, Is.EqualTo(2).Within(0.0001f));
            Assert.That(sink.Events.OfType<ValorantNearsightCast>().Single().CasterSubject, Is.EqualTo("target"));
        });
    }


    [Test]
    public void Emit_OmenTargetEffect_EmitsCastPathHitAndObservedInterval()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantNearsightEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "caster");
        RegisterPlayer(enricher, 40, 50, "target");
        enricher.Emit(Spawn(1, 100, NearsightPaths.OmenProjectile));
        enricher.Emit(Export(1, 2, 100, Projectile(20, owner: 60)));
        enricher.Emit(EffectStarted(1.5f, 3, 40, 100, effectId: 70, duration: 2));
        enricher.Emit(EffectStopped(3.5f, 4, 40, effectId: 70, stopMovementTime: 3.5f));

        var cast = sink.Events.OfType<ValorantNearsightCast>().Single();
        var hit = sink.Events.OfType<ValorantNearsightPlayerHit>().Single();
        var ended = sink.Events.OfType<ValorantNearsightPlayerEffectEnded>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(cast.NearsightKind, Is.EqualTo(ValorantNearsightKind.OmenParanoia));
            Assert.That(cast.CasterCharacterNetGuid, Is.EqualTo(20));
            Assert.That(sink.Events.OfType<ValorantNearsightPathUpdated>().Count(), Is.EqualTo(2));
            Assert.That(sink.Events.OfType<ValorantNearsightActivated>().Single().Evidence,
                Is.EqualTo(ValorantNearsightActivationEvidence.ProjectileActiveFromRelease));
            Assert.That(hit.TargetCharacterNetGuid, Is.EqualTo(40));
            Assert.That(hit.ConfiguredDurationSeconds, Is.EqualTo(2));
            Assert.That(hit.DurationUntilRemoved, Is.False);
            Assert.That(hit.Correlation, Is.EqualTo(ValorantNearsightHitCorrelation.EffectContextProjectile));
            Assert.That(ended.ObservedDurationSeconds, Is.EqualTo(2));
        });
    }

    [Test]
    public void Emit_ReynaSourceWithRepeatedEffectIntervals_EmitsOneHitAndEveryInterval()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantNearsightEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "caster");
        RegisterPlayer(enricher, 40, 50, "target");
        enricher.Emit(Spawn(1, 100, NearsightPaths.ReynaProjectile));
        enricher.Emit(Export(1, 2, 100, ReynaProjectile(20, owner: 60)));
        enricher.Emit(new ActorClosed(1.5f, 3, 100, 1, ChannelCloseReason.Destroyed));
        enricher.Emit(Spawn(1.5f, 110, NearsightPaths.ReynaSource));

        enricher.Emit(EffectStarted(2, 4, 40, 110, effectId: 70, duration: -1));
        enricher.Emit(EffectStopped(2.25f, 5, 40, effectId: 70, stopMovementTime: 2.25f));
        enricher.Emit(EffectStarted(3, 6, 40, 110, effectId: 71, duration: -1));
        enricher.Emit(EffectStopped(3.1f, 7, 40, effectId: 71, stopMovementTime: 3.1f));

        var hit = sink.Events.OfType<ValorantNearsightPlayerHit>().Single();
        var intervals = sink.Events.OfType<ValorantNearsightPlayerEffectEnded>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(hit.NearsightKind, Is.EqualTo(ValorantNearsightKind.ReynaLeer));
            Assert.That(hit.ConfiguredDurationSeconds, Is.Null);
            Assert.That(hit.DurationUntilRemoved, Is.True);
            Assert.That(hit.Correlation, Is.EqualTo(ValorantNearsightHitCorrelation.EffectContextSource));
            Assert.That(intervals.Select(interval => interval.ObservedDurationSeconds),
                Is.EqualTo(new[] { 0.25f, 0.1f }).Within(0.0001f));
            Assert.That(sink.Events.OfType<ValorantNearsightActivated>().Single().SourceActorNetGuid,
                Is.EqualTo(110));
        });
    }

    [Test]
    public void Emit_HarborHeadAndSlowEffects_OnlyHeadEffectCountsAsNearsight()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantNearsightEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "caster");
        enricher.Emit(Spawn(1, 100, NearsightPaths.HarborProjectile));
        enricher.Emit(Export(1, 2, 100, HarborProjectile(20, owner: 60)));
        enricher.Emit(new ActorClosed(1.5f, 3, 100, 1, ChannelCloseReason.Destroyed));
        enricher.Emit(Spawn(1.5f, 110, NearsightPaths.HarborSource));

        enricher.Emit(EffectStarted(2, 4, 20, 60, effectId: 70, duration: 2, attachSocket: "Head"));
        enricher.Emit(EffectStarted(2, 5, 20, 60, effectId: 71, duration: 2, attachSocket: null));

        Assert.That(sink.Events.OfType<ValorantNearsightPlayerHit>().Count(), Is.EqualTo(1));
    }

    private static void RegisterPlayer(
        ValorantNearsightEventEnricher enricher,
        uint characterNetGuid,
        uint playerStateNetGuid,
        string subject)
    {
        var playerState = new BombPlayerStateDescriptor
        {
            SpawnedCharacter = characterNetGuid,
            PossessedCharacter = characterNetGuid,
            Subject = subject,
        };
        playerState.MarkDecoded(nameof(BombPlayerStateDescriptor.SpawnedCharacter));
        playerState.MarkDecoded(nameof(BombPlayerStateDescriptor.PossessedCharacter));
        playerState.MarkDecoded(nameof(BombPlayerStateDescriptor.Subject));
        enricher.Emit(Export(0, 1, playerStateNetGuid, playerState));
    }

    private static OmenNearsightProjectileDescriptor Projectile(uint instigator, uint owner)
    {
        var descriptor = new OmenNearsightProjectileDescriptor
        {
            Instigator = instigator,
            Owner = owner,
            ReplicatedMovement = Movement(),
        };
        MarkProjectileDecoded(descriptor);
        return descriptor;
    }

    private static ReynaNearsightProjectileDescriptor ReynaProjectile(uint instigator, uint owner)
    {
        var descriptor = new ReynaNearsightProjectileDescriptor
        {
            Instigator = instigator,
            Owner = owner,
            ReplicatedMovement = Movement(),
        };
        MarkProjectileDecoded(descriptor);
        return descriptor;
    }

    private static HarborNearsightProjectileDescriptor HarborProjectile(uint instigator, uint owner)
    {
        var descriptor = new HarborNearsightProjectileDescriptor
        {
            Instigator = instigator,
            Owner = owner,
            ReplicatedMovement = Movement(),
        };
        MarkProjectileDecoded(descriptor);
        return descriptor;
    }

    private static void MarkProjectileDecoded(ExportGroupDescriptor descriptor)
    {
        descriptor.MarkDecoded("Instigator");
        descriptor.MarkDecoded("Owner");
        descriptor.MarkDecoded("ReplicatedMovement");
    }

    private static FRepMovement Movement() =>
        new(new FVector(4, 5, 6), null, new FVector(1, 2, 3), null, false, false, 7, 0);

    private static RpcReceived EffectStarted(
        float time,
        int packetId,
        uint targetCharacterNetGuid,
        uint contextActorNetGuid,
        ulong effectId,
        float duration,
        string? attachSocket = "Head") =>
        Rpc(
            time,
            packetId,
            targetCharacterNetGuid,
            "MulticastPlayContinuousEffect",
            new EffectManagerPlayContinuousParameters
            {
                AttachSocket = attachSocket,
                EffectContainer = 500,
                EffectId = effectId,
                StartMovementTime = time,
                FunctionFloatValues =
                [
                    new EffectManagerFunctionFloatValue
                    {
                        Name = new FGameplayTag(1, "FXC.Buff.Duration"),
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

    private static RpcReceived EffectStopped(
        float time,
        int packetId,
        uint targetCharacterNetGuid,
        ulong effectId,
        float stopMovementTime) =>
        Rpc(
            time,
            packetId,
            targetCharacterNetGuid,
            "MulticastStopContinuousEffect",
            new EffectManagerStopContinuousParameters
            {
                EffectId = effectId,
                StopMovementTime = stopMovementTime,
            });

    private static RpcReceived Rpc(
        float time,
        int packetId,
        uint actorNetGuid,
        string functionName,
        object payload) =>
        new(
            time,
            packetId,
            actorNetGuid,
            actorNetGuid,
            1,
            "/Script/ShooterGame.EffectManagerComponent_ClassNetCache",
            functionName,
            "/Script/ShooterGame.EffectManagerComponent:" + functionName,
            0,
            ExportCategory.Effects,
            0,
            0,
            true,
            payload,
            0,
            []);

    private static ActorSpawned Spawn(float time, uint actorNetGuid, string path) =>
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

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
