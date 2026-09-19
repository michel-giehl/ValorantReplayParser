using System.Text.Json;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Valorant.Combat;
using Replay.Valorant.Flashes;
using Replay.Valorant.Movement;
using Replay.Valorant.Nearsights;
using Replay.Valorant.Walls;

namespace CliReader.JsonExport;

internal sealed class ReplayEventJsonWriter
{
    private readonly ReplayJsonNormalizer _normalizer;

    public ReplayEventJsonWriter(ReplayJsonNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public void WriteActorSpawned(Utf8JsonWriter writer, ActorSpawned spawned)
    {
        WriteEventStart(writer, "actor_spawned", spawned);
        writer.WriteNumber("actor_net_guid", spawned.ActorNetGuid);
        writer.WriteNumber("channel", spawned.ChannelIndex);
        writer.WriteBoolean("is_dynamic", spawned.IsDynamic);
        WriteNullableString(writer, "actor_path", spawned.ActorPath);
        writer.WriteNumber("archetype_net_guid", spawned.ArchetypeNetGuid);
        WriteNullableString(writer, "archetype_path", spawned.ArchetypePath);
        WriteNullableString(writer, "replication_class_path", spawned.ReplicationClassPath);
        writer.WriteNumber("level_net_guid", spawned.LevelNetGuid);
        WriteNullableValue(writer, "location", spawned.Location);
        WriteNullableValue(writer, "rotation", spawned.Rotation);
        WriteNullableValue(writer, "scale", spawned.Scale);
        WriteNullableValue(writer, "velocity", spawned.Velocity);
        writer.WriteEndObject();
    }

    public static void WriteActorClosed(Utf8JsonWriter writer, ActorClosed closed)
    {
        WriteEventStart(writer, "actor_closed", closed);
        writer.WriteNumber("actor_net_guid", closed.ActorNetGuid);
        writer.WriteNumber("channel", closed.ChannelIndex);
        writer.WriteString("reason", ReplayJsonNormalizer.ToSnakeCase(closed.Reason.ToString()));
        writer.WriteEndObject();
    }

    public void WriteExportGroup(Utf8JsonWriter writer, ExportGroupReceived exportGroup)
    {
        WriteEventStart(writer, "export_group_received", exportGroup);
        WriteObjectIdentity(writer, exportGroup.ActorNetGuid, exportGroup.ObjectNetGuid, exportGroup.ChannelIndex);
        writer.WriteBoolean("is_actor", exportGroup.IsActor);
        writer.WriteBoolean("is_deleted", exportGroup.IsDeleted);
        writer.WriteNumber("delete_flags", exportGroup.DeleteFlags);
        WriteNullableString(writer, "export_group_path", exportGroup.ExportGroupPath);
        writer.WriteString("kind", ReplayJsonNormalizer.ToSnakeCase(exportGroup.Kind.ToString()));
        WriteCategories(writer, exportGroup.Categories);
        writer.WriteNumber("class_net_guid", exportGroup.ClassNetGuid);
        writer.WriteNumber("outer_net_guid", exportGroup.OuterNetGuid);
        WriteNullableString(writer, "object_path", exportGroup.ObjectPath);
        WriteNullableString(writer, "class_path", exportGroup.ClassPath);
        WriteNullableString(writer, "outer_path", exportGroup.OuterPath);
        WriteDecodeMetadata(
            writer,
            exportGroup.PayloadBits,
            exportGroup.ParsedBits,
            true,
            exportGroup.DecodedFieldCount);
        WritePayload(writer, exportGroup.Payload);
        WriteDiagnosticFields(writer, exportGroup.DiagnosticFields);
        writer.WriteEndObject();
    }

    public void WriteRpc(Utf8JsonWriter writer, RpcReceived rpc)
    {
        WriteEventStart(writer, "rpc_received", rpc);
        WriteObjectIdentity(writer, rpc.ActorNetGuid, rpc.ObjectNetGuid, rpc.ChannelIndex);
        writer.WriteString("class_path", rpc.ClassPath);
        writer.WriteString("function_name", rpc.FunctionName);
        writer.WriteString("function_export_path", rpc.FunctionExportPath);
        writer.WriteNumber("function_handle", rpc.FunctionHandle);
        WriteCategories(writer, rpc.Categories);
        WriteDecodeMetadata(writer, rpc.PayloadBits, rpc.ParsedBits, rpc.WasDecoded, rpc.DecodedFieldCount);
        WritePayload(writer, rpc.Payload);
        WriteDiagnosticFields(writer, rpc.DiagnosticFields);
        writer.WriteEndObject();
    }

    public void WriteValorantShot(Utf8JsonWriter writer, ValorantShotReceived shotReceived)
    {
        WriteEventStart(writer, "valorant_shot_received", shotReceived);
        WriteObjectIdentity(
            writer,
            shotReceived.ActorNetGuid,
            shotReceived.ObjectNetGuid,
            shotReceived.ChannelIndex);

        var shot = shotReceived.Shot;
        writer.WriteStartObject("shot");
        WriteNullableValue(writer, "effect_id", shot.EffectId);
        WriteNullableValue(writer, "start_movement_time", shot.StartMovementTime);
        WriteNullableString(writer, "source_id", shot.SourceId);
        WriteNullableValue(writer, "is_local_effect", shot.IsLocalEffect);
        WriteNullableValue(writer, "is_transient", shot.IsTransient);
        WriteNullableValue(writer, "wait_on_replication_actor", shot.WaitOnReplicationActor);
        WriteNullableValue(writer, "alliance_filter", shot.AllianceFilter);
        WriteNullableValue(writer, "location", shot.Location);
        WriteNullableValue(writer, "rotation", shot.Rotation);
        WriteNullableValue(writer, "ammo_remaining", shot.AmmoRemaining);
        WriteNullableValue(writer, "num_projectiles", shot.NumProjectiles);
        WriteNullableValue(writer, "random_seed", shot.RandomSeed);
        WriteNullableValue(writer, "tracer_option", shot.TracerOption);
        WriteNullableValue(writer, "burst_shot_number", shot.BurstShotNumber);
        WriteNullableValue(writer, "yaw_switch", shot.YawSwitch);
        WriteNullableValue(writer, "firing_player_state", shot.FiringPlayerState);
        WriteNullableValue(writer, "firing_state", shot.FiringState);
        WriteNullableValue(writer, "attack_vectors", shot.AttackVectors);
        WriteNullableValue(writer, "effect_equippable", shot.EffectEquippable);
        WriteEquippable(writer, shot.Equippable);
        writer.WriteString("fire_mode", ReplayJsonNormalizer.ToSnakeCase(shot.FireMode.ToString()));
        WriteNullableString(writer, "fire_mode_evidence", shot.FireModeEvidence);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    public void WriteValorantFlashCast(Utf8JsonWriter writer, ValorantFlashCast cast)
    {
        WriteEventStart(writer, "valorant_flash_cast", cast);
        WriteFlashIdentity(writer, cast.FlashActorNetGuid, cast.FlashKind);
        WriteNullableValue(writer, "caster_character_net_guid", cast.CasterCharacterNetGuid);
        WriteNullableValue(writer, "caster_player_state_net_guid", cast.CasterPlayerStateNetGuid);
        WriteNullableString(writer, "caster_subject", cast.CasterSubject);
        WriteNullableValue(writer, "location", cast.Location);
        WriteNullableValue(writer, "rotation", cast.Rotation);
        WriteNullableValue(writer, "velocity", cast.Velocity);
        writer.WriteEndObject();
    }

    public void WriteValorantFlashPathUpdated(Utf8JsonWriter writer, ValorantFlashPathUpdated path)
    {
        WriteEventStart(writer, "valorant_flash_path_updated", path);
        WriteFlashIdentity(writer, path.FlashActorNetGuid, path.FlashKind);
        writer.WriteNumber("sample_index", path.SampleIndex);
        writer.WriteString("source", ReplayJsonNormalizer.ToSnakeCase(path.Source.ToString()));
        WriteNullableValue(writer, "location", path.Location);
        WriteNullableValue(writer, "rotation", path.Rotation);
        WriteNullableValue(writer, "linear_velocity", path.LinearVelocity);
        WriteNullableValue(writer, "angular_velocity", path.AngularVelocity);
        WriteNullableValue(writer, "server_frame", path.ServerFrame);
        writer.WriteEndObject();
    }

    public void WriteValorantFlashExploded(Utf8JsonWriter writer, ValorantFlashExploded exploded)
    {
        WriteEventStart(writer, "valorant_flash_exploded", exploded);
        WriteFlashIdentity(writer, exploded.FlashActorNetGuid, exploded.FlashKind);
        WriteNullableValue(writer, "location", exploded.Location);
        writer.WriteString("evidence", ReplayJsonNormalizer.ToSnakeCase(exploded.Evidence.ToString()));
        WriteNullableValue(writer, "max_flash_duration_seconds", exploded.MaxFlashDurationSeconds);
        writer.WriteEndObject();
    }

    public void WriteValorantFlashPlayerHit(Utf8JsonWriter writer, ValorantFlashPlayerHit hit)
    {
        WriteEventStart(writer, "valorant_flash_player_hit", hit);
        WriteNullableValue(writer, "flash_actor_net_guid", hit.FlashActorNetGuid);
        if (hit.FlashKind is { } flashKind)
        {
            writer.WriteString("flash_kind", ReplayJsonNormalizer.ToSnakeCase(flashKind.ToString()));
        }
        else
        {
            writer.WriteNull("flash_kind");
        }

        writer.WriteNumber("target_character_net_guid", hit.TargetCharacterNetGuid);
        WriteNullableValue(writer, "target_player_state_net_guid", hit.TargetPlayerStateNetGuid);
        WriteNullableString(writer, "target_subject", hit.TargetSubject);
        WriteNullableValue(writer, "initial_duration_seconds", hit.InitialDurationSeconds);
        WriteNullableValue(writer, "blind_id", hit.BlindId);
        WriteNullableValue(writer, "effect_id", hit.EffectId);
        WriteNullableValue(writer, "blind_config_net_guid", hit.BlindConfigNetGuid);
        WriteNullableValue(writer, "causing_actor_net_guid", hit.CausingActorNetGuid);
        WriteNullableValue(writer, "start_net_movement_time", hit.StartNetMovementTime);
        writer.WriteString("correlation", ReplayJsonNormalizer.ToSnakeCase(hit.Correlation.ToString()));
        writer.WriteString("duration_source", ReplayJsonNormalizer.ToSnakeCase(hit.DurationSource.ToString()));
        writer.WriteEndObject();
    }

    public void WriteValorantNearsightCast(Utf8JsonWriter writer, ValorantNearsightCast cast)
    {
        WriteEventStart(writer, "valorant_nearsight_cast", cast);
        WriteNearsightIdentity(writer, cast.NearsightActorNetGuid, cast.NearsightKind);
        WriteNullableValue(writer, "caster_character_net_guid", cast.CasterCharacterNetGuid);
        WriteNullableValue(writer, "caster_player_state_net_guid", cast.CasterPlayerStateNetGuid);
        WriteNullableString(writer, "caster_subject", cast.CasterSubject);
        WriteNullableValue(writer, "location", cast.Location);
        WriteNullableValue(writer, "rotation", cast.Rotation);
        WriteNullableValue(writer, "velocity", cast.Velocity);
        writer.WriteEndObject();
    }

    public void WriteValorantNearsightPathUpdated(Utf8JsonWriter writer, ValorantNearsightPathUpdated path)
    {
        WriteEventStart(writer, "valorant_nearsight_path_updated", path);
        WriteNearsightIdentity(writer, path.NearsightActorNetGuid, path.NearsightKind);
        writer.WriteNumber("sample_index", path.SampleIndex);
        writer.WriteString("source", ReplayJsonNormalizer.ToSnakeCase(path.Source.ToString()));
        WriteNullableValue(writer, "location", path.Location);
        WriteNullableValue(writer, "rotation", path.Rotation);
        WriteNullableValue(writer, "linear_velocity", path.LinearVelocity);
        WriteNullableValue(writer, "angular_velocity", path.AngularVelocity);
        WriteNullableValue(writer, "server_frame", path.ServerFrame);
        writer.WriteEndObject();
    }

    public void WriteValorantNearsightActivated(Utf8JsonWriter writer, ValorantNearsightActivated activated)
    {
        WriteEventStart(writer, "valorant_nearsight_activated", activated);
        WriteNearsightIdentity(writer, activated.NearsightActorNetGuid, activated.NearsightKind);
        WriteNullableValue(writer, "source_actor_net_guid", activated.SourceActorNetGuid);
        WriteNullableValue(writer, "location", activated.Location);
        writer.WriteString("evidence", ReplayJsonNormalizer.ToSnakeCase(activated.Evidence.ToString()));
        writer.WriteEndObject();
    }

    public void WriteValorantNearsightPlayerHit(Utf8JsonWriter writer, ValorantNearsightPlayerHit hit)
    {
        WriteEventStart(writer, "valorant_nearsight_player_hit", hit);
        WriteNearsightIdentity(writer, hit.NearsightActorNetGuid, hit.NearsightKind);
        writer.WriteNumber("target_character_net_guid", hit.TargetCharacterNetGuid);
        WriteNullableValue(writer, "target_player_state_net_guid", hit.TargetPlayerStateNetGuid);
        WriteNullableString(writer, "target_subject", hit.TargetSubject);
        WriteNullableValue(writer, "configured_duration_seconds", hit.ConfiguredDurationSeconds);
        writer.WriteBoolean("duration_until_removed", hit.DurationUntilRemoved);
        WriteNullableValue(writer, "effect_id", hit.EffectId);
        WriteNullableValue(writer, "effect_container_net_guid", hit.EffectContainerNetGuid);
        writer.WriteNumber("effect_context_net_guid", hit.EffectContextNetGuid);
        WriteNullableValue(writer, "start_net_movement_time", hit.StartNetMovementTime);
        writer.WriteString("correlation", ReplayJsonNormalizer.ToSnakeCase(hit.Correlation.ToString()));
        writer.WriteString("duration_source", ReplayJsonNormalizer.ToSnakeCase(hit.DurationSource.ToString()));
        writer.WriteEndObject();
    }

    public void WriteValorantNearsightPlayerEffectEnded(
        Utf8JsonWriter writer,
        ValorantNearsightPlayerEffectEnded ended)
    {
        WriteEventStart(writer, "valorant_nearsight_player_effect_ended", ended);
        WriteNearsightIdentity(writer, ended.NearsightActorNetGuid, ended.NearsightKind);
        writer.WriteNumber("target_character_net_guid", ended.TargetCharacterNetGuid);
        WriteNullableValue(writer, "target_player_state_net_guid", ended.TargetPlayerStateNetGuid);
        WriteNullableString(writer, "target_subject", ended.TargetSubject);
        writer.WriteNumber("effect_id", ended.EffectId);
        writer.WriteNumber("applied_time_ms", ToMilliseconds(ended.AppliedTimeSeconds));
        WriteNullableValue(writer, "start_net_movement_time", ended.StartNetMovementTime);
        WriteNullableValue(writer, "stop_net_movement_time", ended.StopNetMovementTime);
        writer.WriteNumber("observed_duration_seconds", ended.ObservedDurationSeconds);
        writer.WriteEndObject();
    }

    public void WriteValorantWallPlaced(Utf8JsonWriter writer, ValorantWallPlaced placed)
    {
        WriteEventStart(writer, "valorant_wall_placed", placed);
        WriteWallIdentity(writer, placed.WallActorNetGuid, placed.WallKind);
        WriteNullableValue(writer, "caster_character_net_guid", placed.CasterCharacterNetGuid);
        WriteNullableValue(writer, "caster_player_state_net_guid", placed.CasterPlayerStateNetGuid);
        WriteNullableString(writer, "caster_subject", placed.CasterSubject);
        WriteNullableValue(writer, "location", placed.Location);
        WriteNullableValue(writer, "rotation", placed.Rotation);
        WriteNullableValue(writer, "wall_start", placed.WallStart);
        WriteNullableValue(writer, "wall_end", placed.WallEnd);
        WriteNullableValue(writer, "impact_point", placed.ImpactPoint);
        WriteNullableValue(writer, "impact_normal", placed.ImpactNormal);
        writer.WriteString("evidence", ReplayJsonNormalizer.ToSnakeCase(placed.Evidence.ToString()));
        writer.WriteEndObject();
    }

    public void WriteValorantWallSegmentSpawned(Utf8JsonWriter writer, ValorantWallSegmentSpawned spawned)
    {
        WriteEventStart(writer, "valorant_wall_segment_spawned", spawned);
        writer.WriteNumber("wall_actor_net_guid", spawned.WallActorNetGuid);
        writer.WriteNumber("segment_actor_net_guid", spawned.SegmentActorNetGuid);
        writer.WriteNumber("segment_index", spawned.SegmentIndex);
        WriteNullableValue(writer, "location", spawned.Location);
        WriteNullableValue(writer, "rotation", spawned.Rotation);
        writer.WriteEndObject();
    }

    public void WriteValorantWallActivated(Utf8JsonWriter writer, ValorantWallActivated activated)
    {
        WriteEventStart(writer, "valorant_wall_activated", activated);
        WriteWallIdentity(writer, activated.WallActorNetGuid, activated.WallKind);
        writer.WriteNumber("active_wall_actor_net_guid", activated.ActiveWallActorNetGuid);
        WriteNullableValue(writer, "location", activated.Location);
        WriteNullableValue(writer, "rotation", activated.Rotation);
        WriteNullableValue(writer, "wall_start", activated.WallStart);
        WriteNullableValue(writer, "wall_end", activated.WallEnd);
        WriteNullableValue(writer, "impact_normal", activated.ImpactNormal);
        WriteNullableValue(writer, "trigger_character_net_guid", activated.TriggerCharacterNetGuid);
        WriteNullableValue(writer, "trigger_player_state_net_guid", activated.TriggerPlayerStateNetGuid);
        WriteNullableString(writer, "trigger_subject", activated.TriggerSubject);
        writer.WriteString("evidence", ReplayJsonNormalizer.ToSnakeCase(activated.Evidence.ToString()));
        writer.WriteEndObject();
    }

    public void WriteValorantWallSegmentDestroyed(Utf8JsonWriter writer, ValorantWallSegmentDestroyed destroyed)
    {
        WriteEventStart(writer, "valorant_wall_segment_destroyed", destroyed);
        writer.WriteNumber("wall_actor_net_guid", destroyed.WallActorNetGuid);
        writer.WriteNumber("segment_actor_net_guid", destroyed.SegmentActorNetGuid);
        writer.WriteNumber("segment_index", destroyed.SegmentIndex);
        WriteNullableValue(writer, "location", destroyed.Location);
        writer.WriteNumber("lifetime_seconds", destroyed.LifetimeSeconds);
        writer.WriteString("evidence", ReplayJsonNormalizer.ToSnakeCase(destroyed.Evidence.ToString()));
        writer.WriteEndObject();
    }

    public void WriteValorantWallDestroyed(Utf8JsonWriter writer, ValorantWallDestroyed destroyed)
    {
        WriteEventStart(writer, "valorant_wall_destroyed", destroyed);
        WriteWallIdentity(writer, destroyed.WallActorNetGuid, destroyed.WallKind);
        WriteNullableValue(writer, "active_wall_actor_net_guid", destroyed.ActiveWallActorNetGuid);
        WriteNullableValue(writer, "location", destroyed.Location);
        writer.WriteNumber("lifetime_seconds", destroyed.LifetimeSeconds);
        WriteNullableValue(writer, "active_duration_seconds", destroyed.ActiveDurationSeconds);
        writer.WriteString("evidence", ReplayJsonNormalizer.ToSnakeCase(destroyed.Evidence.ToString()));
        writer.WriteEndObject();
    }

    public void WriteMovement(
        Utf8JsonWriter writer, RemoteCharacterMovementReceived movement)
    {
        var move = movement.Move;
        writer.WriteStartObject();
        WriteEventDiscriminator(writer, "remote_character_movement", movement.TimeSeconds, movement.PacketId);
        WriteObjectIdentity(writer, movement.ActorNetGuid, movement.ObjectNetGuid, movement.ChannelIndex);
        writer.WriteNumber("shooter_character_net_guid", movement.ShooterCharacterNetGuidValue);
        writer.WriteNumber("update_index", movement.UpdateIndex);
        writer.WriteNumber("move_index", movement.MoveIndex);
        writer.WritePropertyName("position");
        ReplayJsonNormalizer.WriteVector(writer, move.Position);
        writer.WriteNumber("yaw", move.Yaw);
        writer.WriteNumber("pitch", move.Pitch);
        WriteNullableValue(writer, "velocity", move.Velocity);
        writer.WriteNumber("timestamp", move.Timestamp);
        writer.WriteNumber("movement_state", move.MovementState);
        writer.WriteNumber("mode_flags", move.ModeFlags);
        writer.WriteNumber("marker", move.Marker);
        writer.WriteNumber("move_type", move.MoveType);
        writer.WritePropertyName("rotation_input");
        ReplayJsonNormalizer.WriteVector(writer, move.RotationInput);
        WriteNullableValue(writer, "variant1_vector", move.Variant1Vector);
        writer.WriteNumber("rotation_yaw_multiplier", move.RotationYawMultiplier);
        writer.WriteBoolean("has_optional_movement_value", move.HasOptionalMovementValue);
        WriteNullableValue(writer, "optional_movement_raw_byte", move.OptionalMovementRawByte);
        WriteNullableValue(writer, "optional_movement_value", move.OptionalMovementValue);
        writer.WriteBoolean("flag48", move.Flag48);
        writer.WriteNumber("packed_angles", move.PackedAngles);
        writer.WriteNumber("raw_yaw", move.RawYaw);
        writer.WriteNumber("raw_pitch", move.RawPitch);
        WriteNullableValue(writer, "variant0_has_external_character_ref", move.Variant0HasExternalCharacterRef);
        WriteNullableValue(writer, "variant0_packed_angles", move.Variant0PackedAngles);
        WriteNullableValue(writer, "variant1_flag", move.Variant1Flag);
        writer.WriteBoolean("error_sentinel", move.ErrorSentinel);
        writer.WriteEndObject();
    }

    private static void WriteEquippable(Utf8JsonWriter writer, ValorantEquippable? equippable)
    {
        if (equippable is null)
        {
            writer.WriteNull("equippable");
            return;
        }

        writer.WriteStartObject("equippable");
        writer.WriteNumber("net_guid", equippable.NetGuid);
        WriteNullableString(writer, "name", equippable.Name);
        writer.WriteString("category", ReplayJsonNormalizer.ToSnakeCase(equippable.Category.ToString()));
        WriteNullableString(writer, "class_path", equippable.ClassPath);
        writer.WriteEndObject();
    }

    private static void WriteEventStart(Utf8JsonWriter writer, string type, ReplayEvent replayEvent)
    {
        writer.WriteStartObject();
        WriteEventDiscriminator(writer, type, replayEvent.TimeSeconds, replayEvent.PacketId);
    }

    private static void WriteEventDiscriminator(
        Utf8JsonWriter writer,
        string type,
        float timeSeconds,
        int packetId)
    {
        writer.WriteString("type", type);
        writer.WriteNumber("time_ms", ToMilliseconds(timeSeconds));
        writer.WriteNumber("packet_id", packetId);
    }

    private static long ToMilliseconds(float seconds) =>
        float.IsFinite(seconds)
            ? (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero)
            : 0;

    private static void WriteObjectIdentity(
        Utf8JsonWriter writer,
        uint actorNetGuid,
        uint objectNetGuid,
        uint channelIndex)
    {
        writer.WriteNumber("actor_net_guid", actorNetGuid);
        writer.WriteNumber("object_net_guid", objectNetGuid);
        writer.WriteNumber("channel", channelIndex);
    }

    private static void WriteFlashIdentity(
        Utf8JsonWriter writer,
        uint flashActorNetGuid,
        ValorantFlashKind flashKind)
    {
        writer.WriteNumber("flash_actor_net_guid", flashActorNetGuid);
        writer.WriteString("flash_kind", ReplayJsonNormalizer.ToSnakeCase(flashKind.ToString()));
    }

    private static void WriteNearsightIdentity(
        Utf8JsonWriter writer,
        uint nearsightActorNetGuid,
        ValorantNearsightKind nearsightKind)
    {
        writer.WriteNumber("nearsight_actor_net_guid", nearsightActorNetGuid);
        writer.WriteString("nearsight_kind", ReplayJsonNormalizer.ToSnakeCase(nearsightKind.ToString()));
    }

    private static void WriteWallIdentity(
        Utf8JsonWriter writer,
        uint wallActorNetGuid,
        ValorantWallKind wallKind)
    {
        writer.WriteNumber("wall_actor_net_guid", wallActorNetGuid);
        writer.WriteString("wall_kind", ReplayJsonNormalizer.ToSnakeCase(wallKind.ToString()));
    }

    private static void WriteDecodeMetadata(
        Utf8JsonWriter writer,
        int payloadBits,
        int parsedBits,
        bool wasDecoded,
        int decodedFieldCount)
    {
        writer.WriteNumber("payload_bits", payloadBits);
        writer.WriteNumber("parsed_bits", parsedBits);
        writer.WriteBoolean("was_decoded", wasDecoded);
        writer.WriteNumber("decoded_field_count", decodedFieldCount);
    }

    private void WritePayload(Utf8JsonWriter writer, object? payload)
    {
        writer.WritePropertyName("payload");
        _normalizer.WriteValue(writer, payload);
    }

    private void WriteDiagnosticFields(
        Utf8JsonWriter writer,
        IReadOnlyList<DecodedReplayField> fields)
    {
        writer.WriteStartArray("diagnostic_fields");
        foreach (var field in fields)
        {
            writer.WriteStartObject();
            writer.WriteNumber("handle", field.Handle);
            WriteNullableString(writer, "name", field.Name);
            WriteNullableString(writer, "export_name", field.ExportName);
            WriteCategories(writer, field.Categories);
            writer.WritePropertyName("value");
            _normalizer.WriteValue(writer, field.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteCategories(Utf8JsonWriter writer, ExportCategory categories)
    {
        writer.WriteStartArray("categories");
        foreach (var category in Enum.GetValues<ExportCategory>())
        {
            if (category is ExportCategory.None or ExportCategory.All || !categories.HasFlag(category))
            {
                continue;
            }

            writer.WriteStringValue(ReplayJsonNormalizer.ToSnakeCase(category.ToString()));
        }

        writer.WriteEndArray();
    }

    private void WriteNullableValue(Utf8JsonWriter writer, string name, object? value)
    {
        writer.WritePropertyName(name);
        _normalizer.WriteValue(writer, value);
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }
}
