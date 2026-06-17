namespace Replay.Models.Contracts;

public interface IHaveReplayInfo
{
    ReplayInfo ReplayInfo { get; set; }
    ReplayInfoSerializationMetadata ReplayInfoSerializationMetadata { get; set; }
}
