using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public struct FieldBinding
{
    public bool Enabled;
    public ExportCategory Categories;
    public IFieldDecoder? Decoder;
    public string? Name;
    public string? ExportName;
}
