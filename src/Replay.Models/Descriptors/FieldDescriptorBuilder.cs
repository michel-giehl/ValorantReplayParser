using System.Reflection;
using Replay.Models.Net;
using Replay.Models.Replay;

namespace Replay.Models.Descriptors;

public sealed class FieldDescriptorBuilder
{
    internal FieldDescriptorBuilder(
        string? exportName,
        string? propertyName,
        PropertyInfo? targetProperty,
        uint? handle,
        ExportCategory categories)
    {
        ExportName = exportName;
        PropertyName = propertyName;
        TargetProperty = targetProperty;
        Handle = handle;
        Categories = categories;
    }

    private string? ExportName { get; }

    private string? PropertyName { get; }

    private PropertyInfo? TargetProperty { get; }

    private uint? Handle { get; }

    private ExportCategory Categories { get; set; }

    private IFieldDecoderDescriptor? Decoder { get; set; }

    private VersionedDefinition<IFieldDecoderDescriptor>? DecoderDefinition { get; set; }

    private Func<NetFieldExportGroup, uint?>? HandleResolver { get; set; }

    private Action<object, DecodedFieldValue, object?>? Assigner { get; set; }

    private Func<NetFieldExportGroup, object?>? BindingStateResolver { get; set; }

    private string? DecodedMarker { get; set; }

    /// <summary>Binds the field by a resolver over the replay export table; see <see cref="FieldDescriptor.HandleResolver"/>.</summary>
    public FieldDescriptorBuilder ResolveHandle(Func<NetFieldExportGroup, uint?> resolver)
    {
        HandleResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }

    /// <summary>Routes the decoded value to a custom assigner; see <see cref="FieldDescriptor.Assigner"/>.</summary>
    public FieldDescriptorBuilder AssignWith(Action<object, DecodedFieldValue, object?> assigner, string decodedMarker)
    {
        Assigner = assigner ?? throw new ArgumentNullException(nameof(assigner));
        DecodedMarker = decodedMarker ?? throw new ArgumentNullException(nameof(decodedMarker));
        return this;
    }

    /// <summary>Computes the state passed to the custom assigner; see <see cref="FieldDescriptor.BindingStateResolver"/>.</summary>
    public FieldDescriptorBuilder WithBindingState(Func<NetFieldExportGroup, object?> resolver)
    {
        BindingStateResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }

    public FieldDescriptorBuilder WithCategories(ExportCategory categories)
    {
        Categories = categories;
        return this;
    }

    public FieldDescriptorBuilder Decode(IFieldDecoderDescriptor decoder)
    {
        Decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        DecoderDefinition = null;
        return this;
    }

    public FieldDescriptorBuilder Decode<TDecoder>(VersionedDefinition<TDecoder> decoderDefinition)
        where TDecoder : IFieldDecoderDescriptor
    {
        ArgumentNullException.ThrowIfNull(decoderDefinition);
        DecoderDefinition = decoderDefinition.Select(static decoder => (IFieldDecoderDescriptor)decoder);
        Decoder = DecoderDefinition.Baseline;
        return this;
    }

    internal FieldDescriptor Build() => new()
    {
        ExportName = ExportName,
        PropertyName = PropertyName,
        TargetProperty = TargetProperty,
        Handle = Handle,
        Categories = Categories,
        Decoder = Decoder,
        DecoderDefinition = DecoderDefinition,
        HandleResolver = HandleResolver,
        Assigner = Assigner,
        BindingStateResolver = BindingStateResolver,
        DecodedMarker = DecodedMarker,
    };
}
