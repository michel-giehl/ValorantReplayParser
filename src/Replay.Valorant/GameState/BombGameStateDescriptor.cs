using Replay.Models.Descriptors;
using Replay.Models.Replay;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed class BombGameStateDescriptor : ExportGroupDescriptor<BombGameStateDescriptor>
{
    public override string Path => "/Game/GameModes/Bomb/BombGameState.BombGameState_C";
    public override ExportCategory Categories => ExportCategory.GameState | ExportCategory.Economy;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public double ReplicatedWorldTimeSecondsDouble { get; set; }
    public string? MatchState { get; set; }
    public uint WinningTeam { get; set; }
    public byte CompletionState { get; set; }
    public ValorantRawPayload? TeamEconomy { get; set; }
    public float DisplayRemainingTime { get; set; }
    public float StateRemainingTime { get; set; }
    public float GamePhaseElapsedTime { get; set; }
    public float AuthGameplayStartTimestamp { get; set; }
    public float AuthGameplayEndTimestamp { get; set; }
    public int NetServerMaxTickRate { get; set; }
    public string? MatchID { get; set; }
    public object? RoundResults { get; set; }
    public byte Phase { get; set; }
    public ValorantRawPayload? RoundParticipantsInfos { get; set; }
    public int RoundNumber { get; set; }
    public byte BombState { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.ReplicatedWorldTimeSecondsDouble, ExportCategory.GameState).Double();
        AddProperty(x => x.MatchState, ExportCategory.GameState).FName();
        AddProperty(x => x.WinningTeam, ExportCategory.GameState).ObjectNetGuid();
        AddProperty(x => x.CompletionState, ExportCategory.GameState).SerializedInt(maxValue: 16);
        AddProperty(x => x.TeamEconomy, ExportCategory.GameState).Decode(ValorantPayloadDecoders.RawPayload("TArray<FAresTeamEconomy>"));
        AddProperty(x => x.DisplayRemainingTime, ExportCategory.GameState).Float();
        AddProperty(x => x.StateRemainingTime, ExportCategory.GameState).Float();
        AddProperty(x => x.GamePhaseElapsedTime, ExportCategory.GameState).Float();
        AddProperty(x => x.AuthGameplayStartTimestamp, ExportCategory.GameState).Float();
        AddProperty(x => x.AuthGameplayEndTimestamp, ExportCategory.GameState).Float();
        AddProperty(x => x.NetServerMaxTickRate, ExportCategory.GameState).Int32();
        AddProperty(x => x.MatchID, ExportCategory.GameState).FString();
        AddProperty(x => x.RoundResults, ExportCategory.GameState).Decode(
            new VersionedDefinition<IFieldDecoderDescriptor>(
                    new CompatibleAresRoundResultsDecoder(AresRoundResultHandles.Release1301))
                .From(
                    new ReplayReleaseVersion(13, 5),
                    new CompatibleAresRoundResultsDecoder(AresRoundResultHandles.Release1305)));
        AddProperty(x => x.Phase, ExportCategory.GameState).EnumByte();
        AddProperty(x => x.RoundParticipantsInfos, ExportCategory.GameState)
            .Decode(ValorantPayloadDecoders.RawPayload("TArray<FRoundParticipantsInfo>"));
        AddProperty(x => x.RoundNumber, ExportCategory.GameState | ExportCategory.Economy).Int32();
        AddProperty(x => x.BombState, ExportCategory.GameState).SerializedInt(maxValue: 16);
    }
}
