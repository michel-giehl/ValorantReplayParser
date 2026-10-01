using Replay.Models.Events;
using Replay.Valorant.Combat;

namespace Replay.Valorant.Inventory;

/// <summary>A known inventory entry. A null GUID means this array entry has not been observed.</summary>
public sealed record ValorantInventoryItem(int Index, uint? ItemNetGuid, ValorantEquippable? Equippable);

/// <summary>Items are null until the slot contents have been observed; an empty list is a known empty slot.</summary>
public sealed record ValorantInventorySlot(
    EAresItemSlot SlotType,
    uint SlotNetGuid,
    IReadOnlyList<ValorantInventoryItem>? Items);

public enum ValorantInventoryEquippableEvidence
{
    ReplicatedEquippableChange,
    ServerCorrection,
}

/// <summary>Detached inventory state merged from replicated deltas. Null scalars are unknown; GUID zero is a known clear.</summary>
public sealed record ValorantInventory(
    uint InventoryNetGuid,
    uint CharacterNetGuid,
    uint? PlayerStateNetGuid,
    string? Subject,
    bool? IsActive,
    uint? SelectedEquippableNetGuid,
    ValorantEquippable? SelectedEquippable,
    ValorantInventoryEquippableEvidence? SelectedEquippableEvidence,
    int? RespawnNumber,
    float? NetTimestamp,
    IReadOnlyList<ValorantInventorySlot> Slots);

public sealed record ValorantInventoryChanged(float TimeSeconds, int PacketId, ValorantInventory Inventory)
    : ReplayEvent(TimeSeconds, PacketId);

public sealed record ValorantInventoryRemoved(
    float TimeSeconds, int PacketId, uint InventoryNetGuid, uint CharacterNetGuid)
    : ReplayEvent(TimeSeconds, PacketId);

/// <summary>Replicated purchase provenance, rather than an inferred purchase from a credit change.</summary>
public sealed record ValorantItemPurchaseInfoChanged(
    float TimeSeconds,
    int PacketId,
    uint ItemNetGuid,
    uint PurchaseComponentNetGuid,
    uint? PurchaseableNetGuid,
    bool? IsCurrentSessionPurchase,
    uint? PurchasingPlayerStateNetGuid,
    EInventoryTransaction? TransactionSource)
    : ReplayEvent(TimeSeconds, PacketId);
