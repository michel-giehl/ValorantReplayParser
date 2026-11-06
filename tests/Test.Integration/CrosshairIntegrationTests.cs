using Replay.Encoding.Archives;
using Replay.Models.Events;
using Replay.Valorant;
using Replay.Valorant.Crosshair;
using Replay.Valorant.Events;
using Replay.Valorant.Events.Crosshair;

namespace Test.Integration;

[Category("Integration")]
public class CrosshairIntegrationTests
{
    private const string Replay12_08 = "c96127a8-f003-48db-a2cd-9c71de5aba15.12_08.vrf";
    private const string Replay12_10 = "9f8b32c5-c243-41ec-bbbb-832582edf652.12_10.vrf";
    private const string Replay12_11 = "5c673443-5bdc-4576-b416-aab3f62471a5.12_11.vrf";
    private const string Replay13_00 = "12974d2b-848f-490d-80ba-5f03a033c2d5.13_00.vrf";

    // The committed replays are all recorded by the same player, who changed profile between them.
    [TestCase(Replay12_08)]
    [TestCase(Replay12_11)]
    [TestCase(Replay13_00)]
    public void DotProfile_DecodesTheSameAcrossReleases(string replay)
    {
        var settings = LastCrosshair(ReadBytes(replay));

        Assert.Multiple(() =>
        {
            Assert.That(settings.ProfileName, Is.EqualTo("GiM dot"));
            Assert.That(settings.UseAdvancedOptions, Is.True);
            Assert.That(settings.Primary.Color, Is.EqualTo(CrosshairColor.White));
            Assert.That(settings.Primary.UseCustomColor, Is.False);
            Assert.That(settings.Primary.ColorCustom, Is.EqualTo(new CrosshairColor(51, 102, 255, 255)));
            Assert.That(settings.Primary.HasOutline, Is.True);
            Assert.That(settings.Primary.DisplayCenterDot, Is.True);
            Assert.That(settings.Primary.CenterDotSize, Is.EqualTo(2f));
            Assert.That(settings.Primary.InnerLines.LineThickness, Is.EqualTo(1f));
            Assert.That(settings.Primary.InnerLines.LineLength, Is.Zero);
            Assert.That(settings.Primary.OuterLines.LineThickness, Is.EqualTo(2f));
            Assert.That(settings.Primary.OuterLines.ShowLines, Is.True);
            // LineOffset and the sniper dot sit at different wire positions on 12.08 than on 12.11+.
            Assert.That(settings.Primary.InnerLines.LineOffset, Is.EqualTo(1f));
            Assert.That(settings.Primary.OuterLines.LineOffset, Is.EqualTo(3f));
            Assert.That(settings.Ads.InnerLines.LineOffset, Is.EqualTo(3f));
            Assert.That(settings.Sniper.DisplayCenterDot, Is.True);
            Assert.That(settings.Sniper.CenterDotSize, Is.EqualTo(0.31f).Within(0.005f));
            Assert.That(settings.Sniper.CenterDotOpacity, Is.EqualTo(1f));
        });
    }

    [Test]
    public void ImportedProfile_DecodesAfterTheProfileChange()
    {
        var settings = LastCrosshair(ReadBytes(Replay12_10));

        Assert.Multiple(() =>
        {
            Assert.That(settings.ProfileName, Does.StartWith("Imported Crosshair"));
            Assert.That(settings.UsePrimaryCrosshairForAds, Is.True);
            Assert.That(settings.Primary.Color, Is.EqualTo(new CrosshairColor(0, 255, 255, 255)));
            Assert.That(settings.Primary.InnerLines.LineThickness, Is.EqualTo(2f));
            Assert.That(settings.Primary.InnerLines.LineLength, Is.EqualTo(3f));
            // The player never sent an inner Primary offset; the outer and ADS ones sit at the legacy wire positions.
            Assert.That(settings.Primary.InnerLines.LineOffset, Is.Zero);
            Assert.That(settings.Primary.OuterLines.LineOffset, Is.EqualTo(10f));
            Assert.That(settings.Ads.InnerLines.LineOffset, Is.EqualTo(3f));
            Assert.That(settings.Ads.OuterLines.LineOffset, Is.EqualTo(10f));
            Assert.That(settings.Sniper.DisplayCenterDot, Is.True);
            Assert.That(settings.Sniper.CenterDotOpacity, Is.EqualTo(1f));
            Assert.That(settings.Primary.InnerLines.ShowShootingError, Is.False);
            Assert.That(settings.Primary.OuterLines.ShowLines, Is.False);
            Assert.That(settings.Primary.OutlineOpacity, Is.EqualTo(1f));
        });
    }

