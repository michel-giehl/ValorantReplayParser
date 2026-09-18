using Replay.Encoding.Net;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.Flashes.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Flashes;

internal sealed class ValorantFlashEventEnricher : IReplayEventSink
{
    private const float DeferredExplosionSeconds = 0.05f;
    private const float UniqueBlindCandidateSeconds = 0.25f;
    private const float EffectFallbackDelaySeconds = 1f;
    private const int MaxOuterDepth = 16;

    private readonly IReplayEventSink _inner;
    private readonly NetGuidCache _netGuidCache;
    private readonly Dictionary<uint, FlashState> _flashes = [];
    private readonly Dictionary<uint, FlashState> _flashBySourceActor = [];
    private readonly Dictionary<uint, PlayerIdentity> _playersByState = [];
    private readonly Dictionary<uint, uint> _playerStateByCharacter = [];
    private readonly HashSet<uint> _playerCharacters = [];
    private readonly HashSet<BlindKey> _emittedBlinds = [];
    private readonly HashSet<TargetEffectKey> _authoritativeTargetEffects = [];
    private readonly HashSet<TargetFlashKey> _authoritativeTargetFlashes = [];
    private readonly HashSet<EffectFallbackKey> _seenEffectFallbacks = [];
    private readonly List<EffectFallbackCandidate> _pendingEffectFallbacks = [];

    public ValorantFlashEventEnricher(IReplayEventSink inner, NetGuidCache netGuidCache)
    {
        _inner = inner;
        _netGuidCache = netGuidCache;
    }

    public void Emit(ReplayEvent replayEvent)
    {
        FlushDeferredExplosions(replayEvent.TimeSeconds);
        FlushEffectFallbacks(replayEvent.TimeSeconds, force: false);

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
        foreach (var state in _flashes.Values.OrderBy(flash => flash.Spawn.TimeSeconds))
        {
            EnsureCastEmitted(state);
            if (!state.Exploded && !state.IsReusableSource && state.CloseTimeSeconds is not null)
            {
                EmitDeferredExplosion(state);
            }
        }

        FlushEffectFallbacks(float.MaxValue, force: true);
    }

    private void TrackActorSpawned(ActorSpawned spawned)
    {
        if (TryResolveFlashKind(spawned, out var kind))
        {
            _flashes[spawned.ActorNetGuid] = new FlashState(spawned, kind);
            return;
        }

        if (!IsSkyeFlashSource(spawned))
        {
            return;
        }

        var state = _flashes.Values
            .Where(flash => flash.Kind == ValorantFlashKind.SkyeGuidingLight && !flash.Exploded)
            .Where(flash => flash.CloseTimeSeconds is { } close &&
                            Math.Abs(close - spawned.TimeSeconds) <= DeferredExplosionSeconds)
            .OrderBy(flash => DistanceSquared(flash.LastLocation, spawned.Location))
            .ThenByDescending(flash => flash.CloseTimeSeconds)
            .FirstOrDefault();
        if (state is null)
        {
            return;
        }

        state.SourceActorNetGuid = spawned.ActorNetGuid;
        state.SourceSpawn = spawned;
        _flashBySourceActor[spawned.ActorNetGuid] = state;
    }

    private void TrackActorClosed(ActorClosed closed)
    {
        if (!_flashes.TryGetValue(closed.ActorNetGuid, out var state) || state.Exploded)
        {
            return;
        }

        state.CloseTimeSeconds = closed.TimeSeconds;
        state.ClosePacketId = closed.PacketId;
    }

