namespace Replay.Unreal;

public sealed class WorldState
{
    public Dictionary<uint, ActorChannelState> Channels { get; } = [];
    public Dictionary<uint, ActorState> ActorsByNetGuid { get; } = [];
    public Dictionary<uint, ObjectState> ObjectsByNetGuid { get; } = [];
    public List<ActorChannelState> ActorChannelHistory { get; } = [];

    internal void Reset()
    {
        Channels.Clear();
        ActorsByNetGuid.Clear();
        ObjectsByNetGuid.Clear();
        ActorChannelHistory.Clear();
    }
}
