namespace Replay.Models.Errors;

public sealed class InvalidReplayDataException(string message, Exception? innerException = null)
    : ReplayParseException(message, innerException);
