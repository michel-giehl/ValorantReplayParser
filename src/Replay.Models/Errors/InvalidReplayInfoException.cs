namespace Replay.Models.Errors;

public sealed class InvalidReplayInfoException(string message) : Exception(message)
{
    public static void ThrowIf(bool predicate, string message)
    {
        if (predicate)
        {
            throw new InvalidReplayInfoException($"Error while parsing replay info: {message}");
        }
    }
}