    // 12.08 and 12.10 replicate the leaves in a different order than 12.11+; every committed replay must bind
    // completely, with no export of the crosshair block left unmatched.
    [TestCase(Replay12_08, "12.08-12.10")]
    [TestCase(Replay12_10, "12.08-12.10")]
    [TestCase(Replay12_11, "12.11+")]
    [TestCase(Replay13_00, "12.11+")]
    public void EveryRelease_BindsWithItsOwnWireOrderAndNoUnmatchedExports(string replay, string wireLayout)
    {
        var updates = Read(ReadBytes(replay)).OfType<ValorantCrosshairUpdated>().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(updates, Is.Not.Empty);
            Assert.That(updates.Select(u => u.WireLayout).Distinct(), Is.EqualTo(new[] { wireLayout }));
            Assert.That(updates.Select(u => u.UnmatchedLeafCount), Has.All.Zero);
            Assert.That(updates.Select(u => u.IsPartial), Has.All.False);
        });
    }

    [Test]
    public void LocalDemos_EveryPlayerStateWithAnIdentityGetsACrosshair()
    {
        var directory = Environment.GetEnvironmentVariable("VALORANT_DEMOS_DIR")
                        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "VALORANT", "Saved", "Demos");
        if (!Directory.Exists(directory))
        {
            Assert.Ignore($"No local replays in '{directory}'; set VALORANT_DEMOS_DIR to run this test.");
        }

        var replays = Directory.EnumerateFiles(directory, "*.vrf").OrderBy(path => path, StringComparer.Ordinal).Take(3).ToArray();
        if (replays.Length == 0)
        {
            Assert.Ignore($"No .vrf files in '{directory}'.");
        }

        foreach (var replay in replays)
        {
            var events = Read(File.ReadAllBytes(replay));
            var withSubject = events.OfType<ExportGroupReceived>()
                .Where(export => export.Payload is Replay.Valorant.GameState.BombPlayerStateDescriptor p
                                 && p.HasDecoded(nameof(p.Subject)) && !string.IsNullOrEmpty(p.Subject))
                .Select(export => export.ActorNetGuid).Distinct().ToArray();
            var crosshairs = events.OfType<ValorantCrosshairUpdated>()
                .GroupBy(update => update.PlayerStateNetGuid)
                .ToDictionary(group => group.Key, group => group.Last().Settings);

            Assert.Multiple(() =>
            {
                Assert.That(withSubject, Is.Not.Empty, replay);
                // A player who never sent a crosshair is rare (21 of 5149 across 536 replays); allow one.
                Assert.That(withSubject.Count(guid => !crosshairs.ContainsKey(guid)), Is.LessThanOrEqualTo(1), replay);
                Assert.That(events.OfType<ValorantCrosshairUpdated>().Where(update => update.IsPartial), Is.Empty, replay);
                foreach (var settings in crosshairs.Values)
                {
                    Assert.That(settings.IsUnset, Is.False, replay);
                    Assert.That(settings.Primary.InnerLines.LineThickness, Is.InRange(0f, 10f), replay);
                    Assert.That(settings.Primary.InnerLines.LineLength, Is.InRange(0f, 20f), replay);
                    Assert.That(settings.Primary.OuterLines.LineOffset, Is.InRange(0f, 40f), replay);
                    Assert.That(settings.Primary.OutlineOpacity, Is.InRange(0f, 1f), replay);
                }
            });
        }
    }

    private static byte[] ReadBytes(string replay) => TestHelper.ReadReplayBytes(replay);

    private static CrosshairSettings LastCrosshair(byte[] replay) =>
        Read(replay).OfType<ValorantCrosshairUpdated>().Last().Settings;

    private static List<ReplayEvent> Read(byte[] replay)
    {
        var sink = new CapturingSink();
        var events = new ValorantEventEnricher(sink);
        new ValorantReplayReader(eventSink: events).Read(new FBinaryArchive(replay));
        events.Complete();
        return sink.Events;
    }

    private sealed class CapturingSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
