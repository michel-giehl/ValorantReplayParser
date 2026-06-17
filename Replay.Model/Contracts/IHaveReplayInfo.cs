namespace Replay.Model.Contracts;

public interface IHaveReplayInfo
{
    ReplayInfo ReplayInfo { get; set; }
    ReplayInfoSerializationMetadata ReplayInfoSerializationMetadata { get; set; }
}
