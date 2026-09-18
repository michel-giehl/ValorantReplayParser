using Replay.Encoding.Archives;
using Replay.Models.Events;
using Replay.Valorant;
using Replay.Valorant.Flashes;
using Replay.Valorant.Flashes.Descriptors;

namespace Test.Integration;

[Category("Integration")]
public class FlashLifecycleIntegrationTests
{
    private const string ReplayFileName = "33a355e8-918a-4784-a9b4-addf5e567710.vrf";
    private const string VyseYoruReplayFileName = "9ec4f967-fd70-4e31-892b-9ad1037a43cb.vrf";

    [Test]
    public void SuppliedFlashReplay_EmitsCompleteRecordedLifecycle()
    {
        var replayPath = Environment.GetEnvironmentVariable("VALORANT_FLASH_REPLAY_PATH");
        if (string.IsNullOrWhiteSpace(replayPath))
        {
            replayPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VALORANT",
                "Saved",
                "Demos",
                ReplayFileName);
        }

        if (!File.Exists(replayPath))
        {
            Assert.Ignore(
                $"Flash integration replay not found. Set VALORANT_FLASH_REPLAY_PATH or place {ReplayFileName} in the VALORANT demo directory.");
        }

        var sink = new CapturingReplayEventSink();
        using var stream = File.OpenRead(replayPath);
        using var archive = new FBinaryArchive(stream);
        var context = ValorantReplayReader.CreateDefault(loggerFactory: null, eventSink: sink).Read(archive);

        var casts = sink.Events.OfType<ValorantFlashCast>().ToArray();
        var paths = sink.Events.OfType<ValorantFlashPathUpdated>().ToArray();
        var explosions = sink.Events.OfType<ValorantFlashExploded>().ToArray();
        var hits = sink.Events.OfType<ValorantFlashPlayerHit>().ToArray();
        var castsByFlash = casts.ToDictionary(cast => cast.FlashActorNetGuid);
        var rawBlindDurations = sink.Events
            .OfType<ExportGroupReceived>()
            .Where(replayEvent => replayEvent.Payload is BlindManagerComponentDescriptor)
            .SelectMany(replayEvent =>
                ((BlindManagerComponentDescriptor)replayEvent.Payload!).ActiveBlinds?
                    .Select(blind => new
                    {
                        replayEvent.ActorNetGuid,
                        blind.BlindId,
                        blind.EffectId,
                        blind.StartNetMovementTime,
                        blind.InitialDuration,
                    }) ?? [])
            .GroupBy(blind => (
                blind.ActorNetGuid,
                blind.BlindId,
                blind.EffectId,
                StartBits: blind.StartNetMovementTime is { } start
                    ? BitConverter.SingleToInt32Bits(start)
                    : 0))
            .ToDictionary(group => group.Key, group => group.First().InitialDuration);

