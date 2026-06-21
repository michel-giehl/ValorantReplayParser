namespace Replay.Models;

public enum ChannelCloseReason
{
    Destroyed,
    Dormancy,
    LevelUnloaded,
    Relevancy,
    TearOff,
    MAX = 15,
}
