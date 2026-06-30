using System.Globalization;
using Microsoft.Extensions.Logging;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;

namespace CliReader;

internal sealed class ActorEventLogger : IReplayEventSink
{
    private const int MaxDetailedActorEvents = 400;
    private const int MaxDecodedValueEvents = 400;

    private readonly ILogger<ActorEventLogger> _logger;
    private readonly Dictionary<uint, ActorIdentity> _actors = [];
    private readonly HashSet<uint> _loggedActorNetGuids = [];
    private int _detailedActorEventCount;
    private int _decodedValueEventCount;

    public ActorEventLogger(ILogger<ActorEventLogger> logger)
    {
        _logger = logger;
    }

    public int SpawnedCount { get; private set; }
    public int ClosedCount { get; private set; }
    public int DestroyedCloseCount { get; private set; }
    public int ExportGroupCount { get; private set; }
    public int SkippedExportGroupCount { get; private set; }
    public int RpcCount { get; private set; }
    public int DecodedFieldCount { get; private set; }

    public void Emit(ReplayEvent replayEvent)
    {
        switch (replayEvent)
        {
            case ActorSpawned spawned:
                SpawnedCount++;
                TrackSpawn(spawned);
                LogSpawn(spawned);
                break;

            case ActorClosed closed:
                ClosedCount++;
                if (closed.Reason == ChannelCloseReason.Destroyed)
                {
                    DestroyedCloseCount++;
                }

                LogClose(closed);
                break;

            case ExportGroupReceived exportGroup:
                ExportGroupCount++;
                if (!exportGroup.WasDecoded)
                {
                    SkippedExportGroupCount++;
                }

                DecodedFieldCount += exportGroup.Fields.Count;
                LogExportGroup(exportGroup);
                break;

            case RpcReceived rpc:
                RpcCount++;
                DecodedFieldCount += rpc.Fields.Count;
                LogRpc(rpc);
                break;
        }
    }

