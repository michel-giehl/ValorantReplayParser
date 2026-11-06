using Replay.Models.Events;
using Replay.Valorant.Crosshair;

namespace Replay.Valorant.Events.Crosshair;

/// <summary>
/// A player's complete crosshair after a <c>BombPlayerState</c> update changed it.
/// </summary>
/// <remarks>
/// <see cref="Settings"/> is a snapshot of the merged state, not the sparse update: every member is present, and
/// members the replay never sent hold the class default object. The event repeats whenever the player edits the crosshair
/// during the match, so the last event for a player is the crosshair they ended the replay with.
/// </remarks>
/// <param name="TimeSeconds">Replay time of the update.</param>
/// <param name="PacketId">Packet that carried the update.</param>
/// <param name="PlayerStateNetGuid">Actor of the player state that owns the crosshair.</param>
/// <param name="Subject">Player id (PUUID) once known; null when the replay never sent it for this state.</param>
/// <param name="Settings">Snapshot of the merged crosshair; safe to keep and mutate.</param>
/// <param name="UnmatchedLeafCount">
/// Largest number of exports, in any update merged into <paramref name="Settings"/>, that fit no leaf of the
/// replay's wire ordering. Those leaves were not decoded. Zero when the replay's export names all fit.
/// </param>
/// <param name="WireLayout">Name of the wire ordering the replay was bound with, when known.</param>
public sealed record ValorantCrosshairUpdated(
    float TimeSeconds,
    int PacketId,
    uint PlayerStateNetGuid,
    string? Subject,
    CrosshairSettings Settings,
    int UnmatchedLeafCount = 0,
    string? WireLayout = null)
    : ReplayEvent(TimeSeconds, PacketId)
{
    /// <summary>
    /// True when part of the crosshair could not be decoded because the replay exports leaves this parser does not
    /// recognise at their position. <see cref="Settings"/> then lacks those members (they hold the class default),
    /// so a consumer should not treat it as the player's full crosshair.
    /// </summary>
    public bool IsPartial => UnmatchedLeafCount > 0;
}
