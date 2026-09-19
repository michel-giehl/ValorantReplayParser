using Replay.Models.Events;
using Replay.Models.Unreal;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.GameState;
using Replay.Valorant.Walls.Descriptors;

namespace Replay.Valorant.Walls;

internal sealed class ValorantWallEventEnricher : IReplayEventSink
{
    private readonly IReplayEventSink _inner;
    private readonly Dictionary<uint, WallState> _walls = [];
    private readonly Dictionary<uint, SegmentState> _segments = [];
    private readonly Dictionary<uint, ActiveVyseWallState> _activeVyseWalls = [];
    private readonly Dictionary<uint, uint> _playerStateByCharacter = [];
    private readonly Dictionary<uint, PlayerIdentity> _playersByState = [];

    public ValorantWallEventEnricher(IReplayEventSink inner)
    {
        _inner = inner;
    }

    public void Emit(ReplayEvent replayEvent)
    {
        switch (replayEvent)
        {
            case ActorSpawned spawned:
                TrackSpawn(spawned);
                break;
            case ExportGroupReceived exportGroup:
                TrackExport(exportGroup);
                break;
            case RpcReceived rpc:
                TrackRpc(rpc);
                break;
            case ActorClosed closed:
                TrackClosed(closed);
                break;
        }

        _inner.Emit(replayEvent);
    }

    public void Complete()
    {
        foreach (var wall in _walls.Values.Where(wall => wall.Kind == ValorantWallKind.SageBarrierOrb))
        {
            EmitSagePlacement(wall);
        }
    }

    private void TrackSpawn(ActorSpawned spawned)
    {
        if (Matches(spawned, WallPaths.SageWall, "Default__GameObject_Thorne_E_Wall_Fortifying_C"))
        {
            _walls[spawned.ActorNetGuid] = new WallState(spawned, ValorantWallKind.SageBarrierOrb);
        }
        else if (Matches(spawned, WallPaths.SageSegment,
                     "Default__GameObject_Thorne_E_Wall_Segment_Fortifying_C"))
        {
            _segments[spawned.ActorNetGuid] = new SegmentState(spawned);
        }
        else if (Matches(spawned, WallPaths.VyseTrap, "Default__GameObject_Nox_WallTrap_C"))
        {
            _walls[spawned.ActorNetGuid] = new WallState(spawned, ValorantWallKind.VyseShear);
        }
        else if (Matches(spawned, WallPaths.VyseWall, "Default__GameObject_Nox_Wall_C"))
        {
            _activeVyseWalls[spawned.ActorNetGuid] = new ActiveVyseWallState(spawned);
        }
    }

    private void TrackExport(ExportGroupReceived exportGroup)
    {
        switch (exportGroup.Payload)
        {
            case SageWallDescriptor wall:
                TrackOwnedWall(exportGroup.ActorNetGuid, wall);
                if (_walls.TryGetValue(exportGroup.ActorNetGuid, out var wallState))
                {
                    EmitSagePlacement(wallState);
                }
                break;
            case VyseWallTrapDescriptor trap:
                TrackOwnedWall(exportGroup.ActorNetGuid, trap);
                break;
            case VyseWallDescriptor activeWall:
                TrackActiveVyseWall(exportGroup.ActorNetGuid, activeWall);
                break;
            case SageWallSegmentDescriptor segment:
                TrackSageSegment(exportGroup, segment);
                break;
            case GenericAgentDescriptor agent:
                if (agent.HasDecoded(nameof(GenericAgentDescriptor.PlayerState)) && agent.PlayerState != 0)
                {
                    SetCharacterPlayerState(exportGroup.ActorNetGuid, agent.PlayerState);
                }
                break;
            case BombPlayerStateDescriptor playerState:
                TrackPlayerState(exportGroup.ActorNetGuid, playerState);
                break;
        }
    }

    private void TrackOwnedWall(uint actorNetGuid, IWallOwnedActor descriptor)
    {
        if (!_walls.TryGetValue(actorNetGuid, out var wall))
        {
            return;
        }

        wall.OwnerNetGuid = descriptor.Owner ?? wall.OwnerNetGuid;
        wall.CasterCharacterNetGuid = descriptor.Instigator ?? wall.CasterCharacterNetGuid;
    }

    private void TrackActiveVyseWall(uint actorNetGuid, IWallOwnedActor descriptor)
    {
        if (!_activeVyseWalls.TryGetValue(actorNetGuid, out var active))
        {
            return;
        }

        active.WallActorNetGuid = descriptor.Owner ?? active.WallActorNetGuid;
        active.CasterCharacterNetGuid = descriptor.Instigator ?? active.CasterCharacterNetGuid;
        if (active.WallActorNetGuid is { } wallActor && _walls.TryGetValue(wallActor, out var wall))
        {
            wall.ActiveWallActorNetGuid = actorNetGuid;
        }
    }

