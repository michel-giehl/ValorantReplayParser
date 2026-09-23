using Replay.Encoding.Net;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.Flashes.Descriptors;
using Replay.Valorant.GameState;
using Replay.Valorant.Nearsights.Descriptors;

namespace Replay.Valorant.Nearsights;

internal sealed class ValorantNearsightEventEnricher : IReplayEventSink
{
    private const float SourceMatchSeconds = 0.05f;
    private const int MaxOuterDepth = 16;

    private readonly IReplayEventSink _inner;
    private readonly NetGuidCache _netGuidCache;
    private readonly Dictionary<uint, NearsightState> _states = [];
    private readonly Dictionary<uint, NearsightState> _stateBySourceActor = [];
    private readonly Dictionary<uint, NearsightState> _stateByAbilityActor = [];
    private readonly Dictionary<uint, PlayerIdentity> _playersByState = [];
    private readonly Dictionary<uint, uint> _playerStateByCharacter = [];
    private readonly HashSet<uint> _playerCharacters = [];
    private readonly HashSet<TargetHitKey> _emittedHits = [];
    private readonly Dictionary<TargetEffectKey, ActiveNearsightEffect> _activeEffects = [];

    public ValorantNearsightEventEnricher(IReplayEventSink inner, NetGuidCache netGuidCache)
    {
        _inner = inner;
        _netGuidCache = netGuidCache;
    }

    public void Emit(ReplayEvent replayEvent)
    {
        switch (replayEvent)
        {
            case ActorSpawned spawned:
                TrackActorSpawned(spawned);
                break;
            case ActorClosed closed:
                TrackActorClosed(closed);
                break;
            case ExportGroupReceived exportGroup:
                TrackExportGroup(exportGroup);
                break;
            case RpcReceived rpc:
                TrackRpc(rpc);
                break;
        }

        _inner.Emit(replayEvent);
    }

    public void Complete()
    {
        foreach (var state in _states.Values.OrderBy(candidate => candidate.Spawn.TimeSeconds))
        {
            EnsureCastEmitted(state);
        }
    }

    private void TrackActorSpawned(ActorSpawned spawned)
    {
        if (TryResolveProjectileKind(spawned, out var projectileKind))
        {
            _states[spawned.ActorNetGuid] = new NearsightState(spawned, projectileKind);
            return;
        }

        if (!TryResolveSourceKind(spawned, out var sourceKind))
        {
            return;
        }

        var state = _states.Values
            .Where(candidate => candidate.Kind == sourceKind && candidate.SourceActorNetGuid is null)
            .Where(candidate => candidate.CloseTimeSeconds is { } close &&
                                Math.Abs(close - spawned.TimeSeconds) <= SourceMatchSeconds)
            .OrderBy(candidate => DistanceSquared(candidate.LastLocation, spawned.Location))
            .ThenByDescending(candidate => candidate.CloseTimeSeconds)
            .FirstOrDefault();
        if (state is null)
        {
            return;
        }

        state.SourceActorNetGuid = spawned.ActorNetGuid;
        state.SourceSpawn = spawned;
        _stateBySourceActor[spawned.ActorNetGuid] = state;
        EnsureCastEmitted(state);
        EmitActivation(
            state,
            spawned.TimeSeconds,
            spawned.PacketId,
            spawned.ActorNetGuid,
            spawned.Location,
            ValorantNearsightActivationEvidence.SourceActorSpawned);
    }

    private void TrackActorClosed(ActorClosed closed)
    {
        if (!_states.TryGetValue(closed.ActorNetGuid, out var state))
        {
            return;
        }

        state.CloseTimeSeconds = closed.TimeSeconds;
    }

