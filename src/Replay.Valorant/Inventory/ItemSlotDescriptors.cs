using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Inventory;

public sealed class ItemSlotDescriptor : ExportGroupDescriptor<ItemSlotDescriptor>
{
    public override string Path => "/Script/ShooterGame.ItemSlot";
    public override ExportCategory Categories => ExportCategory.Inventory;

    public uint? Contents { get; set; }

    protected override void Configure() =>
        AddPropertyHandle(0, x => x.Contents).ObjectNetGuid();
}

public sealed class MultiItemSlotDescriptor : ExportGroupDescriptor<MultiItemSlotDescriptor>
{
    public override string Path => "/Script/ShooterGame.MultiItemSlot";
    public override ExportCategory Categories => ExportCategory.Inventory;

    public ItemSlotContentsUpdate? MultiContents { get; set; }

    protected override void Configure() =>
        AddPropertyHandle(1, x => x.MultiContents).Decode(new ItemSlotContentsDecoder());
}
