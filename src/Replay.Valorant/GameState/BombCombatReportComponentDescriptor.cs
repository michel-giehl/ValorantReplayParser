using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Replay;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

public sealed class BombCombatReportComponentDescriptor : ExportGroupDescriptor<BombCombatReportComponentDescriptor>
{
    public override string Path => "/Game/GameModes/Bomb/Bomb_CombatReportComponent.Bomb_CombatReportComponent_C";
    public override ExportCategory Categories => ExportCategory.GameState | ExportCategory.Gunplay;
    public override ExportGroupKind Kind => ExportGroupKind.Component;

    public CombatReportArrayUpdate<CombatRoundReportUpdate>? Rounds { get; set; }
    public int RoundNum { get; set; }
    public CombatReportArrayUpdate<CharacterCombatReportUpdate>? Reports { get; set; }
    public int RoundNumber { get; set; }
    public float StateRemainingTime { get; set; }
    public float GameTime { get; set; }
    public byte GamePhase { get; set; }
    public CombatReportArrayUpdate<ParticipantInteractionUpdate>? Interactions { get; set; }
    public string? ParticipantSubject { get; set; }
    public string? ParticipantTeamName { get; set; }
    public NetObjectReference ParticipantCharacterIcon { get; set; }
    public float DamageDealt { get; set; }
    public int HitsDealt { get; set; }
    public float DamageRecieved { get; set; }
    public int HitsRecieved { get; set; }
    public bool DidKill { get; set; }
    public byte AssistType { get; set; }
    public NetObjectReference ParticipantsKillerState { get; set; }
    public bool WasKiller { get; set; }
    public CombatReportArrayUpdate<CombatInteractionUpdate>? DealtIteractions { get; set; }
    public NetObjectReference DamageType { get; set; }
    public NetObjectReference DamageIcon { get; set; }
    public CombatReportArrayUpdate<RegionalDamageInteractionUpdate>? RegionalDamageInteractions { get; set; }
    public byte Region { get; set; }
    public int Hits { get; set; }
    public float Damage { get; set; }
    public bool IsWallPen { get; set; }
    public bool IsKill { get; set; }
    public NetObjectReference DestroyedArmor { get; set; }
    public CombatReportArrayUpdate<CombatInteractionUpdate>? ReceivedInteractions { get; set; }
    public int CombatReportIndex { get; set; }
    public NetObjectReference ResurrectorPlayerState { get; set; }
    public bool Died { get; set; }
    public FVector? DeathLocation { get; set; }

    protected override void Configure()
    {
        var decoders = new VersionedDefinition<CombatRoundReportsDecoder>(new())
            .From(new ReplayReleaseVersion(13, 10), new(CombatReportHandles.Release1310));
        AddProperty(x => x.Rounds).Decode(decoders);
        AddProperty(x => x.RoundNum).Int32();
        AddProperty(x => x.Reports).Decode(decoders.Select(decoder => decoder.Reports));
        AddProperty(x => x.RoundNumber).Int32();
        AddProperty(x => x.StateRemainingTime).Float();
        AddProperty(x => x.GameTime).Float();
        AddProperty(x => x.GamePhase).EnumByte();
        AddProperty(x => x.Interactions).Decode(decoders.Select(decoder => decoder.Interactions));
        AddProperty(x => x.ParticipantSubject).FString();
        AddProperty(x => x.ParticipantTeamName).FName();
        AddProperty(x => x.ParticipantCharacterIcon).ObjectNetGuid();
        AddProperty(x => x.DamageDealt).Float();
        AddProperty(x => x.HitsDealt).Int32();
        AddProperty(x => x.DamageRecieved).Float();
        AddProperty(x => x.HitsRecieved).Int32();
        AddProperty("bDidKill", x => x.DidKill).Bool();
        AddProperty(x => x.AssistType).EnumByte();
        AddProperty(x => x.ParticipantsKillerState).ObjectNetGuid();
        AddProperty("bWasKiller", x => x.WasKiller).Bool();
        AddProperty(x => x.DealtIteractions).Decode(decoders.Select(decoder => decoder.DealtInteractions));
        AddProperty(x => x.DamageType).ObjectNetGuid();
        AddProperty(x => x.DamageIcon).ObjectNetGuid();
        AddProperty(x => x.RegionalDamageInteractions)
            .Decode(decoders.Select(decoder => decoder.RegionalInteractions));
        AddProperty(x => x.Region).EnumByte();
        AddProperty(x => x.Hits).Int32();
        AddProperty(x => x.Damage).Float();
        AddProperty("bIsWallPen", x => x.IsWallPen).Bool();
        AddProperty("bIsKill", x => x.IsKill).Bool();
        AddProperty(x => x.DestroyedArmor).ObjectNetGuid();
        AddProperty(x => x.ReceivedInteractions).Decode(decoders.Select(decoder => decoder.ReceivedInteractions));
        AddProperty(x => x.CombatReportIndex).Int32();
        AddProperty(x => x.ResurrectorPlayerState).ObjectNetGuid();
        AddProperty("bDied", x => x.Died).Bool();
        AddProperty(x => x.DeathLocation).FVector();
    }
}