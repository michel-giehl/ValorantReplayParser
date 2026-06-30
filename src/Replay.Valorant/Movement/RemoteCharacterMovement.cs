using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Movement;

public sealed class RemoteCharacterUpdateBatch
{
    public List<RemoteCharacterUpdate> Updates { get; } = [];

    public override string ToString()
    {
        return string.Join(", ", Updates.Select(u => u.ToString()));
    }
}

public sealed class RemoteCharacterUpdate
{
    public int Index { get; init; }
    public uint? ShooterCharacterNetGuidValue { get; set; }
    public ComponentDataStream? ComponentDataStream { get; set; }

    public override string ToString()
    {
        return $"Guid={ShooterCharacterNetGuidValue}|ComponentDataStream={ComponentDataStream}";
    }
}

public sealed class ComponentDataStream
{
    private const byte MovementMagic = 0x52;
    private const double FixedVectorScale = 1.0 / 65536.0;
    private const double OptionalByteScale = 1.0;
    private const double AngleScale = 360.0 / 65536.0;
    private const int MaxMovementPaddingBits = 31;

    public override string ToString()
    {
        return string.Join(",", Moves.Select(_ => _.Position));
    }

    public bool HasMovementSection { get; private set; }
    public bool HasValidMovementMagic { get; private set; }
    public ushort MovementBitCount { get; private set; }
    public long TrailingComponentBitCount { get; private set; }
    public string? MovementParseError { get; private set; }
    public List<MovementMove> Moves { get; } = [];

    public static ComponentDataStream Decode(FBitArchive archive)
    {
        var stream = new ComponentDataStream();
        stream.Parse(archive);
        return stream;
    }

    private void Parse(FBitArchive archive)
    {
        if (TryReadPayloadBytes(archive, out var payloadBytes))
        {
            using var payloadArchive = new BitArchiveReader(payloadBytes, payloadBytes.Length * 8);
            ParseComponentPayload(payloadArchive);
            return;
        }

        ParseComponentPayload(archive);
    }

    private void ParseComponentPayload(FBitArchive archive)
    {
        using var checkpoint = archive.CreateCheckpoint();

        if (!TryReadUInt16(archive, out var movementBitCount))
        {
            checkpoint.Commit();
            return;
        }

        if (movementBitCount == 0)
        {
            HasMovementSection = true;
            MovementBitCount = (ushort)Math.Min(archive.BitsRemaining, ushort.MaxValue);
            ParseMovementSection(archive);
            checkpoint.Commit();
            return;
        }

        if (movementBitCount > archive.BitsRemaining)
        {
            HasMovementSection = true;
            MovementBitCount = (ushort)Math.Min(archive.BitsRemaining, ushort.MaxValue);
            ParseMovementSection(archive);
            checkpoint.Commit();
            return;
        }

        HasMovementSection = true;
        MovementBitCount = movementBitCount;

        using (var movementArchive = archive.ReadSubArchive(movementBitCount))
        {
            ParseMovementSection(movementArchive);
            movementArchive.SkipRemaining();
        }

        if (archive.BitsRemaining > 0)
        {
            TrailingComponentBitCount = archive.BitsRemaining;
            archive.SkipRemaining();
        }

        checkpoint.Commit();
    }

    private static bool TryReadPayloadBytes(FBitArchive archive, out byte[] payloadBytes)
    {
        payloadBytes = [];
        using var checkpoint = archive.CreateCheckpoint();

        if (!TryReadUInt16(archive, out var byteCount) || byteCount == 0 || archive.BitsRemaining < byteCount * 8L)
        {
            return false;
        }

        payloadBytes = archive.ReadBytes(byteCount).ToArray();
        checkpoint.Commit();
        return true;
    }