        Assert.Multiple(() =>
        {
            Assert.That(casts, Has.Length.EqualTo(20));
            Assert.That(casts.Count(cast => cast.FlashKind == ValorantFlashKind.SkyeGuidingLight), Is.EqualTo(6));
            Assert.That(casts.Count(cast => cast.FlashKind == ValorantFlashKind.KayoFlashDriveOverhand), Is.EqualTo(2));
            Assert.That(casts.Count(cast => cast.FlashKind == ValorantFlashKind.KayoFlashDriveUnderhand), Is.EqualTo(2));
            Assert.That(casts.Count(cast => cast.FlashKind == ValorantFlashKind.BreachFlashpoint), Is.EqualTo(5));
            Assert.That(casts.Count(cast => cast.FlashKind == ValorantFlashKind.PhoenixCurveballRight), Is.EqualTo(5));
            Assert.That(explosions, Has.Length.EqualTo(20));
            Assert.That(explosions.Count(explosion =>
                explosion.Evidence == ValorantFlashExplosionEvidence.SkyeFlashSource), Is.EqualTo(6));
            Assert.That(explosions.Count(explosion =>
                explosion.Evidence == ValorantFlashExplosionEvidence.StopProjectileRpc), Is.EqualTo(14));
            Assert.That(paths, Has.Length.EqualTo(686));
            Assert.That(paths.All(path => castsByFlash.ContainsKey(path.FlashActorNetGuid)), Is.True);
            Assert.That(hits, Has.Length.EqualTo(19));
            Assert.That(hits.All(hit => hit.DurationSource == ValorantFlashDurationSource.BlindManagerInitialDuration),
                Is.True);
            Assert.That(hits.All(hit => hit.FlashActorNetGuid is { } flash &&
                                      hit.TargetCharacterNetGuid == castsByFlash[flash].CasterCharacterNetGuid),
                Is.True);
            Assert.That(context.PacketStats.MalformedPacketCount, Is.Zero);
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.Zero);
            Assert.That(context.PacketStats.PartialErrorCount, Is.EqualTo(2));
        });

        foreach (var group in paths.GroupBy(path => path.FlashActorNetGuid))
        {
            Assert.That(
                group.OrderBy(path => path.SampleIndex).Select(path => path.SampleIndex),
                Is.EqualTo(Enumerable.Range(0, group.Count())),
                $"Flash {group.Key} path indices");
        }

        foreach (var hit in hits)
        {
            var key = (
                hit.TargetCharacterNetGuid,
                hit.BlindId!.Value,
                hit.EffectId,
                StartBits: hit.StartNetMovementTime is { } start
                    ? BitConverter.SingleToInt32Bits(start)
                    : 0);
            Assert.That(rawBlindDurations.ContainsKey(key), Is.True, $"Blind {hit.BlindId}");
            Assert.That(hit.InitialDurationSeconds, Is.EqualTo(rawBlindDurations[key]), $"Blind {hit.BlindId}");
        }
    }

    [Test]
    public void SuppliedVyseYoruFlashReplay_EmitsCompleteRecordedLifecycle()
    {
        var replayPath = Environment.GetEnvironmentVariable("VALORANT_FLASH_VYSE_YORU_REPLAY_PATH");
        if (string.IsNullOrWhiteSpace(replayPath))
        {
            replayPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VALORANT",
                "Saved",
                "Demos",
                VyseYoruReplayFileName);
        }

        if (!File.Exists(replayPath))
        {
            Assert.Ignore(
                $"Vyse/Yoru flash integration replay not found. Set VALORANT_FLASH_VYSE_YORU_REPLAY_PATH or place {VyseYoruReplayFileName} in the VALORANT demo directory.");
        }

        var sink = new CapturingReplayEventSink();
        using var stream = File.OpenRead(replayPath);
        using var archive = new FBinaryArchive(stream);
        var context = ValorantReplayReader.CreateDefault(loggerFactory: null, eventSink: sink).Read(archive);

        var casts = sink.Events.OfType<ValorantFlashCast>().ToArray();
        var paths = sink.Events.OfType<ValorantFlashPathUpdated>().ToArray();
        var explosions = sink.Events.OfType<ValorantFlashExploded>().ToArray();
        var hits = sink.Events.OfType<ValorantFlashPlayerHit>().ToArray();
        var castsByFlash = casts.ToDictionary(cast => cast.FlashActorNetGuid);
        var rawBlindDurations = sink.Events
            .OfType<ExportGroupReceived>()
            .Where(replayEvent => replayEvent.Payload is BlindManagerComponentDescriptor)
            .SelectMany(replayEvent =>
                ((BlindManagerComponentDescriptor)replayEvent.Payload!).ActiveBlinds?
                    .Select(blind => new
                    {
                        replayEvent.ActorNetGuid,
                        blind.BlindId,
                        blind.EffectId,
                        blind.StartNetMovementTime,
                        blind.InitialDuration,
                    }) ?? [])
            .GroupBy(blind => (
                blind.ActorNetGuid,
                blind.BlindId,
                blind.EffectId,
                StartBits: blind.StartNetMovementTime is { } start
                    ? BitConverter.SingleToInt32Bits(start)
                    : 0))
            .ToDictionary(group => group.Key, group => group.First().InitialDuration);

        Assert.Multiple(() =>
        {
            Assert.That(casts, Has.Length.EqualTo(7));
            Assert.That(casts.Count(cast => cast.FlashKind == ValorantFlashKind.VyseArcRose), Is.EqualTo(1));
            Assert.That(casts.Count(cast => cast.FlashKind == ValorantFlashKind.YoruBlindside), Is.EqualTo(6));
            Assert.That(explosions, Has.Length.EqualTo(20));
            Assert.That(explosions.Count(explosion =>
                explosion.Evidence == ValorantFlashExplosionEvidence.VyseFlashSource), Is.EqualTo(14));
            Assert.That(explosions.Count(explosion =>
                explosion.Evidence == ValorantFlashExplosionEvidence.StopProjectileRpc), Is.EqualTo(6));
            Assert.That(paths, Has.Length.EqualTo(73));
            Assert.That(paths.All(path => castsByFlash.ContainsKey(path.FlashActorNetGuid)), Is.True);
            Assert.That(hits, Has.Length.EqualTo(21));
            Assert.That(hits.All(hit => hit.DurationSource == ValorantFlashDurationSource.BlindManagerInitialDuration),
                Is.True);
            Assert.That(hits.All(hit => hit.FlashActorNetGuid is { } flash &&
                                      castsByFlash.ContainsKey(flash)), Is.True);
            Assert.That(hits.Count(hit => hit.FlashKind == ValorantFlashKind.VyseArcRose), Is.EqualTo(14));
            Assert.That(hits.Count(hit => hit.FlashKind == ValorantFlashKind.YoruBlindside), Is.EqualTo(7));
            Assert.That(context.PacketStats.MalformedPacketCount, Is.Zero);
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.Zero);
            Assert.That(context.PacketStats.PartialErrorCount, Is.EqualTo(2));
        });

        foreach (var group in paths.GroupBy(path => path.FlashActorNetGuid))
        {
            Assert.That(
                group.OrderBy(path => path.SampleIndex).Select(path => path.SampleIndex),
                Is.EqualTo(Enumerable.Range(0, group.Count())),
                $"Flash {group.Key} path indices");
        }

        foreach (var hit in hits)
        {
            var key = (
                hit.TargetCharacterNetGuid,
                hit.BlindId!.Value,
                hit.EffectId,
                StartBits: hit.StartNetMovementTime is { } start
                    ? BitConverter.SingleToInt32Bits(start)
                    : 0);
            Assert.That(rawBlindDurations.ContainsKey(key), Is.True, $"Blind {hit.BlindId}");
            Assert.That(hit.InitialDurationSeconds, Is.EqualTo(rawBlindDurations[key]), $"Blind {hit.BlindId}");
        }
    }

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
