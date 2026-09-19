using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Descriptors;

public sealed class OwnerExclusivePlayerInfoDescriptor : ExportGroupDescriptor<OwnerExclusivePlayerInfoDescriptor>
{
    public override string Path => "/Script/ShooterGame.OwnerExclusivePlayerInfo";

    public uint? Owner { get; set; }
    public AresPlayerRoundInfo[]? RoundInfos { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.RoundInfos).Decode(new CompatibleAresPlayerRoundInfoDecoder());
    }
}
