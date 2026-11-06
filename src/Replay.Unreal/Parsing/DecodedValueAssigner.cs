using System.Globalization;
using System.Reflection;
using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

internal static class DecodedValueAssigner
{
    public static void Assign(object payload, FieldBinding binding, DecodedFieldValue value)
    {
        if (value.HasValue && binding.Assigner is not null)
        {
            binding.Assigner(payload, value, binding.AssignerState);
            if (payload is ExportGroupDescriptor custom && binding.DecodedMarker is not null)
            {
                custom.MarkDecoded(binding.DecodedMarker);
            }

            return;
        }

        if (!value.HasValue || binding.TargetProperty is null)
        {
            return;
        }

        var targetType = Nullable.GetUnderlyingType(binding.TargetProperty.PropertyType)
                         ?? binding.TargetProperty.PropertyType;
        if (!TryGetAssignmentValue(value, targetType, out var assignmentValue))
        {
            if (value.Kind == DecodedFieldValueKind.Object)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Decoded field '{binding.Name ?? binding.ExportName ?? "<unknown>"}' cannot be assigned to property " +
                $"'{binding.TargetProperty.DeclaringType?.Name}.{binding.TargetProperty.Name}' of type '{targetType.Name}'.");
        }

        try
        {
            binding.TargetProperty.SetValue(payload, assignmentValue);
        }
        catch (Exception exception) when (exception is ArgumentException or TargetInvocationException or MethodAccessException)
        {
            throw new InvalidOperationException(
                $"Could not assign decoded field '{binding.Name ?? binding.ExportName ?? "<unknown>"}' to property " +
                $"'{binding.TargetProperty.DeclaringType?.Name}.{binding.TargetProperty.Name}'.",
                exception);
        }

        if (payload is ExportGroupDescriptor descriptor)
        {
            descriptor.MarkDecoded(binding.TargetProperty.Name);
        }
    }

    internal static bool TryGetAssignmentValue(
        DecodedFieldValue value,
        Type targetType,
        out object? assignmentValue)
    {
        assignmentValue = value.Kind switch
        {
            DecodedFieldValueKind.Bool => value.BoolValue,
            DecodedFieldValueKind.Byte => value.ByteValue,
            DecodedFieldValueKind.Int32 => value.Int32Value,
            DecodedFieldValueKind.UInt32 => value.UInt32Value,
            DecodedFieldValueKind.UInt64 => value.UInt64Value,
            DecodedFieldValueKind.Float => value.FloatValue,
            DecodedFieldValueKind.Double => value.DoubleValue,
            DecodedFieldValueKind.String => value.StringValue,
            DecodedFieldValueKind.ObjectReference => value.ObjectReferenceValue,
            DecodedFieldValueKind.Guid => value.GuidValue,
            DecodedFieldValueKind.Vector => value.VectorValue,
            DecodedFieldValueKind.Rotator => value.RotatorValue,
            DecodedFieldValueKind.Transform => value.TransformValue,
            DecodedFieldValueKind.GameplayTag => value.GameplayTagValue,
            DecodedFieldValueKind.Object => value.ObjectValue,
            DecodedFieldValueKind.RepMovement => value.RepMovementValue,
            _ => null,
        };

        if (assignmentValue is null)
        {
            return !targetType.IsValueType;
        }

        if (targetType.IsInstanceOfType(assignmentValue))
        {
            return true;
        }

        if (targetType.IsEnum)
        {
            try
            {
                assignmentValue = Enum.ToObject(targetType, assignmentValue);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        if (assignmentValue is not IConvertible || !typeof(IConvertible).IsAssignableFrom(targetType)) return false;
        try
        {
            assignmentValue = Convert.ChangeType(assignmentValue, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }
}
