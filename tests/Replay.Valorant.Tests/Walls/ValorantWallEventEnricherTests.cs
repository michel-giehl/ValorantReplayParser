using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Valorant.GameState;
using Replay.Valorant.Walls;
using Replay.Valorant.Walls.Descriptors;

namespace Replay.Valorant.Tests.Walls;

public class ValorantWallEventEnricherTests
{
    [Test]
    public void Emit_SageWallAndSegment_EmitsPlacementAndExactlyOnceDestruction()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantWallEventEnricher(sink);
        RegisterPlayer(enricher, 20, 30, "sage");
        enricher.Emit(Spawn(1, 100, WallPaths.SageWall, new FVector(1, 2, 3)));
        enricher.Emit(Export(1, 2, 100, Owned(new SageWallDescriptor(), 60, 20)));
        enricher.Emit(Spawn(1, 110, WallPaths.SageSegment, new FVector(4, 5, 6)));
        enricher.Emit(Export(1, 3, 110, Owned(new SageWallSegmentDescriptor(), 100, 20)));
        enricher.Emit(Rpc(2, 4, 110, "DisableCollision", WallPaths.SageSegment + ":DisableCollision", null));
        enricher.Emit(Rpc(2.1f, 5, 110, "DisableCollision", WallPaths.SageSegment + ":DisableCollision", null));
        enricher.Emit(new ActorClosed(2.2f, 6, 110, 1, ChannelCloseReason.Destroyed));
        enricher.Emit(new ActorClosed(3, 7, 100, 1, ChannelCloseReason.Destroyed));