    private void TrackSageSegment(ExportGroupReceived exportGroup, SageWallSegmentDescriptor descriptor)
    {
        if (!_segments.TryGetValue(exportGroup.ActorNetGuid, out var segment))
        {
            return;
        }

        if (descriptor.HasDecoded(nameof(SageWallSegmentDescriptor.Owner)) && descriptor.Owner is { } owner &&
            _walls.TryGetValue(owner, out var wall))
        {
            segment.WallActorNetGuid = owner;
            if (!segment.SpawnEmitted)
            {
                segment.SegmentIndex = wall.NextSegmentIndex++;
                segment.SpawnEmitted = true;
                _inner.Emit(new ValorantWallSegmentSpawned(
                    segment.Spawn.TimeSeconds,
                    segment.Spawn.PacketId,
                    owner,
                    segment.Spawn.ActorNetGuid,
                    segment.SegmentIndex,
                    segment.Spawn.Location,
                    segment.Spawn.Rotation));
            }
        }

        if (descriptor.HasDecoded(nameof(SageWallSegmentDescriptor.IsAlive)) && descriptor.IsAlive == false)
        {
            EmitSegmentDestroyed(segment, exportGroup.TimeSeconds, exportGroup.PacketId,
                ValorantWallSegmentDestructionEvidence.ReplicatedNotAlive);
        }
    }

    private void TrackRpc(RpcReceived rpc)
    {
        switch (rpc.Payload)
        {
            case VyseInitializeTrapAnchorsParameters anchors:
                TrackVysePlacement(rpc, anchors);
                return;
            case VyseInitializeWallParameters wall:
                TrackVyseWallInitialized(rpc, wall);
                return;
        }

        if (rpc.FunctionExportPath == WallPaths.SageSegment + ":DisableCollision" &&
            _segments.TryGetValue(rpc.ActorNetGuid, out var segment))
        {
            EmitSegmentDestroyed(segment, rpc.TimeSeconds, rpc.PacketId,
                ValorantWallSegmentDestructionEvidence.DisableCollisionRpc);
        }
        else if (rpc.FunctionExportPath == WallPaths.VyseWall + ":MulticastEnableDynamicCollision" &&
                 _activeVyseWalls.TryGetValue(rpc.ActorNetGuid, out var active))
        {
            EmitVyseActivation(active, rpc.TimeSeconds, rpc.PacketId);
        }
    }

    private void TrackVysePlacement(RpcReceived rpc, VyseInitializeTrapAnchorsParameters anchors)
    {
        if (!_walls.TryGetValue(rpc.ActorNetGuid, out var wall))
        {
            return;
        }

        wall.WallStart = anchors.WallStartPoint;
        wall.WallEnd = anchors.WallEndPoint;
        wall.ImpactPoint = anchors.ImpactPoint;
        wall.ImpactNormal = anchors.ImpactNormal;
        if (wall.PlacementEmitted)
        {
            return;
        }

        wall.PlacementEmitted = true;
        var playerState = GetPlayerState(wall.CasterCharacterNetGuid);
        _inner.Emit(new ValorantWallPlaced(
            rpc.TimeSeconds,
            rpc.PacketId,
            wall.Spawn.ActorNetGuid,
            wall.Kind,
            wall.CasterCharacterNetGuid,
            playerState,
            GetSubject(playerState),
            wall.Spawn.Location,
            wall.Spawn.Rotation,
            wall.WallStart,
            wall.WallEnd,
            wall.ImpactPoint,
            wall.ImpactNormal,
            ValorantWallPlacementEvidence.VyseTrapAnchorsInitialized));
    }

    private void TrackVyseWallInitialized(RpcReceived rpc, VyseInitializeWallParameters parameters)
    {
        if (!_activeVyseWalls.TryGetValue(rpc.ActorNetGuid, out var active))
        {
            return;
        }

        active.WallStart = parameters.WallStartLocation;
        active.WallEnd = parameters.WallEndLocation;
        active.ImpactNormal = parameters.WallImpactNormal;
        active.TriggerCharacterNetGuid = parameters.EnemyTrigger;
        if (active.WallActorNetGuid is null)
        {
            active.WallActorNetGuid = FindVysePlacement(active.WallStart, active.WallEnd)?.Spawn.ActorNetGuid;
        }

        if (active.WallActorNetGuid is { } wallActor && _walls.TryGetValue(wallActor, out var wall))
        {
            wall.ActiveWallActorNetGuid = active.Spawn.ActorNetGuid;
        }
    }

