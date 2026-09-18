using System.Text.Json;
using CliReader.JsonExport;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Replay;
using Replay.Models.Unreal;
using Replay.Unreal.Readers;
using Replay.Valorant.Combat;
using Replay.Valorant.Flashes;
using Replay.Valorant.GameState;
using Replay.Valorant.Movement;

namespace Replay.Valorant.Tests.Export;

public class ReplayExportTests
{
    [Test]
    public void ExportOptions_Create_ResolvesViewerProfile()
    {
        var options = ExportOptions.Create("match.replay", "bundle", "viewer");

        Assert.Multiple(() =>
        {
            Assert.That(options.ReplayPath, Is.EqualTo("match.replay"));
            Assert.That(options.OutputDirectory, Is.EqualTo("bundle"));
            Assert.That(options.ProfileName, Is.EqualTo("viewer"));
            Assert.That(options.ParseProfile.CaptureDiagnosticFields, Is.True);
        });
    }

    [Test]
    public void EventSink_WritesSupportedEventsAndFiltersExportShells()
    {
        using var events = new MemoryStream();
        using var movement = new MemoryStream();
        using (var sink = CreateSink(events, movement))
        {
            sink.Emit(Spawned());
            sink.Emit(new ActorClosed(2, 12, 100, 7, ChannelCloseReason.Destroyed));
            sink.Emit(Export(wasDecoded: false, payload: new TestPayload()));
            sink.Emit(Export(wasDecoded: true, payload: null));
            sink.Emit(Export(wasDecoded: true, payload: new TestPayload()));
            sink.Emit(Rpc());

            var statistics = sink.Statistics;
            Assert.Multiple(() =>
            {
                Assert.That(statistics.EventCount, Is.EqualTo(4));
                Assert.That(statistics.FilteredExportGroupCount, Is.EqualTo(2));
                Assert.That(statistics.UndecodedExportGroupCount, Is.EqualTo(1));
                Assert.That(statistics.EmptyDecodedExportGroupCount, Is.EqualTo(1));
                Assert.That(statistics.FilteredExportGroups.Count, Is.EqualTo(2));
            });
        }

        var documents = ParseLines(events);
        Assert.That(
            documents.Select(document => document.RootElement.GetProperty("type").GetString()),
            Is.EqualTo(new[]
            {
                "actor_spawned",
                "actor_closed",
                "export_group_received",
                "rpc_received",
            }));

        var exported = documents[2].RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(exported.GetProperty("time_ms").GetInt64(), Is.EqualTo(2500));
            Assert.That(exported.GetProperty("actor_net_guid").GetUInt32(), Is.EqualTo(100));
            Assert.That(exported.GetProperty("object_net_guid").GetUInt32(), Is.EqualTo(101));
            Assert.That(exported.GetProperty("channel").GetUInt32(), Is.EqualTo(7));
            Assert.That(exported.GetProperty("was_decoded").GetBoolean(), Is.True);
            Assert.That(exported.GetProperty("categories")[0].GetString(), Is.EqualTo("movement"));
            Assert.That(exported.GetProperty("payload").GetProperty("Health").GetInt32(), Is.EqualTo(87));
            Assert.That(exported.GetProperty("payload").TryGetProperty("Ignored", out _), Is.False);
            Assert.That(documents[3].RootElement.GetProperty("was_decoded").GetBoolean(), Is.False);
        });

        Dispose(documents);
    }

    [Test]
    public void EventSink_WritesValorantShotAsNestedSnakeCaseObject()
    {
        using var events = new MemoryStream();
        using var movement = new MemoryStream();
        using (var sink = CreateSink(events, movement))
        {
            sink.Emit(Shot());

            Assert.Multiple(() =>
            {
                Assert.That(sink.Statistics.EventCount, Is.EqualTo(1));
                Assert.That(sink.Statistics.ValorantShotReceivedCount, Is.EqualTo(1));
                Assert.That(sink.Statistics.MovementCount, Is.Zero);
            });
        }

        using var document = ParseLines(events).Single();
        var exported = document.RootElement;
        var shot = exported.GetProperty("shot");
        Assert.Multiple(() =>
        {
            Assert.That(exported.GetProperty("type").GetString(), Is.EqualTo("valorant_shot_received"));
            Assert.That(exported.GetProperty("time_ms").GetInt64(), Is.EqualTo(4250));
            Assert.That(exported.GetProperty("actor_net_guid").GetUInt32(), Is.EqualTo(100));
            Assert.That(exported.GetProperty("object_net_guid").GetUInt32(), Is.EqualTo(101));
            Assert.That(exported.GetProperty("channel").GetUInt32(), Is.EqualTo(7));
            Assert.That(shot.GetProperty("effect_id").GetUInt64(), Is.EqualTo(99));
            Assert.That(shot.GetProperty("alliance_filter").GetString(), Is.EqualTo("alliance_enemy"));
            Assert.That(shot.GetProperty("location").GetProperty("x").GetDouble(), Is.EqualTo(1));
            Assert.That(shot.GetProperty("rotation").GetProperty("yaw").GetDouble(), Is.EqualTo(5));
            Assert.That(shot.GetProperty("attack_vectors").GetArrayLength(), Is.EqualTo(2));
            Assert.That(shot.GetProperty("fire_mode").GetString(), Is.EqualTo("alternate"));
            Assert.That(shot.GetProperty("fire_mode_evidence").GetString(), Is.EqualTo("source:ZoomedFire"));
            Assert.That(shot.GetProperty("equippable").GetProperty("net_guid").GetUInt32(), Is.EqualTo(500));
            Assert.That(shot.GetProperty("equippable").GetProperty("category").GetString(), Is.EqualTo("rifle"));
        });
        Assert.That(movement.ToArray(), Is.Empty);
    }

    [Test]
    public void EventSink_WritesFlashLifecycleAsSnakeCaseEvents()
    {
        using var events = new MemoryStream();
        using var movement = new MemoryStream();
        using (var sink = CreateSink(events, movement))
        {
            sink.Emit(FlashCast());
            sink.Emit(FlashPath());
            sink.Emit(FlashExplosion());
            sink.Emit(FlashHit());

            Assert.Multiple(() =>
            {
                Assert.That(sink.Statistics.EventCount, Is.EqualTo(4));
                Assert.That(sink.Statistics.ValorantFlashCastCount, Is.EqualTo(1));
                Assert.That(sink.Statistics.ValorantFlashPathUpdatedCount, Is.EqualTo(1));
                Assert.That(sink.Statistics.ValorantFlashExplodedCount, Is.EqualTo(1));
                Assert.That(sink.Statistics.ValorantFlashPlayerHitCount, Is.EqualTo(1));
            });
        }

        var documents = ParseLines(events);
        Assert.That(
            documents.Select(document => document.RootElement.GetProperty("type").GetString()),
            Is.EqualTo(new[]
            {
                "valorant_flash_cast",
                "valorant_flash_path_updated",
                "valorant_flash_exploded",
                "valorant_flash_player_hit",
            }));

        var cast = documents[0].RootElement;
        var path = documents[1].RootElement;
        var explosion = documents[2].RootElement;
        var hit = documents[3].RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(cast.GetProperty("flash_kind").GetString(), Is.EqualTo("kayo_flash_drive_underhand"));
            Assert.That(cast.GetProperty("caster_subject").GetString(), Is.EqualTo("subject-1"));
            Assert.That(path.GetProperty("sample_index").GetInt32(), Is.EqualTo(2));
            Assert.That(path.GetProperty("source").GetString(), Is.EqualTo("replicated_movement"));
            Assert.That(explosion.GetProperty("evidence").GetString(), Is.EqualTo("stop_projectile_rpc"));
            Assert.That(explosion.GetProperty("max_flash_duration_seconds").GetDouble(), Is.EqualTo(2.25));
            Assert.That(hit.GetProperty("blind_id").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(hit.GetProperty("duration_source").GetString(), Is.EqualTo("effect_data_gameplay_tag"));
            Assert.That(hit.GetProperty("correlation").GetString(), Is.EqualTo("causing_projectile"));
        });

        Dispose(documents);
    }

    [Test]
    public void EventSink_WritesStructuredRoundResultPayload()
    {
        using var events = new MemoryStream();
        using var movement = new MemoryStream();
        using (var sink = CreateSink(events, movement))
        {
            sink.Emit(Export(wasDecoded: true, payload: new RoundResultPayload()));
        }

        using var document = ParseLines(events).Single();
        var result = document.RootElement
            .GetProperty("payload")
            .GetProperty("RoundResults")[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("RoundNumber").GetInt32(), Is.Zero);
            Assert.That(result.GetProperty("WinningTeam").GetString(), Is.EqualTo("Blue"));
            Assert.That(result.GetProperty("WinningTeamRole").GetString(), Is.EqualTo("defender"));
            Assert.That(result.GetProperty("RoundResult").GetString(), Is.EqualTo("defuse"));
        });
    }

    [Test]
    public void JsonNormalizer_WritesMemoryAndTreatsNonInvokablePropertiesAsNull()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            new ReplayJsonNormalizer().WriteValue(writer, new ReflectionPayload([1, 2, 3]));
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("Data").GetString(), Is.EqualTo("AQID"));
            Assert.That(document.RootElement.GetProperty("Span").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public void MovementSink_WritesMachineReadableMovementShape()
    {
        using var events = new MemoryStream();
        using var movement = new MemoryStream();
        using (var sink = CreateSink(events, movement))
        {
            var move = Movement();
            var movementReceived = new RemoteCharacterMovementReceived(1.25f, 99, 100, 101, 7, 3, 1234, 4, move);
            sink.EmitRemoteCharacterMovement(movementReceived);
        }

        var documents = ParseLines(movement);
        var exported = documents.Single().RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(exported.GetProperty("type").GetString(), Is.EqualTo("remote_character_movement"));
            Assert.That(exported.GetProperty("time_ms").GetInt64(), Is.EqualTo(1250));
            Assert.That(exported.GetProperty("packet_id").GetInt32(), Is.EqualTo(99));
            Assert.That(exported.GetProperty("shooter_character_net_guid").GetUInt32(), Is.EqualTo(1234));
            Assert.That(exported.GetProperty("update_index").GetInt32(), Is.EqualTo(3));
            Assert.That(exported.GetProperty("move_index").GetInt32(), Is.EqualTo(4));
            Assert.That(exported.GetProperty("position").GetProperty("x").GetDouble(), Is.EqualTo(1.5));
            Assert.That(exported.GetProperty("velocity").GetProperty("z").GetDouble(), Is.EqualTo(6));
            Assert.That(exported.GetProperty("yaw").GetDouble(), Is.EqualTo(90));
            Assert.That(exported.GetProperty("pitch").GetDouble(), Is.EqualTo(45));
            Assert.That(exported.GetProperty("timestamp").GetUInt32(), Is.EqualTo(42));
            Assert.That(exported.GetProperty("movement_state").GetByte(), Is.EqualTo(3));
        });

        Dispose(documents);
    }

    [Test]
    public void ManifestWriter_WritesSchemaIdentityAndCounts()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"valorant-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var archive = new FBinaryArchive(ReadOnlyMemory<byte>.Empty);
            var context = new ReplayReaderContext(archive)
            {
                ReplayInfo = new ReplayInfo { LengthInMs = 60000 },
                ReplayVersion = new ReplayVersion
                {
                    Major = 13,
                    Minor = 1,
                    Patch = 0,
                    Changelist = 123,
                    Branch = "++Ares-Core+release-13.01",
                },
            };
            using var events = new MemoryStream();
            using var movement = new MemoryStream();
            using var sink = CreateSink(events, movement);
            sink.Emit(Spawned());
            sink.Emit(Shot());
            sink.Emit(FlashCast());
            sink.Emit(FlashPath());
            sink.Emit(FlashExplosion());
            sink.Emit(FlashHit());
            sink.Emit(Export(wasDecoded: false, payload: null));

            new ReplayExportManifestWriter().Write(
                directory,
                "match.replay",
                new string('a', 64),
                42,
                "viewer",
                context,
                sink.Statistics);

            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
            var manifest = document.RootElement;
            Assert.Multiple(() =>
            {
                Assert.That(manifest.GetProperty("schema_version").GetInt32(), Is.EqualTo(5));
                Assert.That(manifest.GetProperty("source_sha256").GetString(), Has.Length.EqualTo(64));
                Assert.That(manifest.GetProperty("replay_build").GetString(), Does.EndWith("release-13.01"));
                Assert.That(manifest.GetProperty("duration_ms").GetInt32(), Is.EqualTo(60000));
                Assert.That(manifest.GetProperty("parse_profile").GetString(), Is.EqualTo("viewer"));
                Assert.That(manifest.GetProperty("parser_version").GetString(), Is.Not.Empty);
                Assert.That(manifest.GetProperty("counts").GetProperty("actor_spawned").GetInt32(), Is.EqualTo(1));
                Assert.That(
                    manifest.GetProperty("counts").GetProperty("valorant_shot_received").GetInt32(),
                    Is.EqualTo(1));
                Assert.That(manifest.GetProperty("counts").GetProperty("valorant_flash_cast").GetInt32(), Is.EqualTo(1));
                Assert.That(manifest.GetProperty("counts").GetProperty("valorant_flash_path_updated").GetInt32(), Is.EqualTo(1));
                Assert.That(manifest.GetProperty("counts").GetProperty("valorant_flash_exploded").GetInt32(), Is.EqualTo(1));
                Assert.That(manifest.GetProperty("counts").GetProperty("valorant_flash_player_hit").GetInt32(), Is.EqualTo(1));
                Assert.That(manifest.GetProperty("counts").GetProperty("events").GetInt32(), Is.EqualTo(6));
                Assert.That(manifest.GetProperty("net_field_export_groups").GetArrayLength(), Is.Zero);
                Assert.That(
                    manifest.GetProperty("counts").GetProperty("undecoded_export_groups").GetInt32(),
                    Is.EqualTo(1));
                Assert.That(
                    manifest.GetProperty("filtered_export_group_summary")[0]
                        .GetProperty("path").GetString(),
                    Is.EqualTo("/Game/Export"));
                Assert.That(
                    manifest.GetProperty("filtered_export_group_summary")[0]
                        .GetProperty("sample_class_path").GetString(),
                    Is.EqualTo("/Game/Class"));
                Assert.That(manifest.GetProperty("limitations").GetArrayLength(), Is.GreaterThan(0));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ReplayExportSink CreateSink(Stream events, Stream movement) =>
        new(
            new NdjsonWriter(events),
            new NdjsonWriter(movement),
            new ReplayEventJsonWriter(new ReplayJsonNormalizer()));

    private static ActorSpawned Spawned() =>
        new(
            1,
            10,
            100,
            7,
            true,
            "/Game/Actor",
            200,
            "/Game/Archetype",
            "/Game/Class",
            300,
            new FVector(1, 2, 3),
            new FRotator(4, 5, 6),
            new FVector(1, 1, 1),
            new FVector(7, 8, 9));

    private static ExportGroupReceived Export(bool wasDecoded, object? payload) =>
        new(
            2.5f,
            20,
            100,
            101,
            7,
            false,
            false,
            0,
            "/Game/Export",
            ExportGroupKind.Component,
            ExportCategory.Movement | ExportCategory.Agent,
            200,
            100,
            "/Game/Object",
            "/Game/Class",
            "/Game/Outer",
            64,
            64,
            wasDecoded,
            payload,
            wasDecoded ? 1 : 0,
            []);

    private static RpcReceived Rpc() =>
        new(
            3,
            30,
            100,
            101,
            7,
            "/Game/Class",
            "DoThing",
            "/Game/Class.DoThing",
            9,
            ExportCategory.Ability,
            8,
            0,
            false,
            null,
            0,
            []);

    private static ValorantShotReceived Shot() =>
        new(
            4.25f,
            40,
            100,
            101,
            7,
            new ValorantShot(
                EffectId: 99,
                StartMovementTime: 1.5f,
                SourceId: "ZoomedFire",
                IsLocalEffect: true,
                IsTransient: false,
                WaitOnReplicationActor: 200,
                AllianceFilter: EAresAlliance.AllianceEnemy,
                Location: new FVector(1, 2, 3),
                Rotation: new FRotator(4, 5, 6),
                AmmoRemaining: 24,
                NumProjectiles: 1,
                RandomSeed: 7,
                TracerOption: 2,
                BurstShotNumber: 3,
                YawSwitch: 0,
                FiringPlayerState: 300,
                FiringState: 400,
                AttackVectors: [new FVector(7, 8, 9), new FVector(10, 11, 12)],
                EffectEquippable: 500,
                Equippable: new ValorantEquippable(500, "Vandal", ValorantEquippableCategory.Rifle, "/Game/Vandal"),
                FireMode: ValorantShotFireMode.Alternate,
                FireModeEvidence: "source:ZoomedFire"));

    private static ValorantFlashCast FlashCast() =>
        new(
            5,
            50,
            600,
            ValorantFlashKind.KayoFlashDriveUnderhand,
            100,
            300,
            "subject-1",
            new FVector(1, 2, 3),
            new FRotator(4, 5, 6),
            new FVector(7, 8, 9));

    private static ValorantFlashPathUpdated FlashPath() =>
        new(
            5.1f,
            51,
            600,
            ValorantFlashKind.KayoFlashDriveUnderhand,
            2,
            ValorantFlashPathSampleSource.ReplicatedMovement,
            new FVector(10, 11, 12),
            new FRotator(13, 14, 15),
            new FVector(16, 17, 18),
            new FVector(19, 20, 21),
            22);

    private static ValorantFlashExploded FlashExplosion() =>
        new(
            5.2f,
            52,
            600,
            ValorantFlashKind.KayoFlashDriveUnderhand,
            new FVector(10, 11, 12),
            ValorantFlashExplosionEvidence.StopProjectileRpc,
            2.25);

    private static ValorantFlashPlayerHit FlashHit() =>
        new(
            5.3f,
            53,
            600,
            ValorantFlashKind.KayoFlashDriveUnderhand,
            100,
            300,
            "subject-1",
            1.4f,
            null,
            99,
            null,
            600,
            5.25f,
            ValorantFlashHitCorrelation.CausingProjectile,
            ValorantFlashDurationSource.EffectDataGameplayTag);

    private static MovementMove Movement() =>
        new(
            Marker: 1,
            MoveType: 1,
            Position: new FVector(1.5, 2.5, 3.5),
            Velocity: new FVector(4, 5, 6),
            RotationInput: new FVector(0.1, 0.2, 0.3),
            Variant1Vector: new FVector(4, 5, 6),
            Timestamp: 42,
            ModeFlags: 3,
            MovementState: 3,
            RotationYawMultiplier: 2,
            UnusedByte: 0,
            HasOptionalMovementValue: true,
            OptionalMovementRawByte: 8,
            OptionalMovementValue: 8,
            Flag48: false,
            PackedAngles: 12,
            RawYaw: 100,
            RawPitch: 200,
            Yaw: 90,
            Pitch: 45,
            Variant0HasExternalCharacterRef: null,
            Variant0PackedAngles: null,
            Variant1Flag: true,
            ErrorSentinel: false);

    private static List<JsonDocument> ParseLines(MemoryStream stream) =>
        System.Text.Encoding.UTF8.GetString(stream.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();

    private static void Dispose(IEnumerable<JsonDocument> documents)
    {
        foreach (var document in documents)
        {
            document.Dispose();
        }
    }

    private sealed class TestPayload : IDecodedPayload
    {
        public IReadOnlySet<string> DecodedProperties { get; } = new HashSet<string>
        {
            nameof(Health),
        };

        public int Health { get; } = 87;
        public string Ignored { get; } = "not decoded";

        public bool HasDecoded(string propertyName) => DecodedProperties.Contains(propertyName);
    }

    private sealed class RoundResultPayload
    {
        public AresRoundResult[] RoundResults { get; } =
        [
            new(0, "Blue", AresTeamRole.Defender, AresRoundOutcome.Defuse),
        ];
    }

    private sealed class ReflectionPayload(byte[] data)
    {
        private readonly byte[] _data = data;

        public ReadOnlyMemory<byte> Data => _data;
        public ReadOnlySpan<byte> Span => _data;
    }
}
