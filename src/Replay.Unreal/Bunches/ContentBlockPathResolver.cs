using Replay.Encoding.Net;
using Replay.Unreal.Channels;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Bunches;

internal sealed class ContentBlockPathResolver
{
    private readonly NetGuidCache _netGuidCache;

    public ContentBlockPathResolver(NetGuidCache netGuidCache)
    {
        _netGuidCache = netGuidCache;
    }

    public string? ResolveExportGroupPath(ContentBlockHeader header, ActorChannelState channel)
    {
        return header.IsActor
            ? ResolveActorExportGroupPath(channel)
            : ResolveSubobjectExportGroupPath(header);
    }

    public string? ResolveClassPath(ContentBlockHeader header, ActorChannelState channel)
    {
        return header.IsActor
            ? ResolveActorClassPath(channel)
            : ResolveSubobjectClassPath(header);
    }

    private string? ResolveSubobjectExportGroupPath(ContentBlockHeader header) =>
        header.ClassNetGuid.IsValid && _netGuidCache.TryGetPath(header.ClassNetGuid.Value, out var path)
            ? ResolveExportGroupPath(path, archetypePath: null)
            : null;

    private string? ResolveSubobjectClassPath(ContentBlockHeader header) =>
        header.ClassNetGuid.IsValid && _netGuidCache.TryGetPath(header.ClassNetGuid.Value, out var path)
            ? ResolveClassObjectPath(path, archetypePath: null)
            : null;

    private string? ResolveActorExportGroupPath(ActorChannelState channel) =>
        ResolveExportGroupPath(ResolveActorPackageOrClassPath(channel), channel.ArchetypePath);

    private string? ResolveActorClassPath(ActorChannelState channel) =>
        ResolveClassObjectPath(ResolveActorPackageOrClassPath(channel), channel.ArchetypePath);

    private string? ResolveActorPackageOrClassPath(ActorChannelState channel)
    {
        if (channel.ReplicationClassPath is not null)
        {
            return channel.ReplicationClassPath;
        }

        if (channel.ArchetypeNetGuid.IsValid &&
            _netGuidCache.TryGetOuterPath(channel.ArchetypeNetGuid.Value, out var outerPath))
        {
            return outerPath;
        }

        if (channel.ArchetypePath is not null && !ReplayPath.IsClassDefaultObjectPath(channel.ArchetypePath))
        {
            return channel.ArchetypePath;
        }

        if (channel.ArchetypeNetGuid.IsValid &&
            _netGuidCache.TryGetPath(channel.ArchetypeNetGuid.Value, out var archetypePath) &&
            !ReplayPath.IsClassDefaultObjectPath(archetypePath))
        {
            return archetypePath;
        }

        if (channel.ActorPath is not null)
        {
            return channel.ActorPath;
        }

        return channel.ActorNetGuid.IsValid && _netGuidCache.TryGetPath(channel.ActorNetGuid.Value, out var actorPath)
            ? actorPath
            : null;
    }

    private string? ResolveExportGroupPath(string? packageOrClassPath, string? archetypePath)
    {
        foreach (var candidate in EnumerateClassObjectPathCandidates(packageOrClassPath, archetypePath))
        {
            if (TryGetKnownExportGroupPath(candidate, out var exportGroupPath))
            {
                return exportGroupPath;
            }
        }

        return null;
    }

    private static string? ResolveClassObjectPath(string? packageOrClassPath, string? archetypePath) =>
        EnumerateClassObjectPathCandidates(packageOrClassPath, archetypePath).FirstOrDefault();

    private bool TryGetKnownExportGroupPath(string path, out string exportGroupPath)
    {
        foreach (var key in ReplayPath.LookupKeys(path))
        {
            if (_netGuidCache.ExportGroupsByPath.TryGetValue(key, out var group))
            {
                exportGroupPath = group.PathName;
                return true;
            }
        }

        exportGroupPath = string.Empty;
        return false;
    }

    private static IEnumerable<string> EnumerateClassObjectPathCandidates(string? packageOrClassPath, string? archetypePath)
    {
        var className = GetClassName(archetypePath);
        if (packageOrClassPath is not null && className is not null)
        {
            yield return CombinePackageAndClassName(packageOrClassPath, className);
        }

        if (packageOrClassPath is not null)
        {
            yield return packageOrClassPath;
        }

        if (archetypePath is not null && !ReplayPath.IsClassDefaultObjectPath(archetypePath))
        {
            yield return archetypePath;
        }
    }

    private static string CombinePackageAndClassName(string packageOrClassPath, string className)
    {
        if (packageOrClassPath.EndsWith("." + className, StringComparison.Ordinal) ||
            packageOrClassPath.EndsWith(":" + className, StringComparison.Ordinal))
        {
            return packageOrClassPath;
        }

        return packageOrClassPath + "." + className;
    }

    private static string? GetClassName(string? archetypePath)
    {
        if (archetypePath is null)
        {
            return null;
        }

        var leafStart = archetypePath.LastIndexOfAny(['/', '.', ':']);
        var leaf = leafStart >= 0 ? archetypePath[(leafStart + 1)..] : archetypePath;
        return leaf.StartsWith("Default__", StringComparison.Ordinal)
            ? leaf["Default__".Length..]
            : leaf;
    }
}
