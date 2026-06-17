namespace Replay.Models.Contracts;

public interface IHaveErrors
{
    List<ReplayParseError> Errors { get; }
}
