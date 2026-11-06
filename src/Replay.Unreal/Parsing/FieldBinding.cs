using Replay.Models.Descriptors;
using System.Reflection;

namespace Replay.Unreal.Parsing;

public struct FieldBinding
{
    public bool Enabled;
    public ExportCategory Categories;
    public IFieldDecoder? Decoder;
    public string? Name;
    public string? ExportName;
    public PropertyInfo? TargetProperty;
    public Action<object, DecodedFieldValue, object?>? Assigner;
    public object? AssignerState;
    public string? DecodedMarker;
}
