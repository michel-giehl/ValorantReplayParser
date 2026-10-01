using Replay.Encoding.Net;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Inventory;

internal sealed class ValorantInventoryEventEnricher(IReplayEventSink inner, NetGuidCache netGuidCache)
    : IReplayEventSink
{
    private readonly Dictionary<uint, InventoryState> _inventories = [];
    private readonly Dictionary<uint, uint?[]> _contentsBySlot = [];
    private readonly Dictionary<uint, uint> _actorBySlot = [];
    private readonly Dictionary<uint, uint> _playerByCharacter = [];
    private readonly Dictionary<uint, string?> _subjectByPlayer = [];
    // Keep resolved metadata for this replay: inventory references can outlive the item actor.
    private readonly Dictionary<uint, ValorantEquippable> _items = [];
    private readonly Dictionary<uint, ValorantItemPurchaseInfoChanged> _purchases = [];

    public void Emit(ReplayEvent replayEvent)
    {
        inner.Emit(replayEvent);
        switch (replayEvent)
        {
            case ActorSpawned spawned:
                if (ValorantEquippableResolver.TryResolve(spawned.ActorNetGuid,
                        [spawned.ReplicationClassPath, spawned.ArchetypePath, spawned.ActorPath], out var item))
                {
                    _items[spawned.ActorNetGuid] = item;
                    PublishAll(spawned);
                }
                break;
            case ExportGroupReceived { IsDeleted: true } deleted:
                RemoveObject(deleted.ObjectNetGuid, deleted);
                break;
            case ExportGroupReceived { WasDecoded: true } export:
                TrackExport(export);
                break;
            case ActorClosed { Reason: ChannelCloseReason.Destroyed } closed:
                foreach (var state in _inventories.Values.Where(x => x.ActorNetGuid == closed.ActorNetGuid).ToArray())
                    RemoveObject(state.InventoryNetGuid, closed);
                foreach (var slot in _actorBySlot.Where(x => x.Value == closed.ActorNetGuid).Select(x => x.Key).ToArray())
                {
                    _contentsBySlot.Remove(slot);
                    _actorBySlot.Remove(slot);
                }
                foreach (var purchase in _purchases.Where(x => x.Value.ItemNetGuid == closed.ActorNetGuid).Select(x => x.Key).ToArray())
                    _purchases.Remove(purchase);
                _playerByCharacter.Remove(closed.ActorNetGuid);
                _subjectByPlayer.Remove(closed.ActorNetGuid);
                foreach (var character in _playerByCharacter.Where(x => x.Value == closed.ActorNetGuid).Select(x => x.Key).ToArray())
                    _playerByCharacter.Remove(character);
                PublishAll(closed);
                break;
        }
    }

    private void TrackExport(ExportGroupReceived export)
    {
        var objectGuid = export.ObjectNetGuid != 0 ? export.ObjectNetGuid : export.ActorNetGuid;
        switch (export.Payload)
        {
            case BombPlayerStateDescriptor player:
                if (player.HasDecoded(nameof(player.Subject))) _subjectByPlayer[export.ActorNetGuid] = player.Subject;
                if (player.HasDecoded(nameof(player.SpawnedCharacter)) && player.SpawnedCharacter != 0)
                    _playerByCharacter[player.SpawnedCharacter] = export.ActorNetGuid;
                if (player.HasDecoded(nameof(player.PossessedCharacter)) && player.PossessedCharacter != 0)
                    _playerByCharacter[player.PossessedCharacter] = export.ActorNetGuid;
                PublishAll(export);
                break;
            case GenericAgentDescriptor agent when agent.HasDecoded(nameof(agent.PlayerState)):
                if (agent.PlayerState == 0) _playerByCharacter.Remove(export.ActorNetGuid);
                else _playerByCharacter[export.ActorNetGuid] = agent.PlayerState;
                PublishAll(export);
                break;
            case AresInventoryDescriptor inventory:
                TrackInventory(objectGuid, export, inventory);
                break;
            case ItemSlotDescriptor slot when slot.HasDecoded(nameof(slot.Contents)):
                _actorBySlot[objectGuid] = export.ActorNetGuid;
                _contentsBySlot[objectGuid] = slot.Contents is > 0 ? [slot.Contents] : [];
                PublishAll(export);
                break;
            case MultiItemSlotDescriptor slot when slot.HasDecoded(nameof(slot.MultiContents)) && slot.MultiContents is { } delta:
                _actorBySlot[objectGuid] = export.ActorNetGuid;
                var contents = _contentsBySlot.GetValueOrDefault(objectGuid) ?? [];
                Array.Resize(ref contents, delta.Count);
                foreach (var update in delta.Updates) contents[update.Index] = update.Contents;
                _contentsBySlot[objectGuid] = contents;
                PublishAll(export);
                break;
            case PurchasedItemComponentDescriptor purchase:
                TrackPurchase(objectGuid, export, purchase);
                break;
        }
    }

    private void TrackInventory(uint guid, ExportGroupReceived export, AresInventoryDescriptor delta)
    {
        if (guid == 0 || delta.DecodedProperties.Count == 0) return;
        if (!_inventories.TryGetValue(guid, out var state))
        {
            state = new InventoryState(guid, export.ActorNetGuid);
            _inventories.Add(guid, state);
        }
        if (delta.HasDecoded(nameof(delta.Character))) state.CharacterNetGuid = delta.Character;
        if (delta.HasDecoded(nameof(delta.IsActive))) state.IsActive = delta.IsActive;
        if (delta.HasDecoded(nameof(delta.NewCurrentEquippable)))
        {
            state.SelectedEquippable = delta.NewCurrentEquippable;
            state.SelectedEquippableEvidence = ValorantInventoryEquippableEvidence.ReplicatedEquippableChange;
        }
        if (delta.HasDecoded(nameof(delta.CurrentEquippable)))
        {
            state.SelectedEquippable = delta.CurrentEquippable;
            state.SelectedEquippableEvidence = ValorantInventoryEquippableEvidence.ServerCorrection;
        }
        if (delta.HasDecoded(nameof(delta.RespawnNumber))) state.RespawnNumber = delta.RespawnNumber;
        if (delta.HasDecoded(nameof(delta.NetTimestamp))) state.NetTimestamp = delta.NetTimestamp;
        foreach (var (slot, slotGuid) in delta.GetDecodedItemSlots()) state.Slots[slot] = slotGuid;
        Publish(state, export);
    }

    private void TrackPurchase(uint guid, ExportGroupReceived export, PurchasedItemComponentDescriptor delta)
    {
        var previous = _purchases.GetValueOrDefault(guid);
        var next = new ValorantItemPurchaseInfoChanged(export.TimeSeconds, export.PacketId, export.ActorNetGuid, guid,
            delta.HasDecoded(nameof(delta.Purchaseable)) ? delta.Purchaseable : previous?.PurchaseableNetGuid,
            delta.HasDecoded(nameof(delta.IsCurrentSessionPurchase)) ? delta.IsCurrentSessionPurchase : previous?.IsCurrentSessionPurchase,
            delta.HasDecoded(nameof(delta.PurchasingPlayerState)) ? delta.PurchasingPlayerState : previous?.PurchasingPlayerStateNetGuid,
            delta.HasDecoded(nameof(delta.PurchasableTransactionSource)) ? delta.PurchasableTransactionSource : previous?.TransactionSource);
        if (previous is not null && next with { TimeSeconds = previous.TimeSeconds, PacketId = previous.PacketId } == previous) return;
        if (delta.DecodedProperties.Count == 0) return;
        _purchases[guid] = next;
        inner.Emit(next);
    }

    private void RemoveObject(uint guid, ReplayEvent source)
    {
        if (_inventories.Remove(guid, out var state))
            inner.Emit(new ValorantInventoryRemoved(source.TimeSeconds, source.PacketId, guid, state.CharacterNetGuid));
        _contentsBySlot.Remove(guid);
        _actorBySlot.Remove(guid);
        _purchases.Remove(guid);
        PublishAll(source);
    }

    private void PublishAll(ReplayEvent source)
    {
        foreach (var state in _inventories.Values) Publish(state, source);
    }

    private void Publish(InventoryState state, ReplayEvent source)
    {
        uint? player = _playerByCharacter.TryGetValue(state.CharacterNetGuid, out var found) ? found : null;
        var slots = state.Slots.OrderBy(x => x.Key).Select(pair =>
        {
            IReadOnlyList<ValorantInventoryItem>? items = pair.Value == 0 ? Array.Empty<ValorantInventoryItem>() : null;
            if (_contentsBySlot.TryGetValue(pair.Value, out var contents))
                items = Array.AsReadOnly(contents.Select((guid, index) =>
                    new ValorantInventoryItem(index, guid, ResolveItem(guid))).ToArray());
            return new ValorantInventorySlot(pair.Key, pair.Value, items);
        }).ToArray();
        var snapshot = new ValorantInventory(state.InventoryNetGuid, state.CharacterNetGuid, player,
            player is { } playerGuid ? _subjectByPlayer.GetValueOrDefault(playerGuid) : null,
            state.IsActive, state.SelectedEquippable, ResolveItem(state.SelectedEquippable), state.SelectedEquippableEvidence,
            state.RespawnNumber, state.NetTimestamp, Array.AsReadOnly(slots));
        if (state.LastSnapshot is { } previous && SameSnapshot(snapshot, previous)) return;
        state.LastSnapshot = snapshot;
        inner.Emit(new ValorantInventoryChanged(source.TimeSeconds, source.PacketId, snapshot));
    }

    private ValorantEquippable? ResolveItem(uint? guid)
    {
        if (guid is null or 0) return null;
        return _items.GetValueOrDefault(guid.Value) ?? ValorantEquippableResolver.Resolve(guid.Value, netGuidCache);
    }

    private static bool SameSnapshot(ValorantInventory left, ValorantInventory right)
    {
        if (left with { Slots = right.Slots } != right || left.Slots.Count != right.Slots.Count) return false;
        for (var index = 0; index < left.Slots.Count; index++)
        {
            var a = left.Slots[index];
            var b = right.Slots[index];
            if (a with { Items = b.Items } != b) return false;
            if (a.Items is null || b.Items is null)
            {
                if (a.Items != b.Items) return false;
            }
            else if (!a.Items.SequenceEqual(b.Items)) return false;
        }
        return true;
    }

    private sealed class InventoryState(uint inventoryNetGuid, uint actorNetGuid)
    {
        public uint InventoryNetGuid { get; } = inventoryNetGuid;
        public uint ActorNetGuid { get; } = actorNetGuid;
        public uint CharacterNetGuid { get; set; } = actorNetGuid;
        public bool? IsActive { get; set; }
        public uint? SelectedEquippable { get; set; }
        public ValorantInventoryEquippableEvidence? SelectedEquippableEvidence { get; set; }
        public int? RespawnNumber { get; set; }
        public float? NetTimestamp { get; set; }
        public Dictionary<EAresItemSlot, uint> Slots { get; } = [];
        public ValorantInventory? LastSnapshot { get; set; }
    }
}