    private void ParseMovementSection(FBitArchive archive)
    {
        if (!TryReadByte(archive, out var magic))
        {
            MovementParseError = "Missing movement magic";
            return;
        }

        HasValidMovementMagic = magic == MovementMagic;
        if (!HasValidMovementMagic)
        {
            MovementParseError = $"Invalid movement magic 0x{magic:X2}";
            archive.SkipRemaining();
            return;
        }

        var expectedMarker = 1;
        if (!TryReadBits(archive, 3, out var marker))
        {
            MovementParseError = "Missing first movement marker";
            return;
        }

        while (marker != 0)
        {
            if (marker != expectedMarker)
            {
                MovementParseError = $"Movement marker mismatch: expected {expectedMarker}, got {marker}";
                archive.SkipRemaining();
                return;
            }

            if (!TryReadMove(archive, marker, out var move, out var error))
            {
                MovementParseError = error ?? "Invalid movement record";
                archive.SkipRemaining();
                return;
            }

            Moves.Add(move);

            if (archive.BitsRemaining <= MaxMovementPaddingBits)
            {
                return;
            }

            expectedMarker = NextMarker(expectedMarker);
            if (!TryReadBits(archive, 3, out marker))
            {
                MovementParseError = "Missing next movement marker";
                return;
            }
        }
    }

    private static bool TryReadMove(FBitArchive archive, int marker, out MovementMove move, out string? error)
    {
        move = new MovementMove();
        error = null;

        if (!TryReadBit(archive, out var moveType) ||
            !TryReadByte(archive, out var rotationYawMultiplier) ||
            !TryReadByte(archive, out var movementState) ||
            !TryReadByte(archive, out var unusedByte))
        {
            error = "Missing movement record header";
            return false;
        }

        move.Marker = marker;
        move.MoveType = moveType ? (byte)1 : (byte)0;
        move.RotationYawMultiplier = unchecked((sbyte)rotationYawMultiplier);
        move.ModeFlags = movementState;
        move.MovementState = movementState;
        move.UnusedByte = unusedByte;

        if (!TryReadFixedVector(archive, out var rotationInput) ||
            !TryReadVLQ(archive, out var timestamp) ||
            !TryReadQuantizedVector(archive, 100, out var position))
        {
            error = "Missing movement common vector/timestamp fields";
            return false;
        }

        move.RotationInput = rotationInput;
        move.Timestamp = timestamp;
        move.Position = position;

        if (!TryReadBit(archive, out var hasOptionalByte))
        {
            error = "Missing optional movement value flag";
            return false;
        }

        move.HasOptionalMovementValue = hasOptionalByte;
        if (hasOptionalByte)
        {
            if (!TryReadByte(archive, out var optionalByte))
            {
                error = "Missing optional movement value";
                return false;
            }

            move.OptionalMovementRawByte = optionalByte;
            move.OptionalMovementValue = optionalByte * OptionalByteScale;
        }

        if (!TryReadBit(archive, out var flag48) || !TryReadUInt32(archive, out var packedAngles))
        {
            error = "Missing movement flag/angle fields";
            return false;
        }

        var pitch = (ushort)(packedAngles & 0xFFFF);
        var yaw = (ushort)(packedAngles >> 16);
        move.Flag48 = flag48;
        move.PackedAngles = packedAngles;
        move.RawYaw = yaw;
        move.RawPitch = pitch;
        move.Yaw = yaw * AngleScale;
        move.Pitch = pitch * AngleScale;

        if (moveType)
        {
            if (!TryReadBit(archive, out var variant1Flag) ||
                !TryReadQuantizedVector(archive, 10, out var variant1Vector))
            {
                error = "Missing variant-1 movement fields";
                return false;
            }

            move.Variant1Flag = variant1Flag;
            move.Variant1Vector = variant1Vector;
            move.Velocity = variant1Vector;
        }
        else if (!TryReadVariant0Extra(archive, move, out error))
        {
            return false;
        }

        if (!TryReadBit(archive, out var errorSentinel))
        {
            error = "Missing movement error sentinel";
            return false;
        }

        move.ErrorSentinel = errorSentinel;
        if (errorSentinel)
        {
            error = "Movement error sentinel was set";
        }

        return !errorSentinel;
    }