    private void TrackExportGroup(ExportGroupReceived exportGroup)
    {
        switch (exportGroup.Payload)
        {
            case INearsightProjectilePayload projectile:
                TrackProjectile(exportGroup, projectile);
                break;
            case INearsightSourcePayload source:
                TrackSource(exportGroup.ActorNetGuid, source);
                break;
            case GenericAgentDescriptor agent:
                TrackAgent(exportGroup.ActorNetGuid, agent);
                break;
            case BombPlayerStateDescriptor playerState:
                TrackPlayerState(exportGroup.ActorNetGuid, playerState);
                break;
        }
    }

    private void TrackProjectile(ExportGroupReceived exportGroup, INearsightProjectilePayload projectile)
    {
        if (!_states.TryGetValue(exportGroup.ActorNetGuid, out var state))
        {
            return;
        }

        if (projectile.HasDecoded(nameof(INearsightProjectilePayload.Owner)) && projectile.Owner is > 0)
        {
            state.OwnerNetGuid = projectile.Owner;
            _stateByAbilityActor[projectile.Owner.Value] = state;
        }

        if (projectile.HasDecoded(nameof(INearsightProjectilePayload.Instigator)) && projectile.Instigator is > 0)
        {
            state.InstigatorNetGuid = projectile.Instigator;
        }

        EnsureCastEmitted(state);

        if (!projectile.HasDecoded(nameof(INearsightProjectilePayload.ReplicatedMovement)) ||
            projectile.ReplicatedMovement is not { } movement)
        {
            return;
        }

        state.LastLocation = movement.Location ?? state.LastLocation;
        _inner.Emit(new ValorantNearsightPathUpdated(
            exportGroup.TimeSeconds,
            exportGroup.PacketId,
            state.ActorNetGuid,
            state.Kind,
            state.NextSampleIndex++,
            ValorantNearsightPathSampleSource.ReplicatedMovement,
            movement.Location,
            movement.Rotation,
            movement.LinearVelocity,
            movement.AngularVelocity,
            movement.ServerFrame));
    }

    private void TrackSource(uint sourceActorNetGuid, INearsightSourcePayload source)
    {
        if (!_stateBySourceActor.TryGetValue(sourceActorNetGuid, out var state))
        {
            return;
        }

        if (source.Owner is > 0)
        {
            state.OwnerNetGuid ??= source.Owner;
            _stateByAbilityActor[source.Owner.Value] = state;
        }

        if (source.Instigator is > 0)
        {
            state.InstigatorNetGuid ??= source.Instigator;
        }
    }

    private void TrackRpc(RpcReceived rpc)
    {
        switch (rpc.Payload)
        {
            case EffectManagerPlayContinuousParameters played:
                TrackEffectStarted(rpc, played);
                break;
            case EffectManagerStopContinuousParameters stopped:
                TrackEffectStopped(rpc, stopped);
                break;
        }
    }