    private void TrackExportGroup(ExportGroupReceived exportGroup)
    {
        switch (exportGroup.Payload)
        {
            case IFlashProjectilePayload projectile:
                TrackProjectile(exportGroup, projectile);
                break;
            case IFlashSourcePayload source:
                TrackFlashSource(exportGroup.ActorNetGuid, source);
                break;
            case BlindManagerComponentDescriptor blindManager:
                TrackBlindManager(exportGroup, blindManager);
                break;
            case EffectManagerComponentDescriptor effectManager:
                TrackEffectManager(exportGroup, effectManager);
                break;
            case GenericAgentDescriptor agent:
                TrackAgent(exportGroup.ActorNetGuid, agent);
                break;
            case BombPlayerStateDescriptor playerState:
                TrackPlayerState(exportGroup.ActorNetGuid, playerState);
                break;
        }
    }

    private void TrackProjectile(ExportGroupReceived exportGroup, IFlashProjectilePayload projectile)
    {
        if (!_flashes.TryGetValue(exportGroup.ActorNetGuid, out var state))
        {
            return;
        }

        if (projectile.HasDecoded(nameof(IFlashProjectilePayload.Owner)) && projectile.Owner is > 0)
        {
            state.OwnerNetGuid = projectile.Owner;
        }

        if (projectile.HasDecoded(nameof(IFlashProjectilePayload.Instigator)) && projectile.Instigator is > 0)
        {
            state.InstigatorNetGuid = projectile.Instigator;
        }

        EnsureCastEmitted(state);

        if (!projectile.HasDecoded(nameof(IFlashProjectilePayload.ReplicatedMovement)) ||
            projectile.ReplicatedMovement is not { } movement)
        {
            return;
        }

        state.LastLocation = movement.Location ?? state.LastLocation;
        _inner.Emit(new ValorantFlashPathUpdated(
            exportGroup.TimeSeconds,
            exportGroup.PacketId,
            state.ActorNetGuid,
            state.Kind,
            state.NextSampleIndex++,
            ValorantFlashPathSampleSource.ReplicatedMovement,
            movement.Location,
            movement.Rotation,
            movement.LinearVelocity,
            movement.AngularVelocity,
            movement.ServerFrame));
    }

    private void TrackFlashSource(uint sourceActorNetGuid, IFlashSourcePayload source)
    {
        if (!_flashes.TryGetValue(sourceActorNetGuid, out var state) &&
            !_flashBySourceActor.TryGetValue(sourceActorNetGuid, out state))
        {
            return;
        }

        if (source.Owner is > 0)
        {
            state.OwnerNetGuid = source.Owner;
        }

        if (source.Instigator is > 0)
        {
            state.InstigatorNetGuid = source.Instigator;
        }

        _flashBySourceActor[sourceActorNetGuid] = state;
        EnsureCastEmitted(state);
    }

    private void TrackRpc(RpcReceived rpc)
    {
        if (rpc.FunctionName == "MulticastStopProjectile" &&
            _flashes.TryGetValue(rpc.ActorNetGuid, out var projectile))
        {
            EmitExplosion(
                projectile,
                rpc.TimeSeconds,
                rpc.PacketId,
                projectile.LastLocation,
                ValorantFlashExplosionEvidence.StopProjectileRpc);
            return;
        }

        if (rpc.FunctionName == "Multicast Set Flash Duration" &&
            rpc.Payload is SkyeSetFlashDurationParameters duration &&
            _flashBySourceActor.TryGetValue(rpc.ActorNetGuid, out var skyeFlash))
        {
            skyeFlash.MaxFlashDurationSeconds = duration.MaxFlashDuration;
            var source = skyeFlash.SourceSpawn;
            EmitExplosion(
                skyeFlash,
                source?.TimeSeconds ?? rpc.TimeSeconds,
                source?.PacketId ?? rpc.PacketId,
                source?.Location ?? skyeFlash.LastLocation,
                ValorantFlashExplosionEvidence.SkyeFlashSource);
            return;
        }

        if (rpc.Payload is IEffectDataPayload effectData)
        {
            TrackEffectData(
                rpc.ActorNetGuid,
                rpc.TimeSeconds,
                rpc.PacketId,
                effectData,
                GetEffectId(rpc.Payload),
                GetEffectStartMovementTime(rpc.Payload));
        }
    }

