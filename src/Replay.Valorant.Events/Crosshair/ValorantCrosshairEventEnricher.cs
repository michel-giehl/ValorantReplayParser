using Replay.Encoding.Net;
using Replay.Models.Events;
using Replay.Valorant.Crosshair;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Events.Crosshair;

/// <summary>Merges sparse crosshair updates per player state and emits the resulting full crosshair.</summary>
internal sealed class ValorantCrosshairEventEnricher : IReplayEventSink
{
    private readonly IReplayEventSink _inner;
    private readonly PlayerIdentityTracker _players;
    private readonly Dictionary<uint, PlayerCrosshair> _byPlayerState = [];

    public ValorantCrosshairEventEnricher(IReplayEventSink inner, NetGuidCache netGuidCache)
    {
        _inner = inner;
        _players = new PlayerIdentityTracker(netGuidCache);
    }

    public void Emit(ReplayEvent replayEvent)
    {
        _inner.Emit(replayEvent);
        if (replayEvent is not ExportGroupReceived { Payload: BombPlayerStateDescriptor playerState } export)
        {
            return;
        }

        _players.TrackPlayerState(export.ActorNetGuid, playerState);
        if (export.IsDeleted)
        {
            _byPlayerState.Remove(export.ActorNetGuid);
            return;
        }

        if (playerState.CrosshairSettings is not { Changes.Count: > 0 } update)
        {
            return;
        }

        if (!_byPlayerState.TryGetValue(export.ActorNetGuid, out var player))
        {
            _byPlayerState[export.ActorNetGuid] = player = new PlayerCrosshair();
        }

        update.ApplyTo(player.Settings);
        // The merged state stays incomplete once any update lost leaves, so the flag never resets.
        player.UnmatchedLeafCount = Math.Max(player.UnmatchedLeafCount, update.UnmatchedLeafCount);
        player.WireLayout = update.WireLayout ?? player.WireLayout;
        _inner.Emit(new ValorantCrosshairUpdated(
            export.TimeSeconds,
            export.PacketId,
            export.ActorNetGuid,
            _players.GetSubject(export.ActorNetGuid),
            player.Settings.Snapshot(),
            player.UnmatchedLeafCount,
            player.WireLayout));
    }

    private sealed class PlayerCrosshair
    {
        public CrosshairSettings Settings { get; } = new();

        public int UnmatchedLeafCount { get; set; }

        public string? WireLayout { get; set; }
    }
}