    private static bool TryReadVariant0Extra(FBitArchive archive, MovementMove move, out string? error)
    {
        error = null;
        if (!TryReadBit(archive, out var hasExternalCharacterRef))
        {
            error = "Missing variant-0 external reference flag";
            return false;
        }

        move.Variant0HasExternalCharacterRef = hasExternalCharacterRef;
        if (hasExternalCharacterRef)
        {
            error = "Variant-0 external character reference is not decoded yet";
            return false;
        }

        if (!TryReadUInt32(archive, out var packedAngles))
        {
            error = "Missing variant-0 packed angle dword";
            return false;
        }

        move.Variant0PackedAngles = packedAngles;
        return true;
    }

    private static bool TryReadFixedVector(FBitArchive archive, out FVector vector)
    {
        vector = default;
        if (!TryReadSerializedInt(archive, 0x10000, out var x) ||
            !TryReadSerializedInt(archive, 0x10000, out var y) ||
            !TryReadSerializedInt(archive, 0x10000, out var z))
        {
            return false;
        }

        vector = new FVector(
            ((int)x - 0x8000) * FixedVectorScale,
            ((int)y - 0x8000) * FixedVectorScale,
            ((int)z - 0x8000) * FixedVectorScale)
        {
            ScaleFactor = 65536,
            Bits = 16,
        };
        return true;
    }

    private static bool TryReadQuantizedVector(FBitArchive archive, int scaleFactor, out FVector vector)
    {
        vector = default;
        if (!TryReadSerializedInt(archive, 1 << 7, out var componentBitCountAndExtraInfo))
        {
            return false;
        }

        var componentBits = (int)(componentBitCountAndExtraInfo & 63U);
        var extraInfo = componentBitCountAndExtraInfo >> 6;

        if (componentBits > 0)
        {
            if (!TryReadSignedQuantizedComponent(archive, componentBits, out var x) ||
                !TryReadSignedQuantizedComponent(archive, componentBits, out var y) ||
                !TryReadSignedQuantizedComponent(archive, componentBits, out var z))
            {
                return false;
            }

            vector = new FVector(
                extraInfo > 0 ? x / (double)scaleFactor : x,
                extraInfo > 0 ? y / (double)scaleFactor : y,
                extraInfo > 0 ? z / (double)scaleFactor : z)
            {
                Bits = componentBits,
                ScaleFactor = scaleFactor,
            };
            return true;
        }

        if (extraInfo == 0)
        {
            if (archive.BitsRemaining < 96)
            {
                return false;
            }

            vector = new FVector(archive.ReadSingle(), archive.ReadSingle(), archive.ReadSingle())
            {
                Bits = 32,
                ScaleFactor = scaleFactor,
            };
            return true;
        }

        if (archive.BitsRemaining < 192)
        {
            return false;
        }

        vector = new FVector(archive.ReadDouble(), archive.ReadDouble(), archive.ReadDouble())
        {
            Bits = 64,
            ScaleFactor = scaleFactor,
        };
        return true;
    }

    private static bool TryReadSignedQuantizedComponent(FBitArchive archive, int componentBits, out long value)
    {
        value = 0;
        if (componentBits <= 0 || componentBits > 62 || archive.BitsRemaining < componentBits)
        {
            return false;
        }

        var raw = archive.ReadBitsToUInt64(componentBits);
        var signBit = 1UL << (componentBits - 1);
        value = (long)(raw ^ signBit) - (long)signBit;
        return true;
    }

    private static bool TryReadVLQ(FBitArchive archive, out uint value)
    {
        value = 0;
        var shift = 0;

        while (true)
        {
            if (!TryReadByte(archive, out var b))
            {
                return false;
            }

            value |= (uint)(((b >> 1) & 0x7F) << shift);
            if ((b & 1) == 0)
            {
                return true;
            }

            shift += 7;
            if (shift >= 32)
            {
                return false;
            }
        }
    }

