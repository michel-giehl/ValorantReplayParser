using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Tests.Parsing;

public class FieldPayloadParserTests
{
    [Test]
    public void ParseContentPayload_EmptyPayload_ReturnsNoFields()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "Field"),
        ]);

        var payloadData = BuildFieldPayload();
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = CreateContext("/Game/Test.Test_C");

        var result = new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);

        Assert.Multiple(() =>
        {
            Assert.That(result.DecodedFieldCount, Is.EqualTo(0));
            Assert.That(result.DiagnosticFields, Is.Empty);
            Assert.That(result.Payload, Is.TypeOf<TestPayloadDescriptor>());
            Assert.That(payload.AtEnd, Is.True);
        });
    }

    [Test]
    public void ParseContentPayload_UnknownHandle_SkipsPayload()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "KnownField"),
        ]);

        var payloadData = BuildFieldPayload(
            (handle: 999u, bitCount: 16, data: [0xFF, 0xFF]));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = CreateContext("/Game/Test.Test_C");

        var result = new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);

        Assert.Multiple(() =>
        {
            Assert.That(result.DecodedFieldCount, Is.EqualTo(0));
            Assert.That(result.DiagnosticFields, Is.Empty);
            Assert.That(payload.AtEnd, Is.True);
        });
    }

    [Test]
    public void ParseContentPayload_ZeroBitField_DoesNotInvokeDecoder()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "KnownField"),
            (1, true, PrimitiveDecoders.Int32, "IntField"),
        ]);
        var payloadData = BuildFieldPayload(
            (handle: 0u, bitCount: 0, data: []),
            (handle: 1u, bitCount: 32, data: BitConverter.GetBytes(42)));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = CreateContext("/Game/Test.Test_C");

        var result = new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);
        var typedPayload = (TestPayloadDescriptor)result.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(result.DecodedFieldCount, Is.EqualTo(1));
            Assert.That(typedPayload.KnownField, Is.Zero);
            Assert.That(typedPayload.HasDecoded(nameof(TestPayloadDescriptor.KnownField)), Is.False);
            Assert.That(typedPayload.IntField, Is.EqualTo(42));
            Assert.That(payload.AtEnd, Is.True);
        });
    }

    [Test]
    public void ParseContentPayload_EnabledInt32_ReturnsDecodedField()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.Int32, "IntField"),
        ]);

        var intValue = BitConverter.GetBytes(42);
        var payloadData = BuildFieldPayload((handle: 0u, bitCount: 32, data: intValue));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = CreateContext("/Game/Test.Test_C");

        var result = new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);
        var typedPayload = (TestPayloadDescriptor)result.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(result.DecodedFieldCount, Is.EqualTo(1));
            Assert.That(result.DiagnosticFields, Has.Count.EqualTo(1));
            Assert.That(result.DiagnosticFields[0].Handle, Is.EqualTo(0));
            Assert.That(result.DiagnosticFields[0].Name, Is.EqualTo("IntField"));
            Assert.That(result.DiagnosticFields[0].Value.Kind, Is.EqualTo(DecodedFieldValueKind.Int32));
            Assert.That(result.DiagnosticFields[0].Value.Int32Value, Is.EqualTo(42));
            Assert.That(typedPayload.IntField, Is.EqualTo(42));
            Assert.That(typedPayload.HasDecoded(nameof(TestPayloadDescriptor.IntField)), Is.True);
            Assert.That(payload.AtEnd, Is.True);
        });
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
        var context = CreateContext("/Game/Test.Test_C");

        var result = new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);
        var typedPayload = (TestPayloadDescriptor)result.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(result.DecodedFieldCount, Is.EqualTo(1));
            Assert.That(result.DiagnosticFields, Has.Count.EqualTo(1));
            Assert.That(result.DiagnosticFields[0].Name, Is.EqualTo("IntField"));
            Assert.That(result.DiagnosticFields[0].Value.Int32Value, Is.EqualTo(99));
            Assert.That(typedPayload.IntField, Is.EqualTo(99));
            Assert.That(payload.AtEnd, Is.True);
        });
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
        var context = CreateContext("/Game/Test.Test_C");

        var result = new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);
        var typedPayload = (TestPayloadDescriptor)result.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(result.DecodedFieldCount, Is.EqualTo(2));
            Assert.That(result.DiagnosticFields, Has.Count.EqualTo(2));
            Assert.That(result.DiagnosticFields[0].Value.Int32Value, Is.EqualTo(42));
            Assert.That(result.DiagnosticFields[1].Value.Kind, Is.EqualTo(DecodedFieldValueKind.Float));
            Assert.That(result.DiagnosticFields[1].Value.FloatValue, Is.EqualTo(3.14f));
            Assert.That(typedPayload.IntField, Is.EqualTo(42));
            Assert.That(typedPayload.FloatField, Is.EqualTo(3.14f));
            Assert.That(payload.AtEnd, Is.True);
        });
    }

    [Test]
    public void ParseContentPayload_ObjectNetGuid_ReturnsDecodedGuidField()
    {
        var boundGroup = CreateSimpleBoundGroup("/Game/Test.Test_C", fields:
        [
            (0, true, PrimitiveDecoders.ObjectNetGuid, "GuidField"),
        ]);

        var guidBytes = EncodeIntPacked(42);
        var payloadData = BuildFieldPayload((handle: 0u, bitCount: guidBytes.Length * 8, data: guidBytes));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = CreateContext("/Game/Test.Test_C");

        var result = new FieldPayloadParser().ParseContentPayload(payload, boundGroup, ref context);
        var typedPayload = (TestPayloadDescriptor)result.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(result.DecodedFieldCount, Is.EqualTo(1));
            Assert.That(result.DiagnosticFields, Has.Count.EqualTo(1));
            Assert.That(result.DiagnosticFields[0].Value.Kind, Is.EqualTo(DecodedFieldValueKind.NetGuid));
            Assert.That(result.DiagnosticFields[0].Value.NetGuidValue, Is.EqualTo(42));
            Assert.That(typedPayload.GuidField, Is.EqualTo(42));
            Assert.That(payload.AtEnd, Is.True);
        });
    }

    [Test]
    public void ParseRepLayoutProperties_FunctionParameters_ConsumesZeroTerminatorBit()
    {
        var boundGroup = CreateSimpleBoundGroup(
            "/Game/Test.Test_C:SomeFunction",
            FieldStreamGrammar.FunctionParameters,
            (0, true, PrimitiveDecoders.Int32, "IntField"));
        var payloadData = BuildFunctionParameterPayload(
            terminator: false,
            (handle: 0u, bitCount: 32, data: BitConverter.GetBytes(42)));
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = CreateContext("/Game/Test.Test_C:SomeFunction");

        var result = new FieldPayloadParser().ParseRepLayoutProperties(
            payload,
            boundGroup,
            ref context,
            readPropertyChecksum: false);
        var typedPayload = (TestPayloadDescriptor)result.Payload!;

        Assert.Multiple(() =>
        {
            Assert.That(result.DecodedFieldCount, Is.EqualTo(1));
            Assert.That(typedPayload.IntField, Is.EqualTo(42));
            Assert.That(payload.AtEnd, Is.True);
        });
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

        var payloadData = BuildClassNetCachePayload(handle: 0, functionCount: 1, bitCount: 8, data: [0xFF]);
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = CreateContext("/Game/Test.Test_C_ClassNetCache");

        var invocations = new FieldPayloadParser().ParseClassNetCachePayload(payload, boundCache, ref context);

        Assert.Multiple(() =>
        {
            Assert.That(invocations, Has.Count.EqualTo(1));
            Assert.That(invocations[0].Handle, Is.EqualTo(0));
            Assert.That(invocations[0].Name, Is.EqualTo("SomeFunction"));
            Assert.That(payload.AtEnd, Is.True);
        });
    }

    [Test]
    public void ParseClassNetCachePayload_ConsumesShortTrailingTerminator()
    {
        var functions = new BoundRpcFunction[19];
        functions[4] = new BoundRpcFunction
        {
            Name = "SomeFunction",
            FunctionExportPath = "/Game/Test.Test_C:SomeFunction",
            Enabled = true,
            Decoder = new SkipRpcDecoder(),
        };
        var boundCache = new BoundClassNetCache
        {
            Path = "/Game/Test.Test_C_ClassNetCache",
            SourceDescriptor = new ClassNetCacheDescriptor("/Game/Test.Test_C_ClassNetCache"),
            Grammar = FieldStreamGrammar.ClassNetCache,
            Enabled = true,
            FunctionsByHandle = functions,
        };
        var bits = new List<bool>();
        WriteSerializedInt(bits, value: 4, maxValue: functions.Length);
        WriteIntPacked(bits, 1);
        bits.Add(false);
        bits.AddRange([false, false, false, false, false]);
        var payload = new BitArchiveReader(PackBits(bits), bits.Count);
        var context = CreateContext(boundCache.Path);

        var invocations = new FieldPayloadParser().ParseClassNetCachePayload(payload, boundCache, ref context);

        Assert.Multiple(() =>
        {
            Assert.That(invocations, Has.Count.EqualTo(1));
            Assert.That(invocations[0].Handle, Is.EqualTo(4));
            Assert.That(payload.AtEnd, Is.True);
        });
    }

    [Test]
    public void ParseClassNetCachePayload_OversizedRpcLength_ThrowsContextualInvalidReplayData()
    {
        var boundCache = CreateSingleFunctionBoundCache(new SkipRpcDecoder());
        var payloadData = BuildClassNetCachePayload(handle: 0, functionCount: 1, bitCount: 16, data: [0xFF], dataBitCount: 8);
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = new FieldDecodeContext
        {
            ExportGroupPath = "/Game/Test.Test_C_ClassNetCache",
            CurrentPacketId = 42,
            ChannelIndex = 7,
        };

        var exception = Assert.Throws<InvalidReplayDataException>(() =>
            new FieldPayloadParser().ParseClassNetCachePayload(payload, boundCache, ref context));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.InnerException, Is.TypeOf<ArchiveReadException>());
            Assert.That(((ArchiveReadException)exception.InnerException!).ErrorCode,
                Is.EqualTo(ArchiveErrorCode.InvalidBitCount));
            Assert.That(exception.Message, Does.Contain("SomeFunction"));
            Assert.That(exception.Message, Does.Contain("packet 42"));
            Assert.That(exception.Message, Does.Contain("channel 7"));
        });
    }

    [Test]
    public void ParseClassNetCachePayload_RequiredDecoderUnderRead_Throws()
    {
        var boundCache = CreateSingleFunctionBoundCache(new UnderReadingRpcDecoder());
        var payloadData = BuildClassNetCachePayload(handle: 0, functionCount: 1, bitCount: 8, data: [0xFF]);
        var payload = new BitArchiveReader(payloadData.Bytes, payloadData.BitCount);
        var context = CreateContext("/Game/Test.Test_C_ClassNetCache");

        var exception = Assert.Throws<InvalidReplayDataException>(() =>
            new FieldPayloadParser().ParseClassNetCachePayload(payload, boundCache, ref context));

        Assert.That(exception!.InnerException, Is.TypeOf<ArchiveReadException>());
        Assert.That(((ArchiveReadException)exception.InnerException!).ErrorCode,
            Is.EqualTo(ArchiveErrorCode.UnexpectedTrailingData));
    }

    private static FieldDecodeContext CreateContext(string path) => new()
    {
        ExportGroupPath = path,
    };

    private static BoundExportGroup CreateSimpleBoundGroup(
        string path,
        FieldStreamGrammar grammar = FieldStreamGrammar.RepLayoutProperties,
        params (int Handle, bool Enabled, IFieldDecoder? Decoder, string Name)[] fields)
    {
        var maxHandle = fields.Length > 0 ? fields.Max(f => f.Handle) + 1 : 0;
        var bindings = new FieldBinding[maxHandle];
        foreach (var (handle, enabled, decoder, name) in fields)
        {
            bindings[handle] = new FieldBinding
            {
                Enabled = enabled,
                Categories = ExportCategory.Debug,
                Decoder = decoder,
                Name = name,
                ExportName = name,
                TargetProperty = typeof(TestPayloadDescriptor).GetProperty(name),
            };
        }

        return new BoundExportGroup
        {
            SourceDescriptor = new TestPayloadDescriptor(path),
            Categories = ExportCategory.All,
            Grammar = grammar,
            Enabled = true,
            CaptureDiagnosticFields = true,
            FieldsByHandle = bindings,
        };
    }

    private static BoundClassNetCache CreateSingleFunctionBoundCache(IRpcDecoder decoder) => new()
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
                Decoder = decoder,
            },
        ],
    };

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

    private static FieldPayloadData BuildFunctionParameterPayload(
        bool terminator,
        params (uint handle, int bitCount, byte[] data)[] fields)
    {
        var bits = new List<bool>();
        foreach (var (handle, bitCount, data) in fields)
        {
            WriteIntPacked(bits, handle + 1);
            WriteIntPacked(bits, (uint)bitCount);
            WriteBits(bits, data, bitCount);
        }

        bits.Add(terminator);
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

    private static FieldPayloadData BuildClassNetCachePayload(
        uint handle,
        int functionCount,
        int bitCount,
        byte[] data,
        int? dataBitCount = null)
    {
        var bits = new List<bool>();
        WriteSerializedInt(bits, handle, Math.Max(functionCount, 2));
        WriteIntPacked(bits, (uint)bitCount);
        WriteBits(bits, data, dataBitCount ?? bitCount);
        return new FieldPayloadData(PackBits(bits), bits.Count);
    }

    private static void WriteSerializedInt(List<bool> bits, uint value, int maxValue)
    {
        var valueBitCount = System.Numerics.BitOperations.Log2((uint)maxValue);
        for (var bit = 0; bit < valueBitCount; bit++)
        {
            bits.Add((value & (1u << bit)) != 0);
        }

        var mask = 1u << valueBitCount;
        if (value + mask < maxValue)
        {
            bits.Add((value & mask) != 0);
        }
    }

    private sealed class SkipRpcDecoder : IRpcDecoder
    {
        public DecodedPayloadResult Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            archive.SkipRemaining();
            return DecodedPayloadResult.Empty;
        }
    }

    private sealed class UnderReadingRpcDecoder : IRpcDecoder
    {
        public DecodedPayloadResult Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedPayloadResult.Empty;
    }

    private sealed class TestPayloadDescriptor : ExportGroupDescriptor<TestPayloadDescriptor>
    {
        private readonly string _path;

        public TestPayloadDescriptor()
            : this("/Game/Test.Test_C")
        {
        }

        public TestPayloadDescriptor(string path)
        {
            _path = path;
        }

        public override string Path => _path;

        public int KnownField { get; set; }
        public int IntField { get; set; }
        public float FloatField { get; set; }
        public uint GuidField { get; set; }

        protected override void Configure()
        {
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
