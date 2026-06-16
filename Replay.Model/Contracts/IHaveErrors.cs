namespace Replay.Model.Contracts;

public interface IHaveErrors
{
    List<ReplayParseError> Errors { get; }
}