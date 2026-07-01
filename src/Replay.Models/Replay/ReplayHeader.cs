using Replay.Models.Unreal;

namespace Replay.Models.Replay;

public class ReplayHeader
{
    public uint NetworkVersion { get; init; }

    public uint NetworkChecksum { get; set; }

    public uint EngineNetworkProtocolVersion { get; set; }

    public uint GameNetworkProtocolVersion { get; set; }

    public Guid Guid { get; set; }

    public float MinRecordHz { get; set; }

    public float MaxRecordHz { get; set; }

    public float FrameLimitInMs { get; set; }

    public float CheckpointLimitInMs { get; set; }

    public (string LevelName, uint TimeInMs)[] LevelNamesAndTimes { get; set; } = [];

    public ReplayHeaderFlags Flags { get; set; }

    public string[] GameSpecificData { get; set; } = [];

    public string Platform { get; set; } = string.Empty;

    public byte BuildConfig { get; set; }

    public BuildTargetType BuildTargetType { get; set; }
}

