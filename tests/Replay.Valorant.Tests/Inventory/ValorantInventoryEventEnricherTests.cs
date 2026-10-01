using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Valorant.GameState;
using Replay.Valorant.Inventory;

namespace Replay.Valorant.Tests.Inventory;

public class ValorantInventoryEventEnricherTests
{
    [Test]
    public void SlotBeforeInventory_ResolvesContentsAndLatePlayerIdentity()
    {
        var sink = new Sink();
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(500, "/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C");
        var enricher = new ValorantInventoryEventEnricher(sink, cache);
        enricher.Emit(Export(20, 102, Decoded(new ItemSlotDescriptor { Contents = 500 }, "Contents")));
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { PrimarySlot = 102, CurrentEquippable = 500 }, "PrimarySlot", "CurrentEquippable")));
        var first = sink.Inventories.Single();
        enricher.Emit(Export(10, 10, Decoded(new BombPlayerStateDescriptor { Subject = "player", PossessedCharacter = 20 }, "Subject", "PossessedCharacter")));
        var last = sink.Inventories.Last();
        Assert.Multiple(() =>
        {
            Assert.That(first.InventoryNetGuid, Is.EqualTo(100));
            Assert.That(first.CharacterNetGuid, Is.EqualTo(20));
            Assert.That(first.PlayerStateNetGuid, Is.Null);
            Assert.That(first.SelectedEquippable!.Name, Is.EqualTo("Vandal"));
            Assert.That(first.Slots.Single().Items!.Single().Equippable!.Name, Is.EqualTo("Vandal"));
            Assert.That(last.PlayerStateNetGuid, Is.EqualTo(10));
            Assert.That(last.Subject, Is.EqualTo("player"));
            Assert.That(first.PlayerStateNetGuid, Is.Null, "Snapshots must remain detached from later state");
        });
    }

    [Test]
    public void PartialInventoryUpdate_PreservesUnsentFieldsAndExplicitClears()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { IsActive = true, CurrentEquippable = 500, PrimarySlot = 102 }, "IsActive", "CurrentEquippable", "PrimarySlot")));
        var first = sink.Inventories.Single();
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { CurrentEquippable = 0, SecondarySlot = 0 }, "CurrentEquippable", "SecondarySlot")));
        var next = sink.Inventories.Last();
        Assert.Multiple(() =>
        {
            Assert.That(next.IsActive, Is.True);
            Assert.That(next.SelectedEquippableNetGuid, Is.Zero);
            Assert.That(next.SelectedEquippable, Is.Null);
            Assert.That(next.Slots[0].Items, Is.Null, "Unobserved contents are unknown");
            Assert.That(next.Slots[1].Items, Is.Empty, "A cleared slot is known empty");
            Assert.That(first.Slots, Has.Count.EqualTo(1));
            Assert.That(first.SelectedEquippableNetGuid, Is.EqualTo(500));
        });
    }

    [Test]
    public void SparseMultiSlotUpdates_PreserveEntriesAndShrinkToDeclaredCount()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { BackpackSlot = 102 }, "BackpackSlot")));
        enricher.Emit(Export(20, 102, Multi(3, new(0, 500), new(2, 502))));
        var before = sink.Inventories.Last().Slots.Single().Items!;
        enricher.Emit(Export(20, 102, Multi(3, new ItemSlotContentUpdate(1, 501))));
        var merged = sink.Inventories.Last().Slots.Single().Items!;
        enricher.Emit(Export(20, 102, Multi(1)));
        var shrunk = sink.Inventories.Last().Slots.Single().Items!;
        enricher.Emit(Export(20, 102, Multi(0)));
        Assert.Multiple(() =>
        {
            Assert.That(before.Select(x => x.ItemNetGuid), Is.EqualTo(new uint?[] { 500, null, 502 }));
            Assert.That(merged.Select(x => x.ItemNetGuid), Is.EqualTo(new uint?[] { 500, 501, 502 }));
            Assert.That(shrunk.Single().ItemNetGuid, Is.EqualTo(500));
            Assert.That(sink.Inventories.Last().Slots.Single().Items, Is.Empty);
        });
    }

    [Test]
    public void UndecodedDefaultsAndDuplicateValues_DoNotChangeState()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        var update = Export(20, 100, Decoded(new AresInventoryDescriptor { CurrentEquippable = 500 }, "CurrentEquippable"));
        enricher.Emit(update);
        enricher.Emit(update);
        enricher.Emit(Export(20, 100, new AresInventoryDescriptor()));
        Assert.Multiple(() =>
        {
            Assert.That(sink.Inventories, Has.Count.EqualTo(1));
            Assert.That(sink.Events.OfType<ExportGroupReceived>().ToArray(), Has.Length.EqualTo(3));
            Assert.That(sink.Inventories.Single().SelectedEquippableNetGuid, Is.EqualTo(500));
            Assert.That(sink.Inventories.Single().IsActive, Is.Null);
        });
    }

    [Test]
    public void InitialEmptyPayload_DoesNotPublishUnknownInventory()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Export(20, 100, new AresInventoryDescriptor()));
        Assert.That(sink.Inventories, Is.Empty);
    }

    [Test]
    public void DestroyedPlayerState_ClearsIdentityFromSurvivingInventory()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Export(10, 10, Decoded(new BombPlayerStateDescriptor { Subject = "player", PossessedCharacter = 20 }, "Subject", "PossessedCharacter")));
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { PrimarySlot = 102 }, "PrimarySlot")));
        enricher.Emit(new ActorClosed(1, 2, 10, 1, ChannelCloseReason.Destroyed));
        Assert.Multiple(() =>
        {
            Assert.That(sink.Inventories.Last().PlayerStateNetGuid, Is.Null);
            Assert.That(sink.Inventories.Last().Subject, Is.Null);
        });
    }

    [Test]
    public void ReplicatedSelectionAndCorrection_RecordLatestEvidenceAndClear()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { NewCurrentEquippable = 500 }, "NewCurrentEquippable")));
        var selected = sink.Inventories.Last();
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { CurrentEquippable = 501 }, "CurrentEquippable")));
        var corrected = sink.Inventories.Last();
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { NewCurrentEquippable = 0 }, "NewCurrentEquippable")));
        Assert.Multiple(() =>
        {
            Assert.That(selected.SelectedEquippableNetGuid, Is.EqualTo(500));
            Assert.That(selected.SelectedEquippableEvidence, Is.EqualTo(ValorantInventoryEquippableEvidence.ReplicatedEquippableChange));
            Assert.That(corrected.SelectedEquippableNetGuid, Is.EqualTo(501));
            Assert.That(corrected.SelectedEquippableEvidence, Is.EqualTo(ValorantInventoryEquippableEvidence.ServerCorrection));
            Assert.That(sink.Inventories.Last().SelectedEquippableNetGuid, Is.Zero);
        });
    }

    [Test]
    public void DestroyedActor_ClearsChildSlotContentsBeforeReusedReferences()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Export(20, 102, Decoded(new ItemSlotDescriptor { Contents = 500 }, "Contents")));
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { PrimarySlot = 102 }, "PrimarySlot")));
        enricher.Emit(new ActorClosed(1, 2, 20, 1, ChannelCloseReason.Destroyed));
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { PrimarySlot = 102 }, "PrimarySlot")));
        Assert.That(sink.Inventories.Last().Slots.Single().Items, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DestroyedItem_PreservesResolvedMetadataUntilInventoryReferencesClear(bool multiSlot)
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(new ActorSpawned(0, 0, 500, 1, true, null, 0, null,
            "/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C",
            0, null, null, null, null));
        enricher.Emit(Export(20, 102, multiSlot
            ? Multi(1, new ItemSlotContentUpdate(0, 500))
            : Decoded(new ItemSlotDescriptor { Contents = 500 }, "Contents")));
        enricher.Emit(Export(20, 100, Decoded(
            new AresInventoryDescriptor { PrimarySlot = 102, CurrentEquippable = 500 },
            "PrimarySlot", "CurrentEquippable")));
        var first = sink.Inventories.Single();

        enricher.Emit(new ActorClosed(1, 2, 500, 1, ChannelCloseReason.Destroyed));

        Assert.That(sink.Inventories, Has.Count.EqualTo(1), "Actor destruction must not erase known item metadata");
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { IsActive = true }, "IsActive")));
        var next = sink.Inventories.Last();
        Assert.Multiple(() =>
        {
            Assert.That(first.SelectedEquippable!.Name, Is.EqualTo("Vandal"));
            Assert.That(next.SelectedEquippableNetGuid, Is.EqualTo(500));
            Assert.That(next.SelectedEquippable, Is.EqualTo(first.SelectedEquippable));
            Assert.That(next.Slots.Single().Items!.Single().ItemNetGuid, Is.EqualTo(500));
            Assert.That(next.Slots.Single().Items!.Single().Equippable, Is.EqualTo(first.SelectedEquippable));
        });

        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { CurrentEquippable = 0 }, "CurrentEquippable")));
        enricher.Emit(Export(20, 102, multiSlot
            ? Multi(0)
            : Decoded(new ItemSlotDescriptor { Contents = 0 }, "Contents")));
        var cleared = sink.Inventories.Last();
        Assert.Multiple(() =>
        {
            Assert.That(cleared.SelectedEquippableNetGuid, Is.Zero);
            Assert.That(cleared.SelectedEquippable, Is.Null);
            Assert.That(cleared.Slots.Single().Items, Is.Empty);
        });
    }

    [Test]
    public void Destruction_RemovesOnlyInventoriesOwnedByDestroyedActor()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Export(20, 100, Decoded(new AresInventoryDescriptor { PrimarySlot = 102 }, "PrimarySlot")));
        enricher.Emit(Export(30, 200, Decoded(new AresInventoryDescriptor { PrimarySlot = 202 }, "PrimarySlot")));
        enricher.Emit(new ActorClosed(1, 2, 20, 1, ChannelCloseReason.Destroyed));
        enricher.Emit(Export(20, 102, Decoded(new ItemSlotDescriptor { Contents = 500 }, "Contents")));
        Assert.Multiple(() =>
        {
            Assert.That(sink.Events.OfType<ValorantInventoryRemoved>().Single().InventoryNetGuid, Is.EqualTo(100));
            Assert.That(sink.Inventories, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void PurchaseProvenance_MergesOnlyDecodedProperties()
    {
        var sink = new Sink();
        var enricher = new ValorantInventoryEventEnricher(sink, new NetGuidCache());
        enricher.Emit(Export(500, 501, Decoded(new PurchasedItemComponentDescriptor { Purchaseable = 80, PurchasingPlayerState = 10, IsCurrentSessionPurchase = true }, "Purchaseable", "PurchasingPlayerState", "IsCurrentSessionPurchase")));
        enricher.Emit(Export(500, 501, Decoded(new PurchasedItemComponentDescriptor { IsCurrentSessionPurchase = false }, "IsCurrentSessionPurchase")));
        var last = sink.Events.OfType<ValorantItemPurchaseInfoChanged>().Last();
        Assert.Multiple(() =>
        {
            Assert.That(last.ItemNetGuid, Is.EqualTo(500));
            Assert.That(last.PurchaseableNetGuid, Is.EqualTo(80));
            Assert.That(last.PurchasingPlayerStateNetGuid, Is.EqualTo(10));
            Assert.That(last.IsCurrentSessionPurchase, Is.False);
        });
    }

    private static MultiItemSlotDescriptor Multi(int count, params ItemSlotContentUpdate[] updates) =>
        Decoded(new MultiItemSlotDescriptor { MultiContents = new ItemSlotContentsUpdate(count, Array.AsReadOnly(updates)) }, "MultiContents");

    private static T Decoded<T>(T payload, params string[] properties) where T : ExportGroupDescriptor
    {
        foreach (var property in properties) payload.MarkDecoded(property);
        return payload;
    }

    private static ExportGroupReceived Export(uint actor, uint obj, ExportGroupDescriptor payload) => new(
        0, 0, actor, obj, 0, actor == obj, false, 0, payload.Path, payload.Kind, payload.Categories,
        0, 0, null, null, null, 0, 0, true, payload, payload.DecodedProperties.Count, []);

    private sealed class Sink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];
        public List<ValorantInventory> Inventories => Events.OfType<ValorantInventoryChanged>().Select(x => x.Inventory).ToList();
        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
