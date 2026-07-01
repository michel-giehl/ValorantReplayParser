# ValorantReplayParser

![Build](https://github.com/michel-giehl/ValorantReplayParser/actions/workflows/build.yml/badge.svg?branch=main) ![Test](https://github.com/michel-giehl/ValorantReplayParser/actions/workflows/integration.yml/badge.svg?branch=main) [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=michel-giehl_ValorantReplayParser&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=michel-giehl_ValorantReplayParser)

C# parser for VALORANT replay files (`.vrf`).

## Usage
Note: This project is early in development and API behaviour is likely to change in the future.
```csharp
class TestReplayEventSink : IReplayEventSink
{
    public void Emit(ReplayEvent replayEvent)
    {
        switch (replayEvent)
        {
            case ActorSpawned spawned:
                Console.WriteLine("Actor spawned");
                break;

            case ActorClosed closed:
                Console.WriteLine("Actor destroyed");
                break;

            case ExportGroupReceived exportGroup:
                Console.WriteLine("Export group received");
                break;

            case RpcReceived rpc:
                Console.WriteLine("RPC received");
                break;

            case RemoteCharacterMovementReceived movement:
                Console.WriteLine("Player movement received");
                break;
        }
    }
}

using var file = File.OpenRead("path/to/replay.vrf");
using var archive = new FBinaryArchive(file);

ValorantReplayReader reader = ValorantReplayReader.CreateDefault(
        loggerFactory,
        new TestReplayEventSink(),
        ValorantDescriptors.CreateCatalog(),
        ParseProfile.Default); // Default behaviour: Parse everything

reader.Read(archive);
```

## Progress

| Area              | Status |
|-------------------|--------|
| Player Movement   | ✔      |
| Agents            | ✔      |
| Abilities         | 🚧     |
| Gunplay           | ❌      |
| Game State        | ❌      |
| World State       | ❌      |
| Stable public API | ❌      |

## Projects

- `Replay.Models`: shared models, parse results, constants, context contracts, and parser errors.
- `Replay.Encoding`: byte/bit archives, `FBinaryArchive`, payload transforms and oodle decompression
- `Replay.Unreal`: replay-info, chunk scanning, replay header parsing, and the parsing pipeline used for VALORANT replays.
- `Replay.Valorant`: VALORANT models (export groups, RPCs, and ClassNetCaches).
- `CliReader`: minimal CLI/demo reader.
- `*.Tests`: NUnit test projects.

## Requirements

- .NET 10 SDK

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
