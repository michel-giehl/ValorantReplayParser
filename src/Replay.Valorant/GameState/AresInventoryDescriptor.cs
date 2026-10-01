using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Inventory;

namespace Replay.Valorant.GameState;

public sealed class AresInventoryDescriptor : ExportGroupDescriptor<AresInventoryDescriptor>
{
    public override string Path => "/Script/ShooterGame.AresInventory";
    public override ExportCategory Categories => ExportCategory.Inventory | ExportCategory.Gunplay;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public bool IsActive { get; set; }
    public uint? PrimarySlot { get; set; }
    public uint? SecondarySlot { get; set; }
    public uint? MeleeSlot { get; set; }
    public uint? GrenadeAbilitySlot { get; set; }
    public uint? Ability1Slot { get; set; }
    public uint? Ability2Slot { get; set; }
    public uint? PassiveSlot { get; set; }
    public uint? LevelSlot { get; set; }
    public uint? InvisibleSlot { get; set; }
    public uint? UltimateSlot { get; set; }
    public uint? UnarmedSlot { get; set; }
    public uint? ArmorSlot { get; set; }
    public uint? BackpackSlot { get; set; }
    public uint? TotemSlot { get; set; }
    public uint? PrimaryStorageSlot { get; set; }
    public uint? SecondaryStorageSlot { get; set; }
    public uint NewCurrentEquippable { get; set; }
    public uint Character { get; set; }
    public float NetTimestamp { get; set; }
    public int RespawnNumber { get; set; }
    public uint CurrentEquippable { get; set; }
    public int CorrectionIndex { get; set; }
    public int LastSeenClientCorrectionIndex { get; set; }

    /// <summary>Replicated slot references in EAresItemSlot order; null entries were absent from this update.</summary>
    public IReadOnlyList<uint?> ItemSlots =>
    [
        PrimarySlot, SecondarySlot, MeleeSlot, GrenadeAbilitySlot, Ability1Slot, Ability2Slot,
        PassiveSlot, LevelSlot, InvisibleSlot, UltimateSlot, UnarmedSlot, ArmorSlot,
        BackpackSlot, TotemSlot, PrimaryStorageSlot, SecondaryStorageSlot,
    ];

    public uint? GetItemSlot(EAresItemSlot slot) => slot switch
    {
        EAresItemSlot.Primary => PrimarySlot,
        EAresItemSlot.Secondary => SecondarySlot,
        EAresItemSlot.Melee => MeleeSlot,
        EAresItemSlot.GrenadeAbility => GrenadeAbilitySlot,
        EAresItemSlot.Ability1 => Ability1Slot,
        EAresItemSlot.Ability2 => Ability2Slot,
        EAresItemSlot.Passive => PassiveSlot,
        EAresItemSlot.Level => LevelSlot,
        EAresItemSlot.Invisible => InvisibleSlot,
        EAresItemSlot.Ultimate => UltimateSlot,
        EAresItemSlot.Unarmed => UnarmedSlot,
        EAresItemSlot.Armor => ArmorSlot,
        EAresItemSlot.Backpack => BackpackSlot,
        EAresItemSlot.Totem => TotemSlot,
        EAresItemSlot.PrimaryStorage => PrimaryStorageSlot,
        EAresItemSlot.SecondaryStorage => SecondaryStorageSlot,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Expected one of the 16 inventory slots."),
    };

    public IEnumerable<(EAresItemSlot Slot, uint? SlotNetGuid)> GetItemSlots()
    {
        for (var index = 0; index < (int)EAresItemSlot.Count; index++)
        {
            var slot = (EAresItemSlot)index;
            yield return (slot, GetItemSlot(slot));
        }
    }

    public IEnumerable<(EAresItemSlot Slot, uint SlotNetGuid)> GetDecodedItemSlots()
    {
        foreach (var (slot, netGuid) in GetItemSlots())
        {
            if (HasDecoded($"{slot}Slot") && netGuid is { } value) yield return (slot, value);
        }
    }

    protected override void Configure()
    {
        AddPropertyHandle(1, "bIsActive", x => x.IsActive, ExportCategory.Inventory).Bool();
        // UItemSlot*[16] is replicated as 16 independent fields with the same export name.
        AddPropertyHandle(2, "ItemSlots", x => x.PrimarySlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(3, "ItemSlots", x => x.SecondarySlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(4, "ItemSlots", x => x.MeleeSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(5, "ItemSlots", x => x.GrenadeAbilitySlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(6, "ItemSlots", x => x.Ability1Slot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(7, "ItemSlots", x => x.Ability2Slot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(8, "ItemSlots", x => x.PassiveSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(9, "ItemSlots", x => x.LevelSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(10, "ItemSlots", x => x.InvisibleSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(11, "ItemSlots", x => x.UltimateSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(12, "ItemSlots", x => x.UnarmedSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(13, "ItemSlots", x => x.ArmorSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(14, "ItemSlots", x => x.BackpackSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(15, "ItemSlots", x => x.TotemSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(16, "ItemSlots", x => x.PrimaryStorageSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(17, "ItemSlots", x => x.SecondaryStorageSlot, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(21, x => x.NewCurrentEquippable, ExportCategory.Inventory | ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(22, x => x.Character, ExportCategory.Inventory | ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(23, x => x.NetTimestamp, ExportCategory.Inventory).Float();
        AddPropertyHandle(24, x => x.RespawnNumber, ExportCategory.Inventory).Int32();
        AddPropertyHandle(29, x => x.CurrentEquippable, ExportCategory.Inventory | ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(30, x => x.CorrectionIndex, ExportCategory.Inventory).Int32();
        AddPropertyHandle(31, x => x.LastSeenClientCorrectionIndex, ExportCategory.Inventory).Int32();
    }
}
