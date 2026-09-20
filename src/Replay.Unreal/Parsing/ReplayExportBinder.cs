using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Replay;

namespace Replay.Unreal.Parsing;

internal sealed class ReplayExportBinder
{
    private readonly DescriptorCatalogIndex _catalogIndex;
    private readonly BoundExportStore _store;
    private readonly ParseProfile _parseProfile;
    private readonly ReplayReleaseVersion? _releaseVersion;

    public ReplayExportBinder(
        DescriptorCatalogIndex catalogIndex,
        BoundExportStore store,
        ParseProfile parseProfile,
        ReplayReleaseVersion? releaseVersion = null)
    {
        _catalogIndex = catalogIndex;
        _store = store;
        _parseProfile = parseProfile;
        _releaseVersion = releaseVersion;
    }

    public BoundExportGroup BindExportGroup(
        NetFieldExportGroup replayGroup,
        ExportGroupDescriptor descriptor)
    {
        var maxHandle = checked((int)replayGroup.NetFieldExportsLength);
        var fields = new FieldBinding[maxHandle];
        var groupEnabled = IsPathSelected(descriptor.Path) && IsCategorySelected(descriptor.Categories);

        var allFields = new List<FieldDescriptor>();
        _catalogIndex.CollectFields(descriptor, allFields);

        foreach (var fieldDesc in allFields)
        {
            var handle = ResolveHandle(fieldDesc, replayGroup);
            if (handle is null || handle.Value >= maxHandle)
            {
                continue;
            }

            var h = handle.Value;
            var exportName = fieldDesc.ExportName ?? replayGroup.NetFieldExports[h]?.Name;
            var propertyName = fieldDesc.PropertyName ?? exportName;
            var categories = fieldDesc.Categories == ExportCategory.None
                ? descriptor.Categories
                : fieldDesc.Categories;
            var decoder = ResolveFieldDecoder(fieldDesc);
            var enabled = decoder is not null
                          && groupEnabled
                          && IsFieldSelected(descriptor.Path, propertyName, exportName, categories);

            fields[h] = new FieldBinding
            {
                Enabled = enabled,
                Categories = categories,
                Decoder = enabled ? decoder : null,
                Name = propertyName,
                ExportName = exportName,
                TargetProperty = fieldDesc.TargetProperty,
            };
        }

        for (var i = 0; i < maxHandle; i++)
        {
            if (fields[i].Name is null && fields[i].Decoder is null)
            {
                fields[i] = new FieldBinding
                {
                    Enabled = false,
                    Name = replayGroup.NetFieldExports[i]?.Name,
                    ExportName = replayGroup.NetFieldExports[i]?.Name,
                };
            }
        }

        return new BoundExportGroup
        {
            SourceDescriptor = descriptor,
            Categories = descriptor.Categories,
            Grammar = descriptor.Grammar,
            Enabled = groupEnabled,
            CaptureDiagnosticFields = _parseProfile.CaptureDiagnosticFields,
            FieldsByHandle = fields,
        };
    }

    public BoundClassNetCache BindClassNetCache(
        NetFieldExportGroup replayGroup,
        ClassNetCacheDescriptor descriptor)
    {
        var maxHandle = checked((int)replayGroup.NetFieldExportsLength);
        var functions = new BoundRpcFunction[maxHandle];
        var cacheEnabled = IsPathSelected(descriptor.Path);

        foreach (var rpcDesc in descriptor.FunctionFields)
        {
            var handle = ResolveFunctionHandle(rpcDesc, replayGroup);
            if (handle is null || handle.Value >= maxHandle)
            {
                continue;
            }

            var categories = rpcDesc.Categories;
            var decoder = ResolveRpcDecoder(rpcDesc);
            var functionGroup = _store.GetBoundGroup(rpcDesc.FunctionExportPath)
                                ?? BindInlineParameterDescriptor(rpcDesc);
            var hasFunctionDescriptor = _catalogIndex.TryGetExportDescriptor(rpcDesc.FunctionExportPath, out _);
            var enabled = cacheEnabled
                          && IsFieldSelected(descriptor.Path, rpcDesc.Name, rpcDesc.Name, categories)
                          && (decoder is not null || functionGroup is not null || hasFunctionDescriptor);

            var function = new BoundRpcFunction
            {
                Name = rpcDesc.Name,
                FunctionExportPath = rpcDesc.FunctionExportPath,
                Categories = categories,
                Enabled = enabled,
                CaptureDiagnosticFields = _parseProfile.CaptureDiagnosticFields,
                FunctionGroup = functionGroup,
                Decoder = enabled ? decoder : null,
            };

            functions[handle.Value] = function;
            if (functionGroup is null && hasFunctionDescriptor)
            {
                _store.AddPendingRpcFunction(rpcDesc.FunctionExportPath, function);
            }
        }

        return new BoundClassNetCache
        {
            Path = descriptor.Path,
            SourceDescriptor = descriptor,
            Grammar = descriptor.Grammar,
            Enabled = cacheEnabled,
            FunctionsByHandle = functions,
        };
    }

