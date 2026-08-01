using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Valorant;
using Replay.Valorant.Descriptors.Agents.Mage;
using Replay.Valorant.Descriptors.Agents.Mage.TidalWave;

namespace Test.Integration;

[Category("Integration")]
public class HarborAbilityTests
{
    private class Sink : IReplayEventSink
    {
        private Action<CoveAbilityDescriptor> CoveConsumer { get; set; }
        
        public Sink(Action<CoveAbilityDescriptor> coveConsumer)
        {
            CoveConsumer = coveConsumer;
        }

        public void Emit(ReplayEvent replayEvent)
        {
            switch (replayEvent)
            {
                case ExportGroupReceived export:
                    if (export.Payload is CoveAbilityDescriptor cove)
                    {
                        CoveConsumer(cove);
                    }
                    break;
            }
        }
    }

    private sealed class TidalWaveSink : IReplayEventSink
    {
        public List<TidalWaveChunkDescriptor> Chunks { get; } = [];
        public List<SplineMovementComponentDescriptor> SplineMovements { get; } = [];
        public List<ActorListTransitionContextDescriptor> ActorListTransitions { get; } = [];
        public List<TransformTransitionContextDescriptor> TransformTransitions { get; } = [];
        public List<ReadyingStateComponentDescriptor> ReadyingStates { get; } = [];
        public List<ActivatableActorComponentDescriptor> ActivatableActors { get; } = [];
        public List<MulticastInitializeParameters> Initializations { get; } = [];
        public List<MulticastWallStartLingerParameters> WallStartLingers { get; } = [];
        public List<MulticastStopWaveParameters> StopWaves { get; } = [];
        public List<NetMulticastRemoveForceModuleParameters> RemovedForceModules { get; } = [];
        public int FastOutroCount { get; private set; }
        public int PreviewTimeStartedCount { get; private set; }
        public int DecodedEventCount =>
            Chunks.Count +
            SplineMovements.Count +
            ActorListTransitions.Count +
            TransformTransitions.Count +
            ReadyingStates.Count +
            ActivatableActors.Count +
            Initializations.Count +
            WallStartLingers.Count +
            StopWaves.Count +
            RemovedForceModules.Count +
            FastOutroCount +
            PreviewTimeStartedCount;

        public void Emit(ReplayEvent replayEvent)
        {
            switch (replayEvent)
            {
                case ExportGroupReceived { Payload: TidalWaveChunkDescriptor payload }:
                    Chunks.Add(payload);
                    break;
                case ExportGroupReceived { Payload: SplineMovementComponentDescriptor payload }:
                    SplineMovements.Add(payload);
                    break;
                case ExportGroupReceived { Payload: ActorListTransitionContextDescriptor payload }:
                    ActorListTransitions.Add(payload);
                    break;
                case ExportGroupReceived { Payload: TransformTransitionContextDescriptor payload }:
                    TransformTransitions.Add(payload);
                    break;
                case ExportGroupReceived { Payload: ReadyingStateComponentDescriptor payload }:
                    ReadyingStates.Add(payload);
                    break;
                case ExportGroupReceived { Payload: ActivatableActorComponentDescriptor payload }:
                    ActivatableActors.Add(payload);
                    break;
                case RpcReceived { Payload: MulticastInitializeParameters payload }:
                    Initializations.Add(payload);
                    break;
                case RpcReceived { Payload: MulticastWallStartLingerParameters payload }:
                    WallStartLingers.Add(payload);
                    break;
                case RpcReceived { Payload: MulticastStopWaveParameters payload }:
                    StopWaves.Add(payload);
                    break;
                case RpcReceived { Payload: NetMulticastRemoveForceModuleParameters payload }:
                    RemovedForceModules.Add(payload);
                    break;
                case RpcReceived { FunctionName: "MulticastPlayFastOutro" }:
                    FastOutroCount++;
                    break;
                case RpcReceived { FunctionName: "OnPreviewTimeStarted" }:
                    PreviewTimeStartedCount++;
                    break;
            }
        }
    }

    [Test]
    public void TestCove()
    {
        var archive = new FBinaryArchive(TestHelper.ReadReplayBytes("harbor.vrf"));

        var coveConsumer = Substitute.For<Action<CoveAbilityDescriptor>>();
        var parseNothingProfile = new ParseProfile
        {
            EnabledCategories = ExportCategory.Ability,
        };
        var eventSink = new Sink(coveConsumer);

        _ = ValorantReplayReader.CreateDefault(new NullLoggerFactory(), eventSink, parseNothingProfile).Read(archive);
        Assert.Multiple(() =>
        {
            Assert.That(coveConsumer.ReceivedCalls().All(_ => _.GetArguments().Cast<CoveAbilityDescriptor>().First().Instigator is 134 or null), Is.True);
            Assert.That(coveConsumer.ReceivedCalls().Select(_ => _.GetArguments().Cast<CoveAbilityDescriptor>().First().ReplicatedMovement).Count(_ => _ is not null), Is.EqualTo(1276));
            Assert.That(coveConsumer.ReceivedCalls().Select(_ => _.GetArguments().Cast<CoveAbilityDescriptor>().First().ReplicatedMovement).Count(_ => _ is null), Is.EqualTo(1));
        });
    }

    [Test]
    public void TestTidalWave()
    {
        var archive = new FBinaryArchive(TestHelper.ReadReplayBytes("harbor.vrf"));
        var eventSink = new TidalWaveSink();
        var abilityProfile = new ParseProfile
        {
            EnabledCategories = ExportCategory.Ability,
        };

        _ = ValorantReplayReader.CreateDefault(new NullLoggerFactory(), eventSink, abilityProfile).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(archive.AtEnd, Is.True);
            Assert.That(eventSink.Chunks, Has.Count.EqualTo(10));
            Assert.That(eventSink.ActorListTransitions, Has.Count.EqualTo(28));
            Assert.That(eventSink.TransformTransitions, Has.Count.EqualTo(6));
            Assert.That(eventSink.WallStartLingers, Has.Count.EqualTo(5));

            Assert.That(eventSink.ActorListTransitions.Any(value =>
                value.Actors is { BitCount: 64, Data.Length: 8 }), Is.True);
            Assert.That(eventSink.ActorListTransitions.Any(value =>
                value.Actors is { BitCount: 56, Data.Length: 7 }), Is.True);
            Assert.That(eventSink.TransformTransitions.Any(value =>
                value.Rotation is { BitCount: 192, Data.Length: 24 } &&
                value.Translation is { Bits: 64 }), Is.True);
            Assert.That(eventSink.WallStartLingers.All(value =>
                value.LingerWallStopPosition == 12.470505714416504), Is.True);
        });
    }

    [Test]
    public void TidalWave_DoesNotDecode_WhenAbilitiesDisabled()
    {
        var archive = new FBinaryArchive(TestHelper.ReadReplayBytes("harbor.vrf"));
        var eventSink = new TidalWaveSink();

        _ = ValorantReplayReader.CreateDefault(
                new NullLoggerFactory(),
                eventSink,
                new ParseProfile { EnabledCategories = ExportCategory.None })
            .Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(archive.AtEnd, Is.True);
            Assert.That(eventSink.DecodedEventCount, Is.Zero);
        });
    }
}
