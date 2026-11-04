using System.Collections.Concurrent;
using System.Reflection;
using Replay.Models.Net;

namespace Replay.Models.Descriptors;

/// <summary>
/// Who a replicated actor names as its owner, instigator and creator: only fields the replay sent, and only ones
/// that point at something. Owner is sometimes a player's character and sometimes a parent actor, and telling
/// which is the consumer's job.
/// </summary>
public readonly record struct ActorOwnerReferences(uint? Owner, uint? Instigator, uint? Creator)
{
    private const string OwnerField = "Owner";
    private const string InstigatorField = "Instigator";
    private const string CreatorField = "CreatedByCharacter";
    private static readonly ConcurrentDictionary<Type, FieldReaders> Readers = new();

    public bool IsEmpty => Owner is null && Instigator is null && Creator is null;

    /// <summary>Works on any decoded payload, because descriptors declare these fields with different types.</summary>
    public static ActorOwnerReferences Read(object? payload)
    {
        if (payload is not IDecodedPayload decoded) return default;
        var fields = Readers.GetOrAdd(payload.GetType(), FieldReaders.For);
        return new ActorOwnerReferences(
            Read(fields.Owner, OwnerField, decoded), Read(fields.Instigator, InstigatorField, decoded),
            Read(fields.Creator, CreatorField, decoded));
    }

    private static uint? Read(PropertyInfo? property, string field, IDecodedPayload payload) =>
        property is not null && payload.HasDecoded(field)
            ? property.GetValue(payload) switch
            {
                NetObjectReference { NetGuid: > 0 } reference => reference.NetGuid,
                _ => null,
            }
            : null;

    private sealed record FieldReaders(PropertyInfo? Owner, PropertyInfo? Instigator, PropertyInfo? Creator)
    {
        public static FieldReaders For(Type type) => new(Find(type, OwnerField), Find(type, InstigatorField), Find(type, CreatorField));

        private static PropertyInfo? Find(Type type, string field) => type.GetProperty(field) is { } property &&
            (property.PropertyType == typeof(NetObjectReference) || property.PropertyType == typeof(NetObjectReference?))
                ? property
                : null;
    }
}
