namespace Replay.Model;

public abstract record ReplayParseError(string Message, Exception? Exception = null);

public sealed record InvalidReplayHeaderError(string Message, Exception? Exception = null)
    : ReplayParseError(Message, Exception);

public sealed record InvalidReplayInfoError(string Message, Exception? Exception = null)
    : ReplayParseError(Message, Exception);
