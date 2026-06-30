using Replay.Encoding.PayloadEncryption;
using Replay.Models.Errors;

namespace Replay.Unreal.Bunches.Payload;

internal static class PayloadTransformSupport
{
    public static PayloadTransformRegistry DefaultRegistry { get; } = PayloadTransformRegistry.CreateDefault();

    public static void ValidateSupported(string replayVersion)
    {
        if (string.IsNullOrWhiteSpace(replayVersion))
        {
            return;
        }

        _ = GetRequired(DefaultRegistry, replayVersion);
    }

    public static IPayloadTransform GetRequired(PayloadTransformRegistry registry, string replayVersion)
    {
        try
        {
            return registry.GetRequired(replayVersion);
        }
        catch (UnsupportedPayloadTransformVersionException)
        {
            throw new InvalidReplayInfoException(
                $"Unsupported VALORANT property payload transform for replay version '{replayVersion}'.");
        }
    }
}