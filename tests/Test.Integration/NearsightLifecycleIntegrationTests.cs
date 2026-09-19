using Replay.Encoding.Archives;
using Replay.Models.Events;
using Replay.Unreal.Readers;
using Replay.Valorant;
using Replay.Valorant.Nearsights;

namespace Test.Integration;

[Category("Integration")]
public class NearsightLifecycleIntegrationTests
{
    private const string FullReplayFileName = "ea910dfd-548b-440d-b80d-0497fa0195f9.vrf";
    private const string HarborReplayFileName = "39ed07cd-f223-47ef-a042-7008f3af45c2.vrf";

    [Test]
    public void SuppliedHarborReplay_EmitsRecordedNearsightLifecycle()
    {
        var replayPath = ResolveReplayPath("VALORANT_NEARSIGHT_HARBOR_REPLAY_PATH", HarborReplayFileName);
        if (!File.Exists(replayPath))
        {
            Assert.Ignore($"Harbor near-sight replay not found. Set VALORANT_NEARSIGHT_HARBOR_REPLAY_PATH or place {HarborReplayFileName} in the VALORANT demo directory.");
        }

        var (sink, context) = Read(replayPath);
        var casts = sink.Events.OfType<ValorantNearsightCast>().ToArray();
        var paths = sink.Events.OfType<ValorantNearsightPathUpdated>().ToArray();
        var activations = sink.Events.OfType<ValorantNearsightActivated>().ToArray();
        var hits = sink.Events.OfType<ValorantNearsightPlayerHit>().ToArray();
        var ended = sink.Events.OfType<ValorantNearsightPlayerEffectEnded>().ToArray();
        var castsByActor = casts.ToDictionary(cast => cast.NearsightActorNetGuid);

        Assert.Multiple(() =>
        {
            Assert.That(casts, Has.Length.EqualTo(5));
            Assert.That(casts.All(cast => cast.NearsightKind == ValorantNearsightKind.HarborStormSurge), Is.True);
            Assert.That(paths, Has.Length.EqualTo(37));
            Assert.That(paths.All(path => castsByActor.ContainsKey(path.NearsightActorNetGuid)), Is.True);
            Assert.That(activations, Has.Length.EqualTo(5));
            Assert.That(activations.All(activation => activation.Evidence == ValorantNearsightActivationEvidence.SourceActorSpawned), Is.True);
            Assert.That(hits, Has.Length.EqualTo(2));
            Assert.That(hits.All(hit => hit.ConfiguredDurationSeconds == 2), Is.True);
            Assert.That(hits.All(hit => hit.TargetCharacterNetGuid == castsByActor[hit.NearsightActorNetGuid].CasterCharacterNetGuid), Is.True);
            Assert.That(ended, Has.Length.EqualTo(2));
            Assert.That(ended.All(effect => effect.ObservedDurationSeconds == 2), Is.True);
            Assert.That(context.PacketStats.MalformedPacketCount, Is.Zero);
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.Zero);
            Assert.That(context.PacketStats.PartialErrorCount, Is.EqualTo(2));
        });

