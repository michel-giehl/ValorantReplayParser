using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Replay;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Tests.Parsing;

public class RepLayoutArrayDecodersTests
{
    [Test]
    public void DynamicArray_DecodesStructuredElementsByHandle()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(1);
            writer.WriteIntPacked(1);
            WriteField(writer, 2, field => field.WriteSingle(1.25f));
            writer.WriteIntPacked(0);
            writer.WriteIntPacked(0);
        });
        var context = new FieldDecodeContext();

        var value = RepLayoutArrayDecoders.DynamicArray<TestArrayElement>().Decode(ref context, archive);
        var elements = (TestArrayElement[])value.ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(value.Kind, Is.EqualTo(DecodedFieldValueKind.Object));
            Assert.That(elements, Has.Length.EqualTo(1));
            Assert.That(elements[0].FloatValue, Is.EqualTo(1.25f));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void DynamicArray_EmptyPayloadDecodesEmptyArray()
    {
        var archive = new BitArchiveReader(ReadOnlyMemory<byte>.Empty, bitCount: 0);
        var context = new FieldDecodeContext();

        var value = RepLayoutArrayDecoders.DynamicArray<TestArrayElement>().Decode(ref context, archive);
        var elements = (TestArrayElement[])value.ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(value.Kind, Is.EqualTo(DecodedFieldValueKind.Object));
            Assert.That(elements, Is.Empty);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void DynamicArray_DecodesPrimitiveElementsByIndex()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(3);
            writer.WriteIntPacked(1);
            writer.WriteByte(0x12);
            writer.WriteIntPacked(3);
            writer.WriteByte(0xA5);
            writer.WriteIntPacked(0);
        });
        var context = new FieldDecodeContext();

        var value = RepLayoutArrayDecoders.DynamicArray<byte>(PrimitiveDecoders.Byte)
            .Decode(ref context, archive);
        var elements = (byte[])value.ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(value.Kind, Is.EqualTo(DecodedFieldValueKind.Object));
            Assert.That(elements, Is.EqualTo(new byte[] { 0x12, 0, 0xA5 }));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void DynamicArray_RejectsElementCountAboveInt32Maximum()
    {
        var archive = CreateArchive(writer => writer.WriteIntPacked(uint.MaxValue));
        var context = new FieldDecodeContext();

        var exception = Assert.Throws<ArchiveReadException>(() =>
            RepLayoutArrayDecoders.DynamicArray<byte>(PrimitiveDecoders.Byte)
                .Decode(ref context, archive));

        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.InvalidCount));
    }

    [Test]
    public void DynamicArray_RejectsIncompatiblePrimitiveDecoderValue()
    {
        var archive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(1);
            writer.WriteIntPacked(1);
        });
        var context = new FieldDecodeContext();

        Assert.Throws<InvalidOperationException>(() =>
            RepLayoutArrayDecoders.DynamicArray<byte>(IncompatibleDecoder.Instance)
                .Decode(ref context, archive));
    }

    [Test]
    public void DynamicArray_ResolvesVersionedElementDescriptorForEachDecodeSession()
    {
        var descriptors = new VersionedDefinition<TestArrayElement>(new TestArrayElement(handle: 2))
            .From(new ReplayReleaseVersion(13, 5), new TestArrayElement(handle: 3));
        var decoder = RepLayoutArrayDecoders.DynamicArray(descriptors);
        var currentArchive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(1);
            writer.WriteIntPacked(1);
            WriteField(writer, 3, field => field.WriteSingle(2.5f));
            writer.WriteIntPacked(0);
            writer.WriteIntPacked(0);
        });
        var legacyArchive = CreateArchive(writer =>
        {
            writer.WriteIntPacked(1);
            writer.WriteIntPacked(1);
            WriteField(writer, 2, field => field.WriteSingle(1.5f));
            writer.WriteIntPacked(0);
            writer.WriteIntPacked(0);
        });
        var currentContext = new FieldDecodeContext
        {
            ReplayReleaseVersion = new ReplayReleaseVersion(13, 5),
        };
        var legacyContext = new FieldDecodeContext
        {
            ReplayReleaseVersion = new ReplayReleaseVersion(13, 1),
        };

        var current = (TestArrayElement[])decoder.Decode(ref currentContext, currentArchive).ObjectValue!;
        var legacy = (TestArrayElement[])decoder.Decode(ref legacyContext, legacyArchive).ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(current.Single().FloatValue, Is.EqualTo(2.5f));
            Assert.That(legacy.Single().FloatValue, Is.EqualTo(1.5f));
        });
    }

    private static void WriteField(BitWriter writer, uint handle, Action<BitWriter> writePayload)
    {
        var payload = new BitWriter();
        writePayload(payload);

        writer.WriteIntPacked(handle + 1);
        writer.WriteIntPacked((uint)payload.BitCount);
        writer.WriteBits(payload.ToArray(), payload.BitCount);
    }

    private static BitArchiveReader CreateArchive(Action<BitWriter> write)
    {
        var writer = new BitWriter();
        write(writer);
        return new BitArchiveReader(writer.ToArray(), writer.BitCount);
    }

    private sealed class TestArrayElement : ExportGroupDescriptor<TestArrayElement>
    {
        private readonly uint _handle;

        public TestArrayElement()
            : this(2)
        {
        }

        public TestArrayElement(uint handle)
        {
            _handle = handle;
        }

        public float? FloatValue { get; set; }

        protected override void Configure()
        {
            AddPropertyHandle(_handle, x => x.FloatValue).Float();
        }
    }

    private sealed class IncompatibleDecoder : IFieldDecoder
    {
        public static readonly IncompatibleDecoder Instance = new();

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedFieldValue.FromObject("not a byte");
    }

    private sealed class BitWriter
    {
        private readonly List<bool> _bits = [];

        public int BitCount => _bits.Count;

        public void WriteByte(byte value)
        {
            for (var i = 0; i < 8; i++)
            {
                _bits.Add((value & (1 << i)) != 0);
            }
        }

        public void WriteBits(byte[] bytes, int bitCount)
        {
            for (var i = 0; i < bitCount; i++)
            {
                _bits.Add((bytes[i >> 3] & (1 << (i & 7))) != 0);
            }
        }

        public void WriteSingle(float value)
        {
            foreach (var b in BitConverter.GetBytes(value))
            {
                WriteByte(b);
            }
        }

        public void WriteIntPacked(uint value)
        {
            do
            {
                var byteVal = (byte)((value & 0x7F) << 1);
                value >>= 7;
                if (value != 0)
                {
                    byteVal |= 1;
                }

                WriteByte(byteVal);
            } while (value != 0);
        }

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
    }
}
