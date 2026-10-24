using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Unreal;

namespace Replay.Unreal.Parsing;

public static class PrimitiveDecoders
{
    public static readonly IFieldDecoder Int32 = new Int32Decoder();
    public static readonly IFieldDecoder UInt16 = new UInt16Decoder();
    public static readonly IFieldDecoder UInt32 = new UInt32Decoder();
    public static readonly IFieldDecoder UInt64 = new UInt64Decoder();
    public static readonly IFieldDecoder Float = new FloatDecoder();
    public static readonly IFieldDecoder Double = new DoubleDecoder();
    public static readonly IFieldDecoder Bool = new BoolDecoder();
    public static readonly IFieldDecoder Byte = new ByteDecoder();
    public static readonly IFieldDecoder EnumByte = Byte;
    public static readonly IFieldDecoder FString = new FStringDecoder();
    public static readonly IFieldDecoder FName = new FNameDecoder();
    public static readonly IFieldDecoder ObjectNetGuid = new ObjectNetGuidDecoder();
    public static readonly IFieldDecoder Guid = new GuidDecoder();
    public static readonly IFieldDecoder Vector = new DoubleVectorDecoder();
    public static readonly IFieldDecoder VectorFloat = new FloatVectorDecoder();
    public static readonly IFieldDecoder VectorDouble = Vector;
    public static readonly IFieldDecoder VectorNetQuantize = new QuantizedVectorDecoder(scaleFactor: 1);
    public static readonly IFieldDecoder VectorNetQuantize10 = new QuantizedVectorDecoder(scaleFactor: 10);
    public static readonly IFieldDecoder VectorNetQuantize100 = new QuantizedVectorDecoder(scaleFactor: 100);
    public static readonly IFieldDecoder VectorNetQuantizeNormal = new FixedNormalVectorDecoder();
    public static readonly IFieldDecoder RotatorShort = VectorDecoders.RotationShort;
    public static readonly IFieldDecoder Transform = VectorDecoders.Transform;
    public static readonly IFieldDecoder NormalizedQuaternion = new NormalizedQuaternionDecoder();
    public static readonly IFieldDecoder EnumRemainingBits = new EnumRemainingBitsDecoder();
    public static readonly IFieldDecoder GameplayTag = new GameplayTagDecoder();
    public static readonly IFieldDecoder RepMovement = new ReplicatedMovementDecoder(
        ERotatorQuantization.ShortComponents);
    public static readonly IFieldDecoder RepMovementByte = new ReplicatedMovementDecoder(
        ERotatorQuantization.ByteComponents);
    public static readonly IFieldDecoder Skip = new SkipDecoder();

    public static IFieldDecoder SerializedInt(int maxValue) => new SerializedIntDecoder(maxValue);
    public static IFieldDecoder ByteArray(int maxBytes) => new ByteArrayDecoder(maxBytes);

    public static IFieldDecoder RepMovementWithRotation(ERotatorQuantization rotationQuantization) =>
        rotationQuantization switch
        {
            ERotatorQuantization.ByteComponents => RepMovementByte,
            ERotatorQuantization.ShortComponents => RepMovement,
            _ => throw new ArgumentOutOfRangeException(
                nameof(rotationQuantization), rotationQuantization, null),
        };