    private void EmitVyseActivation(ActiveVyseWallState active, float timeSeconds, int packetId)
    {
        if (active.ActivationEmitted || active.WallActorNetGuid is not { } wallActor)
        {
            return;
        }

        active.ActivationEmitted = true;
        active.ActivationTimeSeconds = timeSeconds;
        var triggerPlayerState = GetPlayerState(active.TriggerCharacterNetGuid);
        _inner.Emit(new ValorantWallActivated(
            timeSeconds,
            packetId,
            wallActor,
            active.Spawn.ActorNetGuid,
            ValorantWallKind.VyseShear,
            active.Spawn.Location,
            active.Spawn.Rotation,
            active.WallStart,
            active.WallEnd,
            active.ImpactNormal,
            active.TriggerCharacterNetGuid,
            triggerPlayerState,
            GetSubject(triggerPlayerState),
            ValorantWallActivationEvidence.VyseDynamicCollisionEnabled));
    }

    private void TrackClosed(ActorClosed closed)
    {
        if (_segments.TryGetValue(closed.ActorNetGuid, out var segment))
        {
            EmitSegmentDestroyed(segment, closed.TimeSeconds, closed.PacketId,
                ValorantWallSegmentDestructionEvidence.ActorDestroyedFallback);
            return;
        }

        if (_activeVyseWalls.TryGetValue(closed.ActorNetGuid, out var active) &&
            active.WallActorNetGuid is { } placementActor)
        {
            EmitWallDestroyed(
                placementActor,
                active.Spawn.ActorNetGuid,
                ValorantWallKind.VyseShear,
                active.Spawn.Location,
                _walls.TryGetValue(placementActor, out var wall) ? wall.Spawn.TimeSeconds : active.Spawn.TimeSeconds,
                active.ActivationTimeSeconds,
                closed,
                ValorantWallDestructionEvidence.VyseActiveWallActorDestroyed);
            return;
        }

        if (!_walls.TryGetValue(closed.ActorNetGuid, out var state) || state.DestroyedEmitted)
        {
            return;
        }

        if (state.Kind == ValorantWallKind.VyseShear && state.ActiveWallActorNetGuid is not null)
        {
            return;
        }

        EmitWallDestroyed(
            state.Spawn.ActorNetGuid,
            null,
            state.Kind,
            state.Spawn.Location,
            state.Spawn.TimeSeconds,
            null,
            closed,
            state.Kind == ValorantWallKind.SageBarrierOrb
                ? ValorantWallDestructionEvidence.SageRootActorDestroyed
                : ValorantWallDestructionEvidence.VyseUnactivatedTrapDestroyed);
    }

    private void EmitSagePlacement(WallState wall)
    {
        if (wall.PlacementEmitted)
        {
            return;
        }

        wall.PlacementEmitted = true;
        var playerState = GetPlayerState(wall.CasterCharacterNetGuid);
        _inner.Emit(new ValorantWallPlaced(
            wall.Spawn.TimeSeconds,
            wall.Spawn.PacketId,
            wall.Spawn.ActorNetGuid,
            wall.Kind,
            wall.CasterCharacterNetGuid,
            playerState,
            GetSubject(playerState),
            wall.Spawn.Location,
            wall.Spawn.Rotation,
            null,
            null,
            null,
            null,
            ValorantWallPlacementEvidence.SageWallActorSpawned));
    }

    private void EmitSegmentDestroyed(
        SegmentState segment,
        float timeSeconds,
        int packetId,
        ValorantWallSegmentDestructionEvidence evidence)
    {
        if (segment.DestroyedEmitted || segment.WallActorNetGuid is not { } wallActor)
        {
            return;
        }

        segment.DestroyedEmitted = true;
        _inner.Emit(new ValorantWallSegmentDestroyed(
            timeSeconds,
            packetId,
            wallActor,
            segment.Spawn.ActorNetGuid,
            segment.SegmentIndex,
            segment.Spawn.Location,
            Math.Max(0, timeSeconds - segment.Spawn.TimeSeconds),
            evidence));
    }

    private void EmitWallDestroyed(
        uint wallActorNetGuid,
        uint? activeWallActorNetGuid,
        ValorantWallKind kind,
        FVector? location,
        float placedTimeSeconds,
        float? activationTimeSeconds,
        ActorClosed closed,
        ValorantWallDestructionEvidence evidence)
    {
        if (_walls.TryGetValue(wallActorNetGuid, out var wall))
        {
            if (wall.DestroyedEmitted)
            {
                return;
            }

            wall.DestroyedEmitted = true;
        }

        _inner.Emit(new ValorantWallDestroyed(
            closed.TimeSeconds,
            closed.PacketId,
            wallActorNetGuid,
            activeWallActorNetGuid,
            kind,
            location,
            Math.Max(0, closed.TimeSeconds - placedTimeSeconds),
            activationTimeSeconds is { } activated
                ? Math.Max(0, closed.TimeSeconds - activated)
                : null,
            evidence));
    }

