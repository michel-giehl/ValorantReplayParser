using System.Reflection;
using System.Text.Json;
using Replay.Unreal.Readers;
using Replay.Valorant;

namespace CliReader.JsonExport;

internal sealed class ReplayExportManifestWriter
{
    private const int SchemaVersion = 7;

    public void Write(
        string outputDirectory,
        string sourcePath,
        string sourceSha256,
        long sourceSize,
        string profileName,
        ReplayReaderContext context,
        ReplayExportStatistics statistics)
    {
        var path = Path.Combine(outputDirectory, "manifest.json");
        var temporaryPath = path + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteManifest(
                    writer,
                    sourcePath,
                    sourceSha256,
                    sourceSize,
                    profileName,
                    context,
                    statistics);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private static void WriteManifest(
        Utf8JsonWriter writer,
        string sourcePath,
        string sourceSha256,
        long sourceSize,
        string profileName,
        ReplayReaderContext context,
        ReplayExportStatistics statistics)
    {
        var version = context.ReplayVersion;
        var parserAssembly = typeof(ValorantReplayReader).Assembly;

        writer.WriteStartObject();
        writer.WriteNumber("schema_version", SchemaVersion);
        writer.WriteString("source_file", Path.GetFileName(sourcePath));
        writer.WriteString("source_sha256", sourceSha256);
        writer.WriteNumber("source_size_bytes", sourceSize);
        writer.WriteString("replay_build", version.Branch);
        writer.WriteString("replay_version", $"{version.Major}.{version.Minor}.{version.Patch}");
        writer.WriteNumber("replay_changelist", version.Changelist);
        writer.WriteNumber("duration_ms", context.ReplayInfo.LengthInMs);
        writer.WriteString("parse_profile", profileName);
        writer.WriteString("parser_assembly", parserAssembly.GetName().Name);
        writer.WriteString("parser_version", ParserVersion(parserAssembly));
        WriteStats(writer, context);
        WriteCounts(writer, statistics);
        WriteNetFieldExportGroups(writer, context);
        WriteFilteredExportGroups(writer, statistics);
        WriteLimitations(writer);
        writer.WriteEndObject();
    }

    private static void WriteStats(Utf8JsonWriter writer, ReplayReaderContext context)
    {
        var stats = context.PacketStats;
        writer.WriteStartObject("stats");
        writer.WriteNumber("packet_count", stats.PacketCount);
        writer.WriteNumber("packets_with_bunches", stats.PacketsWithBunches);
        writer.WriteNumber("bunch_count", stats.BunchCount);
        writer.WriteNumber("malformed_packet_count", stats.MalformedPacketCount);
        writer.WriteNumber("partial_error_count", stats.PartialErrorCount);
        writer.WriteNumber("total_packet_bytes", stats.TotalPacketBytes);
        writer.WriteEndObject();
    }

    private static void WriteCounts(Utf8JsonWriter writer, ReplayExportStatistics statistics)
    {
        writer.WriteStartObject("counts");
        writer.WriteNumber("movement", statistics.MovementCount);
        writer.WriteNumber("events", statistics.EventCount);
        writer.WriteNumber("actor_spawned", statistics.ActorSpawnedCount);
        writer.WriteNumber("actor_closed", statistics.ActorClosedCount);
        writer.WriteNumber("export_group_received", statistics.ExportGroupCount);
        writer.WriteNumber("rpc_received", statistics.RpcCount);
        writer.WriteNumber("valorant_shot_received", statistics.ValorantShotReceivedCount);
        writer.WriteNumber("valorant_flash_cast", statistics.ValorantFlashCastCount);
        writer.WriteNumber("valorant_flash_path_updated", statistics.ValorantFlashPathUpdatedCount);
        writer.WriteNumber("valorant_flash_exploded", statistics.ValorantFlashExplodedCount);
        writer.WriteNumber("valorant_flash_player_hit", statistics.ValorantFlashPlayerHitCount);
        writer.WriteNumber("valorant_nearsight_cast", statistics.ValorantNearsightCastCount);
        writer.WriteNumber("valorant_nearsight_path_updated", statistics.ValorantNearsightPathUpdatedCount);
        writer.WriteNumber("valorant_nearsight_activated", statistics.ValorantNearsightActivatedCount);
        writer.WriteNumber("valorant_nearsight_player_hit", statistics.ValorantNearsightPlayerHitCount);
        writer.WriteNumber(
            "valorant_nearsight_player_effect_ended",
            statistics.ValorantNearsightPlayerEffectEndedCount);
        writer.WriteNumber("valorant_wall_placed", statistics.ValorantWallPlacedCount);
        writer.WriteNumber("valorant_wall_segment_spawned", statistics.ValorantWallSegmentSpawnedCount);
        writer.WriteNumber("valorant_wall_activated", statistics.ValorantWallActivatedCount);
        writer.WriteNumber("valorant_wall_segment_destroyed", statistics.ValorantWallSegmentDestroyedCount);
        writer.WriteNumber("valorant_wall_destroyed", statistics.ValorantWallDestroyedCount);
        writer.WriteNumber("filtered_export_groups", statistics.FilteredExportGroupCount);
        writer.WriteNumber("undecoded_export_groups", statistics.UndecodedExportGroupCount);
        writer.WriteNumber("empty_decoded_export_groups", statistics.EmptyDecodedExportGroupCount);
        writer.WriteEndObject();
    }

    private static void WriteFilteredExportGroups(
        Utf8JsonWriter writer,
        ReplayExportStatistics statistics)
    {
        writer.WriteStartArray("filtered_export_group_summary");
        foreach (var summary in statistics.FilteredExportGroups
                     .OrderByDescending(item => item.Count)
                     .ThenBy(item => item.Path, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("path", summary.Path);
            writer.WriteString("kind", ReplayJsonNormalizer.ToSnakeCase(summary.Kind.ToString()));
            writer.WriteBoolean("was_decoded", summary.WasDecoded);
            writer.WriteNumber("count", summary.Count);
            writer.WriteNumber("payload_bits", summary.PayloadBits);
            writer.WriteNumber("sample_actor_net_guid", summary.ActorNetGuid);
            writer.WriteNumber("sample_object_net_guid", summary.ObjectNetGuid);
            writer.WriteNumber("sample_class_net_guid", summary.ClassNetGuid);
            writer.WriteNumber("sample_outer_net_guid", summary.OuterNetGuid);
            writer.WriteString("sample_object_path", summary.ObjectPath);
            writer.WriteString("sample_class_path", summary.ClassPath);
            writer.WriteString("sample_outer_path", summary.OuterPath);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteNetFieldExportGroups(
        Utf8JsonWriter writer,
        ReplayReaderContext context)
    {
        writer.WriteStartArray("net_field_export_groups");
        foreach (var group in context.NetGuidCache.ExportGroupsByPath.Values
                     .OrderBy(item => item.PathName, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("path", group.PathName);
            writer.WriteNumber("path_name_index", group.PathNameIndex);
            writer.WriteStartArray("fields");
            foreach (var field in group.NetFieldExports.Where(item => item is not null))
            {
                writer.WriteStartObject();
                writer.WriteNumber("handle", field!.Handle);
                writer.WriteString("name", field.Name);
                writer.WriteNumber("compatible_checksum", field.CompatibleChecksum);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteLimitations(Utf8JsonWriter writer)
    {
        writer.WriteStartArray("limitations");
        writer.WriteStringValue(
            "Only the latest decoded move in each remote-character update is currently emitted.");
        writer.WriteStringValue(
            "Undecoded export groups and decoded export shells without payloads are omitted.");
        writer.WriteStringValue(
            $"Unknown payload object graphs are bounded to {ReplayJsonNormalizer.MaxDepth} levels and " +
            $"{ReplayJsonNormalizer.MaxCollectionItems} collection items.");
        writer.WriteEndArray();
    }

    private static string ParserVersion(Assembly assembly)
    {
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
