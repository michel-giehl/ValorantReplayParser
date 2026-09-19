namespace Replay.Models.Descriptors;

public interface IReplayPathAliasProvider
{
    string? GetAlternatePath(string path);
}
