using Replay.Valorant.Economy;
using Replay.Valorant.Inventory;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Models.Events;
using Replay.Valorant;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Test.Integration;

[Category("Integration")]
public class InventoryEconomyIntegrationTests
{
    private const string ReplayFileName = "450cf6ad-c541-4785-8529-44b442db52b6.vrf";

    [TestCase(ExportCategory.All)]
    [TestCase(ExportCategory.Inventory)]
    [TestCase(ExportCategory.Economy)]
    public void SuppliedMatchReplay_EmitsObservedInventoryAndEconomy(ExportCategory categories)
    {
        var replayPath = Environment.GetEnvironmentVariable("VALORANT_INVENTORY_ECONOMY_REPLAY_PATH");
        if (string.IsNullOrWhiteSpace(replayPath))
            replayPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VALORANT", "Saved", "Demos", ReplayFileName);
        if (!File.Exists(replayPath))
            Assert.Ignore($"Inventory/economy replay not found. Set VALORANT_INVENTORY_ECONOMY_REPLAY_PATH or supply {ReplayFileName}.");

        var sink = new InventoryEconomySink();
        using var stream = File.OpenRead(replayPath);
        var result = new ValorantReplayReader(eventSink: sink,
            parseProfile: new ParseProfile { EnabledCategories = categories }).Read(stream);

        Assert.Multiple(() =>
        {
            Assert.That(result.PacketStats.MalformedPacketCount, Is.Zero);
            Assert.That(result.BunchPayloadStats.MalformedPayloadCount, Is.Zero);
            Assert.That(result.Diagnostics.Any(x => x.Code == ReplayDiagnosticCode.RawPayloadFallback &&
                x.ExportGroupPath is "/Script/ShooterGame.AresInventory" or "/Script/ShooterGame.MultiItemSlot" or
                    "/Script/ShooterGame.OwnerExclusivePlayerInfo"), Is.False);
            Assert.That(sink.RequestCount, Is.Positive);
            Assert.That(sink.PurchaseCount, Is.Positive);
            if (categories.HasFlag(ExportCategory.Inventory))
            {
                Assert.That(sink.InventoryCount, Is.Positive);
            }
            if (categories.HasFlag(ExportCategory.Economy))
            {
                Assert.That(sink.CreditCount, Is.Positive);
                Assert.That(sink.RoundLoadoutCount, Is.Positive);
            }
        });
    }

    private sealed class InventoryEconomySink : IReplayEventSink
    {
        public int InventoryCount { get; private set; }
        public int CreditCount { get; private set; }
        public int RoundLoadoutCount { get; private set; }
        public int RequestCount { get; private set; }
        public int PurchaseCount { get; private set; }

        public void Emit(ReplayEvent replayEvent)
        {
            switch (replayEvent)
            {
                case ExportGroupReceived { Payload: AresInventoryDescriptor inventory } when inventory.DecodedProperties.Count > 0:
                    InventoryCount++;
                    break;
                case ExportGroupReceived { IsDeleted: false, Payload: MoneyManagementComponentDescriptor money }
                    when money.HasDecoded(nameof(MoneyManagementComponentDescriptor.Money)):
                    CreditCount++;
                    break;
                case ExportGroupReceived { IsDeleted: false, Payload: OwnerExclusivePlayerInfoDescriptor info }
                    when info.HasDecoded(nameof(OwnerExclusivePlayerInfoDescriptor.RoundInfos)):
                    if (info.RoundInfos?.Any(round => round.StartOfRoundLoadoutValue is not null ||
                            round.EndOfRoundLoadoutValue is not null) == true) RoundLoadoutCount++;
                    break;
                case RpcReceived { Payload: IGunRequestParameters }:
                    RequestCount++;
                    break;
                case ExportGroupReceived { IsDeleted: false, Payload: PurchasedItemComponentDescriptor purchase } when purchase.DecodedProperties.Count > 0:
                    PurchaseCount++;
                    break;
            }
        }
    }
}
