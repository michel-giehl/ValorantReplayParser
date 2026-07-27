using System.Text.Json;
using CliReader.JsonExport;
using Replay.Models.Events;
using Replay.Valorant.Combat;
using Replay.Valorant.Movement;

namespace Replay.BrowserWasm;

internal sealed class BrowserReplayEventSink(Utf8JsonWriter writer) : IReplayEventSink
{
    private readonly ReplayEventJsonWriter _eventWriter = new(new ReplayJsonNormalizer());

    public BrowserReplayEventCounts Counts { get; } = new();

    private static int currentTick = 0;
    private static float lastTimestamp = 0f;

    public void Emit(ReplayEvent replayEvent)
    {
        if (Math.Abs(replayEvent.TimeSeconds - lastTimestamp) > .0001f)
        {
            lastTimestamp = replayEvent.TimeSeconds;
            currentTick++;
        }

        switch (replayEvent)
        {
            case ActorSpawned spawned:
                // _eventWriter.WriteActorSpawned(writer, spawned);
                Counts.ActorSpawned++;
                break;
            case ActorClosed closed:
                // ReplayEventJsonWriter.WriteActorClosed(writer, closed);
                Counts.ActorClosed++;
                break;
            case ExportGroupReceived { WasDecoded: true, Payload: not null } exportGroup:
                _eventWriter.WriteExportGroup(writer, exportGroup);
                Counts.ExportGroups++;
                break;
            case ExportGroupReceived:
                Counts.FilteredExportGroups++;
                break;
            case RpcReceived rpc:
                _eventWriter.WriteRpc(writer, rpc);
                Counts.Rpcs++;
                break;
            case ValorantShotReceived shot:
                _eventWriter.WriteValorantShot(writer, shot);
                Counts.Shots++;
                break;
            case RemoteCharacterMovementReceived movement:
                if (currentTick % 4 == 0)
                {
                    _eventWriter.WriteMovement(writer, movement);
                }

                Counts.Movement++;
                break;
        }
    }
}

internal sealed class BrowserReplayEventCounts
{
    public int ActorSpawned { get; set; }
    public int ActorClosed { get; set; }
    public int ExportGroups { get; set; }
    public int FilteredExportGroups { get; set; }
    public int Rpcs { get; set; }
    public int Shots { get; set; }
    public int Movement { get; set; }

    public int Emitted =>
        ActorSpawned +
        ActorClosed +
        ExportGroups +
        Rpcs +
        Shots +
        Movement;
}