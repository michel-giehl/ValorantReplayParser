namespace Replay.Models;

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
    public List<ReplayEventInfo> Checkpoints { get; } = [];
    public List<ReplayEventInfo> Events { get; } = [];
    public List<ReplayDataChunkInfo> DataChunks { get; } = [];

    public void Reset()
    {
        LengthInMs = 0;
        NetworkVersion = 0;
        Changelist = 0;
        FriendlyName = string.Empty;
        Timestamp = 0;
        TotalDataSizeInBytes = 0;
        IsLive = false;
        IsValid = false;
        Compressed = false;
        Encrypted = false;
        EncryptionKey = [];
        HeaderChunkIndex = NoChunkIndex;
        Chunks.Clear();
        Checkpoints.Clear();
        Events.Clear();
        DataChunks.Clear();
    }
}

public sealed class ReplayChunkInfo
{
    public ReplayChunkType ChunkType { get; set; } = ReplayChunkType.Unknown;
    public int SizeInBytes { get; set; }
    public long TypeOffset { get; set; }
    public long DataOffset { get; set; }
}

public sealed class ReplayEventInfo
{
    public int ChunkIndex { get; set; } = ReplayInfo.NoChunkIndex;
    public string Id { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
    public uint Time1 { get; set; }
    public uint Time2 { get; set; }
    public int SizeInBytes { get; set; }
    public long EventDataOffset { get; set; }
}

public sealed class ReplayDataChunkInfo
{
    public int ChunkIndex { get; set; } = ReplayInfo.NoChunkIndex;
    public uint Time1 { get; set; }
    public uint Time2 { get; set; }
    public int SizeInBytes { get; set; }
    public int MemorySizeInBytes { get; set; }
    public long ReplayDataOffset { get; set; }
    public long StreamOffset { get; set; }
}