    private WallState? FindVysePlacement(FVector? start, FVector? end)
    {
        if (start is null || end is null)
        {
            return null;
        }

        var candidate = _walls.Values
            .Where(wall => wall.Kind == ValorantWallKind.VyseShear && wall.ActiveWallActorNetGuid is null)
            .OrderBy(wall => DistanceSquared(wall.WallStart, start) + DistanceSquared(wall.WallEnd, end))
            .FirstOrDefault();
        return candidate is not null &&
               DistanceSquared(candidate.WallStart, start) + DistanceSquared(candidate.WallEnd, end) <= 1
            ? candidate
            : null;
    }

    private void TrackPlayerState(uint playerStateNetGuid, BombPlayerStateDescriptor playerState)
    {
        if (!_playersByState.TryGetValue(playerStateNetGuid, out var identity))
        {
            identity = new PlayerIdentity();
            _playersByState.Add(playerStateNetGuid, identity);
        }

        if (playerState.HasDecoded(nameof(BombPlayerStateDescriptor.Subject)))
        {
            identity.Subject = playerState.Subject;
        }

        if (playerState.HasDecoded(nameof(BombPlayerStateDescriptor.PossessedCharacter)))
        {
            SetCharacterPlayerState(playerState.PossessedCharacter, playerStateNetGuid);
        }
        else if (playerState.HasDecoded(nameof(BombPlayerStateDescriptor.SpawnedCharacter)))
        {
            SetCharacterPlayerState(playerState.SpawnedCharacter, playerStateNetGuid);
        }
    }

    private void SetCharacterPlayerState(uint characterNetGuid, uint playerStateNetGuid)
    {
        if (characterNetGuid != 0 && playerStateNetGuid != 0)
        {
            _playerStateByCharacter[characterNetGuid] = playerStateNetGuid;
        }
    }

    private uint? GetPlayerState(uint? characterNetGuid) =>
        characterNetGuid is { } character && _playerStateByCharacter.TryGetValue(character, out var playerState)
            ? playerState
            : null;

    private string? GetSubject(uint? playerStateNetGuid) =>
        playerStateNetGuid is { } playerState && _playersByState.TryGetValue(playerState, out var identity)
            ? identity.Subject
            : null;

    private static bool Matches(ActorSpawned spawned, string path, string archetype) =>
        spawned.ArchetypePath == path || spawned.ArchetypePath == archetype ||
        spawned.ReplicationClassPath == path || spawned.ActorPath == path;

    private static double DistanceSquared(FVector? left, FVector? right)
    {
        if (left is not { } a || right is not { } b)
        {
            return double.MaxValue / 4;
        }

        var x = a.X - b.X;
        var y = a.Y - b.Y;
        var z = a.Z - b.Z;
        return x * x + y * y + z * z;
    }

    private sealed class WallState(ActorSpawned spawn, ValorantWallKind kind)
    {
        public ActorSpawned Spawn { get; } = spawn;
        public ValorantWallKind Kind { get; } = kind;
        public uint? OwnerNetGuid { get; set; }
        public uint? CasterCharacterNetGuid { get; set; }
        public FVector? WallStart { get; set; }
        public FVector? WallEnd { get; set; }
        public FVector? ImpactPoint { get; set; }
        public FVector? ImpactNormal { get; set; }
        public uint? ActiveWallActorNetGuid { get; set; }
        public int NextSegmentIndex { get; set; }
        public bool PlacementEmitted { get; set; }
        public bool DestroyedEmitted { get; set; }
    }

    private sealed class SegmentState(ActorSpawned spawn)
    {
        public ActorSpawned Spawn { get; } = spawn;
        public uint? WallActorNetGuid { get; set; }
        public int SegmentIndex { get; set; }
        public bool SpawnEmitted { get; set; }
        public bool DestroyedEmitted { get; set; }
    }

    private sealed class ActiveVyseWallState(ActorSpawned spawn)
    {
        public ActorSpawned Spawn { get; } = spawn;
        public uint? WallActorNetGuid { get; set; }
        public uint? CasterCharacterNetGuid { get; set; }
        public FVector? WallStart { get; set; }
        public FVector? WallEnd { get; set; }
        public FVector? ImpactNormal { get; set; }
        public uint? TriggerCharacterNetGuid { get; set; }
        public float? ActivationTimeSeconds { get; set; }
        public bool ActivationEmitted { get; set; }
    }

    private sealed class PlayerIdentity
    {
        public string? Subject { get; set; }
    }
}
