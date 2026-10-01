using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Descriptors;

public sealed class OwnerExclusivePlayerInfoDescriptor : ExportGroupDescriptor<OwnerExclusivePlayerInfoDescriptor>
{
    public override string Path => "/Script/ShooterGame.OwnerExclusivePlayerInfo";
    public override ExportCategory Categories => ExportCategory.Economy;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public uint? Owner { get; set; }
    public uint? AresController { get; set; }
    public int? EndOfRoundBeforeRewardsMoney { get; set; }
    public bool? LoadoutFinalized { get; set; }
    public AresPlayerRoundInfo[]? RoundInfos { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.AresController).ObjectNetGuid();
        AddProperty(x => x.EndOfRoundBeforeRewardsMoney).Int32();
        AddProperty("bLoadoutFinalized", x => x.LoadoutFinalized).Bool();
        AddProperty(x => x.RoundInfos).Decode(new AresPlayerRoundInfoDecoder());
    }
}
