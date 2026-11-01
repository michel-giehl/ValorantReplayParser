using Replay.Models.Events;

namespace Replay.Valorant.Events.Rounds;

public enum ValorantRoundPhase
{
    /// <summary><c>ClientResetRound</c>: the next round's buy phase begins.</summary>
    BuyPhaseStarted,

    /// <summary><c>ClientBuyPhaseEnd</c>: the round goes live.</summary>
    RoundLive,

    /// <summary><c>MulticastEndRound</c>: the round is decided.</summary>
    RoundEnded,
}

/// <summary>
/// One round boundary, emitted just before the RPC it came from so consumers of that RPC already see the new phase.
/// A boundary arrives once for each actor its RPC was sent to.
/// </summary>
public sealed record ValorantRoundPhaseChanged(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    ValorantRoundPhase Phase)
    : ReplayEvent(TimeSeconds, PacketId)
{
    /// <summary>Whatever lives only for a round stops here: at its end, or at a reset when no end was sent.</summary>
    public bool EndsRound => Phase is ValorantRoundPhase.BuyPhaseStarted or ValorantRoundPhase.RoundEnded;

    public static ValorantRoundPhaseChanged? From(RpcReceived rpc) => PhaseOf(rpc.FunctionName) is { } phase
        ? new ValorantRoundPhaseChanged(rpc.TimeSeconds, rpc.PacketId, rpc.ActorNetGuid, phase)
        : null;

    private static ValorantRoundPhase? PhaseOf(string functionName) => functionName switch
    {
        "ClientResetRound" => ValorantRoundPhase.BuyPhaseStarted,
        "ClientBuyPhaseEnd" => ValorantRoundPhase.RoundLive,
        "MulticastEndRound" => ValorantRoundPhase.RoundEnded,
        _ => null,
    };
}
