using Replay.Encoding.Net;
using Replay.Models.Events;
using Replay.Valorant.Crosshair;
using Replay.Valorant.Events.Crosshair;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Events.Tests.Crosshair;

public class ValorantCrosshairEventEnricherTests
{
    private const int InnerThickness = 23;
    private const int DisplayDot = 18;
    private const int ColorR = 2;

    [Test]
    public void Emit_FirstUpdate_ForwardsNativeEventThenEmitsTheMergedCrosshair()
    {
        var sink = new CapturingSink();
        var enricher = new ValorantCrosshairEventEnricher(sink, new NetGuidCache());
        var export = PlayerState(1.5f, 7, 30, "jett", update =>
        {
            update.Add(ColorR, (byte)0);
            update.Add(InnerThickness, 3f);
            update.Add(DisplayDot, true);
        });

        enricher.Emit(export);

        var crosshair = sink.Events.OfType<ValorantCrosshairUpdated>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(sink.Events[0], Is.SameAs(export));
            Assert.That(sink.Events[1], Is.SameAs(crosshair));
            Assert.That(crosshair.PlayerStateNetGuid, Is.EqualTo(30));
            Assert.That(crosshair.Subject, Is.EqualTo("jett"));
            Assert.That(crosshair.TimeSeconds, Is.EqualTo(1.5f));
            Assert.That(crosshair.PacketId, Is.EqualTo(7));
            Assert.That(crosshair.Settings.Primary.InnerLines.LineThickness, Is.EqualTo(3f));
            Assert.That(crosshair.Settings.Primary.DisplayCenterDot, Is.True);
            Assert.That(crosshair.Settings.Primary.Color, Is.EqualTo(new CrosshairColor(0, 255, 255, 255)));
        });
    }

    [Test]
    public void Emit_LaterUpdate_MergesOntoThePreviousStateAndKeepsEarlierSnapshotsIntact()
    {
        var sink = new CapturingSink();
        var enricher = new ValorantCrosshairEventEnricher(sink, new NetGuidCache());
        enricher.Emit(PlayerState(1, 1, 30, "jett", update =>
        {
            update.Add(InnerThickness, 3f);
            update.Add(DisplayDot, true);
        }));
        enricher.Emit(PlayerState(9, 2, 30, null, update => update.Add(InnerThickness, 5f)));

        var events = sink.Events.OfType<ValorantCrosshairUpdated>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(events, Has.Length.EqualTo(2));
            Assert.That(events[0].Settings.Primary.InnerLines.LineThickness, Is.EqualTo(3f));
            Assert.That(events[1].Settings.Primary.InnerLines.LineThickness, Is.EqualTo(5f));
            Assert.That(events[1].Settings.Primary.DisplayCenterDot, Is.True);
            Assert.That(events[1].Subject, Is.EqualTo("jett"));
        });
    }

    [Test]
    public void Emit_PlayersAreKeptApart()
    {
        var sink = new CapturingSink();
        var enricher = new ValorantCrosshairEventEnricher(sink, new NetGuidCache());
        enricher.Emit(PlayerState(1, 1, 30, "a", update => update.Add(InnerThickness, 3f)));
        enricher.Emit(PlayerState(1, 2, 32, "b", update => update.Add(DisplayDot, true)));

        var events = sink.Events.OfType<ValorantCrosshairUpdated>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(events[1].Settings.Primary.InnerLines.LineThickness, Is.Zero);
            Assert.That(events[1].Settings.Primary.DisplayCenterDot, Is.True);
            Assert.That(events[1].Subject, Is.EqualTo("b"));
        });
    }

    [Test]
    public void Emit_CompleteUpdates_AreNotFlaggedPartial()
    {
        var sink = new CapturingSink();
        var enricher = new ValorantCrosshairEventEnricher(sink, new NetGuidCache());

        enricher.Emit(PlayerState(1, 1, 30, "jett", update => update.Add(InnerThickness, 3f)));

        var crosshair = sink.Events.OfType<ValorantCrosshairUpdated>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(crosshair.UnmatchedLeafCount, Is.Zero);
            Assert.That(crosshair.IsPartial, Is.False);
        });
    }

    [Test]
    public void Emit_UpdateWithUnmatchedLeaves_FlagsThePlayerPartialFromThenOn()
    {
        var sink = new CapturingSink();
        var enricher = new ValorantCrosshairEventEnricher(sink, new NetGuidCache());
        enricher.Emit(PlayerState(1, 1, 30, "jett", update =>
        {
            update.Add(InnerThickness, 3f);
            update.UnmatchedLeafCount = 2;
        }));
        // A later update from a clean table does not make the merged state complete again.
        enricher.Emit(PlayerState(9, 2, 30, null, update => update.Add(InnerThickness, 5f)));
        enricher.Emit(PlayerState(9, 3, 32, "b", update => update.Add(InnerThickness, 5f)));

        var events = sink.Events.OfType<ValorantCrosshairUpdated>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(events[0].IsPartial, Is.True);
            Assert.That(events[0].UnmatchedLeafCount, Is.EqualTo(2));
            Assert.That(events[1].IsPartial, Is.True);
            Assert.That(events[1].UnmatchedLeafCount, Is.EqualTo(2));
            Assert.That(events[2].IsPartial, Is.False);
        });
    }

    [Test]
    public void Emit_UpdateWithoutCrosshair_EmitsNothingExtra()
    {
        var sink = new CapturingSink();
        var enricher = new ValorantCrosshairEventEnricher(sink, new NetGuidCache());

        enricher.Emit(PlayerState(1, 1, 30, "jett", update: null));

        Assert.That(sink.Events.OfType<ValorantCrosshairUpdated>(), Is.Empty);
    }

    [Test]
    public void Emit_ThroughTheFullEnricher_ReachesTheConsumerSink()
    {
        var sink = new CapturingSink();
        var events = new ValorantEventEnricher(sink);

        events.Emit(PlayerState(1, 1, 30, "jett", update => update.Add(DisplayDot, true)));

        Assert.That(sink.Events.OfType<ValorantCrosshairUpdated>().Single().Settings.Primary.DisplayCenterDot, Is.True);
    }

    private static ExportGroupReceived PlayerState(
        float time, int packet, uint playerState, string? subject, Action<CrosshairSettingsUpdate>? update)
    {
        var descriptor = new BombPlayerStateDescriptor();
        if (subject is not null)
        {
            descriptor.Subject = subject;
            descriptor.MarkDecoded(nameof(BombPlayerStateDescriptor.Subject));
        }

        if (update is not null)
        {
            descriptor.CrosshairSettings = new CrosshairSettingsUpdate();
            update(descriptor.CrosshairSettings);
            descriptor.MarkDecoded(nameof(BombPlayerStateDescriptor.CrosshairSettings));
        }

        return new ExportGroupReceived(time, packet, playerState, playerState, 1, true, false, 0,
            descriptor.Path, descriptor.Kind, descriptor.Categories, 0, 0, null, null, null, 0, 0, true, descriptor, 0, []);
    }

    private sealed class CapturingSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
