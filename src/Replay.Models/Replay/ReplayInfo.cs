namespace Replay.Models.Replay;

public sealed class ReplayInfo
{
    public const int NoChunkIndex = -1;

    public int LengthInMs { get; set; }
    public uint NetworkVersion { get; set; }
    public uint Changelist { get; set; }
    public string FriendlyName { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public long TotalDataSizeInBytes { get; set; }
    public bool IsLive { get; set; }
    public bool IsValid { get; set; }
    public bool Compressed { get; set; }
    public bool Encrypted { get; set; }
    public byte[] EncryptionKey { get; set; } = [];
    public int HeaderChunkIndex { get; set; } = NoChunkIndex;
    public List<ReplayChunkInfo> Chunks { get; } = [];
    public List<ReplayDataChunkInfo> DataChunks { get; } = [];
}

public sealed class ReplayChunkInfo
{
    public ReplayChunkType ChunkType { get; init; } = ReplayChunkType.Unknown;
    public int SizeInBytes { get; init; }
    public long TypeOffset { get; init; }
    public long DataOffset { get; init; }
}

public sealed class ReplayDataChunkInfo
{
    public int ChunkIndex { get; init; } = ReplayInfo.NoChunkIndex;
    public uint Time1 { get; init; }
    public uint Time2 { get; init; }
    public int SizeInBytes { get; init; }
    public int MemorySizeInBytes { get; init; }
    public long ReplayDataOffset { get; init; }
    public long StreamOffset { get; init; }
}
