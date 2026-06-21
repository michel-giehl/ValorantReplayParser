using Replay.Models;
using Replay.Unreal;
using Serilog;

namespace CliReader;

internal sealed class ActorEventLogger : IReplayEventSink
{
    private const int MaxDetailedEvents = 400;

    private readonly ILogger _logger;
    private readonly Dictionary<uint, ActorIdentity> _actors = [];
    private readonly HashSet<uint> _loggedActorNetGuids = [];
    private int _detailedEventCount;

    public ActorEventLogger(ILogger logger)
    {
        _logger = logger.ForContext("SourceContext", "ActorEvents");
    }

    public int OpenedCount { get; private set; }
    public int SpawnedCount { get; private set; }
    public int ClosedCount { get; private set; }
    public int DestroyedCount { get; private set; }

    public void Emit(ReplayEvent replayEvent)
    {
        switch (replayEvent)
        {
            case ActorOpened opened:
                OpenedCount++;
                _actors[opened.ActorNetGuid] = new ActorIdentity(
                    opened.ActorPath,
                    opened.ArchetypePath,
                    SpawnTimeSeconds: null);
                break;

            case ActorSpawned spawned:
                SpawnedCount++;
                TrackSpawn(spawned);
                LogSpawn(spawned);
                break;

            case ActorClosed closed:
                ClosedCount++;
                if (closed.Reason != ChannelCloseReason.Destroyed)
                {
                    LogClose(closed);
                }

                break;

            case ActorDestroyed destroyed:
                DestroyedCount++;
                LogDestroyed(destroyed);
                break;
        }
    }

    public void LogSummary(WorldState worldState)
    {
        var actors = worldState.ActorsByNetGuid.Values;
        var activeActors = actors.Count(actor => actor.LifecycleStatus == ActorLifecycleStatus.Open);
        var dormantActors = actors.Count(actor => actor.LifecycleStatus == ActorLifecycleStatus.Dormant);
        var destroyedActors = actors.Count(actor => actor.LifecycleStatus == ActorLifecycleStatus.Destroyed);
        var activeSubobjects = worldState.ObjectsByNetGuid.Values.Count(subobject => subobject.IsActive);

        _logger.Information(
            "World state: {ActorCount} actors ({ActiveActorCount} active, {DormantActorCount} dormant, {DestroyedActorCount} destroyed), {SubobjectCount} subobjects ({ActiveSubobjectCount} active), {ChannelCount} channels",
            worldState.ActorsByNetGuid.Count,
            activeActors,
            dormantActors,
            destroyedActors,
            worldState.ObjectsByNetGuid.Count,
            activeSubobjects,
            worldState.Channels.Count);

        _logger.Information(
            "Actor events: {OpenedCount} opened, {SpawnedCount} spawned, {ClosedCount} closed, {DestroyedCount} destroyed",
            OpenedCount,
            SpawnedCount,
            ClosedCount,
            DestroyedCount);

        var commonArchetypes = actors
            .Where(actor => !string.IsNullOrWhiteSpace(actor.ArchetypePath))
            .Where(actor => IsInterestingPath(actor.ArchetypePath!))
            .GroupBy(actor => actor.ArchetypePath!, StringComparer.Ordinal)
            .Select(group => new { Path = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Path, StringComparer.Ordinal)
            .Take(25)
            .ToArray();

        if (commonArchetypes.Length > 0)
        {
            _logger.Information(
                "Most common analytics-relevant actor archetypes:{NewLine}{ActorArchetypes}",
                Environment.NewLine,
                string.Join(
                    Environment.NewLine,
                    commonArchetypes.Select(group => $"  {group.Count,5}  {group.Path}")));
        }

        if (_detailedEventCount >= MaxDetailedEvents)
        {
            _logger.Information(
                "Detailed actor-event output was limited to {MaxDetailedEvents} lines.",
                MaxDetailedEvents);
        }
    }

    private void TrackSpawn(ActorSpawned spawned)
    {
        _actors.TryGetValue(spawned.ActorNetGuid, out var identity);
        _actors[spawned.ActorNetGuid] = new ActorIdentity(
            identity?.ActorPath,
            spawned.ArchetypePath ?? identity?.ArchetypePath,
            spawned.TimeSeconds);
    }

    private void LogSpawn(ActorSpawned spawned)
    {
        var path = DisplayPath(spawned.ActorNetGuid);
        if (!IsInterestingPath(path))
        {
            return;
        }

        if (!TryReserveDetailedEvent())
        {
            return;
        }

        _loggedActorNetGuids.Add(spawned.ActorNetGuid);
        _logger.Information(
            "[{TimeSeconds,8:F3}s] Spawn actor {ActorNetGuid} on channel {ChannelIndex}: {ActorPath} at {Location}, velocity {Velocity}",
            spawned.TimeSeconds,
            spawned.ActorNetGuid,
            spawned.ChannelIndex,
            path,
            FormatVector(spawned.Location),
            FormatVector(spawned.Velocity));
    }

    private void LogClose(ActorClosed closed)
    {
        if (!_loggedActorNetGuids.Contains(closed.ActorNetGuid))
        {
            return;
        }

        if (!TryReserveDetailedEvent())
        {
            return;
        }

        _logger.Information(
            "[{TimeSeconds,8:F3}s] Close actor {ActorNetGuid} on channel {ChannelIndex}: {ActorPath}, reason {CloseReason}",
            closed.TimeSeconds,
            closed.ActorNetGuid,
            closed.ChannelIndex,
            DisplayPath(closed.ActorNetGuid),
            closed.Reason);
    }

    private void LogDestroyed(ActorDestroyed destroyed)
    {
        if (!_loggedActorNetGuids.Contains(destroyed.ActorNetGuid))
        {
            return;
        }

        if (!TryReserveDetailedEvent())
        {
            return;
        }

        _actors.TryGetValue(destroyed.ActorNetGuid, out var identity);
        var lifetime = identity?.SpawnTimeSeconds is { } spawnTime
            ? FormattableString.Invariant($"{destroyed.TimeSeconds - spawnTime:F3}s")
            : "unknown";

        _logger.Information(
            "[{TimeSeconds,8:F3}s] Destroy actor {ActorNetGuid} on channel {ChannelIndex}: {ActorPath}, lifetime {Lifetime}",
            destroyed.TimeSeconds,
            destroyed.ActorNetGuid,
            destroyed.ChannelIndex,
            DisplayPath(destroyed.ActorNetGuid),
            lifetime);
    }

    private bool TryReserveDetailedEvent()
    {
        if (_detailedEventCount >= MaxDetailedEvents)
        {
            return false;
        }

        _detailedEventCount++;
        return true;
    }

    private string DisplayPath(uint actorNetGuid)
    {
        if (!_actors.TryGetValue(actorNetGuid, out var identity))
        {
            return "<unknown>";
        }

        return identity.ArchetypePath ?? identity.ActorPath ?? "<unresolved>";
    }

    private static string FormatVector(FVector? vector) =>
        vector is { } value
            ? FormattableString.Invariant($"({value.X:F1}, {value.Y:F1}, {value.Z:F1})")
            : "<unknown>";

    private static bool IsInterestingPath(string path) =>
        path.Contains("Player", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Character", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("_PC", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Pawn", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Ability", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Gun", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Weapon", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Equippable", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Bomb", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Projectile", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Smoke", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Flash", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Trap", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Wall", StringComparison.OrdinalIgnoreCase);

    private sealed record ActorIdentity(
        string? ActorPath,
        string? ArchetypePath,
        float? SpawnTimeSeconds);
}
