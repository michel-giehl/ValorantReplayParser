using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Replay;

namespace Replay.Unreal.Parsing;

public static class RepLayoutArrayDecoders
{
    public static IFieldDecoder DynamicArray<TElement>()
        where TElement : ExportGroupDescriptor<TElement>, new() =>
        new DescriptorDynamicArrayDecoder<TElement>(
            new VersionedDefinition<ExportGroupDescriptor>(new TElement()));

    public static IFieldDecoder DynamicArray<TElement>(VersionedDefinition<TElement> elementDescriptors)
        where TElement : ExportGroupDescriptor
    {
        ArgumentNullException.ThrowIfNull(elementDescriptors);
        var expectedPath = elementDescriptors.Baseline.Path;
        return new DescriptorDynamicArrayDecoder<TElement>(
            elementDescriptors.Select(descriptor => descriptor.Path == expectedPath
                ? (ExportGroupDescriptor)descriptor
                : throw new ArgumentException(
                    $"Versioned array element descriptors must use the same path. Expected '{expectedPath}', got '{descriptor.Path}'.",
                    nameof(elementDescriptors))));
    }

    public static IFieldDecoder DynamicArray<TElement>(IFieldDecoder elementDecoder) =>
        new DynamicArrayDecoder<TElement>(elementDecoder ?? throw new ArgumentNullException(nameof(elementDecoder)));