    private void TrackEffectManager(
        ExportGroupReceived exportGroup,
        EffectManagerComponentDescriptor effectManager)
    {
        if (!effectManager.HasDecoded(nameof(EffectManagerComponentDescriptor.ServerActiveEffects)) ||
            effectManager.ServerActiveEffects is null)
        {
            return;
        }

        foreach (var effect in effectManager.ServerActiveEffects)
        {
            if (effect is null)
            {
                continue;
            }

            TrackEffectData(
                exportGroup.ActorNetGuid,
                exportGroup.TimeSeconds,
                exportGroup.PacketId,
                effect,
                effect.EffectId,
                startNetMovementTime: null);
        }
    }

    private void TrackEffectData(
        uint targetCharacterNetGuid,
        float timeSeconds,
        int packetId,
        IEffectDataPayload effectData,
        ulong? effectId,
        float? startNetMovementTime)
    {
        if (!_playerCharacters.Contains(targetCharacterNetGuid))
        {
            return;
        }

        var duration = effectData.FloatValues?
            .FirstOrDefault(value => value.Name?.TagName == "FXC.TimedStateDuration")
            ?.Value;
        var contextActor = effectData.ObjectValues?
            .FirstOrDefault(value => value.Name?.TagName == "FXC.EffectContext")
            ?.Value;
        if (duration is not { } durationValue ||
            !float.IsFinite(durationValue) ||
            durationValue < 0 ||
            contextActor is not > 0)
        {
            return;
        }

        var resolution = ResolveFlashFromActor(contextActor);
        if (resolution.State is not { Exploded: true, ExplosionTimeSeconds: { } explosion } state ||
            timeSeconds < explosion)
        {
            return;
        }

        var key = new EffectFallbackKey(
            targetCharacterNetGuid,
            effectId,
            contextActor.Value,
            startNetMovementTime is { } start ? BitConverter.SingleToInt32Bits(start) : 0,
            BitConverter.SingleToInt32Bits(durationValue));
        if (!_seenEffectFallbacks.Add(key))
        {
            return;
        }

        _pendingEffectFallbacks.Add(new EffectFallbackCandidate(
            timeSeconds,
            packetId,
            targetCharacterNetGuid,
            state,
            durationValue,
            effectId,
            contextActor,
            startNetMovementTime,
            resolution.Correlation));
    }

    private void TrackBlindManager(
        ExportGroupReceived exportGroup,
        BlindManagerComponentDescriptor blindManager)
    {
        if (!blindManager.HasDecoded(nameof(BlindManagerComponentDescriptor.ActiveBlinds)) ||
            blindManager.ActiveBlinds is null)
        {
            return;
        }

        foreach (var blind in blindManager.ActiveBlinds)
        {
            if (blind is null)
            {
                continue;
            }

            if (blind.EffectId is { } effectId)
            {
                _authoritativeTargetEffects.Add(new TargetEffectKey(exportGroup.ActorNetGuid, effectId));
            }

            var key = new BlindKey(
                exportGroup.ActorNetGuid,
                blind.BlindId,
                blind.EffectId,
                blind.StartNetMovementTime is { } start
                    ? BitConverter.SingleToInt32Bits(start)
                    : 0);
            if (!_emittedBlinds.Add(key))
            {
                continue;
            }

            var resolution = ResolveFlash(
                blind.CausingActor,
                blind.BlindConfig,
                exportGroup.TimeSeconds);
            if (resolution.State is { } resolvedFlash)
            {
                _authoritativeTargetFlashes.Add(
                    new TargetFlashKey(exportGroup.ActorNetGuid, resolvedFlash.ActorNetGuid));
            }

            if (resolution.State is { Kind: ValorantFlashKind.VyseArcRose } vyseState)
            {
                EmitExplosion(
                    vyseState,
                    exportGroup.TimeSeconds,
                    exportGroup.PacketId,
                    vyseState.Spawn.Location ?? vyseState.LastLocation,
                    ValorantFlashExplosionEvidence.VyseFlashSource);
            }
            else if (resolution.State is { } state && !state.Exploded && state.SourceSpawn is { } source)
            {
                EmitExplosion(
                    state,
                    source.TimeSeconds,
                    source.PacketId,
                    source.Location ?? state.LastLocation,
                    ValorantFlashExplosionEvidence.SkyeFlashSource);
            }

            var playerStateNetGuid = GetPlayerState(exportGroup.ActorNetGuid);
            var duration = blind.InitialDuration is { } value && float.IsFinite(value) && value >= 0
                ? (float?)value
                : null;
            _inner.Emit(new ValorantFlashPlayerHit(
                exportGroup.TimeSeconds,
                exportGroup.PacketId,
                resolution.State?.ActorNetGuid,
                resolution.State?.Kind ?? ResolveKindFromBlindConfig(blind.BlindConfig),
                exportGroup.ActorNetGuid,
                playerStateNetGuid,
                GetSubject(playerStateNetGuid),
                duration,
                blind.BlindId,
                blind.EffectId,
                blind.BlindConfig,
                blind.CausingActor,
                blind.StartNetMovementTime,
                resolution.Correlation,
                duration.HasValue
                    ? ValorantFlashDurationSource.BlindManagerInitialDuration
                    : ValorantFlashDurationSource.Missing));
        }
    }