    private void TrackEffectStarted(RpcReceived rpc, EffectManagerPlayContinuousParameters effect)
    {
        if (!_playerCharacters.Contains(rpc.ActorNetGuid) ||
            !string.Equals(effect.AttachSocket, "Head", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var duration = effect.FloatValues
            .FirstOrDefault(value => value.Name?.TagName == "FXC.Buff.Duration")
            ?.Value;
        var contextActor = effect.ObjectValues
            .FirstOrDefault(value => value.Name?.TagName == "FXC.EffectContext")
            ?.Value;
        if (duration is not { } durationValue || !float.IsFinite(durationValue) || contextActor is not > 0)
        {
            return;
        }

        var resolution = ResolveState(contextActor.Value);
        if (resolution.State is not { } state)
        {
            return;
        }

        var targetPlayerState = GetPlayerState(rpc.ActorNetGuid);
        if (_emittedHits.Add(new TargetHitKey(state.ActorNetGuid, rpc.ActorNetGuid)))
        {
            _inner.Emit(new ValorantNearsightPlayerHit(
                rpc.TimeSeconds,
                rpc.PacketId,
                state.ActorNetGuid,
                state.Kind,
                rpc.ActorNetGuid,
                targetPlayerState,
                GetSubject(targetPlayerState),
                durationValue >= 0 ? durationValue : null,
                durationValue < 0,
                effect.EffectId,
                effect.EffectContainer,
                contextActor.Value,
                effect.StartMovementTime,
                resolution.Correlation,
                ValorantNearsightDurationSource.TargetEffectBuffDuration));
        }

        if (effect.EffectId is { } effectId)
        {
            _activeEffects[new TargetEffectKey(rpc.ActorNetGuid, effectId)] = new ActiveNearsightEffect(
                state,
                rpc.ActorNetGuid,
                effectId,
                rpc.TimeSeconds,
                effect.StartMovementTime);
        }
    }

    private void TrackEffectStopped(RpcReceived rpc, EffectManagerStopContinuousParameters effect)
    {
        if (effect.EffectId is not { } effectId ||
            !_activeEffects.Remove(new TargetEffectKey(rpc.ActorNetGuid, effectId), out var active))
        {
            return;
        }

        var observedDuration = GetObservedDuration(
            active.AppliedTimeSeconds,
            rpc.TimeSeconds,
            active.StartMovementTime,
            effect.StopMovementTime);
        var targetPlayerState = GetPlayerState(active.TargetCharacterNetGuid);
        _inner.Emit(new ValorantNearsightPlayerEffectEnded(
            rpc.TimeSeconds,
            rpc.PacketId,
            active.State.ActorNetGuid,
            active.State.Kind,
            active.TargetCharacterNetGuid,
            targetPlayerState,
            GetSubject(targetPlayerState),
            active.EffectId,
            active.AppliedTimeSeconds,
            active.StartMovementTime,
            effect.StopMovementTime,
            observedDuration));
    }

    private NearsightResolution ResolveState(uint contextActorNetGuid)
    {
        if (_states.TryGetValue(contextActorNetGuid, out var projectile))
        {
            return new NearsightResolution(
                projectile,
                ValorantNearsightHitCorrelation.EffectContextProjectile);
        }

        if (_stateBySourceActor.TryGetValue(contextActorNetGuid, out var source))
        {
            return new NearsightResolution(source, ValorantNearsightHitCorrelation.EffectContextSource);
        }

        if (_stateByAbilityActor.TryGetValue(contextActorNetGuid, out var ability))
        {
            return new NearsightResolution(ability, ValorantNearsightHitCorrelation.EffectContextAbility);
        }

        var current = contextActorNetGuid;
        for (var depth = 0; depth < MaxOuterDepth; depth++)
        {
            if (!_netGuidCache.TryGetOuterNetGuid(current, out NetworkGuid outer))
            {
                break;
            }

            current = outer.Value;
            if (_states.TryGetValue(current, out var fromOuter) ||
                _stateBySourceActor.TryGetValue(current, out fromOuter) ||
                _stateByAbilityActor.TryGetValue(current, out fromOuter))
            {
                return new NearsightResolution(
                    fromOuter,
                    ValorantNearsightHitCorrelation.EffectContextOuter);
            }
        }

        return default;
    }

    private void EnsureCastEmitted(NearsightState state)
    {
        if (state.CastEmitted)
        {
            return;
        }

        var caster = ResolveCaster(state);
        var playerState = GetPlayerState(caster);
        _inner.Emit(new ValorantNearsightCast(
            state.Spawn.TimeSeconds,
            state.Spawn.PacketId,
            state.ActorNetGuid,
            state.Kind,
            caster,
            playerState,
            GetSubject(playerState),
            state.Spawn.Location,
            state.Spawn.Rotation,
            state.Spawn.Velocity));
        state.CastEmitted = true;

        _inner.Emit(new ValorantNearsightPathUpdated(
            state.Spawn.TimeSeconds,
            state.Spawn.PacketId,
            state.ActorNetGuid,
            state.Kind,
            state.NextSampleIndex++,
            ValorantNearsightPathSampleSource.SpawnTransform,
            state.Spawn.Location,
            state.Spawn.Rotation,
            state.Spawn.Velocity,
            null,
            null));

        if (state.Kind == ValorantNearsightKind.OmenParanoia)
        {
            EmitActivation(
                state,
                state.Spawn.TimeSeconds,
                state.Spawn.PacketId,
                null,
                state.Spawn.Location,
                ValorantNearsightActivationEvidence.ProjectileActiveFromRelease);
        }
    }

    private void EmitActivation(
        NearsightState state,
        float timeSeconds,
        int packetId,
        uint? sourceActorNetGuid,
        FVector? location,
        ValorantNearsightActivationEvidence evidence)
    {
        if (state.ActivationEmitted)
        {
            return;
        }

        state.ActivationEmitted = true;
        _inner.Emit(new ValorantNearsightActivated(
            timeSeconds,
            packetId,
            state.ActorNetGuid,
            state.Kind,
            sourceActorNetGuid,
            location,
            evidence));
    }

    private uint? ResolveCaster(NearsightState state)
    {
        if (state.InstigatorNetGuid is > 0)
        {
            return state.InstigatorNetGuid;
        }

        if (state.OwnerNetGuid is not > 0)
        {
            return null;
        }

        var current = state.OwnerNetGuid.Value;
        for (var depth = 0; depth < MaxOuterDepth; depth++)
        {
            if (_playerCharacters.Contains(current) || _playerStateByCharacter.ContainsKey(current))
            {
                return current;
            }

            if (!_netGuidCache.TryGetOuterNetGuid(current, out var outer))
            {
                break;
            }

            current = outer.Value;
        }

        return state.OwnerNetGuid;
    }

    private void TrackAgent(uint characterNetGuid, GenericAgentDescriptor agent)
    {
        _playerCharacters.Add(characterNetGuid);
        if (agent.HasDecoded(nameof(GenericAgentDescriptor.PlayerState)) && agent.PlayerState != 0)
        {
            SetCharacterPlayerState(characterNetGuid, agent.PlayerState);
        }
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
        // Possession establishes ownership, not an agent body: cameras and drones
        // also appear as PossessedCharacter. Process SpawnedCharacter independently.
        if (playerState.HasDecoded(nameof(BombPlayerStateDescriptor.SpawnedCharacter)) &&
            playerState.SpawnedCharacter != 0)
        {
            _playerCharacters.Add(playerState.SpawnedCharacter);
            SetCharacterPlayerState(playerState.SpawnedCharacter, playerStateNetGuid);
        }
    }

    private void SetCharacterPlayerState(uint characterNetGuid, uint playerStateNetGuid)
    {
        if (characterNetGuid == 0 || playerStateNetGuid == 0)
        {
            return;
        }

        _playerStateByCharacter[characterNetGuid] = playerStateNetGuid;
    }

    private uint? GetPlayerState(uint? characterNetGuid) =>
        characterNetGuid is { } character && _playerStateByCharacter.TryGetValue(character, out var playerState)
            ? playerState
            : null;

    private string? GetSubject(uint? playerStateNetGuid) =>
        playerStateNetGuid is { } playerState && _playersByState.TryGetValue(playerState, out var identity)
            ? identity.Subject
            : null;

    private static float GetObservedDuration(
        float appliedTimeSeconds,
        float endedTimeSeconds,
        float? startMovementTime,
        float? stopMovementTime)
    {
        if (startMovementTime is { } start && stopMovementTime is { } stop &&
            float.IsFinite(start) && float.IsFinite(stop) && stop >= start)
        {
            return stop - start;
        }

        return Math.Max(0, endedTimeSeconds - appliedTimeSeconds);
    }

    private static bool TryResolveProjectileKind(ActorSpawned spawned, out ValorantNearsightKind kind) =>
        TryResolveKind(
            spawned,
            (NearsightPaths.OmenProjectile, "Default__Projectile_Wraith_Q_NearsightMissile_C",
                ValorantNearsightKind.OmenParanoia),
            (NearsightPaths.ReynaProjectile, "Default__Projectile_Vampire_4_NearsightAoE_C",
                ValorantNearsightKind.ReynaLeer),
            (NearsightPaths.HarborProjectile, "Default__Projectile_Mage_4_SplashGrenade_C",
                ValorantNearsightKind.HarborStormSurge),
            out kind);

    private static bool TryResolveSourceKind(ActorSpawned spawned, out ValorantNearsightKind kind) =>
        TryResolveKind(
            spawned,
            (NearsightPaths.ReynaSource, "Default__GameObject_Vampire_4_NearsightAOE_Source_C",
                ValorantNearsightKind.ReynaLeer),
            (NearsightPaths.HarborSource, "Default__GameObject_Mage_4_SplashGrenade_C",
                ValorantNearsightKind.HarborStormSurge),
            out kind);

    private static bool TryResolveKind(
        ActorSpawned spawned,
        (string Path, string Archetype, ValorantNearsightKind Kind) first,
        (string Path, string Archetype, ValorantNearsightKind Kind) second,
        out ValorantNearsightKind kind) =>
        TryResolveKind(spawned, [first, second], out kind);

    private static bool TryResolveKind(
        ActorSpawned spawned,
        (string Path, string Archetype, ValorantNearsightKind Kind) first,
        (string Path, string Archetype, ValorantNearsightKind Kind) second,
        (string Path, string Archetype, ValorantNearsightKind Kind) third,
        out ValorantNearsightKind kind) =>
        TryResolveKind(spawned, [first, second, third], out kind);

    private static bool TryResolveKind(
        ActorSpawned spawned,
        IReadOnlyList<(string Path, string Archetype, ValorantNearsightKind Kind)> candidates,
        out ValorantNearsightKind kind)
    {
        foreach (var path in new[] { spawned.ArchetypePath, spawned.ReplicationClassPath, spawned.ActorPath })
        {
            var candidate = candidates.FirstOrDefault(item => path is not null &&
                (path == item.Path || path == item.Archetype));
            if (candidate.Path is not null)
            {
                kind = candidate.Kind;
                return true;
            }
        }

        kind = default;
        return false;
    }

    private static double DistanceSquared(FVector? left, FVector? right)
    {
        if (left is not { } a || right is not { } b)
        {
            return double.MaxValue;
        }

        var x = a.X - b.X;
        var y = a.Y - b.Y;
        var z = a.Z - b.Z;
        return x * x + y * y + z * z;
    }

    private sealed class NearsightState(ActorSpawned spawn, ValorantNearsightKind kind)
    {
        public ActorSpawned Spawn { get; } = spawn;
        public uint ActorNetGuid => Spawn.ActorNetGuid;
        public ValorantNearsightKind Kind { get; } = kind;
        public uint? OwnerNetGuid { get; set; }
        public uint? InstigatorNetGuid { get; set; }
        public FVector? LastLocation { get; set; } = spawn.Location;
        public int NextSampleIndex { get; set; }
        public bool CastEmitted { get; set; }
        public bool ActivationEmitted { get; set; }
        public float? CloseTimeSeconds { get; set; }
        public uint? SourceActorNetGuid { get; set; }
        public ActorSpawned? SourceSpawn { get; set; }
    }

    private sealed class PlayerIdentity
    {
        public string? Subject { get; set; }
    }

    private readonly record struct TargetHitKey(uint NearsightActorNetGuid, uint TargetCharacterNetGuid);

    private readonly record struct TargetEffectKey(uint TargetCharacterNetGuid, ulong EffectId);

    private sealed record ActiveNearsightEffect(
        NearsightState State,
        uint TargetCharacterNetGuid,
        ulong EffectId,
        float AppliedTimeSeconds,
        float? StartMovementTime);

    private readonly record struct NearsightResolution(
        NearsightState? State,
        ValorantNearsightHitCorrelation Correlation);
}
