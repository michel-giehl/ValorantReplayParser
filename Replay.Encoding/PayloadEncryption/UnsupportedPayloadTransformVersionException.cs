namespace Replay.Encoding.PayloadEncryption;

public sealed class UnsupportedPayloadTransformVersionException(string replayVersion)
    : NotSupportedException($"No payload transform is registered for replay version '{replayVersion}'.")
{
    public string ReplayVersion { get; } = replayVersion;
}