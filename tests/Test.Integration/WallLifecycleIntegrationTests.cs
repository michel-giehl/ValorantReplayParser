using Replay.Encoding.Archives;
using Replay.Models.Events;
using Replay.Unreal.Readers;
using Replay.Valorant;
using Replay.Valorant.Walls;

namespace Test.Integration;

[Category("Integration")]
public class WallLifecycleIntegrationTests
{
    private const string PlacementReplayFileName = "2f5d648c-3f1d-4d41-b93a-dfb85976941a.vrf";
    private const string ActivationReplayFileName = "6f55ee39-4ec7-491b-ae55-b2193b958fca.vrf";

    [Test]
    public void SuppliedPlacementReplay_EmitsSageDestructionAndVysePlacementLifecycles()
    {
        var path = ResolvePath("VALORANT_WALL_PLACEMENT_REPLAY_PATH", PlacementReplayFileName);
        if (!File.Exists(path))
        {
            Assert.Ignore($"Wall placement replay not found. Set VALORANT_WALL_PLACEMENT_REPLAY_PATH or place {PlacementReplayFileName} in the VALORANT demo directory.");
        }

        var (sink, context) = Read(path);
        var placed = sink.Events.OfType<ValorantWallPlaced>().ToArray();
        var segments = sink.Events.OfType<ValorantWallSegmentSpawned>().ToArray();
        var segmentDestroyed = sink.Events.OfType<ValorantWallSegmentDestroyed>().ToArray();
        var destroyed = sink.Events.OfType<ValorantWallDestroyed>().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(placed, Has.Length.EqualTo(8));
            Assert.That(placed.Count(wall => wall.WallKind == ValorantWallKind.SageBarrierOrb), Is.EqualTo(5));
            Assert.That(placed.Count(wall => wall.WallKind == ValorantWallKind.VyseShear), Is.EqualTo(3));
            Assert.That(placed.Where(wall => wall.WallKind == ValorantWallKind.VyseShear)
                .All(wall => wall.WallStart is not null && wall.WallEnd is not null), Is.True);
            Assert.That(segments, Has.Length.EqualTo(17));
            Assert.That(segmentDestroyed, Has.Length.EqualTo(17));
            Assert.That(segmentDestroyed.Count(value =>
                value.Evidence == ValorantWallSegmentDestructionEvidence.DisableCollisionRpc), Is.EqualTo(13));
            Assert.That(sink.Events.OfType<ValorantWallActivated>(), Is.Empty);
            Assert.That(destroyed, Has.Length.EqualTo(8));
            Assert.That(context.PacketStats.MalformedPacketCount, Is.Zero);
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.Zero);
            Assert.That(context.PacketStats.PartialErrorCount, Is.EqualTo(2));
        });

        var damagedSegment = segmentDestroyed.Single(value => value.SegmentActorNetGuid == 466);
        Assert.Multiple(() =>
        {
            Assert.That(damagedSegment.TimeSeconds, Is.EqualTo(26.615f).Within(0.001f));
            Assert.That(damagedSegment.WallActorNetGuid, Is.EqualTo(414));
            Assert.That(damagedSegment.Evidence,
                Is.EqualTo(ValorantWallSegmentDestructionEvidence.DisableCollisionRpc));
        });
    }

    [Test]
    public void SuppliedActivationReplay_EmitsVyseActivationWithTriggerAndActiveDuration()
    {
        var path = ResolvePath("VALORANT_WALL_ACTIVATION_REPLAY_PATH", ActivationReplayFileName);
        if (!File.Exists(path))
        {
            Assert.Ignore($"Wall activation replay not found. Set VALORANT_WALL_ACTIVATION_REPLAY_PATH or place {ActivationReplayFileName} in the VALORANT demo directory.");
        }

        var (sink, context) = Read(path);
        var placed = sink.Events.OfType<ValorantWallPlaced>().ToArray();
        var segments = sink.Events.OfType<ValorantWallSegmentSpawned>().ToArray();
        var segmentDestroyed = sink.Events.OfType<ValorantWallSegmentDestroyed>().ToArray();
        var destroyed = sink.Events.OfType<ValorantWallDestroyed>().ToArray();
        var activated = sink.Events.OfType<ValorantWallActivated>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(placed, Has.Length.EqualTo(34));
            Assert.That(placed.Count(wall => wall.WallKind == ValorantWallKind.SageBarrierOrb), Is.EqualTo(24));
            Assert.That(placed.Count(wall => wall.WallKind == ValorantWallKind.VyseShear), Is.EqualTo(10));
            Assert.That(segments, Has.Length.EqualTo(93));
            Assert.That(segmentDestroyed, Has.Length.EqualTo(90));
            Assert.That(destroyed, Has.Length.EqualTo(32));
            Assert.That(activated.TimeSeconds, Is.EqualTo(1343.908f).Within(0.001f));
            Assert.That(activated.WallActorNetGuid, Is.EqualTo(31980));
            Assert.That(activated.ActiveWallActorNetGuid, Is.EqualTo(32492));
            Assert.That(activated.TriggerCharacterNetGuid, Is.EqualTo(1362));
            Assert.That(activated.TriggerPlayerStateNetGuid, Is.EqualTo(284));
            Assert.That(activated.WallStart, Is.EqualTo(placed.Single(wall => wall.WallActorNetGuid == 31980).WallStart));
            Assert.That(activated.WallEnd, Is.EqualTo(placed.Single(wall => wall.WallActorNetGuid == 31980).WallEnd));
            Assert.That(context.PacketStats.MalformedPacketCount, Is.Zero);
            Assert.That(context.PacketStats.PartialErrorCount, Is.EqualTo(141));
        });

        var activeWallDestroyed = destroyed.Single(value => value.WallActorNetGuid == 31980);
        Assert.Multiple(() =>
        {
            Assert.That(activeWallDestroyed.ActiveWallActorNetGuid, Is.EqualTo(32492));
            Assert.That(activeWallDestroyed.ActiveDurationSeconds, Is.EqualTo(6.0006104f).Within(0.0001f));
            Assert.That(activeWallDestroyed.Evidence,
                Is.EqualTo(ValorantWallDestructionEvidence.VyseActiveWallActorDestroyed));
        });
    }

    private static (CapturingReplayEventSink Sink, ReplayReaderContext Context) Read(string replayPath)
    {
        var sink = new CapturingReplayEventSink();
        using var stream = File.OpenRead(replayPath);
        using var archive = new FBinaryArchive(stream);
        var context = ValorantReplayReader.CreateDefault(loggerFactory: null, eventSink: sink).Read(archive);
        return (sink, context);
    }

    private static string ResolvePath(string variable, string fileName)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VALORANT", "Saved", "Demos", fileName)
            : configured;
    }

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];
        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
