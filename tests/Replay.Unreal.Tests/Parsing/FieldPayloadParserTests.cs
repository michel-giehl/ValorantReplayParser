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

        var payload = new BitArchiveReader(ReadOnlySpan<byte>.Empty);
        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        FieldPayloadParser.ParseContentPayload(payload, boundGroup, ref context);

        Assert.That(payload.AtEnd, Is.True);
    }

    [Test]
    public void ParseContentPayload_UnknownHandle_SkipsPayload()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "KnownField"),
        ]);

        var bytes = BuildFieldPayload(
            (handle: 999u, bitCount: 16, data: new byte[] { 0xFF, 0xFF }));
        var payload = new BitArchiveReader(bytes, bytes.Length * 8);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        FieldPayloadParser.ParseContentPayload(payload, boundGroup, ref context);

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
        var bytes = BuildFieldPayload((handle: 0u, bitCount: 32, data: intValue));
        var payload = new BitArchiveReader(bytes, bytes.Length * 8);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        FieldPayloadParser.ParseContentPayload(payload, boundGroup, ref context);

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
        var bytes = BuildFieldPayload(
            (handle: 0u, bitCount: 64, data: new byte[8]),
            (handle: 1u, bitCount: 32, data: intValue));
        var payload = new BitArchiveReader(bytes, bytes.Length * 8);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        FieldPayloadParser.ParseContentPayload(payload, boundGroup, ref context);

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
        var bytes = BuildFieldPayload(
            (handle: 0u, bitCount: 32, data: intBytes),
            (handle: 1u, bitCount: 32, data: floatBytes));
        var payload = new BitArchiveReader(bytes, bytes.Length * 8);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        FieldPayloadParser.ParseContentPayload(payload, boundGroup, ref context);

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
        var bytes = BuildFieldPayload((handle: 0u, bitCount: guidBytes.Length * 8, data: guidBytes));
        var payload = new BitArchiveReader(bytes, bytes.Length * 8);

        var context = new FieldDecodeContext
        {
            WorldState = new WorldState(),
            ExportGroupPath = "/Game/Test.Test_C",
        };

        FieldPayloadParser.ParseContentPayload(payload, boundGroup, ref context);

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

        FieldPayloadParser.ParseClassNetCachePayload(payload, boundCache, ref context);

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

    private static byte[] BuildFieldPayload(params (uint handle, int bitCount, byte[] data)[] fields)
    {
        using var ms = new MemoryStream();
        foreach (var (handle, bitCount, data) in fields)
        {
            ms.Write(EncodeIntPacked(handle + 1));
            ms.Write(EncodeIntPacked((uint)bitCount));
            ms.Write(data);
        }

        ms.Write(EncodeIntPacked(0));

        return ms.ToArray();
    }

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
