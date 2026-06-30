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
        var leaf = leafStart >= 0 ? path[(leafStart + 1)..] : path;
        return leaf.StartsWith("Default__", StringComparison.Ordinal);
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
        if (path.Contains(CoreSegment, StringComparison.Ordinal))
        {
            return path.Replace(CoreSegment, "/", StringComparison.Ordinal);
        }

        if (!path.StartsWith(CharactersRoot, StringComparison.Ordinal))
        {
            return null;
        }

        return CharactersRoot + "_Core/" + path[CharactersRoot.Length..];
    }
}