    public BoundClassNetCache? BindClassNetCache(ClassNetCacheDescriptor descriptor)
    {
        var maxHandle = descriptor.FunctionFields
            .Where(function => function.Handle.HasValue)
            .Select(function => function.Handle!.Value)
            .DefaultIfEmpty()
            .Max();
        if (maxHandle == 0 && descriptor.FunctionFields.All(function => function.Handle is null))
        {
            return null;
        }

        var exports = new NetFieldExport?[checked((int)maxHandle + 1)];
        foreach (var function in descriptor.FunctionFields)
        {
            if (function.Handle is not { } handle || handle >= (uint)exports.Length)
            {
                continue;
            }

            exports[checked((int)handle)] = new NetFieldExport
            {
                Handle = handle,
                CompatibleChecksum = 0,
                Name = function.Name,
            };
        }

        return BindClassNetCache(new NetFieldExportGroup
        {
            PathName = descriptor.Path,
            PathNameIndex = 0,
            NetFieldExports = exports,
        }, descriptor);
    }

    private BoundExportGroup? BindInlineParameterDescriptor(RpcDescriptor rpcDesc)
    {
        var descriptor = _catalogIndex.ResolveParameterDescriptor(rpcDesc)
                         ?? CreateInlineParameterDescriptor(rpcDesc);
        if (descriptor is null)
        {
            return null;
        }

        var allFields = new List<FieldDescriptor>();
        _catalogIndex.CollectFields(descriptor, allFields);
        var maxHandle = allFields
            .Where(field => field.Handle.HasValue)
            .Select(field => field.Handle!.Value)
            .DefaultIfEmpty()
            .Max();
        if (maxHandle == 0 && allFields.All(field => field.Handle is null))
        {
            return null;
        }

        var fields = new FieldBinding[checked((int)maxHandle + 1)];
        var groupEnabled = IsPathSelected(descriptor.Path) && IsCategorySelected(descriptor.Categories);

        foreach (var fieldDesc in allFields)
        {
            if (fieldDesc.Handle is not { } handle || handle >= (uint)fields.Length)
            {
                continue;
            }

            var fieldIndex = checked((int)handle);
            var exportName = fieldDesc.ExportName;
            var propertyName = fieldDesc.PropertyName ?? exportName;
            var categories = fieldDesc.Categories == ExportCategory.None
                ? descriptor.Categories
                : fieldDesc.Categories;
            var decoder = ResolveFieldDecoder(fieldDesc);
            var enabled = decoder is not null
                          && groupEnabled
                          && IsFieldSelected(descriptor.Path, propertyName, exportName, categories);

            fields[fieldIndex] = new FieldBinding
            {
                Enabled = enabled,
                Categories = categories,
                Decoder = enabled ? decoder : null,
                Name = propertyName,
                ExportName = exportName,
                TargetProperty = fieldDesc.TargetProperty,
            };
        }

        return new BoundExportGroup
        {
            SourceDescriptor = descriptor,
            Categories = descriptor.Categories,
            Grammar = descriptor.Grammar,
            Enabled = groupEnabled,
            CaptureDiagnosticFields = _parseProfile.CaptureDiagnosticFields,
            FieldsByHandle = fields,
        };
    }

