using Replay.Models.Events;
using Replay.Valorant.Combat;
using Replay.Valorant.Flashes;
using Replay.Valorant.Movement;

namespace CliReader.JsonExport;

internal sealed class ReplayExportSink :
    IReplayEventSink,
    IRemoteCharacterMovementSink,
    IDisposable
{
    private readonly NdjsonWriter _events;
    private readonly NdjsonWriter _movement;
    private readonly ReplayEventJsonWriter _eventWriter;

    internal ReplayExportSink(
        NdjsonWriter events,
        NdjsonWriter movement,
        ReplayEventJsonWriter eventWriter)
    {
        _events = events;
        _movement = movement;
        _eventWriter = eventWriter;
    }

    public ReplayExportStatistics Statistics { get; } = new();

    public static ReplayExportSink Create(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var events = NdjsonWriter.Create(Path.Combine(outputDirectory, "events.ndjson"));
        try
        {
            var movement = NdjsonWriter.Create(Path.Combine(outputDirectory, "movement.ndjson"));
            return new ReplayExportSink(
                events,
                movement,
                new ReplayEventJsonWriter(new ReplayJsonNormalizer()));
        }
        catch
        {
            events.Dispose();
            throw;
        }
    }

    public void Emit(ReplayEvent replayEvent)
    {
        switch (replayEvent)
        {
            case ActorSpawned spawned:
                _events.Write(writer => _eventWriter.WriteActorSpawned(writer, spawned));
                Statistics.ActorSpawnedCount++;
                break;
            case ActorClosed closed:
                _events.Write(writer => ReplayEventJsonWriter.WriteActorClosed(writer, closed));
                Statistics.ActorClosedCount++;
                break;
            case ExportGroupReceived exportGroup:
                EmitExportGroup(exportGroup);
                break;
            case RpcReceived rpc:
                _events.Write(writer => _eventWriter.WriteRpc(writer, rpc));
                Statistics.RpcCount++;
                break;
            case ValorantShotReceived shot:
                _events.Write(writer => _eventWriter.WriteValorantShot(writer, shot));
                Statistics.ValorantShotReceivedCount++;
                break;
            case ValorantFlashCast cast:
                _events.Write(writer => _eventWriter.WriteValorantFlashCast(writer, cast));
                Statistics.ValorantFlashCastCount++;
                break;
            case ValorantFlashPathUpdated path:
                _events.Write(writer => _eventWriter.WriteValorantFlashPathUpdated(writer, path));
                Statistics.ValorantFlashPathUpdatedCount++;
                break;
            case ValorantFlashExploded exploded:
                _events.Write(writer => _eventWriter.WriteValorantFlashExploded(writer, exploded));
                Statistics.ValorantFlashExplodedCount++;
                break;
            case ValorantFlashPlayerHit hit:
                _events.Write(writer => _eventWriter.WriteValorantFlashPlayerHit(writer, hit));
                Statistics.ValorantFlashPlayerHitCount++;
                break;
            case RemoteCharacterMovementReceived movement:
                EmitRemoteCharacterMovement(movement);
                break;
        }
    }

    public void EmitRemoteCharacterMovement(RemoteCharacterMovementReceived movement)
    {
        _movement.Write(writer => _eventWriter.WriteMovement(writer, movement));
        Statistics.MovementCount++;
    }

    public void Dispose()
    {
        try
        {
            _events.Dispose();
        }
        finally
        {
            _movement.Dispose();
        }
    }

    private void EmitExportGroup(ExportGroupReceived exportGroup)
    {
        if (!exportGroup.WasDecoded || exportGroup.Payload is null)
        {
            Statistics.RecordFilteredExportGroup(exportGroup);
            return;
        }

        _events.Write(writer => _eventWriter.WriteExportGroup(writer, exportGroup));
        Statistics.ExportGroupCount++;
    }
}