    private static bool TryReadSerializedInt(FBitArchive archive, int maxValue, out uint value)
    {
        value = 0;
        for (uint mask = 1; value + mask < maxValue; mask <<= 1)
        {
            if (!TryReadBit(archive, out var bit))
            {
                return false;
            }

            if (bit)
            {
                value |= mask;
            }
        }

        return true;
    }

    private static bool TryReadBit(FBitArchive archive, out bool value) => archive.TryReadBit(out value);

    private static bool TryReadBits(FBitArchive archive, int bitCount, out int value)
    {
        value = 0;
        if (bitCount is < 0 or > sizeof(int) * 8 || archive.BitsRemaining < bitCount)
        {
            return false;
        }

        value = (int)archive.ReadBitsToUInt64(bitCount);
        return true;
    }

    private static bool TryReadByte(FBitArchive archive, out byte value) => archive.TryReadByte(out value);

    private static bool TryReadUInt16(FBitArchive archive, out ushort value)
    {
        value = 0;
        if (archive.BitsRemaining < 16)
        {
            return false;
        }

        value = archive.ReadUInt16();
        return true;
    }

    private static bool TryReadUInt32(FBitArchive archive, out uint value)
    {
        value = 0;
        if (archive.BitsRemaining < 32)
        {
            return false;
        }

        value = archive.ReadUInt32();
        return true;
    }

    private static int NextMarker(int marker)
    {
        var next = (marker + 1) & 7;
        return next < 2 ? 1 : next;
    }
}

public sealed class MovementMove
{
    public int Marker { get; set; }
    public byte MoveType { get; set; }
    public FVector? Position { get; set; }
    public FVector? Velocity { get; set; }
    public FVector? RotationInput { get; set; }
    public FVector? Variant1Vector { get; set; }
    public uint Timestamp { get; set; }
    public byte ModeFlags { get; set; }
    public byte MovementState { get; set; }
    public sbyte RotationYawMultiplier { get; set; }
    public byte UnusedByte { get; set; }
    public bool HasOptionalMovementValue { get; set; }
    public byte? OptionalMovementRawByte { get; set; }
    public double? OptionalMovementValue { get; set; }
    public bool Flag48 { get; set; }
    public uint PackedAngles { get; set; }
    public ushort RawYaw { get; set; }
    public ushort RawPitch { get; set; }
    public double Yaw { get; set; }
    public double Pitch { get; set; }
    public bool? Variant0HasExternalCharacterRef { get; set; }
    public uint? Variant0PackedAngles { get; set; }
    public bool? Variant1Flag { get; set; }
    public bool ErrorSentinel { get; set; }
}

public sealed record RemoteCharacterMovementReceived(
    float TimeSeconds,
    int PacketId,
    uint ActorNetGuid,
    uint ObjectNetGuid,
    uint ChannelIndex,
    int UpdateIndex,
    uint ShooterCharacterNetGuidValue,
    int MoveIndex,
    MovementMove Move)
    : ReplayEvent(TimeSeconds, PacketId);

internal sealed class RemoteCharacterUpdatesRpcDecoder : IRpcDecoder
{
    private const int RemoteCharacterUpdatesHandle = 1;
    private const int ShooterCharacterNetGuidValueHandle = 2;
    private const int ComponentDataStreamHandle = 3;
    private const int MaxRemoteCharacterUpdates = 256;

    public static RemoteCharacterUpdatesRpcDecoder Instance { get; } = new();

    private RemoteCharacterUpdatesRpcDecoder()
    {
    }

    public IReadOnlyList<DecodedReplayField> Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        if (!archive.TryReadBit(out _))
        {
            return [];
        }

