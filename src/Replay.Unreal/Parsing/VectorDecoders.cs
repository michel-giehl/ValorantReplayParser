using Replay.Encoding.Archives;
using Replay.Models.Unreal;

namespace Replay.Unreal.Parsing;

public static class VectorDecoders
{
    public static readonly IFieldDecoder Vector100 = new QuantizedVectorDecoder(scaleFactor: 10);
    public static readonly IFieldDecoder Vector1 = new QuantizedVectorDecoder(scaleFactor: 1);
    public static readonly IFieldDecoder VectorDouble = new DoubleVectorDecoder();
    public static readonly IFieldDecoder VectorFloat = new FloatVectorDecoder();
    public static readonly IFieldDecoder RotationShort = new ShortRotationDecoder();

    private sealed class QuantizedVectorDecoder : IFieldDecoder
    {
        private readonly int _scaleFactor;

        public QuantizedVectorDecoder(int scaleFactor)
        {
            _scaleFactor = scaleFactor;
        }

        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var shouldQuantize = archive.ReadBit();
            FVector vector;
            if (shouldQuantize)
            {
                var componentBitCountAndExtraInfo = archive.ReadSerializedInt(1 << 7);
                var componentBitCount = (int)(componentBitCountAndExtraInfo & 63U);
                var extraInfo = componentBitCountAndExtraInfo >> 6;

                if (componentBitCount > 0)
                {
                    var x = archive.ReadBitsToUInt64(componentBitCount);
                    var y = archive.ReadBitsToUInt64(componentBitCount);
                    var z = archive.ReadBitsToUInt64(componentBitCount);

                    var signBit = 1UL << componentBitCount - 1;
                    var fX = (long)(x ^ signBit) - (long)signBit;
                    var fY = (long)(y ^ signBit) - (long)signBit;
                    var fZ = (long)(z ^ signBit) - (long)signBit;

                    if (extraInfo <= 0)
                    {
                        vector = new FVector(fX, fY, fZ)
                        {
                            Bits = componentBitCount,
                            ScaleFactor = _scaleFactor,
                        };
                    }
                    else
                    {
                        vector = new FVector(fX / (double)_scaleFactor, fY / (double)_scaleFactor, fZ / (double)_scaleFactor)
                        {
                            Bits = componentBitCount,
                            ScaleFactor = _scaleFactor,
                        };
                    }
                }
                else if (extraInfo == 0)
                {
                    vector = new FVector(archive.ReadSingle(), archive.ReadSingle(), archive.ReadSingle())
                    {
                        Bits = 32,
                        ScaleFactor = _scaleFactor,
                    };
                }
                else
                {
                    vector = new FVector(archive.ReadDouble(), archive.ReadDouble(), archive.ReadDouble())
                    {
                        Bits = 64,
                        ScaleFactor = _scaleFactor,
                    };
                }
            }
            else
            {
                vector = new FVector(archive.ReadDouble(), archive.ReadDouble(), archive.ReadDouble())
                {
                    Bits = 64,
                };
            }

            if (context.WorldState is null || !context.ActorNetGuid.IsValid) return;
            var actor = context.WorldState.GetActor(context.ActorNetGuid.Value);
            if (actor is not null)
            {
                actor.Location = vector;
            }
        }
    }

    private sealed class DoubleVectorDecoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var vector = new FVector(archive.ReadDouble(), archive.ReadDouble(), archive.ReadDouble())
            {
                Bits = 64,
            };

            if (context.WorldState is null || !context.ActorNetGuid.IsValid) return;
            var actor = context.WorldState.GetActor(context.ActorNetGuid.Value);
            if (actor is not null)
            {
                actor.Location = vector;
            }
        }
    }

    private sealed class FloatVectorDecoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var vector = new FVector(archive.ReadSingle(), archive.ReadSingle(), archive.ReadSingle())
            {
                Bits = 32,
            };

            if (context.WorldState is null || !context.ActorNetGuid.IsValid) return;
            var actor = context.WorldState.GetActor(context.ActorNetGuid.Value);
            if (actor is not null)
            {
                actor.Location = vector;
            }
        }
    }

    private sealed class ShortRotationDecoder : IFieldDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var pitch = ReadCompressedComponent(archive);
            var yaw = ReadCompressedComponent(archive);
            var roll = ReadCompressedComponent(archive);
            var rotation = new FRotator(pitch, yaw, roll);

            if (context.WorldState is null || !context.ActorNetGuid.IsValid) return;
            var actor = context.WorldState.GetActor(context.ActorNetGuid.Value);
            if (actor is not null)
            {
                actor.Rotation = rotation;
            }
        }

        private static float ReadCompressedComponent(FBitArchive archive)
        {
            if (!archive.ReadBit()) return 0.0f;
            const float scale = 360.0f / 65536.0f;
            return archive.ReadUInt16() * scale;
        }
    }
}
