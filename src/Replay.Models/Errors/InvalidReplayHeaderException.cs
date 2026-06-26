namespace Replay.Models.Errors;

public sealed class InvalidReplayHeaderException(string message, Exception? innerException = null)
    : ReplayParseException(message, innerException);