        var fields = new List<DecodedReplayField>();
        while (!archive.AtEnd)
        {
            var encodedHandle = archive.ReadIntPacked();
            if (encodedHandle == 0)
            {
                break;
            }

            var handle = checked((int)encodedHandle - 1);
            var payloadBits = archive.ReadIntPacked();
            if (payloadBits > int.MaxValue || archive.BitsRemaining < payloadBits)
            {
                throw new ArchiveReadException(
                    ArchiveErrorCode.InvalidBitCount,
                    nameof(RemoteCharacterUpdatesRpcDecoder),
                    archive.Position,
                    archive.Length,
                    payloadBits);
            }

            using var fieldPayload = archive.ReadSubArchive((int)payloadBits);
            if (handle != RemoteCharacterUpdatesHandle)
            {
                fieldPayload.SkipRemaining();
                continue;
            }

            var batch = ReadRemoteCharacterUpdates(fieldPayload);
            fieldPayload.SkipRemaining();
            EmitMovementEvents(ref context, batch);
            fields.Add(new DecodedReplayField(
                handle,
                "RemoteCharacterUpdates",
                "RemoteCharacterUpdates",
                ExportCategory.Movement,
                DecodedFieldValue.FromObject(batch)));
        }

        return fields;
    }

    private static RemoteCharacterUpdateBatch ReadRemoteCharacterUpdates(FBitArchive archive)
    {
        var updateCount = archive.ReadIntPacked();
        if (updateCount > MaxRemoteCharacterUpdates)
        {
            throw new ArchiveReadException(
                ArchiveErrorCode.InvalidCount,
                nameof(ReadRemoteCharacterUpdates),
                archive.Position,
                archive.Length,
                updateCount);
        }

        var batch = new RemoteCharacterUpdateBatch();
        var updates = new RemoteCharacterUpdate?[updateCount];

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

            var index = checked((int)encodedIndex - 1);
            if ((uint)index >= updateCount)
            {
                archive.SkipRemaining();
                break;
            }

            updates[index] = ReadRemoteCharacterUpdate(archive, index);
        }

        foreach (var update in updates)
        {
            if (update is not null)
            {
                batch.Updates.Add(update);
            }
        }

        return batch;
    }

    private static RemoteCharacterUpdate ReadRemoteCharacterUpdate(FBitArchive archive, int index)
    {
        var update = new RemoteCharacterUpdate { Index = index };
        while (!archive.AtEnd)
        {
            var encodedHandle = archive.ReadIntPacked();
            if (encodedHandle == 0)
            {
                break;
            }

            var handle = checked((int)encodedHandle - 1);
            var payloadBits = archive.ReadIntPacked();
            if (payloadBits > int.MaxValue || archive.BitsRemaining < payloadBits)
            {
                throw new ArchiveReadException(
                    ArchiveErrorCode.InvalidBitCount,
                    nameof(ReadRemoteCharacterUpdate),
                    archive.Position,
                    archive.Length,
                    payloadBits);
            }

            using var fieldPayload = archive.ReadSubArchive((int)payloadBits);
            switch (handle)
            {
                case ShooterCharacterNetGuidValueHandle:
                    update.ShooterCharacterNetGuidValue = fieldPayload.ReadUInt32();
                    break;
                case ComponentDataStreamHandle:
                    update.ComponentDataStream = ComponentDataStream.Decode(fieldPayload);
                    break;
            }

            fieldPayload.SkipRemaining();
        }

        return update;
    }

    private static void EmitMovementEvents(ref FieldDecodeContext context, RemoteCharacterUpdateBatch batch)
    {
        if (context.EventSink is null)
        {
            return;
        }

        foreach (var update in batch.Updates)
        {
            if (update is not { ShooterCharacterNetGuidValue: { } shooterGuid, ComponentDataStream: { } componentDataStream })
            {
                continue;
            }

            for (var moveIndex = 0; moveIndex < componentDataStream.Moves.Count; moveIndex++)
            {
                context.EventSink.Emit(new RemoteCharacterMovementReceived(
                    context.CurrentTimeSeconds,
                    context.CurrentPacketId,
                    context.ActorNetGuid.Value,
                    context.ObjectNetGuid.Value,
                    context.ChannelIndex,
                    update.Index,
                    shooterGuid,
                    moveIndex,
                    componentDataStream.Moves[moveIndex]));
            }
        }
    }
}