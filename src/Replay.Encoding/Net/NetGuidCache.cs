using System.Diagnostics.CodeAnalysis;
using Replay.Models.Net;

namespace Replay.Encoding.Net;

public sealed class NetGuidCache
{
    private readonly Dictionary<string, NetFieldExportGroup> _exportGroupsByPath = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, NetFieldExportGroup> _exportGroupsByPathIndex = [];
    private readonly Dictionary<uint, string> _pathByNetGuid = [];
    private readonly Dictionary<uint, NetworkGuid> _outerNetGuidByNetGuid = [];

    public IReadOnlyDictionary<string, NetFieldExportGroup> ExportGroupsByPath => _exportGroupsByPath;

    public IReadOnlyDictionary<uint, NetFieldExportGroup> ExportGroupsByPathIndex => _exportGroupsByPathIndex;

    public IReadOnlyDictionary<uint, string> PathByNetGuid => _pathByNetGuid;

    public IReadOnlyDictionary<uint, NetworkGuid> OuterNetGuidByNetGuid => _outerNetGuidByNetGuid;

    public NetFieldExportGroup AddExportGroup(NetFieldExportGroup group)
    {
        _exportGroupsByPath.TryGetValue(group.PathName, out var existingByPath);
        _exportGroupsByPathIndex.TryGetValue(group.PathNameIndex, out var existingByIndex);

        ValidateEquality(group, existingByPath, existingByIndex);

        var existingGroup = existingByPath ?? existingByIndex;
        if (existingGroup is null)
        {
            _exportGroupsByPath[group.PathName] = group;
            _exportGroupsByPathIndex[group.PathNameIndex] = group;
            return group;
        }

        var mergedGroup = MergeWithExistingExportGroup(group, existingGroup);
        return mergedGroup;
    }

    private NetFieldExportGroup MergeWithExistingExportGroup(NetFieldExportGroup group, NetFieldExportGroup existingGroup)
    {
        var mergedLength = Math.Max(existingGroup.NetFieldExportsLength, group.NetFieldExportsLength);
        var mergedGroup = new NetFieldExportGroup
        {
            PathName = group.PathName,
            PathNameIndex = group.PathNameIndex,
            NetFieldExports = new NetFieldExport?[checked((int)mergedLength)],
        };

        CopyExports(existingGroup.NetFieldExports, mergedGroup.NetFieldExports);
        CopyExports(group.NetFieldExports, mergedGroup.NetFieldExports);

        ReplaceGroupReferences(existingGroup, mergedGroup);
        _exportGroupsByPath[group.PathName] = mergedGroup;
        _exportGroupsByPathIndex[group.PathNameIndex] = mergedGroup;
        return mergedGroup;
    }

    private static void ValidateEquality(NetFieldExportGroup group, NetFieldExportGroup? existingByPath,
        NetFieldExportGroup? existingByIndex)
    {
        if (existingByPath is not null &&
            existingByIndex is not null &&
            !ReferenceEquals(existingByPath, existingByIndex))
        {
            throw new InvalidOperationException(
                $"Export group path {group.PathName} and path index {group.PathNameIndex} resolve to different groups.");
        }
    }

    public NetFieldExportGroup GetExportGroup(uint pathNameIndex) =>
        _exportGroupsByPathIndex[pathNameIndex];

    public bool TryGetExportGroup(
        uint pathNameIndex,
        [NotNullWhen(true)] out NetFieldExportGroup? group) =>
        _exportGroupsByPathIndex.TryGetValue(pathNameIndex, out group);

    public void SetNetGuidPath(uint netGuid, string pathName, NetworkGuid outerNetGuid = default)
     {
        _pathByNetGuid[netGuid] = pathName;
        if (outerNetGuid.IsValid)
        {
            _outerNetGuidByNetGuid[netGuid] = outerNetGuid;
        }
        else
        {
            _outerNetGuidByNetGuid.Remove(netGuid);
        }
     }

    public bool TryGetPath(uint netGuid, out string pathName)
    {
        if (_pathByNetGuid.TryGetValue(netGuid, out var value))
        {
            pathName = value;
            return true;
        }

        pathName = string.Empty;
        return false;
    }

    public bool TryGetOuterNetGuid(uint netGuid, out NetworkGuid outerNetGuid) =>
        _outerNetGuidByNetGuid.TryGetValue(netGuid, out outerNetGuid);

    public bool TryGetOuterPath(uint netGuid, out string outerPath)
    {
        if (TryGetOuterNetGuid(netGuid, out var outerNetGuid) && TryGetPath(outerNetGuid.Value, out outerPath))
        {
            return true;
        }

        outerPath = string.Empty;
        return false;
    }

    public void Clear()
    {
        _exportGroupsByPath.Clear();
        _exportGroupsByPathIndex.Clear();
        _pathByNetGuid.Clear();
        _outerNetGuidByNetGuid.Clear();
    }

    private static void CopyExports(NetFieldExport?[] source, NetFieldExport?[] destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] is not null)
            {
                destination[i] = source[i];
            }
        }
    }

    private void ReplaceGroupReferences(NetFieldExportGroup existingGroup, NetFieldExportGroup mergedGroup)
    {
        foreach (var pathName in _exportGroupsByPath
                     .Where(pair => ReferenceEquals(pair.Value, existingGroup))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _exportGroupsByPath[pathName] = mergedGroup;
        }

        foreach (var pathNameIndex in _exportGroupsByPathIndex
                     .Where(pair => ReferenceEquals(pair.Value, existingGroup))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _exportGroupsByPathIndex[pathNameIndex] = mergedGroup;
        }
    }
}