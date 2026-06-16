using Replay.Encoding.PayloadEncryption.VersionedTransforms;

namespace Replay.Encoding.PayloadEncryption;

public sealed class PayloadTransformRegistry
{
    private readonly Dictionary<string, IPayloadTransform> _transforms;

    private PayloadTransformRegistry(IEnumerable<IPayloadTransform> transforms)
    {
        ArgumentNullException.ThrowIfNull(transforms);

        _transforms = new Dictionary<string, IPayloadTransform>(StringComparer.Ordinal);
        foreach (var transform in transforms)
        {
            foreach (var version in transform.SupportedReplayVersions)
            {
                if (!_transforms.TryAdd(version, transform))
                {
                    throw new ArgumentException($"Replay version '{version}' has more than one payload transform.",
                        nameof(transforms));
                }
            }
        }
    }

    public static PayloadTransformRegistry CreateDefault() => new([
        new ValorantSeededTransform12_10(),
        new ValorantSeededTransform12_11(),
    ]);

    public IPayloadTransform GetRequired(string replayVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayVersion);

        if (_transforms.TryGetValue(replayVersion, out var transform))
        {
            return transform;
        }

        throw new UnsupportedPayloadTransformVersionException(replayVersion);
    }
}