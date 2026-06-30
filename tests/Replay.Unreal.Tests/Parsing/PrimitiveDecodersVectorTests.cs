using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Tests.Parsing;

public class PrimitiveDecodersVectorTests
{
    [Test]
    public void Vector_ReadsUnquantizedDoubleVector()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteDouble(1.25);
            writer.WriteDouble(-2.5);
            writer.WriteDouble(3.75);
        });
        var context = new FieldDecodeContext();

        var value = PrimitiveDecoders.Vector.Decode(ref context, archive);

        Assert.Multiple(() =>
        {
            Assert.That(value.Kind, Is.EqualTo(DecodedFieldValueKind.Vector));
            AssertVector(value.VectorValue, 1.25, -2.5, 3.75);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void VectorFloat_ReadsUnquantizedFloatVector()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteSingle(1.25f);
            writer.WriteSingle(-2.5f);
            writer.WriteSingle(3.75f);
        });
        var context = new FieldDecodeContext();

        var value = PrimitiveDecoders.VectorFloat.Decode(ref context, archive);

        Assert.Multiple(() =>
        {
            Assert.That(value.Kind, Is.EqualTo(DecodedFieldValueKind.Vector));
            AssertVector(value.VectorValue, 1.25, -2.5, 3.75);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [TestCase(nameof(PrimitiveDecoders.VectorNetQuantize), 1, 10.0, -2.0, 3.0, 6)]
    [TestCase(nameof(PrimitiveDecoders.VectorNetQuantize10), 10, 1.2, -3.4, 5.6, 7)]
    [TestCase(nameof(PrimitiveDecoders.VectorNetQuantize100), 100, 1.23, -4.56, 7.89, 11)]
    public void QuantizedVector_ReadsPackedVector(
        string decoderName,
        int scaleFactor,
        double x,
        double y,
        double z,
        int componentBitCount)
    {
        var archive = CreateArchive(writer =>
            writer.WriteQuantizedVector(x, y, z, scaleFactor, componentBitCount));
        var context = new FieldDecodeContext();

        var value = GetVectorDecoder(decoderName).Decode(ref context, archive);

        Assert.Multiple(() =>
        {
            Assert.That(value.Kind, Is.EqualTo(DecodedFieldValueKind.Vector));
            AssertVector(value.VectorValue, x, y, z);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void VectorNetQuantizeNormal_ReadsFixedNormalVector()
    {
        var archive = CreateArchive(writer => writer.WriteFixedNormalVector(0.5, -1.0, 1.0));
        var context = new FieldDecodeContext();

        var value = PrimitiveDecoders.VectorNetQuantizeNormal.Decode(ref context, archive);

        Assert.Multiple(() =>
        {
            Assert.That(value.Kind, Is.EqualTo(DecodedFieldValueKind.Vector));
            AssertVector(value.VectorValue, 16384.0 / 32767.0, -1.0, 1.0);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [TestCase(1, 10.0, -2.0, 3.0, 6)]
    [TestCase(10, 1.2, -3.4, 5.6, 7)]
    [TestCase(100, 1.23, -4.56, 7.89, 11)]
    public void ReadQuantizedVector_DecodesPackedScaledComponents(
        int scaleFactor,
        double x,
        double y,
        double z,
        int componentBitCount)
    {
        var archive = CreateArchive(writer =>
            writer.WriteQuantizedVector(x, y, z, scaleFactor, componentBitCount));

        var vector = ArchiveVectorReaders.ReadQuantizedVector(archive, scaleFactor);

        AssertVector(vector, x, y, z);
        Assert.That(vector.Bits, Is.EqualTo(componentBitCount));
        Assert.That(vector.ScaleFactor, Is.EqualTo(scaleFactor));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void ReadFixedVectorNormal_DecodesFixedComponents()
    {
        var archive = CreateArchive(writer => writer.WriteFixedNormalVector(0.5, -1.0, 1.0));

        var vector = ArchiveVectorReaders.ReadFixedVectorNormal(archive);

        AssertVector(vector, 16384.0 / 32767.0, -1.0, 1.0);
        Assert.That(vector.Bits, Is.EqualTo(16));
        Assert.That(vector.ScaleFactor, Is.EqualTo(32767));
        Assert.That(archive.AtEnd, Is.True);
    }

    private static IFieldDecoder GetVectorDecoder(string decoderName) => decoderName switch
    {
        nameof(PrimitiveDecoders.VectorNetQuantize) => PrimitiveDecoders.VectorNetQuantize,
        nameof(PrimitiveDecoders.VectorNetQuantize10) => PrimitiveDecoders.VectorNetQuantize10,
        nameof(PrimitiveDecoders.VectorNetQuantize100) => PrimitiveDecoders.VectorNetQuantize100,
        _ => throw new ArgumentOutOfRangeException(nameof(decoderName), decoderName, null),
    };

    private static BitArchiveReader CreateArchive(Action<BitWriter> write)
    {
        var writer = new BitWriter();
        write(writer);
        return new BitArchiveReader(writer.ToArray(), writer.BitCount);
    }

    private static void AssertVector(FVector vector, double x, double y, double z)
    {
        Assert.Multiple(() =>
        {
            Assert.That(vector.X, Is.EqualTo(x).Within(1e-9));
            Assert.That(vector.Y, Is.EqualTo(y).Within(1e-9));
            Assert.That(vector.Z, Is.EqualTo(z).Within(1e-9));
        });
    }

    private sealed class BitWriter
    {
        private readonly List<bool> _bits = [];

        public int BitCount => _bits.Count;

        public void WriteSerializedInt(uint value, int maxValue)
        {
            uint writtenValue = 0;
            for (uint mask = 1; writtenValue + mask < maxValue; mask <<= 1)
            {
                var bit = (value & mask) != 0;
                _bits.Add(bit);
                if (bit)
                {
                    writtenValue |= mask;
                }
            }
        }

        public void WriteQuantizedVector(
            double x,
            double y,
            double z,
            int scaleFactor,
            int componentBitCount)
        {
            var info = (uint)(componentBitCount | (1 << 6));
            WriteSerializedInt(info, 1 << 7);
            WriteSignedBits(RoundToInt(x * scaleFactor), componentBitCount);
            WriteSignedBits(RoundToInt(y * scaleFactor), componentBitCount);
            WriteSignedBits(RoundToInt(z * scaleFactor), componentBitCount);
        }

        public void WriteFixedNormalVector(double x, double y, double z)
        {
            WriteFixedNormalComponent(x);
            WriteFixedNormalComponent(y);
            WriteFixedNormalComponent(z);
        }

        public void WriteSingle(float value) => WriteUInt32(BitConverter.SingleToUInt32Bits(value));

        public void WriteDouble(double value) => WriteUInt64(BitConverter.DoubleToUInt64Bits(value));

        public byte[] ToArray()
        {
            var bytes = new byte[(_bits.Count + 7) / 8];
            for (var i = 0; i < _bits.Count; i++)
            {
                if (_bits[i])
                {
                    bytes[i >> 3] |= (byte)(1 << (i & 7));
                }
            }

            return bytes;
        }

        private void WriteFixedNormalComponent(double value)
        {
            const int bias = 1 << 15;
            const int maxDelta = (1 << 16) - 1;
            const int scale = bias - 1;

            var delta = RoundToInt(value * scale) + bias;
            delta = Math.Clamp(delta, 0, maxDelta);
            WriteSerializedInt((uint)delta, 1 << 16);
        }

        private void WriteUInt32(uint value)
        {
            foreach (var b in BitConverter.GetBytes(value))
            {
                WriteByte(b);
            }
        }

        private void WriteUInt64(ulong value)
        {
            foreach (var b in BitConverter.GetBytes(value))
            {
                WriteByte(b);
            }
        }

        private void WriteByte(byte value)
        {
            for (var i = 0; i < 8; i++)
            {
                _bits.Add((value & (1 << i)) != 0);
            }
        }

        private void WriteSignedBits(long value, int bitCount)
        {
            var mask = bitCount == 64 ? ulong.MaxValue : (1UL << bitCount) - 1;
            WriteBits(bitCount, (ulong)value & mask);
        }

        private void WriteBits(int count, ulong value)
        {
            for (var i = 0; i < count; i++)
            {
                _bits.Add((value & (1UL << i)) != 0);
            }
        }

        private static int RoundToInt(double value) =>
            (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