    private sealed class DescriptorDynamicArrayDecoder<TElement>(
        VersionedDefinition<ExportGroupDescriptor> elementDescriptors)
        : IFieldDecoder, IReplayReleaseAwareFieldDecoder
    {
        public IFieldDecoder Resolve(ReplayReleaseVersion? releaseVersion)
        {
            var descriptor = elementDescriptors.Resolve(releaseVersion, "nested array element descriptor");
            return new DynamicArrayDecoder<TElement>(descriptor, releaseVersion);
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            Resolve(context.ReplayReleaseVersion).Decode(ref context, archive);
    }

    private sealed class DynamicArrayDecoder<TElement> : IFieldDecoder
    {
        private readonly ExportGroupDescriptor? _descriptor;
        private readonly IFieldDecoder? _elementDecoder;
        private readonly FieldBinding[] _fieldsByHandle;

        public DynamicArrayDecoder(
            ExportGroupDescriptor descriptor,
            ReplayReleaseVersion? releaseVersion)
        {
            _descriptor = descriptor;
            _fieldsByHandle = CreateBindings(descriptor, releaseVersion);
        }

        public DynamicArrayDecoder(IFieldDecoder elementDecoder)
        {
            _elementDecoder = elementDecoder;
            _fieldsByHandle = [];
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            if (archive.AtEnd)
            {
                return DecodedFieldValue.FromObject(Array.Empty<TElement>());
            }

            var elementCount = archive.ReadIntPacked();
            if (elementCount > int.MaxValue)
            {
                throw new ArchiveReadException(
                    ArchiveErrorCode.InvalidCount,
                    nameof(DynamicArrayDecoder<TElement>),
                    archive.Position,
                    archive.Length,
                    elementCount);
            }

            var elements = new TElement[elementCount];

            if (_elementDecoder is { } primitiveDecoder)
            {
                while (!archive.AtEnd)
                {
                    var encodedIndex = archive.ReadIntPacked();
                    if (encodedIndex == 0)
                    {
                        break;
                    }

                    var index = checked(encodedIndex - 1);
                    if (index >= elementCount)
                    {
                        archive.SkipRemaining();
                        break;
                    }

                    elements[index] = DecodePrimitiveElement(primitiveDecoder, ref context, archive);
                }

                return DecodedFieldValue.FromObject(elements);
            }

            while (!archive.AtEnd)
            {
                var encodedIndex = archive.ReadIntPacked();
                if (encodedIndex == 0)
                {
                    if (archive.BitsRemaining == 8)
                    {
                        _ = archive.ReadIntPacked();
                    }

                    break;
                }

                var index = checked(encodedIndex - 1);
                if (index >= elementCount)
                {
                    archive.SkipRemaining();
                    break;
                }

                var element = (TElement)_descriptor!.CreatePayloadInstance();
                DecodeElement(element, ref context, archive);
                elements[index] = element;
            }

            return DecodedFieldValue.FromObject(elements);
        }

        private static TElement DecodePrimitiveElement(
            IFieldDecoder decoder,
            ref FieldDecodeContext context,
            FBitArchive archive)
        {
            var decodedValue = decoder.Decode(ref context, archive);

            if (!decodedValue.HasValue ||
                !DecodedValueAssigner.TryGetAssignmentValue(decodedValue, typeof(TElement), out var assignmentValue))
            {
                throw new InvalidOperationException(
                    $"Primitive dynamic-array decoder '{decoder.GetType().Name}' returned a value that cannot be assigned to '{typeof(TElement).Name}'.");
            }

            return (TElement)assignmentValue!;
        }

        private void DecodeElement(TElement element, ref FieldDecodeContext context, FBitArchive archive)
        {
            while (!archive.AtEnd)
            {
                var encodedHandle = archive.ReadIntPacked();
                if (encodedHandle == 0)
                {
                    break;
                }

                var handle = checked((int)(encodedHandle - 1));
                var payloadBits = archive.ReadIntPacked();
                if (payloadBits == 0)
                {
                    continue;
                }

                if (payloadBits > int.MaxValue || archive.BitsRemaining < payloadBits)
                {
                    throw new ArchiveReadException(
                        ArchiveErrorCode.InvalidBitCount,
                        nameof(DynamicArrayDecoder<TElement>),
                        archive.Position,
                        archive.Length,
                        payloadBits);
                }

                var fieldPayload = archive.ReadSubArchive((int)payloadBits);
                if ((uint)handle >= (uint)_fieldsByHandle.Length ||
                    !_fieldsByHandle[handle].Enabled ||
                    _fieldsByHandle[handle].Decoder is not { } decoder)
                {
                    fieldPayload.SkipRemaining();
                    continue;
                }

                var binding = _fieldsByHandle[handle];
                var previousFieldName = context.FieldName;
                var previousCategories = context.Categories;
                context.FieldName = binding.Name;
                context.Categories = binding.Categories;

                var decodedValue = decoder.Decode(ref context, fieldPayload);
                if (!fieldPayload.AtEnd)
                {
                    fieldPayload.EnsureFullyConsumed($"array field '{binding.Name}' (handle {handle})");
                }

                DecodedValueAssigner.Assign(element!, binding, decodedValue);
                context.FieldName = previousFieldName;
                context.Categories = previousCategories;
            }
        }

        private static FieldBinding[] CreateBindings(
            ExportGroupDescriptor descriptor,
            ReplayReleaseVersion? releaseVersion)
        {
            var maxHandle = descriptor.Fields
                .Where(field => field.Handle.HasValue)
                .Select(field => field.Handle!.Value)
                .DefaultIfEmpty()
                .Max();
            var fields = new FieldBinding[checked((int)maxHandle + 1)];

            foreach (var field in descriptor.Fields)
            {
                if (field.Handle is null)
                {
                    continue;
                }

                var decoderDescriptor = field.DecoderDefinition?.Resolve(releaseVersion, "nested array field decoder")
                                        ?? field.Decoder;
                var decoder = decoderDescriptor as IFieldDecoder
                              ?? throw new InvalidOperationException(
                                  $"Array element field '{field.PropertyName ?? field.ExportName ?? field.Handle.ToString()}' uses an incompatible decoder type.");
                if (decoder is IReplayReleaseAwareFieldDecoder releaseAware)
                {
                    decoder = releaseAware.Resolve(releaseVersion);
                }

                var categories = field.Categories == ExportCategory.None
                    ? descriptor.Categories
                    : field.Categories;
                fields[field.Handle.Value] = new FieldBinding
                {
                    Enabled = true,
                    Categories = categories,
                    Decoder = decoder,
                    Name = field.PropertyName ?? field.ExportName,
                    ExportName = field.ExportName,
                    TargetProperty = field.TargetProperty,
                };
            }

            return fields;
        }
    }
}
