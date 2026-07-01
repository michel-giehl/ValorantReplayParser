using Replay.Encoding.Net;
using Replay.Unreal.Channels;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Bunches;

internal sealed class ContentBlockPathResolver
{
    private readonly NetGuidCache _netGuidCache;
    private readonly Dictionary<ulong, string> _actorExportGroupPathByChannel = [];
    private readonly Dictionary<ulong, string> _actorClassPathByChannel = [];
    private readonly Dictionary<uint, string> _subobjectExportGroupPathByClassNetGuid = [];
    private readonly Dictionary<uint, string> _subobjectClassPathByClassNetGuid = [];

    public ContentBlockPathResolver(NetGuidCache netGuidCache)
    {
        _netGuidCache = netGuidCache;
    }

    public string? ResolveExportGroupPath(ContentBlockHeader header, ActorChannelState channel)
    {
        return header.IsActor
            ? ResolveCachedActorExportGroupPath(channel)
            : ResolveSubobjectExportGroupPath(header);
    }

    public string? ResolveClassPath(ContentBlockHeader header, ActorChannelState channel)
    {
        return header.IsActor
            ? ResolveCachedActorClassPath(channel)
            : ResolveSubobjectClassPath(header);
    }

    private string? ResolveSubobjectExportGroupPath(ContentBlockHeader header)
    {
        if (!header.ClassNetGuid.IsValid)
        {
            return null;
        }

        var classNetGuid = header.ClassNetGuid.Value;
        if (_subobjectExportGroupPathByClassNetGuid.TryGetValue(classNetGuid, out var cached))
        {
            return cached;
        }

        if (!_netGuidCache.TryGetPath(classNetGuid, out var path))
        {
            return null;
        }

        var resolved = ResolveExportGroupPath(path, archetypePath: null);
        if (resolved is not null)
        {
            _subobjectExportGroupPathByClassNetGuid[classNetGuid] = resolved;
        }

        return resolved;
    }

    private string? ResolveSubobjectClassPath(ContentBlockHeader header)
    {
        if (!header.ClassNetGuid.IsValid)
        {
            return null;
        }

        var classNetGuid = header.ClassNetGuid.Value;
        if (_subobjectClassPathByClassNetGuid.TryGetValue(classNetGuid, out var cached))
        {
            return cached;
        }

        if (!_netGuidCache.TryGetPath(classNetGuid, out var path))
        {
            return null;
        }

        var resolved = ResolveClassObjectPath(path, archetypePath: null);
        if (resolved is not null)
        {
            _subobjectClassPathByClassNetGuid[classNetGuid] = resolved;
        }

        return resolved;
    }

    private string? ResolveCachedActorExportGroupPath(ActorChannelState channel)
    {
        var key = ActorCacheKey(channel);
        if (_actorExportGroupPathByChannel.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var resolved = ResolveExportGroupPath(ResolveActorPackageOrClassPath(channel), channel.ArchetypePath);
        if (resolved is not null)
        {
            _actorExportGroupPathByChannel[key] = resolved;
        }

        return resolved;
    }

    private string? ResolveCachedActorClassPath(ActorChannelState channel)
    {
        var key = ActorCacheKey(channel);
        if (_actorClassPathByChannel.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var resolved = ResolveClassObjectPath(ResolveActorPackageOrClassPath(channel), channel.ArchetypePath);
        if (resolved is not null)
        {
            _actorClassPathByChannel[key] = resolved;
        }

        return resolved;
    }

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
        if (TryResolveCandidate(packageOrClassPath, archetypePath, out var exportGroupPath))
        {
            return exportGroupPath;
        }

        return packageOrClassPath is not null && TryGetKnownExportGroupPath(packageOrClassPath, out exportGroupPath)
            ? exportGroupPath
            : TryResolveArchetypeCandidate(archetypePath, out exportGroupPath) ? exportGroupPath : null;
    }

    private static string? ResolveClassObjectPath(string? packageOrClassPath, string? archetypePath)
    {
        if (TryCreateCombinedCandidate(packageOrClassPath, archetypePath, out var combined))
        {
            return combined;
        }

        if (packageOrClassPath is not null)
        {
            return packageOrClassPath;
        }

        return archetypePath is not null && !ReplayPath.IsClassDefaultObjectPath(archetypePath)
            ? archetypePath
            : null;
    }

    private bool TryGetKnownExportGroupPath(string path, out string exportGroupPath)
    {
        if (_netGuidCache.ExportGroupsByPath.TryGetValue(path, out var group))
        {
            exportGroupPath = group.PathName;
            return true;
        }

        if (ReplayPath.TryGetAlias(path, out var alias) &&
            _netGuidCache.ExportGroupsByPath.TryGetValue(alias, out group))
        {
            exportGroupPath = group.PathName;
            return true;
        }

        exportGroupPath = string.Empty;
        return false;
    }

    private bool TryResolveCandidate(
        string? packageOrClassPath,
        string? archetypePath,
        out string exportGroupPath)
    {
        exportGroupPath = string.Empty;
        return TryCreateCombinedCandidate(packageOrClassPath, archetypePath, out var combined) &&
               TryGetKnownExportGroupPath(combined, out exportGroupPath);
    }

    private bool TryResolveArchetypeCandidate(string? archetypePath, out string exportGroupPath)
    {
        exportGroupPath = string.Empty;
        return archetypePath is not null &&
               !ReplayPath.IsClassDefaultObjectPath(archetypePath) &&
               TryGetKnownExportGroupPath(archetypePath, out exportGroupPath);
    }

    private static bool TryCreateCombinedCandidate(
        string? packageOrClassPath,
        string? archetypePath,
        out string combined)
    {
        combined = string.Empty;
        if (packageOrClassPath is null || !TryGetClassNameRange(archetypePath, out var classStart, out var classLength))
        {
            return false;
        }

        var className = archetypePath!.AsSpan(classStart, classLength);
        if (EndsWithClassName(packageOrClassPath, className))
        {
            combined = packageOrClassPath;
            return true;
        }

        combined = string.Concat(packageOrClassPath, ".", className);
        return true;
    }

    private static bool EndsWithClassName(string packageOrClassPath, ReadOnlySpan<char> className)
    {
        var separatorIndex = packageOrClassPath.Length - className.Length - 1;
        if (separatorIndex < 0)
        {
            return false;
        }

        var separator = packageOrClassPath[separatorIndex];
        return (separator == '.' || separator == ':') &&
               packageOrClassPath.AsSpan(separatorIndex + 1).SequenceEqual(className);
    }

    private static bool TryGetClassNameRange(string? archetypePath, out int start, out int length)
    {
        start = 0;
        length = 0;
        if (archetypePath is null)
        {
            return false;
        }

        var leafStart = archetypePath.LastIndexOfAny(['/', '.', ':']);
        start = leafStart + 1;
        length = archetypePath.Length - start;
        if (length == 0)
        {
            return false;
        }

        const string defaultPrefix = "Default__";
        if (archetypePath.AsSpan(start, length).StartsWith(defaultPrefix, StringComparison.Ordinal))
        {
            start += defaultPrefix.Length;
            length -= defaultPrefix.Length;
        }

        return length > 0;
    }

    private static ulong ActorCacheKey(ActorChannelState channel) =>
        ((ulong)channel.ChannelIndex << 32) | channel.ActorNetGuid.Value;
}
