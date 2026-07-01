using Replay.Encoding.Archives;

namespace Replay.Encoding.PayloadEncryption;

public interface IPayloadTransform
{
    IReadOnlyCollection<string> SupportedReplayVersions { get; }

    int GetOutputByteCount(int bitCount);

    void Apply(FBitArchive input, uint seed, Span<byte> output);

    void Apply(FBitArchive input, int bitCount, uint seed, Span<byte> output);
}