        var placed = sink.Events.OfType<ValorantWallPlaced>().Single();
        var segment = sink.Events.OfType<ValorantWallSegmentSpawned>().Single();
        var segmentDestroyed = sink.Events.OfType<ValorantWallSegmentDestroyed>().Single();
        var destroyed = sink.Events.OfType<ValorantWallDestroyed>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(placed.WallKind, Is.EqualTo(ValorantWallKind.SageBarrierOrb));
            Assert.That(placed.CasterCharacterNetGuid, Is.EqualTo(20));
            Assert.That(placed.CasterSubject, Is.EqualTo("sage"));
            Assert.That(segment.WallActorNetGuid, Is.EqualTo(100));
            Assert.That(segment.SegmentIndex, Is.Zero);
            Assert.That(segmentDestroyed.Evidence,
                Is.EqualTo(ValorantWallSegmentDestructionEvidence.DisableCollisionRpc));
            Assert.That(segmentDestroyed.LifetimeSeconds, Is.EqualTo(1));
            Assert.That(destroyed.Evidence, Is.EqualTo(ValorantWallDestructionEvidence.SageRootActorDestroyed));
            Assert.That(destroyed.LifetimeSeconds, Is.EqualTo(2));
        });
    }

    [Test]
    public void Emit_VyseTrapActivation_ConnectsPlacementActiveWallAndTrigger()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantWallEventEnricher(sink);
        RegisterPlayer(enricher, 20, 30, "vyse");
        RegisterPlayer(enricher, 40, 50, "trigger");
        enricher.Emit(Spawn(1, 100, WallPaths.VyseTrap, new FVector(1, 2, 3)));
        enricher.Emit(Export(1, 2, 100, Owned(new VyseWallTrapDescriptor(), 60, 20)));
        enricher.Emit(Rpc(1, 3, 100, "MulticastInitializeTrapAnchors",
            WallPaths.VyseTrap + ":MulticastInitializeTrapAnchors", TrapAnchors()));
        enricher.Emit(Spawn(4, 110, WallPaths.VyseWall, new FVector(1, 2, 0)));
        enricher.Emit(Export(4, 4, 110, Owned(new VyseWallDescriptor(), 100, 20)));
        enricher.Emit(Rpc(4, 5, 110, "MulticastInitializeWall",
            WallPaths.VyseWall + ":MulticastInitializeWall", ActiveWall(40)));
        enricher.Emit(Rpc(4.4f, 6, 110, "MulticastEnableDynamicCollision",
            WallPaths.VyseWall + ":MulticastEnableDynamicCollision", null));
        enricher.Emit(new ActorClosed(4.4f, 7, 100, 1, ChannelCloseReason.Destroyed));
        enricher.Emit(new ActorClosed(10.4f, 8, 110, 1, ChannelCloseReason.Destroyed));

        var placed = sink.Events.OfType<ValorantWallPlaced>().Single();
        var activated = sink.Events.OfType<ValorantWallActivated>().Single();
        var destroyed = sink.Events.OfType<ValorantWallDestroyed>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(placed.WallActorNetGuid, Is.EqualTo(100));
            Assert.That(placed.WallStart, Is.EqualTo(new FVector(1, 2, 0)));
            Assert.That(activated.WallActorNetGuid, Is.EqualTo(100));
            Assert.That(activated.ActiveWallActorNetGuid, Is.EqualTo(110));
            Assert.That(activated.TriggerCharacterNetGuid, Is.EqualTo(40));
            Assert.That(activated.TriggerSubject, Is.EqualTo("trigger"));
            Assert.That(destroyed.Evidence,
                Is.EqualTo(ValorantWallDestructionEvidence.VyseActiveWallActorDestroyed));
            Assert.That(destroyed.ActiveDurationSeconds, Is.EqualTo(6f).Within(0.0001f));
        });
    }

    private static T Owned<T>(T descriptor, uint owner, uint instigator)
        where T : WallOwnedActorDescriptor<T>
    {
        descriptor.Owner = owner;
        descriptor.Instigator = instigator;
        descriptor.MarkDecoded(nameof(descriptor.Owner));
        descriptor.MarkDecoded(nameof(descriptor.Instigator));
        return descriptor;
    }

    private static VyseInitializeTrapAnchorsParameters TrapAnchors() => new()
    {
        WallStartPoint = new FVector(1, 2, 0),
        WallEndPoint = new FVector(5, 2, 0),
        ImpactPoint = new FVector(1, 2, 3),
        ImpactNormal = new FVector(1, 0, 0),
    };

    private static VyseInitializeWallParameters ActiveWall(uint trigger) => new()
    {
        WallStartLocation = new FVector(1, 2, 0),
        WallEndLocation = new FVector(5, 2, 0),
        WallImpactNormal = new FVector(1, 0, 0),
        EnemyTrigger = trigger,
    };

    private static void RegisterPlayer(
        ValorantWallEventEnricher enricher,
        uint characterNetGuid,
        uint playerStateNetGuid,
        string subject)
    {
        var descriptor = new BombPlayerStateDescriptor
        {
            PossessedCharacter = characterNetGuid,
            Subject = subject,
        };
        descriptor.MarkDecoded(nameof(BombPlayerStateDescriptor.PossessedCharacter));
        descriptor.MarkDecoded(nameof(BombPlayerStateDescriptor.Subject));
        enricher.Emit(Export(0, 1, playerStateNetGuid, descriptor));
    }

    private static ActorSpawned Spawn(float time, uint actorNetGuid, string path, FVector location) =>
        new(time, 1, actorNetGuid, 1, true, null, 0,
            "Default__" + path[(path.LastIndexOf('.') + 1)..], path, 0, location,
            new FRotator(0, 90, 0), null, new FVector(0, 0, 0));

    private static ExportGroupReceived Export(
        float time,
        int packetId,
        uint actorNetGuid,
        ExportGroupDescriptor payload) =>
        new(time, packetId, actorNetGuid, actorNetGuid, 1, true, false, 0, payload.Path, payload.Kind,
            payload.Categories, 0, 0, null, null, null, 0, 0, true, payload, 0, []);

    private static RpcReceived Rpc(
        float time,
        int packetId,
        uint actorNetGuid,
        string functionName,
        string functionPath,
        object? payload) =>
        new(time, packetId, actorNetGuid, actorNetGuid, 1, functionPath, functionName, functionPath, 0,
            ExportCategory.Ability, 0, 0, true, payload, 0, []);

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];
        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
