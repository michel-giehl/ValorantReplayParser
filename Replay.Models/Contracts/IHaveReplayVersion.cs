namespace Replay.Models.Contracts;

public interface IHaveReplayVersion
{
    ReplayVersion ReplayVersion { get; set; }
    UEVersion UEVersion { get; set; }
}
