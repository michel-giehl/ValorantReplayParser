namespace Replay.Models.Errors;

public sealed class InvalidReplayInfoException(string message) : Exception(message);
