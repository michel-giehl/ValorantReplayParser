using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Effects.Replay;
using Replay.Valorant.Events.Combat;
using Replay.Valorant.Events.Flashes;
using Replay.Valorant.Events.Movement;
using Replay.Valorant.Events.Rounds;
using Replay.Valorant.Flashes.Descriptors;
using Replay.Valorant.Movement;

namespace Replay.Valorant.Events.Tests;

public class ValorantEventEnricherTests
{
    [Test]
    public void Emit_NativeRpcAndGuidExports_ProducesResolvedShotAndForwardsNativeEvent()
    {
        var sink = new CapturingSink();
        var events = new ValorantEventEnricher(sink);
        events.Emit(new NetGuidExported(1, -1, 500,
            "/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C", 0));
        events.Emit(new NetGuidExported(1, 2, 600, "PrimaryFiringState", 500));
        var rpc = Rpc(new ReplayPlayContinuousEffectAtLocationParameters
        {
            EffectId = 42,
            ObjectValues = [new EffectDataObject { Name = new FGameplayTag(0, "FiringState.FiringState"), Object = new NetObjectReference(600, null) }],
        });

        events.Emit(rpc);

        var shot = sink.Events.OfType<ValorantShotReceived>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(shot.Shot.Equippable?.Name, Is.EqualTo("Vandal"));
            Assert.That(shot.Shot.FireMode, Is.EqualTo(ValorantShotFireMode.Primary));
            Assert.That(shot.Shot.EffectId, Is.EqualTo(42));
            Assert.That(shot.TimeSeconds, Is.EqualTo(rpc.TimeSeconds));
            Assert.That(shot.PacketId, Is.EqualTo(rpc.PacketId));
            Assert.That(shot.ActorNetGuid, Is.EqualTo(rpc.ActorNetGuid));
            Assert.That(shot.ObjectNetGuid, Is.EqualTo(rpc.ObjectNetGuid));
            Assert.That(shot.ChannelIndex, Is.EqualTo(rpc.ChannelIndex));
            Assert.That(sink.Events.OfType<RpcReceived>().Single(), Is.SameAs(rpc));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Emit_MovementBatch_EmitsOnlyLatestMoveWhenCharacterIsKnown(bool missingCharacter)
    {
        // Native remote-character RPC with two moves (timestamps 42 and 84).
        const string payload = "BAQQBCoICBgASRMAACDEIhgBAABIhcAAAAAgACAAIBUAAAD0BwAABAgAAA4IAAAAAAAAAABEYAAAABAAEAAQFQAAABIEAAATBAAAFAQAAAAAAAAAAAgRGCUFHAAAAAA=";
        using var archive = new BitArchiveReader(Convert.FromBase64String(payload), 754);
        var decoder = (IRpcDecoder)ValorantDescriptors.CreateCatalog().ClassNetCacheDescriptors
            .Single(d => d.Path == "/Game/Characters/_Core/BaseReplayController.BaseReplayController_C_ClassNetCache")
            .FunctionFields.Single(f => f.Name == "ReplaysClientReceiveRemoteCharacterUpdatesSingleArrayNoAutonomous").Decoder!;
        var context = new FieldDecodeContext();
        var batch = (RemoteCharacterUpdateBatch)decoder.Decode(ref context, archive).Payload!;
        Assert.That(batch.MoveCount, Is.EqualTo(2));
        if (missingCharacter) batch.Updates[0].ShooterCharacterNetGuidValue = null;
        var sink = new CapturingSink();
        var events = new ValorantEventEnricher(sink);
        var rpc = Rpc(batch);

        events.Emit(rpc);

        var movements = sink.Events.OfType<RemoteCharacterMovementReceived>().ToArray();
        Assert.That(sink.Events.OfType<RpcReceived>().Single(), Is.SameAs(rpc));
        Assert.That(movements, Has.Length.EqualTo(missingCharacter ? 0 : 1));
        if (missingCharacter) return;
        var movement = movements.Single();
        Assert.Multiple(() =>
        {
            Assert.That(movement.ShooterCharacterNetGuidValue, Is.EqualTo(1234));
            Assert.That(movement.MoveIndex, Is.EqualTo(1));
            Assert.That(movement.Move.Timestamp, Is.EqualTo(84));
            Assert.That(movement.Move.Position.X, Is.EqualTo(10));
            Assert.That(movement.TimeSeconds, Is.EqualTo(rpc.TimeSeconds));
            Assert.That(movement.PacketId, Is.EqualTo(rpc.PacketId));
            Assert.That(movement.ActorNetGuid, Is.EqualTo(rpc.ActorNetGuid));
            Assert.That(movement.ObjectNetGuid, Is.EqualTo(rpc.ObjectNetGuid));
            Assert.That(movement.ChannelIndex, Is.EqualTo(rpc.ChannelIndex));
        });
    }

    [Test]
    public void Emit_UndecodedRpc_IsForwardedWithoutDerivedEvents()
    {
        var sink = new CapturingSink();
        var events = new ValorantEventEnricher(sink);
        var rpc = Rpc(new ReplayPlayContinuousEffectAtLocationParameters()) with { WasDecoded = false };
        events.Emit(rpc);
        Assert.That(sink.Events, Is.EqualTo(new[] { rpc }));
    }

    [Test]
    public void Complete_FlushesDeferredFlashOnceAndRejectsMoreInput()
    {
        var sink = new CapturingSink();
        var events = new ValorantEventEnricher(sink);
        var spawn = new ActorSpawned(1, 2, 100, 0, false, null, 0, null,
            FlashPaths.KayoOverhandProjectile, 0, null, null, null, null);
        events.Emit(spawn);
        events.Complete();
        var count = sink.Events.Count;
        events.Complete();

        Assert.Multiple(() =>
        {
            Assert.That(sink.Events.OfType<ValorantFlashCast>().Count(), Is.EqualTo(1));
            Assert.That(sink.Events, Has.Count.EqualTo(count));
            Assert.Throws<InvalidOperationException>(() => events.Emit(spawn));
        });
    }

    [TestCase("ClientResetRound", ValorantRoundPhase.BuyPhaseStarted)]
    [TestCase("ClientBuyPhaseEnd", ValorantRoundPhase.RoundLive)]
    [TestCase("MulticastEndRound", ValorantRoundPhase.RoundEnded)]
    public void Emit_RoundRpc_PrecedesTheNativeEventWithItsPhase(string function, ValorantRoundPhase expected)
    {
        var sink = new CapturingSink();
        var rpc = new RpcReceived(12.5f, 99, 100, 101, 7, "native class", function, "native export", 0,
            ExportCategory.All, 0, 0, false, null, 1, []);

        new ValorantEventEnricher(sink).Emit(rpc);

        Assert.That(sink.Events, Is.EqualTo(new ReplayEvent[] { new ValorantRoundPhaseChanged(12.5f, 99, 100, expected), rpc }));
    }

    private static RpcReceived Rpc(object payload) => new(
        12.5f, 99, 100, 101, 7, "native class", "native function", "native export", 0,
        ExportCategory.All, 0, 0, true, payload, 1, []);

    private sealed class CapturingSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];
        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
