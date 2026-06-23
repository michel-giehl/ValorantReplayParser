using Replay.Encoding.Archives;
using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public interface IFieldDecoder : IFieldDecoderDescriptor
{
    void Decode(ref FieldDecodeContext context, FBitArchive archive);
}

public interface IRpcDecoder : IRpcDecoderDescriptor
{
    void Decode(ref FieldDecodeContext context, FBitArchive archive);
}
