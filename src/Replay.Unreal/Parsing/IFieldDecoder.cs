using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Events;

namespace Replay.Unreal.Parsing;

public interface IFieldDecoder : IFieldDecoderDescriptor
{
    DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive);
}

public interface IRpcDecoder : IRpcDecoderDescriptor
{
    IReadOnlyList<DecodedReplayField> Decode(ref FieldDecodeContext context, FBitArchive archive);
}