        AssertMonotonicPathIndices(paths);
    }

    [Test]
    public void SuppliedOmenReynaReplay_MatchesKnownCastAndHitAnchors()
    {
        var replayPath = ResolveReplayPath("VALORANT_NEARSIGHT_FULL_REPLAY_PATH", FullReplayFileName);
        if (!File.Exists(replayPath))
        {
            Assert.Ignore($"Omen/Reyna near-sight replay not found. Set VALORANT_NEARSIGHT_FULL_REPLAY_PATH or place {FullReplayFileName} in the VALORANT demo directory.");
        }

        var (sink, context) = Read(replayPath);
        var casts = sink.Events.OfType<ValorantNearsightCast>().ToArray();
        var paths = sink.Events.OfType<ValorantNearsightPathUpdated>().ToArray();
        var activations = sink.Events.OfType<ValorantNearsightActivated>().ToArray();
        var hits = sink.Events.OfType<ValorantNearsightPlayerHit>().ToArray();
        var ended = sink.Events.OfType<ValorantNearsightPlayerEffectEnded>().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(casts, Has.Length.EqualTo(48));
            Assert.That(paths, Has.Length.EqualTo(602));
            Assert.That(activations, Has.Length.EqualTo(48));
            Assert.That(hits, Has.Length.EqualTo(27));
            Assert.That(ended, Has.Length.EqualTo(29));
            Assert.That(context.PacketStats.MalformedPacketCount, Is.Zero);
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.EqualTo(12));
            Assert.That(context.PacketStats.PartialErrorCount, Is.EqualTo(470));
        });

        AssertOmenHit(casts, hits, ended, 10062, 538, 2, 1.8671875f);
        AssertNoHit(casts, hits, 12046, ValorantNearsightKind.ReynaLeer);
        AssertOmenHit(casts, hits, ended, 14650, 1154, 2, 2.40625f);

        var reynaCast = casts.Single(cast => cast.NearsightActorNetGuid == 25814);
        var reynaHit = hits.Single(hit => hit.NearsightActorNetGuid == 25814);
        var reynaIntervals = ended.Where(effect => effect.NearsightActorNetGuid == 25814).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(reynaCast.NearsightKind, Is.EqualTo(ValorantNearsightKind.ReynaLeer));
            Assert.That(reynaHit.TargetCharacterNetGuid, Is.EqualTo(1352));
            Assert.That(reynaHit.ConfiguredDurationSeconds, Is.Null);
            Assert.That(reynaHit.DurationUntilRemoved, Is.True);
            Assert.That(reynaIntervals.Select(effect => effect.ObservedDurationSeconds),
                Is.EquivalentTo(new[] { 0.3671875f, 0.109375f }));
        });

        AssertMonotonicPathIndices(paths);
    }

    private static void AssertOmenHit(
        IEnumerable<ValorantNearsightCast> casts,
        IEnumerable<ValorantNearsightPlayerHit> hits,
        IEnumerable<ValorantNearsightPlayerEffectEnded> ended,
        uint actorNetGuid,
        uint targetNetGuid,
        float configuredDuration,
        float observedDuration)
    {
        var cast = casts.Single(value => value.NearsightActorNetGuid == actorNetGuid);
        var hit = hits.Single(value => value.NearsightActorNetGuid == actorNetGuid);
        var effectEnded = ended.Single(value => value.NearsightActorNetGuid == actorNetGuid);
        Assert.Multiple(() =>
        {
            Assert.That(cast.NearsightKind, Is.EqualTo(ValorantNearsightKind.OmenParanoia));
            Assert.That(hit.TargetCharacterNetGuid, Is.EqualTo(targetNetGuid));
            Assert.That(hit.ConfiguredDurationSeconds, Is.EqualTo(configuredDuration));
            Assert.That(hit.DurationUntilRemoved, Is.False);
            Assert.That(effectEnded.ObservedDurationSeconds, Is.EqualTo(observedDuration));
        });
    }

    private static void AssertNoHit(
        IEnumerable<ValorantNearsightCast> casts,
        IEnumerable<ValorantNearsightPlayerHit> hits,
        uint actorNetGuid,
        ValorantNearsightKind expectedKind)
    {
        Assert.That(casts.Single(cast => cast.NearsightActorNetGuid == actorNetGuid).NearsightKind, Is.EqualTo(expectedKind));
        Assert.That(hits.Any(hit => hit.NearsightActorNetGuid == actorNetGuid), Is.False);
    }

    private static void AssertMonotonicPathIndices(IEnumerable<ValorantNearsightPathUpdated> paths)
    {
        foreach (var group in paths.GroupBy(path => path.NearsightActorNetGuid))
        {
            Assert.That(
                group.OrderBy(path => path.SampleIndex).Select(path => path.SampleIndex),
                Is.EqualTo(Enumerable.Range(0, group.Count())),
                $"Near-sight {group.Key} path indices");
        }
    }

    private static (CapturingReplayEventSink Sink, ReplayReaderContext Context) Read(string replayPath)
    {
        var sink = new CapturingReplayEventSink();
        using var stream = File.OpenRead(replayPath);
        using var archive = new FBinaryArchive(stream);
        var context = ValorantReplayReader.CreateDefault(loggerFactory: null, eventSink: sink).Read(archive);
        return (sink, context);
    }

    private static string ResolveReplayPath(string environmentVariable, string fileName)
    {
        var configuredPath = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VALORANT", "Saved", "Demos", fileName)
            : configuredPath;
    }

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
