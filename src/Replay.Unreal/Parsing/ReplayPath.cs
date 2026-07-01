namespace Replay.Unreal.Parsing;

internal static class ReplayPath
{
    public const string ClassNetCacheSuffix = "_ClassNetCache";
    private const string CoreSegment = "/_Core/";
    private const string CharactersRoot = "/Game/Characters/";

    public static IEnumerable<string> LookupKeys(string path)
    {
        yield return path;

        var coreAlias = TryGetCoreAlias(path);
        if (coreAlias is not null)
        {
            yield return coreAlias;
        }
    }

    public static IEnumerable<string> ClassNetCacheLookupKeys(string path)
    {
        foreach (var key in LookupKeys(path))
        {
            yield return key;

            var suffixless = RemoveClassNetCacheSuffix(key);
            if (suffixless is not null)
            {
                yield return suffixless;
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

    public static bool TryGetAlias(string path, out string alias)
    {
        var coreSegmentIndex = path.IndexOf(CoreSegment, StringComparison.Ordinal);
        if (coreSegmentIndex >= 0)
        {
            alias = string.Concat(
                path.AsSpan(0, coreSegmentIndex),
                "/",
                path.AsSpan(coreSegmentIndex + CoreSegment.Length));
            return true;
        }

        if (path.StartsWith(CharactersRoot, StringComparison.Ordinal))
        {
            alias = string.Concat(
                CharactersRoot,
                "_Core/",
                path.AsSpan(CharactersRoot.Length));
            return true;
        }

        alias = string.Empty;
        return false;
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

    private static string? TryGetCoreAlias(string path)
    {
        return TryGetAlias(path, out var alias) ? alias : null;
    }
}
