namespace Replay.Valorant.Inventory;

/// <summary>VALORANT's inventory slots, in their replicated ItemSlots array order.</summary>
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

    /// <summary>Slot appended in 13.09; its game name is unknown and it stayed empty in every observed replay.</summary>
    Unnamed1309 = 16,
    Count = 17,
    Any = 253,
    Invalid = 254,
}
