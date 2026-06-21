# ValorantReplayParser

![Build](https://github.com/michel-giehl/ValorantReplayParser/actions/workflows/build.yml/badge.svg?branch=main) ![Test](https://github.com/michel-giehl/ValorantReplayParser/actions/workflows/integration.yml/badge.svg?branch=main) [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=michel-giehl_ValorantReplayParser&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=michel-giehl_ValorantReplayParser)

C# parser for VALORANT replay files (`.vrf`).

## Projects

- `Replay.Models`: shared models, parse results, constants, context contracts, and parser errors.
- `Replay.Encoding`: byte/bit archives, `FBinaryArchive`, payload transforms, and future compression/net-field decoding primitives.
- `Replay.Unreal`: replay-info, chunk scanning, replay header parsing, and the parsing pipeline used for VALORANT replays.
- `Replay.Valorant`: VALORANT-specific models and interpretation such as export groups, RPCs, and ClassNetCaches.
- `CliReader`: minimal CLI/demo reader.
- `*.Tests`: NUnit test projects.

## Requirements

- .NET 10 SDK

## Project Progress

| Area | Status |
| --- |------|
| Binary and bit archives | ✔    |
| Replay info parsing | ✔    |
| Replay chunk scanning | ✔    |
| Replay header parsing | ✔    |
| Payload transform | ✔    |
| Oodle decompression | ✔    |
| Minimal CLI reader | ✔    |
| Replay data stream parsing | ✔    |
| Packet parsing | ✔    |
| Bunch processing |  ❌    |
| Net field parsing | ❌    |
| VALORANT export groups | ❌    |
| VALORANT RPCs | ❌    |
| ClassNetCaches | ❌    |
| Match/game-state models | ❌    |
| Stable public API | ❌    |

## Build And Test

```powershell
dotnet build "ValorantReplayParser.sln"
dotnet test "ValorantReplayParser.sln"
```

## CLI

```powershell
dotnet run --project "src\CliReader\CliReader.csproj" -- "C:\path\to\replay.vrf"
```


## Special Thanks
* To the folks from [FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor) for their amazing work and documentation of the replay system
* GPT 5.5 for reverse engineering VALORANT payload transformation & movement encoding
