namespace Replay.Models.Net;

public enum ChannelCloseReason
{
    Destroyed,
    Dormancy,
    LevelUnloaded,
    Relevancy,
    TearOff,
    MAX = 15,
}