    private sealed class FloatVectorDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadFloatVector(archive));
    }

    private sealed class NormalizedQuaternionDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var x = archive.ReadDouble();
            var y = archive.ReadDouble();
            var z = archive.ReadDouble();
            var squaredLength = x * x + y * y + z * z;
            if (!double.IsFinite(squaredLength) || squaredLength > 1.000001)
                throw new ArchiveReadException(ArchiveErrorCode.InvalidData, nameof(NormalizedQuaternionDecoder),
                    archive.Position, archive.Length, 192, "Invalid normalized quaternion components.");
            return DecodedFieldValue.FromObject(new FQuat(x, y, z, Math.Sqrt(Math.Max(0, 1 - squaredLength))));
        }
    }

    private sealed class DoubleVectorDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadDoubleVector(archive));
    }

    private sealed class QuantizedVectorDecoder : IFieldDecoder
    {
        private readonly int _scaleFactor;

        public QuantizedVectorDecoder(int scaleFactor)
        {
            _scaleFactor = scaleFactor;
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadQuantizedVector(archive, _scaleFactor));
    }

    private sealed class FixedNormalVectorDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromVector(ArchiveVectorReaders.ReadFixedVectorNormal(archive));
    }

    private sealed class Int32Decoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromInt32(archive.ReadInt32());
    }

    private sealed class UInt16Decoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromUInt32(archive.ReadUInt16());
    }

    private sealed class UInt32Decoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromUInt32(archive.ReadUInt32());
    }

    private sealed class UInt64Decoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromUInt64(archive.ReadUInt64());
    }

    private sealed class FloatDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromFloat(archive.ReadSingle());
    }

    private sealed class DoubleDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromDouble(archive.ReadDouble());
    }

    private sealed class BoolDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromBool(archive.ReadBit());
    }

    private sealed class ByteDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromByte(archive.ReadByte());
    }

    private sealed class FStringDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromString(archive.ReadFString());
    }

    private sealed class FNameDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromString(archive.ReadFName());
    }

    private sealed class ObjectNetGuidDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var netGuid = archive.ReadIntPacked();
            var path = context.NetGuidCache?.TryGetFullPath(netGuid, out var fullPath) == true ? fullPath : null;
            return DecodedFieldValue.FromObjectReference(new NetObjectReference(netGuid, path));
        }
    }

    private sealed class SerializedIntDecoder : IFieldDecoder
    {
        private readonly int _maxValue;

        public SerializedIntDecoder(int maxValue)
        {
            if (maxValue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, null);
            }

            _maxValue = maxValue;
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromUInt32(archive.ReadSerializedInt(_maxValue));
    }

    private sealed class GuidDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromGuid(archive.ReadGuid());
    }

    private sealed class EnumRemainingBitsDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            if (archive.BitsRemaining == 0)
            {
                return DecodedFieldValue.FromUInt32(0);
            }

            if (archive.BitsRemaining > 32)
            {
                throw new ArchiveReadException(
                    ArchiveErrorCode.InvalidBitCount,
                    nameof(EnumRemainingBitsDecoder),
                    archive.Position,
                    archive.Length,
                    archive.BitsRemaining);
            }

            return DecodedFieldValue.FromUInt32((uint)archive.ReadBitsToUInt64((int)archive.BitsRemaining));
        }
    }

    private sealed class GameplayTagDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var tagIndex = archive.ReadIntPacked();
            string? tagName = null;
            if (context.NetGuidCache?.TryGetGameplayTagName(tagIndex, out var resolvedTagName) == true)
            {
                tagName = resolvedTagName;
            }

            return DecodedFieldValue.FromGameplayTag(new FGameplayTag(tagIndex, tagName));
        }
    }

    private sealed class ReplicatedMovementDecoder : IFieldDecoder
    {
        private readonly ERotatorQuantization _rotationQuantization;

        public ReplicatedMovementDecoder(ERotatorQuantization rotationQuantization)
        {
            _rotationQuantization = rotationQuantization;
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var bSimulatedPhysicsSleep = archive.ReadBit();
            var bRepPhysics = archive.ReadBit();
            var bRepServerFrame = archive.ReadBit();
            var bRepServerHandle = archive.ReadBit();
            var location = VectorNetQuantize100.Decode(ref context, archive).VectorValue;
            var rotation = _rotationQuantization switch
            {
                ERotatorQuantization.ByteComponents => ArchiveVectorReaders.ReadRotationByte(archive),
                ERotatorQuantization.ShortComponents => ArchiveVectorReaders.ReadRotationShort(archive),
                _ => throw new InvalidOperationException(
                    $"Unsupported rotation quantization '{_rotationQuantization}'."),
            };
            var linearVelocity = VectorNetQuantize.Decode(ref context, archive).VectorValue;
            FVector? angularVelocity = null;
            uint serverFrame = 0;
            uint serverPhysicsHandle = 0;

            if (bRepPhysics)
            {
                angularVelocity = VectorNetQuantize.Decode(ref context, archive).VectorValue;
            }

            if (bRepServerFrame)
            {
                serverFrame = archive.ReadIntPacked();
            }

            if (bRepServerHandle)
            {
                serverPhysicsHandle = archive.ReadIntPacked();
            }

            return DecodedFieldValue.FromRepMovement(new FRepMovement(
                linearVelocity,
                angularVelocity,
                location,
                rotation,
                bSimulatedPhysicsSleep,
                bRepPhysics,
                serverFrame,
                serverPhysicsHandle));
        }
    }

    private sealed class ByteArrayDecoder : IFieldDecoder
    {
        private readonly int _maxBytes;

        public ByteArrayDecoder(int maxBytes)
        {
            if (maxBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, null);
            }

            _maxBytes = maxBytes;
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var count = archive.ReadIntPacked();
            if (count > _maxBytes)
            {
                throw new ArchiveReadException(
                    ArchiveErrorCode.InvalidCount,
                    nameof(ByteArrayDecoder),
                    archive.Position,
                    archive.Length,
                    count,
                    $"Byte array field '{context.FieldName ?? "<unknown>"}' declared {count} bytes; maximum is {_maxBytes}.");
            }

            return DecodedFieldValue.FromObject(archive.ReadBytes(checked((int)count)).ToArray());
        }
    }

    private sealed class SkipDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            archive.SkipRemaining();
            return DecodedFieldValue.None;
        }
    }
}