    private FlashResolution ResolveFlash(
        uint? causingActorNetGuid,
        uint? blindConfigNetGuid,
        float timeSeconds)
    {
        var direct = ResolveFlashFromActor(causingActorNetGuid);
        if (direct.State is not null)
        {
            return direct;
        }

        var configKind = ResolveKindFromBlindConfig(blindConfigNetGuid);
        if (configKind is null)
        {
            return FlashResolution.Unresolved;
        }

        var candidates = _flashes.Values
            .Where(flash => MatchesKindFamily(flash.Kind, configKind.Value))
            .Where(flash => flash.ExplosionTimeSeconds is { } explosion &&
                            timeSeconds >= explosion &&
                            timeSeconds - explosion <= UniqueBlindCandidateSeconds)
            .Take(2)
            .ToArray();
        return candidates.Length == 1
            ? new FlashResolution(candidates[0], ValorantFlashHitCorrelation.BlindConfigAndUniqueExplosion)
            : FlashResolution.Unresolved;
    }

    private FlashResolution ResolveFlashFromActor(uint? actorNetGuid)
    {
        if (actorNetGuid is not > 0)
        {
            return FlashResolution.Unresolved;
        }

        if (_flashes.TryGetValue(actorNetGuid.Value, out var projectile))
        {
            return new FlashResolution(projectile, ValorantFlashHitCorrelation.CausingProjectile);
        }

        if (_flashBySourceActor.TryGetValue(actorNetGuid.Value, out var source))
        {
            return new FlashResolution(source, ValorantFlashHitCorrelation.CausingFlashSource);
        }

        var current = actorNetGuid.Value;
        for (var depth = 0; depth < MaxOuterDepth; depth++)
        {
            if (!_netGuidCache.TryGetOuterNetGuid(current, out NetworkGuid outer))
            {
                break;
            }

            current = outer.Value;
            if (_flashes.TryGetValue(current, out var fromOuter) ||
                _flashBySourceActor.TryGetValue(current, out fromOuter))
            {
                return new FlashResolution(fromOuter, ValorantFlashHitCorrelation.CausingActorOuter);
            }
        }

        return FlashResolution.Unresolved;
    }

