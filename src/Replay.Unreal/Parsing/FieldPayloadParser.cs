using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Events;

namespace Replay.Unreal.Parsing;

public class FieldPayloadParser
{
    public IReadOnlyList<DecodedReplayField> ParseContentPayload(
        FBitArchive payload,
        BoundExportGroup boundGroup,
        ref FieldDecodeContext context) =>
        ParseRepLayoutProperties(payload, boundGroup, ref context);

    public IReadOnlyList<DecodedReplayField> ParseRepLayoutProperties(
        FBitArchive payload,
        BoundExportGroup boundGroup,
        ref FieldDecodeContext context,
        bool readPropertyChecksum = true)
    {
        if (boundGroup.Grammar is FieldStreamGrammar.ClassNetCache)
        {
            GetLogger(context).LogWarning(
                "Export group '{ExportGroupPath}' cannot be parsed with class-net-cache grammar as content payload.",
                context.ExportGroupPath ?? boundGroup.SourceDescriptor.Path);
            payload.SkipRemaining();
            return Array.Empty<DecodedReplayField>();
        }

        if (readPropertyChecksum)
        {
            _ = payload.ReadBit();
        }

        var fields = new List<DecodedReplayField>();
        while (!payload.AtEnd)
        {
            if (ParseProperty(payload, boundGroup, ref context, fields))
            {
                return fields;
            }
        }

        return fields;
    }

    public IReadOnlyList<DecodedRpcInvocation> ParseClassNetCachePayload(
        FBitArchive payload,
        BoundClassNetCache boundCache,
        ref FieldDecodeContext context)
    {
        if (boundCache.Grammar is not FieldStreamGrammar.ClassNetCache)
        {
            GetLogger(context).LogWarning(
                "Class net cache '{BoundCachePath}' has unsupported grammar '{FieldStreamGrammar}'.",
                boundCache.Path,
                boundCache.Grammar);
            payload.SkipRemaining();
            return Array.Empty<DecodedRpcInvocation>();
        }

        if (boundCache.FunctionsByHandle.Length == 0)
        {
            payload.SkipRemaining();
            return Array.Empty<DecodedRpcInvocation>();
        }

        var invocations = new List<DecodedRpcInvocation>();
        while (!payload.AtEnd)
        {
            var handle = (int)payload.ReadSerializedInt(boundCache.FunctionsByHandle.Length);
            var payloadBits = payload.ReadIntPacked();
            if (payloadBits > int.MaxValue || payload.BitsRemaining < payloadBits)
            {
                GetLogger(context).LogWarning(
                    "Malformed class net cache payload: handle={Handle}, bits={PayloadBits}, remaining={PayloadBitsRemaining}",
                    handle,
                    payloadBits,
                    payload.BitsRemaining);
                payload.SkipRemaining();
                return invocations;
            }

            BoundRpcFunction? rpcFunction = null;
            if ((uint)handle < boundCache.FunctionsByHandle.Length)
            {
                rpcFunction = boundCache.FunctionsByHandle[handle];
            }

            var rpcPayload = payload.ReadSubArchive((int)payloadBits);
            if (rpcFunction is null || !rpcFunction.Enabled)
            {
                rpcPayload.SkipRemaining();
                continue;
            }

            context.FieldName = rpcFunction.Name;
            context.Categories = rpcFunction.Categories;

            var beforeRpc = rpcPayload.BitsRemaining;
            IReadOnlyList<DecodedReplayField> fields;
            var wasDecoded = true;

            if (rpcFunction.Decoder is not null)
            {
                fields = rpcFunction.Decoder.Decode(ref context, rpcPayload);
                if (!rpcPayload.AtEnd)
                {
                    rpcPayload.EnsureFullyConsumed($"RPC '{rpcFunction.Name}' (handle {handle})");
                }
            }
            else if (rpcFunction.FunctionGroup is { Enabled: true })
            {
                fields = ParseRepLayoutProperties(
                    rpcPayload,
                    rpcFunction.FunctionGroup,
                    ref context,
                    readPropertyChecksum: rpcFunction.FunctionGroup.Grammar == FieldStreamGrammar.RepLayoutProperties);
            }
            else
            {
                rpcPayload.SkipRemaining();
                fields = Array.Empty<DecodedReplayField>();
                wasDecoded = false;
            }

            invocations.Add(new DecodedRpcInvocation(
                handle,
                rpcFunction.Name,
                rpcFunction.FunctionExportPath,
                rpcFunction.Categories,
                (int)payloadBits,
                checked((int)(beforeRpc - rpcPayload.BitsRemaining)),
                wasDecoded,
                fields));
        }

        return invocations;
    }

    private bool ParseProperty(
        FBitArchive payload,
        BoundExportGroup boundGroup,
        ref FieldDecodeContext context,
        List<DecodedReplayField> fields)
    {
        var encodedHandle = payload.ReadIntPacked();
        if (encodedHandle == 0)
        {
            return true;
        }

        var handle = checked((int)(encodedHandle - 1));
        var payloadBits = payload.ReadIntPacked();
        if (payloadBits > int.MaxValue || payload.BitsRemaining < payloadBits)
        {
            GetLogger(context).LogWarning(
                "Malformed field payload: handle={Handle}, bits={PayloadBits}, remaining={PayloadBitsRemaining}",
                handle,
                payloadBits,
                payload.BitsRemaining);
            payload.SkipRemaining();
            return true;
        }

        var fieldBinding = GetBinding(handle, boundGroup);
        if (!fieldBinding.Enabled || fieldBinding.Decoder is null)
        {
            payload.SkipBits(payloadBits);
            return false;
        }

        context.FieldName = fieldBinding.Name;
        context.Categories = fieldBinding.Categories;

        var fieldPayload = payload.ReadSubArchive((int)payloadBits);
        var decodedValue = fieldBinding.Decoder.Decode(ref context, fieldPayload);
        if (!fieldPayload.AtEnd)
        {
            fieldPayload.EnsureFullyConsumed($"field '{fieldBinding.Name}' (handle {handle})");
        }

        if (decodedValue.HasValue)
        {
            fields.Add(new DecodedReplayField(
                handle,
                fieldBinding.Name,
                fieldBinding.ExportName,
                fieldBinding.Categories,
                decodedValue));
        }

        return false;
    }

    private FieldBinding GetBinding(int handle, BoundExportGroup boundGroup)
    {
        if ((uint)handle < (uint)boundGroup.FieldsByHandle.Length)
        {
            return boundGroup.FieldsByHandle[handle];
        }

        return default;
    }

    private static ILogger<FieldPayloadParser> GetLogger(FieldDecodeContext context) =>
        context.LoggerFactory?.CreateLogger<FieldPayloadParser>() ?? NullLogger<FieldPayloadParser>.Instance;
}

public sealed record DecodedRpcInvocation(
    int Handle,
    string Name,
    string FunctionExportPath,
    ExportCategory Categories,
    int PayloadBits,
    int ParsedBits,
    bool WasDecoded,
    IReadOnlyList<DecodedReplayField> Fields);