    private static ExportGroupDescriptor? CreateInlineParameterDescriptor(RpcDescriptor rpcDesc)
    {
        if (rpcDesc.Fields.Count == 0)
        {
            return null;
        }

        return new ExportGroupDescriptor(
            rpcDesc.FunctionExportPath,
            rpcDesc.Categories,
            ExportGroupKind.ClassNetCache,
            FieldStreamGrammar.FunctionParameters,
            fields: rpcDesc.Fields);
    }

    private static uint? ResolveHandle(FieldDescriptor fieldDesc, NetFieldExportGroup replayGroup)
    {
        if (fieldDesc.Handle.HasValue)
        {
            return fieldDesc.Handle.Value;
        }

        if (fieldDesc.ExportName is not null)
        {
            for (uint i = 0; i < replayGroup.NetFieldExportsLength; i++)
            {
                if (replayGroup.NetFieldExports[i]?.Name == fieldDesc.ExportName)
                {
                    return i;
                }
            }
        }

        return null;
    }

    private static uint? ResolveFunctionHandle(RpcDescriptor rpcDesc, NetFieldExportGroup replayGroup)
    {
        if (rpcDesc.Handle.HasValue)
        {
            return rpcDesc.Handle.Value;
        }

        for (uint i = 0; i < replayGroup.NetFieldExportsLength; i++)
        {
            if (replayGroup.NetFieldExports[i]?.Name == rpcDesc.Name)
            {
                return i;
            }
        }

        return null;
    }

    private IFieldDecoder? ResolveFieldDecoder(FieldDescriptor fieldDesc)
    {
        var decoderDescriptor = fieldDesc.DecoderDefinition?.Resolve(_releaseVersion, "field decoder")
                                ?? fieldDesc.Decoder;
        if (decoderDescriptor is null)
        {
            return null;
        }

        var decoder = decoderDescriptor as IFieldDecoder
                      ?? throw new InvalidOperationException(
                   $"Field descriptor '{fieldDesc.PropertyName ?? fieldDesc.ExportName ?? fieldDesc.Handle?.ToString() ?? "<unnamed>"}' uses an incompatible decoder type.");
        return decoder is IReplayReleaseAwareFieldDecoder releaseAware
            ? releaseAware.Resolve(_releaseVersion)
            : decoder;
    }

    private IRpcDecoder? ResolveRpcDecoder(RpcDescriptor rpcDesc)
    {
        var decoderDescriptor = rpcDesc.DecoderDefinition?.Resolve(_releaseVersion, "RPC decoder")
                                ?? rpcDesc.Decoder;
        if (decoderDescriptor is null)
        {
            return null;
        }

        return decoderDescriptor as IRpcDecoder
               ?? throw new InvalidOperationException(
                   $"RPC descriptor '{rpcDesc.Name}' uses an incompatible decoder type.");
    }

    private bool IsPathSelected(string path)
    {
        if (_parseProfile.IncludedPaths is not null && !_parseProfile.IncludedPaths.Contains(path))
        {
            return false;
        }

        return _parseProfile.ExcludedPaths is null || !_parseProfile.ExcludedPaths.Contains(path);
    }

    private bool IsFieldSelected(string path, string? propertyName, string? exportName, ExportCategory categories)
    {
        if (!IsCategorySelected(categories))
        {
            return false;
        }

        if (_parseProfile.IncludedFields is null)
        {
            return true;
        }

        return IsIncludedFieldName(propertyName, path) || IsIncludedFieldName(exportName, path);
    }

    private bool IsIncludedFieldName(string? fieldName, string path)
    {
        return fieldName is not null
               && _parseProfile.IncludedFields is not null
               && (_parseProfile.IncludedFields.Contains(fieldName)
                   || _parseProfile.IncludedFields.Contains($"{path}:{fieldName}"));
    }

    private bool IsCategorySelected(ExportCategory categories)
    {
        if (_parseProfile.EnabledCategories == ExportCategory.None)
        {
            return false;
        }

        return categories == ExportCategory.None
               || (categories & _parseProfile.EnabledCategories) != 0;
    }
}