    private void FlushEffectFallbacks(float currentTimeSeconds, bool force)
    {
        for (var index = 0; index < _pendingEffectFallbacks.Count;)
        {
            var candidate = _pendingEffectFallbacks[index];
            if (!force && currentTimeSeconds - candidate.TimeSeconds <= EffectFallbackDelaySeconds)
            {
                index++;
                continue;
            }

            _pendingEffectFallbacks.RemoveAt(index);
            if ((candidate.EffectId is { } effectId &&
                 _authoritativeTargetEffects.Contains(
                     new TargetEffectKey(candidate.TargetCharacterNetGuid, effectId))) ||
                _authoritativeTargetFlashes.Contains(
                    new TargetFlashKey(candidate.TargetCharacterNetGuid, candidate.State.ActorNetGuid)))
            {
                continue;
            }

            var playerStateNetGuid = GetPlayerState(candidate.TargetCharacterNetGuid);
            _inner.Emit(new ValorantFlashPlayerHit(
                candidate.TimeSeconds,
                candidate.PacketId,
                candidate.State.ActorNetGuid,
                candidate.State.Kind,
                candidate.TargetCharacterNetGuid,
                playerStateNetGuid,
                GetSubject(playerStateNetGuid),
                candidate.DurationSeconds,
                BlindId: null,
                candidate.EffectId,
                BlindConfigNetGuid: null,
                candidate.ContextActorNetGuid,
                candidate.StartNetMovementTime,
                candidate.Correlation,
                ValorantFlashDurationSource.EffectDataGameplayTag));
        }
    }

    private static ulong? GetEffectId(object payload) => payload switch
    {
        EffectManagerPlayContinuousParameters continuous => continuous.EffectId,
        EffectManagerUpdateContinuousParameters update => update.EffectId,
        ActiveEffectInfoDescriptor active => active.EffectId,
        _ => null,
    };

