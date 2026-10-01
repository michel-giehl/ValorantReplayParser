namespace Replay.Valorant.Inventory;

public enum EInventoryTransaction : byte
{
    Purchase = 0,
    PickUp = 1,
    FulfillRequest = 2,
    RefundRequest = 3,
    Drop = 4,
    Sell = 5,
    Transfer = 6,
    Trash = 7,
    Store = 8,
    Retrieve = 9,
    Default = 10,
    Other = 11,
    OtherGrant = 12,
    OtherRemove = 13,
    DefaultGrantStartOfInRound = 14,
    Count = 15,
}
