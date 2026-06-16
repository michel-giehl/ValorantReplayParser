namespace Replay.Model;

public class InvalidReplayHeaderException(string message) : Exception(message)
{
    public static void ThrowIf(bool predicate, string message)
    {
        if (predicate)
        {
            throw new InvalidReplayHeaderException($"Error while parsing replay header:  {message}");
        }
    }
}