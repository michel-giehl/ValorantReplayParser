using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

/// <summary>Per-team entry of <c>FAresRoundResult.TeamResults</c>, replicated since 13.09.</summary>
public sealed class AresTeamRoundResult(uint teamNameHandle) : ExportGroupDescriptor<AresTeamRoundResult>
{
    public string? TeamName { get; set; }
    public int? TeamNumber { get; set; }
    public AresTeamRole? TeamRole { get; set; }

    /// <summary>Raw outcome enum; observed as 1 for the round winner and 2 for the loser.</summary>
    public byte? TeamOutcome { get; set; }

    public override object CreatePayloadInstance() => new AresTeamRoundResult(teamNameHandle);

    protected override void Configure()
    {
        AddPropertyHandle(teamNameHandle, x => x.TeamName).FName();
        AddPropertyHandle(teamNameHandle + 1, x => x.TeamNumber).Int32();
        AddPropertyHandle(teamNameHandle + 2, x => x.TeamRole).EnumRemainingBits();
        AddPropertyHandle(teamNameHandle + 3, x => x.TeamOutcome).EnumRemainingBits();
    }
}
