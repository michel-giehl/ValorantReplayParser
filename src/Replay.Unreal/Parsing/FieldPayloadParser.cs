using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

public class FieldPayloadParser
{
    public void ParseContentPayload(
        FBitArchive payload,
        BoundExportGroup boundGroup,
        ref FieldDecodeContext context) =>
        ParseRepLayoutProperties(payload, boundGroup, ref context);

    public void ParseRepLayoutProperties(
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
            return;
        }

        if (readPropertyChecksum)
        {
            _ = payload.ReadBit();
        }

        while (!payload.AtEnd)
        {
            if (ParseProperty(payload, boundGroup, ref context))
            {
                return; // done
            }
        }
    }

    private bool ParseProperty(FBitArchive payload, BoundExportGroup boundGroup, ref FieldDecodeContext context)
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
        fieldBinding.Decoder.Decode(ref context, fieldPayload);
        if (!fieldPayload.AtEnd)
        {
            fieldPayload.EnsureFullyConsumed($"field '{fieldBinding.Name}' (handle {handle})");
        }

        return false;
    }

    public void ParseClassNetCachePayload(
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
            return;
        }

        if (boundCache.FunctionsByHandle.Length == 0)
        {
            payload.SkipRemaining();
            return;
        }

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
                return;
            }

            BoundRpcFunction? rpcFunction = null;
            if ((uint)handle < boundCache.FunctionsByHandle.Length)
            {
                rpcFunction = boundCache.FunctionsByHandle[handle];
            }

            if (rpcFunction is null || !rpcFunction.Enabled)
            {
                payload.SkipBits(payloadBits);
                continue;
            }

            context.FieldName = rpcFunction.Name;
            context.Categories = rpcFunction.Categories;

            var rpcPayload = payload.ReadSubArchive((int)payloadBits);
            if (rpcFunction.Decoder is not null)
            {
                rpcFunction.Decoder.Decode(ref context, rpcPayload);
            }
            else if (rpcFunction.FunctionGroup is { Enabled: true })
            {
                ParseRepLayoutProperties(
                    rpcPayload,
                    rpcFunction.FunctionGroup,
                    ref context,
                    readPropertyChecksum: rpcFunction.FunctionGroup.Grammar == FieldStreamGrammar.RepLayoutProperties);
            }
            else
            {
                rpcPayload.SkipRemaining();
            }
        }
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
