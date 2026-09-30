using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed class BombGameStateClassNetCacheDescriptor : ClassNetCacheDescriptor<BombGameStateClassNetCacheDescriptor>
{
    public override string Path => "/Game/GameModes/Bomb/BombGameState.BombGameState_C_ClassNetCache";

    protected override void Configure()
    {
        AddFunction("ClientBuyPhaseEnd", "/Game/GameModes/Bomb/BombGameState.BombGameState_C:ClientBuyPhaseEnd",
                ExportCategory.GameState)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);
        AddFunction("ClientRoundStart", "/Game/GameModes/Bomb/BombGameState.BombGameState_C:ClientRoundStart",
                ExportCategory.GameState)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);
        AddFunction("Multicast Side Switch Event",
                "/Game/GameModes/Bomb/BombGameState.BombGameState_C:Multicast Side Switch Event",
                ExportCategory.GameState)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);
        AddFunction("ClientResetRound", "/Script/ShooterGame.ShooterGameState:ClientResetRound",
                ExportCategory.GameState)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);

        AddFunction<MulticastEndRoundParameters>(
            "MulticastEndRound",
            "/Script/ShooterGame.ShooterGameState:MulticastEndRound",
            ExportCategory.GameState);

        AddFunction<MulticastEnterPlayspaceParameters>(
            "MulticastEnterPlayspace",
            "/Script/ShooterGame.ShooterGameState:MulticastEnterPlayspace",
            ExportCategory.GameState);

        AddFunction<MulticastReceivePlayerResurrectEventParameters>(
            "MulticastReceivePlayerResurrectEvent",
            "/Script/ShooterGame.ShooterGameState:MulticastReceivePlayerResurrectEvent",
            ExportCategory.GameState | ExportCategory.Gunplay);

        AddFunction<OrbPickedUpRpcParameters>(
            "OrbPickedUpRPC", "/Game/BaseGameState.BaseGameState_C:OrbPickedUpRPC",
            ExportCategory.GameState);

        AddTemporaryDeathBase();
        AddTemporaryDeathPoint();

        AddFunction<MulticastSetPhaseParameters>(
            "MulticastSetPhase",
            "/Script/ShooterGame.ShooterGameState:MulticastSetPhase",
            ExportCategory.GameState);

        AddFunction<MulticastResetForRespawnParameters>(
            "MulticastResetForRespawn",
            "/Script/ShooterGame.AresGameStateBase:MulticastResetForRespawn",
            ExportCategory.GameState);
    }

    private void AddTemporaryDeathBase()
    {
        AddFunction<MulticastReceivePlayerTemporaryDeathEventBaseParameters>(
            "MulticastReceivePlayerTemporaryDeathEvent_Base",
            "/Script/ShooterGame.ShooterGameState:MulticastReceivePlayerTemporaryDeathEvent_Base",
            ExportCategory.GameState | ExportCategory.Gunplay);
    }

    private void AddTemporaryDeathPoint()
    {
        AddFunction<MulticastReceivePlayerTemporaryDeathEventPointParameters>(
            "MulticastReceivePlayerTemporaryDeathEvent_Point",
            "/Script/ShooterGame.ShooterGameState:MulticastReceivePlayerTemporaryDeathEvent_Point",
            ExportCategory.GameState | ExportCategory.Gunplay);
    }
}