    public void LogSummary()
    {
        _logger.LogInformation(
            "Actor events: {SpawnedCount} spawned/opened, {ClosedCount} closed ({DestroyedCloseCount} destroyed)",
            SpawnedCount,
            ClosedCount,
            DestroyedCloseCount);

        _logger.LogInformation(
            "Decoded events: {ExportGroupCount} export groups ({SkippedExportGroupCount} skipped), {RpcCount} RPCs, {DecodedFieldCount} fields",
            ExportGroupCount,
            SkippedExportGroupCount,
            RpcCount,
            DecodedFieldCount);

        var commonArchetypes = _actors.Values
            .Where(actor => !string.IsNullOrWhiteSpace(actor.ArchetypePath))
            .Where(actor => ActorPathInterestFilter.IsInteresting(actor.ArchetypePath!))
            .GroupBy(actor => actor.ArchetypePath!, StringComparer.Ordinal)
            .Select(group => new { Path = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Path, StringComparer.Ordinal)
            .Take(25)
            .ToArray();

        if (commonArchetypes.Length > 0)
        {
            _logger.LogInformation(
                "Most common analytics-relevant actor archetypes:{NewLine}{ActorArchetypes}",
                Environment.NewLine,
                string.Join(
                    Environment.NewLine,
                    commonArchetypes.Select(group => $"  {group.Count,5}  {group.Path}")));
        }

        if (_detailedActorEventCount >= MaxDetailedActorEvents)
        {
            _logger.LogInformation(
                "Detailed actor-event output was limited to {MaxDetailedEvents} lines.",
                MaxDetailedActorEvents);
        }

        if (_decodedValueEventCount >= MaxDecodedValueEvents)
        {
            _logger.LogInformation(
                "Decoded value output was limited to {MaxDecodedValueEvents} lines.",
                MaxDecodedValueEvents);
        }
    }

    private void TrackSpawn(ActorSpawned spawned)
    {
        _actors[spawned.ActorNetGuid] = new ActorIdentity(
            spawned.ActorPath,
            spawned.ArchetypePath,
            spawned.TimeSeconds);
    }

    private void LogSpawn(ActorSpawned spawned)
    {
        var path = DisplayPath(spawned.ActorNetGuid);
        if (!ActorPathInterestFilter.IsInteresting(path))
        {
            return;
        }

        if (!TryReserveDetailedActorEvent())
        {
            return;
        }

        _loggedActorNetGuids.Add(spawned.ActorNetGuid);
        _logger.LogInformation(
            "[{TimeSeconds,8:F3}s] Spawn/open actor {ActorNetGuid} on channel {ChannelIndex}: {ActorPath} at {Location}, velocity {Velocity}",
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

        if (!TryReserveDetailedActorEvent())
        {
            return;
        }

        _actors.TryGetValue(closed.ActorNetGuid, out var identity);
        var lifetime = identity?.SpawnTimeSeconds is { } spawnTime
            ? FormattableString.Invariant($"{closed.TimeSeconds - spawnTime:F3}s")
            : "unknown";

        _logger.LogInformation(
            "[{TimeSeconds,8:F3}s] Close actor {ActorNetGuid} on channel {ChannelIndex}: {ActorPath}, reason {CloseReason}, lifetime {Lifetime}",
            closed.TimeSeconds,
            closed.ActorNetGuid,
            closed.ChannelIndex,
            DisplayPath(closed.ActorNetGuid),
            closed.Reason,
            lifetime);
    }

    private void LogExportGroup(ExportGroupReceived exportGroup)
    {
        if (exportGroup.Fields.Count == 0 || !TryReserveDecodedValueEvent())
        {
            return;
        }

        _logger.LogInformation(
            "[{TimeSeconds,8:F3}s] Export {ExportGroupPath} actor {ActorNetGuid} object {ObjectNetGuid}: {Fields}",
            exportGroup.TimeSeconds,
            exportGroup.ExportGroupPath ?? "<unresolved>",
            exportGroup.ActorNetGuid,
            exportGroup.ObjectNetGuid,
            FormatFields(exportGroup.Fields));
    }

    private void LogRpc(RpcReceived rpc)
    {
        if (!TryReserveDecodedValueEvent())
        {
            return;
        }

        _logger.LogInformation(
            "[{TimeSeconds,8:F3}s] RPC {FunctionName} actor {ActorNetGuid} object {ObjectNetGuid}: {Fields}",
            rpc.TimeSeconds,
            rpc.FunctionName,
            rpc.ActorNetGuid,
            rpc.ObjectNetGuid,
            rpc.Fields.Count == 0 ? "<no decoded fields>" : FormatFields(rpc.Fields));
    }

    private bool TryReserveDetailedActorEvent()
    {
        if (_detailedActorEventCount >= MaxDetailedActorEvents)
        {
            return false;
        }

        _detailedActorEventCount++;
        return true;
    }

    private bool TryReserveDecodedValueEvent()
    {
        if (_decodedValueEventCount >= MaxDecodedValueEvents)
        {
            return false;
        }

        _decodedValueEventCount++;
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

    private static string FormatFields(IReadOnlyList<DecodedReplayField> fields)
    {
        const int maxFields = 8;
        var values = fields
            .Take(maxFields)
            .Select(field => $"{field.Name ?? field.ExportName ?? $"handle:{field.Handle}"}={FormatValue(field.Value)}");
        var formatted = string.Join(", ", values);
        return fields.Count > maxFields
            ? formatted + FormattableString.Invariant($", ... +{fields.Count - maxFields}")
            : formatted;
    }

    private static string FormatValue(DecodedFieldValue value) => value.Kind switch
    {
        DecodedFieldValueKind.Bool => value.BoolValue ? "true" : "false",
        DecodedFieldValueKind.Byte => value.ByteValue.ToString("G", CultureInfo.InvariantCulture),
        DecodedFieldValueKind.Int32 => value.Int32Value.ToString("G", CultureInfo.InvariantCulture),
        DecodedFieldValueKind.UInt32 => value.UInt32Value.ToString("G", CultureInfo.InvariantCulture),
        DecodedFieldValueKind.Float => value.FloatValue.ToString("G", CultureInfo.InvariantCulture),
        DecodedFieldValueKind.NetGuid => FormattableString.Invariant($"net:{value.NetGuidValue}"),
        DecodedFieldValueKind.Vector => FormatVector(value.VectorValue),
        DecodedFieldValueKind.Rotator => FormatRotator(value.RotatorValue),
        _ => "<none>",
    };

    private static string FormatVector(FVector? vector) =>
        vector is { } value ? FormatVector(value) : "<unknown>";

    private static string FormatVector(FVector vector) =>
        FormattableString.Invariant($"({vector.X:F1}, {vector.Y:F1}, {vector.Z:F1})");

    private static string FormatRotator(FRotator rotator) =>
        FormattableString.Invariant($"({rotator.Pitch:F1}, {rotator.Yaw:F1}, {rotator.Roll:F1})");

    private sealed record ActorIdentity(
        string? ActorPath,
        string? ArchetypePath,
        float? SpawnTimeSeconds);
}