using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Models.Events;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;

namespace Replay.Valorant.Descriptors;

internal static class ValorantPayloadDecoders
{
    public const int MaxInputEventBytes = 64 * 1024;

    public static readonly IRpcDecoder NoParametersRpc = new NoParametersRpcDecoder();
    public static readonly IRpcDecoder SkipPayloadRpc = new SkipPayloadRpcDecoder();
    public static readonly IFieldDecoder Equippable = new EquippableDecoder();

    public static IRpcDecoder RawRpc(string typeName) => new RawRpcDecoder(typeName);

    public static IFieldDecoder RawPayload(string typeName) => new RawPayloadDecoder(typeName);

    public static IFieldDecoder CapturedPayload(string typeName) =>
        new CapturedPayloadDecoder(typeName);

    public static IFieldDecoder PrimitiveOrRaw(IFieldDecoder primitiveDecoder, string typeName) =>
        new PrimitiveOrRawDecoder(primitiveDecoder, typeName);

    private sealed class PrimitiveOrRawDecoder : IFieldDecoder
    {
        private readonly IFieldDecoder _primitiveDecoder;
        private readonly RawPayloadDecoder _rawDecoder;

        public PrimitiveOrRawDecoder(IFieldDecoder primitiveDecoder, string typeName)
        {
            _primitiveDecoder = primitiveDecoder;
            _rawDecoder = new RawPayloadDecoder(typeName);
        }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            string? fallbackReason = null;
            using (var checkpoint = archive.CreateCheckpoint())
            {
                try
                {
                    var value = _primitiveDecoder.Decode(ref context, archive);
                    if (archive.AtEnd)
                    {
                        checkpoint.Commit();
                        return value;
                    }

                    fallbackReason = $"Typed decoder left {archive.BitsRemaining} bits unread.";
                }
                catch (ArchiveReadException exception)
                {
                    fallbackReason = exception.Message;
                }
                catch (OverflowException exception)
                {
                    fallbackReason = exception.Message;
                }
            }

            context.Diagnostics?.Add(new ReplayDiagnostic(
                ReplayDiagnosticCode.RawPayloadFallback,
                $"Field '{context.FieldName}' fell back to raw payload: {fallbackReason}",
                context.CurrentPacketId,
                context.ChannelIndex,
                context.CurrentTimeSeconds,
                context.ExportGroupPath,
                context.FieldName));
            return _rawDecoder.Decode(ref context, archive);
        }
    }

    private sealed class RawPayloadDecoder(string typeName) : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var bitCount = checked((int)archive.BitsRemaining);
            var data = archive.ReadBits(bitCount);

            return DecodedFieldValue.FromObject(new ValorantRawPayload(typeName, bitCount, data));
        }
    }

    private sealed class CapturedPayloadDecoder(string typeName) : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var bitCount = checked((int)archive.BitsRemaining);
            var bytes = new byte[(bitCount + 7) / 8];
            for (var bit = 0; bit < bitCount; bit++)
            {
                if (archive.ReadBit())
                {
                    bytes[bit / 8] |= (byte)(1 << (bit % 8));
                }
            }

            return DecodedFieldValue.FromObject(new ValorantCapturedPayload(
                typeName,
                bitCount,
                Convert.ToBase64String(bytes)));
        }
    }

    private sealed class NoParametersRpcDecoder : IRpcDecoder
    {
        public DecodedPayloadResult Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            archive.SkipRemaining();
            return DecodedPayloadResult.Empty;
        }
    }

    private sealed class RawRpcDecoder : IRpcDecoder
    {
        private readonly string _typeName;

        public RawRpcDecoder(string typeName)
        {
            _typeName = typeName;
        }

        public DecodedPayloadResult Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var bitCount = checked((int)archive.BitsRemaining);
            archive.SkipRemaining();
            var payload = new ValorantRawPayload(_typeName, bitCount);
            var diagnostics = context.CaptureDiagnosticFields
                ? new[]
                {
                    new DecodedReplayField(
                    Handle: -1,
                    Name: "Payload",
                    ExportName: null,
                    context.Categories,
                    DecodedFieldValue.FromObject(payload)),
                }
                : [];

            return new DecodedPayloadResult(payload, DecodedFieldCount: 1, diagnostics);
        }
    }

    private sealed class SkipPayloadRpcDecoder : IRpcDecoder
    {
        public DecodedPayloadResult Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            archive.SkipBits(checked((int)archive.BitsRemaining));
            return DecodedPayloadResult.Empty;
        }
    }

    private sealed class EquippableDecoder : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var netGuid = archive.ReadIntPacked();
            return DecodedFieldValue.FromObject(ValorantEquippableResolver.Resolve(netGuid, context.NetGuidCache));
        }
    }
}
public sealed record ValorantCapturedPayload(
    string TypeName,
    int BitCount,
    string BitsBase64);
