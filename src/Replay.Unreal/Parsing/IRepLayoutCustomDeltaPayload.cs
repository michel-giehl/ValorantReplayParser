using Replay.Encoding.Archives;

namespace Replay.Unreal.Parsing;

/// <summary>Opt-in native delta data following a terminated RepLayout property stream.</summary>
public interface IRepLayoutCustomDeltaPayload
{
    void ReadCustomDelta(ref FieldDecodeContext context, FBitArchive payload);
}