    private static float? GetEffectStartMovementTime(object payload) => payload switch
    {
        EffectManagerPlayContinuousParameters continuous => continuous.StartMovementTime,
        EffectManagerPlayOneShotParameters oneShot => oneShot.StartMovementTime,
        _ => null,
    };

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
        else if (playerState.HasDecoded(nameof(BombPlayerStateDescriptor.SpawnedCharacter)))
        {
            SetCharacterPlayerState(playerState.SpawnedCharacter, playerStateNetGuid);
        }
    }

    private void SetCharacterPlayerState(uint characterNetGuid, uint playerStateNetGuid)
    {
        if (characterNetGuid == 0 || playerStateNetGuid == 0)
        {
            return;
        }

        _playerCharacters.Add(characterNetGuid);
        _playerStateByCharacter[characterNetGuid] = playerStateNetGuid;
    }

    private void EnsureCastEmitted(FlashState state)
    {
        if (state.CastEmitted)
        {
            return;
        }

        var caster = ResolveCaster(state);
        var playerState = GetPlayerState(caster);
        _inner.Emit(new ValorantFlashCast(
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

        _inner.Emit(new ValorantFlashPathUpdated(
            state.Spawn.TimeSeconds,
            state.Spawn.PacketId,
            state.ActorNetGuid,
            state.Kind,
            state.NextSampleIndex++,
            ValorantFlashPathSampleSource.SpawnTransform,
            state.Spawn.Location,
            state.Spawn.Rotation,
            state.Spawn.Velocity,
            null,
            null));
    }

    private uint? ResolveCaster(FlashState state)
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

    private void FlushDeferredExplosions(float currentTimeSeconds)
    {
        foreach (var state in _flashes.Values
                     .Where(flash => !flash.Exploded &&
                     !flash.IsReusableSource &&
                     flash.CloseTimeSeconds is { } close &&
                                     currentTimeSeconds - close > DeferredExplosionSeconds)
                     .ToArray())
        {
            EmitDeferredExplosion(state);
        }
    }

    private void EmitDeferredExplosion(FlashState state)
    {
        if (state.SourceSpawn is { } source)
        {
            EmitExplosion(
                state,
                source.TimeSeconds,
                source.PacketId,
                source.Location ?? state.LastLocation,
                ValorantFlashExplosionEvidence.SkyeFlashSource);
            return;
        }

        EmitExplosion(
            state,
            state.CloseTimeSeconds!.Value,
            state.ClosePacketId,
            state.LastLocation,
            ValorantFlashExplosionEvidence.ActorDestroyedFallback);
    }

    private void EmitExplosion(
        FlashState state,
        float timeSeconds,
        int packetId,
        FVector? location,
        ValorantFlashExplosionEvidence evidence)
    {
        if (state.Exploded && !state.IsReusableSource)
        {
            return;
        }

        EnsureCastEmitted(state);
        state.Exploded = true;
        state.ExplosionTimeSeconds = timeSeconds;
        state.LastLocation = location ?? state.LastLocation;
        _inner.Emit(new ValorantFlashExploded(
            timeSeconds,
            packetId,
            state.ActorNetGuid,
            state.Kind,
            state.LastLocation,
            evidence,
            state.MaxFlashDurationSeconds));
    }

    private uint? GetPlayerState(uint? characterNetGuid) =>
        characterNetGuid is { } character && _playerStateByCharacter.TryGetValue(character, out var playerState)
            ? playerState
            : null;

    private string? GetSubject(uint? playerStateNetGuid) =>
        playerStateNetGuid is { } playerState && _playersByState.TryGetValue(playerState, out var identity)
            ? identity.Subject
            : null;

    private ValorantFlashKind? ResolveKindFromBlindConfig(uint? blindConfigNetGuid)
    {
        if (blindConfigNetGuid is not > 0 ||
            !_netGuidCache.TryGetPath(blindConfigNetGuid.Value, out var path))
        {
            return null;
        }

        if (path.Contains("Guide", StringComparison.OrdinalIgnoreCase))
        {
            return ValorantFlashKind.SkyeGuidingLight;
        }

        if (path.Contains("Grenadier", StringComparison.OrdinalIgnoreCase))
        {
            return ValorantFlashKind.KayoFlashDriveOverhand;
        }

        if (path.Contains("Breach", StringComparison.OrdinalIgnoreCase))
        {
            return ValorantFlashKind.BreachFlashpoint;
        }

        return path.Contains("Phoenix", StringComparison.OrdinalIgnoreCase)
            ? ValorantFlashKind.PhoenixCurveballLeft
            : null;
    }

    private static bool MatchesKindFamily(ValorantFlashKind candidate, ValorantFlashKind configKind) =>
        candidate == configKind ||
        candidate is ValorantFlashKind.KayoFlashDriveOverhand or ValorantFlashKind.KayoFlashDriveUnderhand &&
        configKind is ValorantFlashKind.KayoFlashDriveOverhand or ValorantFlashKind.KayoFlashDriveUnderhand ||
        candidate is ValorantFlashKind.PhoenixCurveballLeft or ValorantFlashKind.PhoenixCurveballRight &&
        configKind is ValorantFlashKind.PhoenixCurveballLeft or ValorantFlashKind.PhoenixCurveballRight;

    private static bool TryResolveFlashKind(ActorSpawned spawned, out ValorantFlashKind kind)
    {
        foreach (var path in new[] { spawned.ArchetypePath, spawned.ReplicationClassPath, spawned.ActorPath })
        {
            ValorantFlashKind? resolved = path switch
            {
                FlashPaths.SkyeProjectile or "Default__Projectile_Guide_E_HawkFlash_C" =>
                    ValorantFlashKind.SkyeGuidingLight,
                FlashPaths.KayoOverhandProjectile or "Default__Projectile_C_Grenadier_Flash_C" =>
                    ValorantFlashKind.KayoFlashDriveOverhand,
                FlashPaths.KayoUnderhandProjectile or "Default__Projectile_C_Grenadier_Flash_Underhand_C" =>
                    ValorantFlashKind.KayoFlashDriveUnderhand,
                FlashPaths.BreachProjectile or "Default__Projectile_Breach_Q_ThroughWalls_Flash_C" =>
                    ValorantFlashKind.BreachFlashpoint,
                FlashPaths.PhoenixLeftProjectile or "Default__Projectile_Phoenix_E_FlareCurve_Synced_C" =>
                    ValorantFlashKind.PhoenixCurveballLeft,
                FlashPaths.PhoenixRightProjectile or "Default__Projectile_Phoenix_E_FlareCurve_Synced_Right_C" =>
                    ValorantFlashKind.PhoenixCurveballRight,
                FlashPaths.VyseFlashTrap or "Default__GameObject_Nox_StealthingTrap_Flash_2_C" =>
                    ValorantFlashKind.VyseArcRose,
                FlashPaths.YoruProjectile or "Default__Projectile_Stealth_Q_BounceFlash_C" =>
                    ValorantFlashKind.YoruBlindside,
                _ => null,
            };
            if (resolved is { } value)
            {
                kind = value;
                return true;
            }
        }

        kind = default;
        return false;
    }

    private static bool IsSkyeFlashSource(ActorSpawned spawned)
    {
        return spawned.ArchetypePath is FlashPaths.SkyeFlashSource or
                   "Default__GameObject_Guide_E_HawkFlash_FlashSource_C" ||
               spawned.ReplicationClassPath is FlashPaths.SkyeFlashSource or
                   "Default__GameObject_Guide_E_HawkFlash_FlashSource_C" ||
               spawned.ActorPath is FlashPaths.SkyeFlashSource or
                   "Default__GameObject_Guide_E_HawkFlash_FlashSource_C";
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

    private sealed class FlashState(ActorSpawned spawn, ValorantFlashKind kind)
    {
        public ActorSpawned Spawn { get; } = spawn;
        public uint ActorNetGuid => Spawn.ActorNetGuid;
        public ValorantFlashKind Kind { get; } = kind;
        public uint? OwnerNetGuid { get; set; }
        public uint? InstigatorNetGuid { get; set; }
        public FVector? LastLocation { get; set; } = spawn.Location;
        public int NextSampleIndex { get; set; }
        public bool CastEmitted { get; set; }
        public bool Exploded { get; set; }
        public float? ExplosionTimeSeconds { get; set; }
        public float? CloseTimeSeconds { get; set; }
        public int ClosePacketId { get; set; }
        public uint? SourceActorNetGuid { get; set; }
        public ActorSpawned? SourceSpawn { get; set; }
        public double? MaxFlashDurationSeconds { get; set; }
        public bool IsReusableSource => Kind == ValorantFlashKind.VyseArcRose;
    }

    private sealed class PlayerIdentity
    {
        public string? Subject { get; set; }
    }

    private readonly record struct BlindKey(
        uint TargetCharacterNetGuid,
        uint BlindId,
        ulong? EffectId,
        int StartMovementTimeBits);

    private readonly record struct TargetEffectKey(uint TargetCharacterNetGuid, ulong EffectId);

    private readonly record struct TargetFlashKey(uint TargetCharacterNetGuid, uint FlashActorNetGuid);

    private readonly record struct EffectFallbackKey(
        uint TargetCharacterNetGuid,
        ulong? EffectId,
        uint ContextActorNetGuid,
        int StartMovementTimeBits,
        int DurationBits);

    private sealed record EffectFallbackCandidate(
        float TimeSeconds,
        int PacketId,
        uint TargetCharacterNetGuid,
        FlashState State,
        float DurationSeconds,
        ulong? EffectId,
        uint? ContextActorNetGuid,
        float? StartNetMovementTime,
        ValorantFlashHitCorrelation Correlation);

    private readonly record struct FlashResolution(
        FlashState? State,
        ValorantFlashHitCorrelation Correlation)
    {
        public static FlashResolution Unresolved { get; } =
            new(null, ValorantFlashHitCorrelation.Unresolved);
    }
}
