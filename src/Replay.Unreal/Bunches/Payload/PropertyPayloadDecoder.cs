using System.Buffers;
using Replay.Encoding.Archives;
using Replay.Encoding.PayloadEncryption;
using Replay.Models.Errors;

namespace Replay.Unreal.Bunches.Payload;

internal sealed class PropertyPayloadDecoder : IPropertyPayloadDecoder
{
    private readonly PayloadTransformRegistry _registry;
    private string? _cachedReplayVersion;
    private IPayloadTransform? _cachedTransform;

    public PropertyPayloadDecoder(PayloadTransformRegistry registry)
    {
        _registry = registry;
    }

    public FBitArchive Decode(FBitArchive payload, uint actorNetGuid, string replayVersion)
    {
        var bitCount = checked((int)payload.BitsRemaining);
        var transform = GetTransform(replayVersion);
        var byteCount = transform.GetOutputByteCount(bitCount);
        var owner = MemoryPool<byte>.Shared.Rent(byteCount);

        try
        {
            var seed = checked((uint)bitCount) ^ actorNetGuid;
            transform.Apply(payload, seed, owner.Memory.Span[..byteCount]);
            return new BitArchiveReader(owner, bitCount);
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    private IPayloadTransform GetTransform(string replayVersion)
    {
        if (replayVersion == _cachedReplayVersion && _cachedTransform is not null)
        {
            return _cachedTransform;
        }

        try
        {
            _cachedTransform = _registry.GetRequired(replayVersion);
            _cachedReplayVersion = replayVersion;
            return _cachedTransform;
        }
        catch (UnsupportedPayloadTransformVersionException)
        {
            throw new InvalidReplayInfoException(
                $"Unsupported VALORANT property payload transform for replay version '{replayVersion}'.");
        }
    }
}
