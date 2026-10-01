using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Economy;

public sealed class MoneyManagementComponentDescriptor
    : ExportGroupDescriptor<MoneyManagementComponentDescriptor>
{
    public const string ExportPath = "/Script/ShooterGame.MoneyManagementComponent";

    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.Economy;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public int? Money { get; set; }
    public int? StartOfRoundMoney { get; set; }
    public int? TotalMoneyGranted { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Money).Int32();
        AddProperty(x => x.StartOfRoundMoney).Int32();
        AddProperty(x => x.TotalMoneyGranted).Int32();
    }
}
