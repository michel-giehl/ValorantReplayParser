using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Unreal;
using Replay.Valorant.Descriptors.Effects.Locational;
using Replay.Valorant.Events.Footsteps;
using Replay.Valorant.Flashes.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Events.Tests.Footsteps;

public class ValorantFootstepEventEnricherTests
{
    [Test]
    public void Emit_FootstepEffect_EmitsStepWithCharacterIdentityBeforeNativeRpc()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFootstepEventEnricher(sink, new NetGuidCache());
        RegisterPlayer(enricher, 20, 30, "jett");
        var rpc = Rpc(1.5f, 4, Effect("FXC_Footstep", 20, material: 5, intensity: 1));
        enricher.Emit(rpc);

        var step = sink.Events.OfType<ValorantFootstepReceived>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(sink.Events[^2], Is.SameAs(step));
            Assert.That(sink.Events[^1], Is.SameAs(rpc));
            Assert.That(step.Kind, Is.EqualTo(ValorantFootstepKind.Footstep));
            Assert.That(step.TimeSeconds, Is.EqualTo(1.5f));
            Assert.That(step.CharacterNetGuid, Is.EqualTo(20));
            Assert.That(step.PlayerStateNetGuid, Is.EqualTo(30));
            Assert.That(step.Subject, Is.EqualTo("jett"));
            Assert.That(step.Location, Is.EqualTo(new FVector(1, 2, 3)));
            Assert.That(step.Rotation, Is.EqualTo(new FRotator(0, 90, 0)));
            Assert.That(step.Surface, Is.EqualTo(5f));
            Assert.That(step.Intensity, Is.EqualTo(1f));
            Assert.That(step.StartMovementTime, Is.EqualTo(12.5f));
            Assert.That(step.EffectContainer!.Value.NetGuid, Is.EqualTo(99));
        });
    }

    [TestCase("FXC_JumpLaunch", ValorantFootstepKind.JumpLaunch)]
    [TestCase("FXC_JumpLand", ValorantFootstepKind.JumpLand)]
    public void Emit_JumpEffects_MapToTheirKinds(string container, ValorantFootstepKind kind)
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFootstepEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Rpc(2, 5, Effect(container, 20, material: 1, intensity: 1)));

        var step = sink.Events.OfType<ValorantFootstepReceived>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(step.Kind, Is.EqualTo(kind));
            Assert.That(step.CharacterNetGuid, Is.EqualTo(20));
            Assert.That(step.PlayerStateNetGuid, Is.Null);
            Assert.That(step.Subject, Is.Null);
        });
    }

    [Test]
    public void Emit_OtherLocationalEffectsAndUndecodedRpcs_OnlyForwardTheNativeEvent()
    {
        var sink = new CapturingReplayEventSink();
        var enricher = new ValorantFootstepEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Rpc(1, 1, Effect("FXC_Gun_Bounce", 20, material: 1, intensity: 1)));
        enricher.Emit(Rpc(1, 2, Effect("FXC_Footstep", 20, material: 1, intensity: 1)) with { WasDecoded = false });
        enricher.Emit(Rpc(1, 3, new ClientPlayOneShotEffectAtLocationParameters()));

        Assert.That(sink.Events, Has.Count.EqualTo(3));
        Assert.That(sink.Events.OfType<ValorantFootstepReceived>(), Is.Empty);
    }

    private static ClientPlayOneShotEffectAtLocationParameters Effect(
        string container,
        uint character,
        float material,
        float intensity) => new()
    {
        EffectContainer = new(99, $"/Game/Characters/_Core/{container}.{container}_C"),
        FunctionFloatValues =
        [
            new EffectManagerFunctionFloatValue { Name = new FGameplayTag(1, "FXC.Intensity"), Value = intensity },
            new EffectManagerFunctionFloatValue { Name = new FGameplayTag(2, "FXC.Material"), Value = material },
        ],
        FunctionObjectValues =
        [
            new EffectManagerFunctionObjectValue { Name = new FGameplayTag(3, "FXC.EffectContext"), Value = new(character, null) },
        ],
        Location = new FVector(1, 2, 3),
        Rotation = new FRotator(0, 90, 0),
        StartMovementTime = 12.5f,
    };

    private static void RegisterPlayer(
        ValorantFootstepEventEnricher enricher,
        uint characterNetGuid,
        uint playerStateNetGuid,
        string subject)
    {
        var descriptor = new BombPlayerStateDescriptor
        {
            PossessedCharacter = new(characterNetGuid, null),
            Subject = subject,
        };
        descriptor.MarkDecoded(nameof(BombPlayerStateDescriptor.PossessedCharacter));
        descriptor.MarkDecoded(nameof(BombPlayerStateDescriptor.Subject));
        enricher.Emit(new ExportGroupReceived(0, 1, playerStateNetGuid, playerStateNetGuid, 1, true, false, 0,
            descriptor.Path, descriptor.Kind, descriptor.Categories, 0, 0, null, null, null, 0, 0, true, descriptor, 0, []));
    }

    private static RpcReceived Rpc(float time, int packetId, ClientPlayOneShotEffectAtLocationParameters payload) =>
        new(time, packetId, 2, 6, 1, LocationalEffectManagerClassNetCacheDescriptor.ComponentPath,
            "ClientPlayOneShotEffectAtLocation", ClientPlayOneShotEffectAtLocationParameters.ExportPath, 2,
            ExportCategory.Effects, 0, 0, true, payload, 0, []);

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];
        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
