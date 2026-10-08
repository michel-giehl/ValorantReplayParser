using Replay.Encoding.PayloadEncryption.VersionedTransforms;

namespace Replay.Encoding.PayloadEncryption;

public sealed class PayloadTransformRegistry
{
    private readonly Dictionary<string, IPayloadTransform> _transforms;

    private PayloadTransformRegistry(IEnumerable<IPayloadTransform> transforms)
    {
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
        new ValorantSeededTransform11_06(),
        new ValorantSeededTransform11_07(),
        new ValorantSeededTransform11_08(),
        new ValorantSeededTransform11_09(),
        new ValorantSeededTransform11_10(),
        new ValorantSeededTransform11_11(),
        new ValorantSeededTransform12_00(),
        new ValorantSeededTransform12_01(),
        new ValorantSeededTransform12_02(),
        new ValorantSeededTransform12_03(),
        new ValorantSeededTransform12_04(),
        new ValorantSeededTransform12_05(),
        new ValorantSeededTransform12_06(),
        new ValorantSeededTransform12_07(),
        new ValorantSeededTransform12_08(),
        new ValorantSeededTransform12_09(),
        new ValorantSeededTransform12_10(),
        new ValorantSeededTransform12_11(),
        new ValorantSeededTransform13_00(),
        new ValorantSeededTransform13_01(),
        new ValorantSeededTransform13_02(),
        new ValorantSeededTransform13_04(),
        new ValorantSeededTransform13_05(),
        new ValorantSeededTransformChina13_05(),
        new ValorantSeededTransform13_06(),
    ]);

    public IPayloadTransform GetRequired(string replayVersion)
    {
        if (_transforms.TryGetValue(replayVersion, out var transform))
        {
            return transform;
        }

        throw new UnsupportedPayloadTransformVersionException(replayVersion);
    }
}
