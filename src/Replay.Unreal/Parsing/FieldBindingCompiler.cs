using Replay.Models.Descriptors;
using Replay.Models.Replay;

namespace Replay.Unreal.Parsing;

internal static class FieldBindingCompiler
{
    public static FieldBinding Compile(
        FieldDescriptor field,
        ExportCategory inheritedCategories,
        ReplayReleaseVersion? releaseVersion,
        string? replayExportName = null,
        bool arrayElement = false)
    {
        var definition = field.DecoderDefinition?.Resolve(
            releaseVersion, arrayElement ? "nested array field decoder" : "field decoder") ?? field.Decoder;
        if (definition is not null and not IFieldDecoder || (arrayElement && definition is null))
        {
            throw new InvalidOperationException(arrayElement
                ? $"Array element field '{field.PropertyName ?? field.ExportName ?? field.Handle.ToString()}' uses an incompatible decoder type."
                : $"Field descriptor '{field.PropertyName ?? field.ExportName ?? field.Handle?.ToString() ?? "<unnamed>"}' uses an incompatible decoder type.");
        }

        var decoder = (IFieldDecoder?)definition;
        if (decoder is IReplayReleaseAwareFieldDecoder releaseAware)
            decoder = releaseAware.Resolve(releaseVersion);

        var exportName = field.ExportName ?? replayExportName;
        return new FieldBinding
        {
            Enabled = arrayElement || decoder is not null,
            Categories = field.Categories == ExportCategory.None ? inheritedCategories : field.Categories,
            Decoder = decoder,
            Name = field.PropertyName ?? exportName,
            ExportName = exportName,
            TargetProperty = field.TargetProperty,
            Assigner = field.Assigner,
            DecodedMarker = field.DecodedMarker,
        };
    }
}
