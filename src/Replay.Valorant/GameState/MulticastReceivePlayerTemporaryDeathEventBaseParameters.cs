using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;

namespace Replay.Valorant.GameState;

public sealed class MulticastReceivePlayerTemporaryDeathEventBaseParameters : DamageParameters<MulticastReceivePlayerTemporaryDeathEventBaseParameters>
{
    public MulticastReceivePlayerTemporaryDeathEventBaseParameters()
        : this(hasKillCreditPlayerState: true)
    {
    }

    public MulticastReceivePlayerTemporaryDeathEventBaseParameters(bool hasKillCreditPlayerState, bool hasDamageIcon = false)
        : base(hasKillCreditPlayerState, hasDamageIcon)
    {
    }

    public override string Path => "/Script/ShooterGame.ShooterGameState:MulticastReceivePlayerTemporaryDeathEvent_Base";
    public override ExportCategory Categories => ExportCategory.GameState | ExportCategory.Gunplay;
    public NetObjectReference? DamagerPlayer { get; set; }
    public NetObjectReference? DownedPlayer { get; set; }
    public NetObjectReference? TemporaryDeathEquippableUsed { get; set; }
    public bool? RecoversInstantly { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.DamagerPlayer).ObjectNetGuid();
        AddPropertyHandle(1, x => x.DownedPlayer).ObjectNetGuid();
        AddSharedFields(2);
        AddPropertyHandle(LayoutHandle(28, 2), "EquippableUsed", x => x.TemporaryDeathEquippableUsed).ObjectNetGuid();
        AddPropertyHandle(LayoutHandle(29, 2), "bRecoversInstantly", x => x.RecoversInstantly).Bool();
    }
}
