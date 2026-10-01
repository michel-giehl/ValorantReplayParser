namespace Replay.Valorant.Inventory;

/// <summary>VALORANT's 16 inventory slots, in their replicated ItemSlots array order.</summary>
public enum EAresItemSlot : byte
{
    Primary = 0,
    Secondary = 1,
    Melee = 2,
    GrenadeAbility = 3,
    Ability1 = 4,
    Ability2 = 5,
    Passive = 6,
    Level = 7,
    Invisible = 8,
    Ultimate = 9,
    Unarmed = 10,
    Armor = 11,
    Backpack = 12,
    Totem = 13,
    PrimaryStorage = 14,
    SecondaryStorage = 15,
    Count = 16,
    Any = 253,
    Invalid = 254,
}
