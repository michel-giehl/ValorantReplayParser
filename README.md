# ValorantReplayParser

![Build](https://github.com/michel-giehl/ValorantReplayParser/actions/workflows/build.yml/badge.svg?branch=main) ![Test](https://github.com/michel-giehl/ValorantReplayParser/actions/workflows/integration.yml/badge.svg?branch=main) [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=michel-giehl_ValorantReplayParser&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=michel-giehl_ValorantReplayParser)

`ValorantReplayParser` reads VALORANT replay files (`.vrf`) and emits typed gameplay events and replay metadata. It is a narrow, VALORANT-specific parser, not a general Unreal Engine replay parser.

The first NuGet release, `0.1.0-alpha.1`, is experimental. Replay formats and the public API may change as the reverse-engineered format understanding improves. A replay version passing the compatibility checks does not mean every gameplay feature in that replay is decoded.

## Requirements and installation

The package targets `net10.0`; consumers need the .NET 10 runtime. Building this repository requires the .NET 10 SDK. Install the prerelease with:

```powershell
dotnet add package ValorantReplayParser --version 0.1.0-alpha.1
```

## Read a replay

The default constructor selects the full parse profile and the built-in VALORANT descriptor catalog. Supply an `IReplayEventSink` to receive events. The sink is called synchronously as events are produced:

```csharp
using System;
using System.IO;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Valorant;

var reader = new ValorantReplayReader(eventSink: new ConsoleEventSink());

try
{
    // Open a fresh stream at the start of the replay for each read.
    using var stream = File.OpenRead("path/to/replay.vrf");
    ValorantReplayReadResult result = reader.Read(stream);

    Console.WriteLine($"Read status: {result.Status}");
    Console.WriteLine($"Packets: {result.PacketStats.PacketCount}");

    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    }

    if (result.SuppressedDiagnosticCount > 0)
    {
        Console.Error.WriteLine(
            $"{result.SuppressedDiagnosticCount} additional diagnostics were omitted from the retained list.");
    }
}
catch (ReplayParseException exception)
{
    // Includes invalid replay metadata/version and malformed replay data.
    Console.Error.WriteLine($"Replay could not be parsed: {exception.Message}");
}

sealed class ConsoleEventSink : IReplayEventSink
{
    public void Emit(ReplayEvent replayEvent)
    {
        Console.WriteLine(
            $"{replayEvent.GetType().Name} at {replayEvent.TimeSeconds:0.000}s " +
            $"(packet {replayEvent.PacketId})");
    }
}
```

`Read` returns a detached `ValorantReplayReadResult` containing metadata, packet and bunch statistics, diagnostics, and export-group summaries. It does not retain the input archive or stream, so the result remains usable after those inputs are disposed.

`Status` is `Completed` when no recoverable warnings were recorded and `CompletedWithWarnings` when one or more warnings were recorded. `Diagnostics` retains up to 1,000 structured warnings; `TotalDiagnosticCount` counts all warnings and `SuppressedDiagnosticCount` reports warnings beyond that retained limit. Current warnings identify partial-bunch sequence errors, incomplete partial bunches at end of input, and speculative typed-decoder fallbacks to raw payloads.

Malformed replay structure, invalid required reads, unsupported full-parse versions, and resource-limit violations fail with a `ReplayParseException` subtype. A returned result means the container was traversed under those rules; it does not promise that every known or unknown VALORANT feature was semantically decoded or that the file contains a complete match.

Events are streamed to the sink and are provisional until `Read` returns successfully. If a later parse error occurs, events already emitted cannot be rolled back. Some semantic enrichers defer events until related replay data is available, so event timestamps are not guaranteed to increase globally in emission order. If the sink throws, parsing stops and the original sink exception is propagated.

## Metadata-only reads and input ownership

Use `ReadMetadata` to inspect replay metadata without parsing packet payloads. The example reuses the `reader` from the preceding sample:

```csharp
using var metadataStream = File.OpenRead("path/to/replay.vrf");
ValorantReplayMetadata metadata = reader.ReadMetadata(metadataStream);

Console.WriteLine(metadata.FullParseSupportStatus);
Console.WriteLine(metadata.FullParseUnsupportedReason);
```

Metadata parsing can report `UnsupportedVersion`; a full `Read` rejects such a replay early. `ReadMetadata` still throws for malformed metadata. Reopen the file for a later full read: the stream overload reads from its current position, buffers the remaining bytes, and advances the supplied stream to its end. The reader does not dispose the caller's stream. `FBinaryArchive(Stream)` also buffers the remaining stream contents in memory, so replay size contributes to memory use; the partial-bunch limits do not bound this whole-file buffer.

The same reader instance may be reused for sequential reads, but it does not support concurrent or reentrant reads. With the `FBinaryArchive` overloads, the archive must be positioned at the start of the replay for each operation. Supply a new archive positioned at the beginning for a metadata read followed by a full read.

## Parse profiles and support

The default `ParseProfile` enables all currently defined categories and selects all built-in descriptors and fields unless additional filters are set. `ParseProfile.Minimal` sets enabled categories to `None`, which filters out fields and RPCs assigned gameplay categories. Category-neutral fields can still be decoded. It is useful when replay metadata and packet/bunch traversal are needed without categorized gameplay decoding; it is not a lightweight substitute for full semantic parsing and it does not bypass replay-version validation. Other profiles can select categories, export paths, and fields.

The full reader requires replay version `5.3.2`, game network protocol version `0`, Unreal version values `522/1009`, and a registered payload transform for the replay branch. The transform registry currently contains exactly these branches:

- `++Ares-Core+release-12.10`
- `++Ares-Core+release-12.11`
- `++Ares-Core+release-13.00`
- `++Ares-Core+release-13.01`
- `++Ares-Core+release-13.02`
- `++Ares-Core+release-13.04`
- `++Ares-Core+release-13.05`

An unregistered branch or a mismatch in the replay, protocol, or Unreal versions is unsupported. The reader fails early rather than trying a guessed compatibility path. These version checks establish compatibility with the parser's known container and transform layout, not complete gameplay-semantic coverage.

`WasDecoded` on emitted export-group and RPC events indicates that the applicable decoder path ran; it is not a completeness guarantee. For decoded descriptor payloads, `HasDecoded(propertyName)` indicates whether that specific property was decoded and assigned. A `RawPayloadFallback` diagnostic means an explicitly speculative typed decoder rolled back and preserved the bounded payload as raw data.

## Custom descriptor catalogs

The `descriptorCatalog` constructor argument replaces the built-in descriptor catalog completely. To add custom descriptors while keeping the built-in VALORANT descriptors, begin with `ValorantDescriptors.CreateCatalog()` and add your descriptors to that catalog before constructing the reader:

```csharp
using Replay.Valorant;
using Replay.Valorant.Descriptors;
using Replay.Models.Descriptors;

var catalog = ValorantDescriptors.CreateCatalog();
catalog.Add(new ExportGroupDescriptor("/Game/Custom/Example", ExportCategory.Debug));
var reader = new ValorantReplayReader(descriptorCatalog: catalog);
```

Replace the example descriptor with your descriptor implementation and field definitions.

Starting from `CreateCatalog()` also preserves VALORANT's stable-subobject class paths and path-alias provider. Passing a new empty `DescriptorCatalog` intentionally replaces those defaults too.

## Progress

| Area | Status |
| --- | --- |
| Player Movement | ✔ |
| Agents | ✔ |
| Abilities | 🚧 |
| Gunplay | ✔ |
| Game State | ❌ |
| World State | ❌ |
| Stable public API | ❌ |

## Projects

- `Replay.Models`: shared models, parse results, constants, context contracts, and parser errors.
- `Replay.Encoding`: byte/bit archives, `FBinaryArchive`, payload transforms, and Oodle decompression.
- `Replay.Unreal`: replay-info, chunk scanning, replay-header parsing, and the replay-container pipeline.
- `Replay.Valorant`: VALORANT models, export groups, RPCs, ClassNetCaches, and gameplay interpretation.
- `CliReader`: CLI/demo entry point.
- `*.Tests`: NUnit test projects.

## Build and test

```powershell
dotnet build "ValorantReplayParser.sln"
dotnet test "ValorantReplayParser.sln"
```

## CLI

Log decoded replay activity and a parse summary:

```powershell
dotnet run --project "src\CliReader\CliReader.csproj" -- log "C:\path\to\replay.vrf"
```

Export replay events and movement as JSON:

```powershell
dotnet run --project "src\CliReader\CliReader.csproj" -- export "C:\path\to\replay.vrf" --output "C:\path\to\export"
```

Show the full command or subcommand help:

```powershell
dotnet run --project "src\CliReader\CliReader.csproj" -- --help
dotnet run --project "src\CliReader\CliReader.csproj" -- export --help
```

## Special thanks

- To the folks from [FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor) for their work and documentation of the replay system.
- GPT 5.5 for reverse engineering VALORANT payload transformation and movement encoding.

## License and disclaimer

This project is distributed under the MIT License; see [LICENSE](LICENSE).

This project is an independent, community-developed tool and is not affiliated with, endorsed by, sponsored by, or approved by Riot Games. VALORANT, Riot Games, and all related trademarks are the property of Riot Games, Inc.
