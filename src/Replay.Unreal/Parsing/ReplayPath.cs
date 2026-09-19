using Replay.Models.Descriptors;

namespace Replay.Unreal.Parsing;

internal static class ReplayPath
{
    public const string ClassNetCacheSuffix = "_ClassNetCache";
    private const string DefaultObjectPrefix = "Default__";

    public static IEnumerable<string> LookupKeys(string path, IReplayPathAliasProvider? aliasProvider = null)
    {
        yield return path;

        var defaultAlias = TryGetDefaultObjectAlias(path);
        if (defaultAlias is not null)
        {
            yield return defaultAlias;
        }

        var alternatePath = aliasProvider?.GetAlternatePath(path);
        if (alternatePath is not null && !string.Equals(alternatePath, path, StringComparison.Ordinal))
        {
            yield return alternatePath;
        }
    }

    public static IEnumerable<string> ClassNetCacheLookupKeys(
        string path,
        IReplayPathAliasProvider? aliasProvider = null)
    {
        foreach (var key in LookupKeys(path, aliasProvider))
        {
            yield return key;

            var suffixless = RemoveClassNetCacheSuffix(key);
            if (suffixless is not null)
            {
                yield return suffixless;
            }
            else
            {
                yield return key + ClassNetCacheSuffix;
            }
        }
    }

    public static string? GetDefaultObjectName(string path)
    {
        var leafStart = path.LastIndexOfAny(['/', '.', ':']);
        var leaf = leafStart >= 0 ? path[(leafStart + 1)..] : path;
        return leaf.Length == 0 ? null : "Default__" + leaf;
    }

    public static bool IsClassDefaultObjectPath(string path)
    {
        var leafStart = path.LastIndexOfAny(['/', '.', ':']);
        return path.AsSpan(leafStart + 1).StartsWith("Default__", StringComparison.Ordinal);
    }

    private static string? RemoveClassNetCacheSuffix(string path)
    {
        var aliasLength = path.Length - ClassNetCacheSuffix.Length;
        if (aliasLength <= 0 || !path.EndsWith(ClassNetCacheSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        return path[..aliasLength];
    }

    private static string? TryGetDefaultObjectAlias(string path)
    {
        if (path.StartsWith(DefaultObjectPrefix, StringComparison.Ordinal))
        {
            return path[DefaultObjectPrefix.Length..];
        }

        return path.IndexOfAny(['/', '.', ':']) < 0
            ? DefaultObjectPrefix + path
            : null;
    }
}
