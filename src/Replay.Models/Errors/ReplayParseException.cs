namespace Replay.Models.Errors;

public abstract class ReplayParseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
