namespace Replay.Models;

public sealed class PlaybackPacket
{
    public int ReplayDataChunkIndex { get; set; } = ReplayInfo.NoChunkIndex;
    public int PacketIndex { get; set; }
    public int CurrentLevelIndex { get; set; }
    public uint SeenLevelIndex { get; set; }
    public float TimeSeconds { get; set; }
    public byte[] Data { get; set; } = [];
}
