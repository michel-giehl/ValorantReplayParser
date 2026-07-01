namespace Replay.Models.Events;

public abstract record ReplayEvent(float TimeSeconds, int PacketId);