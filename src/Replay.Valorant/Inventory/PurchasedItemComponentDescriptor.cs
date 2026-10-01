using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Inventory;

public sealed class PurchasedItemComponentDescriptor : ExportGroupDescriptor<PurchasedItemComponentDescriptor>
{
    public override string Path => "/Script/ShooterGame.PurchasedItemComponent";
    public override ExportCategory Categories => ExportCategory.Inventory | ExportCategory.Economy;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public uint? Purchaseable { get; set; }
    public bool? IsCurrentSessionPurchase { get; set; }
    public uint? PurchasingPlayerState { get; set; }
    public EInventoryTransaction? PurchasableTransactionSource { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(2, x => x.Purchaseable).ObjectNetGuid();
        AddPropertyHandle(3, "bIsCurrentSessionPurchase", x => x.IsCurrentSessionPurchase).Bool();
        AddPropertyHandle(4, x => x.PurchasingPlayerState).ObjectNetGuid();
        AddPropertyHandle(5, x => x.PurchasableTransactionSource).EnumRemainingBits();
    }
}
