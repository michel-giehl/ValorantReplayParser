using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Unreal.World;

namespace Replay.Unreal.Tests.Parsing;

public class FieldPayloadParserTests
{
    [Test]
    public void ParseContentPayload_EmptyPayload_DoesNothing()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "Field"),
        ]);

        var payloadData = BuildFieldPayload();
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);

        Assert.That(payload.AtEnd, Is.True);
    }

    [Test]
    public void ParseContentPayload_UnknownHandle_SkipsPayload()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "KnownField"),
        ]);

        var payloadData = BuildFieldPayload(
            (handle: 999u, bitCount: 16, data: new byte[] { 0xFF, 0xFF }));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);

        Assert.That(payload.AtEnd, Is.True);
    }

    [Test]
    public void ParseContentPayload_EnabledInt32_DecodesCorrectly()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "IntField"),
        ]);

        var intValue = BitConverter.GetBytes(42);
        var payloadData = BuildFieldPayload((handle: 0u, bitCount: 32, data: intValue));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);

        Assert.That(payload.AtEnd, Is.True);
    }

    [Test]
    public void ParseContentPayload_DisabledField_SkipsPayload()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, false, null, "DisabledField"),
            (1, true, PrimitiveDecoders.Int32, "IntField"),
        ]);

        var intValue = BitConverter.GetBytes(99);
        var payloadData = BuildFieldPayload(
            (handle: 0u, bitCount: 64, data: new byte[8]),
            (handle: 1u, bitCount: 32, data: intValue));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);

        Assert.That(payload.AtEnd, Is.True);
    }

    [Test]
    public void ParseContentPayload_MultiField_AllDecoded()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "IntField"),
            (1, true, PrimitiveDecoders.Float, "FloatField"),
        ]);

        var floatBytes = BitConverter.GetBytes(3.14f);
        var intBytes = BitConverter.GetBytes(42);
        var payloadData = BuildFieldPayload(
            (handle: 0u, bitCount: 32, data: intBytes),
            (handle: 1u, bitCount: 32, data: floatBytes));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);

        Assert.That(payload.AtEnd, Is.True);
    }

    [Test]
    public void ParseContentPayload_ObjectNetGuid_DecodesCorrectly()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.ObjectNetGuid, "GuidField"),
        ]);

        var guidBytes = EncodeIntPacked(42);
        var payloadData = BuildFieldPayload((handle: 0u, bitCount: guidBytes.Length * 8, data: guidBytes));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);

        Assert.That(payload.AtEnd, Is.True);
    }

    [Test]
    public void ParseClassNetCachePayload_HandleUsesSerializedIntGrammar()
    {
        var boundCache = new BoundClassNetCache
        {
            Path = "/Game/Test.Test_C_ClassNetCache",
            SourceDescriptor = new ClassNetCacheDescriptor("/Game/Test.Test_C_ClassNetCache"),
            Grammar = FieldStreamGrammar.ClassNetCache,
            Enabled = true,
            FunctionsByHandle =
            [
                new BoundRpcFunction
                {
                    Name = "SomeFunction",
                    FunctionExportPath = "/Game/Test.Test_C:SomeFunction",
                    Enabled = true,
                    Decoder = new SkipRpcDecoder(),
                },
            ],
        };

        var bytes = BuildClassNetCachePayload(bitCount: 8, data: [0xFF]);
        var payload = new BitArchiveReader(bytes, bytes.Length * 8);
        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C_ClassNetCache",
        };

        new FieldPayloadParser().ParseClassNetCachePayload(payload, boundCache, ref context);

        Assert.That(payload.AtEnd, Is.True);
    }

    private static BoundExportGroup CreateSimpleBoundGroup(
        string path,
        params (int Handle, bool Enabled, IFieldDecoder? Decoder, string Name)[] fields)
    {
        var maxHandle = fields.Length > 0 ? fields.Max(f => f.Handle) + 1 : 0;
        var bindings = new FieldBinding[maxHandle];
        foreach (var (handle, enabled, decoder, name) in fields)
        {
            bindings[handle] = new FieldBinding
            {
                Enabled = enabled,
                Decoder = decoder,
                Name = name,
            };
        }

        return new BoundExportGroup
        {
            SourceDescriptor = new ExportGroupDescriptor(path),
            Categories = ExportCategory.All,
            Grammar = FieldStreamGrammar.RepLayoutProperties,
            Enabled = true,
            FieldsByHandle = bindings,
        };
    }

    private static FieldPayloadData BuildFieldPayload(params (uint handle, int bitCount, byte[] data)[] fields)
    {
        var bits = new List<bool>();
        bits.Add(false);
        foreach (var (handle, bitCount, data) in fields)
        {
            WriteIntPacked(bits, handle + 1);
            WriteIntPacked(bits, (uint)bitCount);
            WriteBits(bits, data, bitCount);
        }

        WriteIntPacked(bits, 0);

        return new FieldPayloadData(PackBits(bits), bits.Count);
    }

    private static void WriteIntPacked(List<bool> bits, uint value)
    {
        var bytes = EncodeIntPacked(value);
        WriteBits(bits, bytes, bytes.Length * 8);
    }

    private static void WriteBits(List<bool> bits, byte[] data, int bitCount)
    {
        for (var i = 0; i < bitCount; i++)
        {
            bits.Add((data[i >> 3] & (1 << (i & 7))) != 0);
        }
    }

    private static byte[] PackBits(List<bool> bits)
    {
        var bytes = new byte[(bits.Count + 7) / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
            {
                bytes[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        return bytes;
    }

    private readonly record struct FieldPayloadData(byte[] Bytes, int BitCount);

    private static byte[] BuildClassNetCachePayload(int bitCount, byte[] data)
    {
        using var ms = new MemoryStream();
        ms.Write(EncodeIntPacked((uint)bitCount));
        ms.Write(data);
        return ms.ToArray();
    }

    private sealed class SkipRpcDecoder : IRpcDecoder
    {
        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            archive.SkipRemaining();
        }
    }

    private static byte[] EncodeIntPacked(uint value)
    {
        using var ms = new MemoryStream();
        do
        {
            var byteVal = (byte)((value & 0x7F) << 1);
            value >>= 7;
            if (value != 0)
            {
                byteVal |= 1;
            }
            ms.WriteByte(byteVal);
        } while (value != 0);

        return ms.ToArray();
    }
}
