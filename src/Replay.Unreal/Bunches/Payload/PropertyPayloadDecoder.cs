using Replay.Encoding.Archives;
using Replay.Encoding.PayloadEncryption;

namespace Replay.Unreal.Bunches.Payload;

internal sealed class PropertyPayloadDecoder : IPropertyPayloadDecoder
{
    private readonly PayloadTransformRegistry _registry;
    private string? _cachedReplayVersion;
    private IPayloadTransform? _cachedTransform;
    private byte[] _decodeBuffer = [];

    public PropertyPayloadDecoder(PayloadTransformRegistry registry)
    {
        _registry = registry;
    }

    public FBitArchive Decode(FBitArchive payload, int bitCount, uint actorNetGuid, string replayVersion)
    {
        var transform = GetTransform(replayVersion);
        var byteCount = transform.GetOutputByteCount(bitCount);
        if (_decodeBuffer.Length < byteCount)
        {
            Array.Resize(ref _decodeBuffer, byteCount);
        }

        var seed = checked((uint)bitCount) ^ actorNetGuid;
        transform.Apply(payload, bitCount, seed, _decodeBuffer.AsSpan(0, byteCount));

        return new BitArchiveReader(_decodeBuffer.AsMemory(0, byteCount), bitCount);
    }

    private IPayloadTransform GetTransform(string replayVersion)
    {
        if (replayVersion == _cachedReplayVersion && _cachedTransform is not null)
        {
            return _cachedTransform;
        }

        _cachedTransform = PayloadTransformSupport.GetRequired(_registry, replayVersion);
        _cachedReplayVersion = replayVersion;
        return _cachedTransform;
    }
}
