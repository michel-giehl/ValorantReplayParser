using System.Diagnostics.CodeAnalysis;

namespace Replay.Encoding.Net;

public sealed class NetGuidCache
{
    public Dictionary<string, NetFieldExportGroup> ExportGroupsByPath { get; } = [];

    public Dictionary<uint, NetFieldExportGroup> ExportGroupsByPathIndex { get; } = [];

    public Dictionary<uint, string> PathByNetGuid { get; } = [];

    public NetFieldExportGroup AddExportGroup(NetFieldExportGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        ExportGroupsByPath.TryGetValue(group.PathName, out var existingByPath);
        ExportGroupsByPathIndex.TryGetValue(group.PathNameIndex, out var existingByIndex);

        if (existingByPath is not null &&
            existingByIndex is not null &&
            !ReferenceEquals(existingByPath, existingByIndex))
        {
            throw new InvalidOperationException(
                $"Export group path {group.PathName} and path index {group.PathNameIndex} resolve to different groups.");
        }

        var existingGroup = existingByPath ?? existingByIndex;
        if (existingGroup is null)
        {
            ExportGroupsByPath[group.PathName] = group;
            ExportGroupsByPathIndex[group.PathNameIndex] = group;
            return group;
        }

        var mergedLength = Math.Max(existingGroup.NetFieldExportsLength, group.NetFieldExportsLength);
        var mergedGroup = new NetFieldExportGroup
        {
            PathName = group.PathName,
            PathNameIndex = group.PathNameIndex,
            NetFieldExportsLength = mergedLength,
            NetFieldExports = new NetFieldExport?[checked((int)mergedLength)],
        };

        CopyExports(existingGroup.NetFieldExports, mergedGroup.NetFieldExports);
        CopyExports(group.NetFieldExports, mergedGroup.NetFieldExports);

        ReplaceGroupReferences(existingGroup, mergedGroup);
        ExportGroupsByPath[group.PathName] = mergedGroup;
        ExportGroupsByPathIndex[group.PathNameIndex] = mergedGroup;
        return mergedGroup;
    }

    public NetFieldExportGroup GetExportGroup(uint pathNameIndex) =>
        ExportGroupsByPathIndex[pathNameIndex];

    public bool TryGetExportGroup(
        uint pathNameIndex,
        [NotNullWhen(true)] out NetFieldExportGroup? group) =>
        ExportGroupsByPathIndex.TryGetValue(pathNameIndex, out group);

    public void SetNetGuidPath(uint netGuid, string pathName)
    {
        ArgumentNullException.ThrowIfNull(pathName);
        PathByNetGuid[netGuid] = pathName;
    }

    public bool TryGetPath(uint netGuid, out string pathName)
    {
        if (PathByNetGuid.TryGetValue(netGuid, out var value))
        {
            pathName = value;
            return true;
        }

        pathName = string.Empty;
        return false;
    }

    public void Clear()
    {
        ExportGroupsByPath.Clear();
        ExportGroupsByPathIndex.Clear();
        PathByNetGuid.Clear();
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
        foreach (var pathName in ExportGroupsByPath
                     .Where(pair => ReferenceEquals(pair.Value, existingGroup))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            ExportGroupsByPath[pathName] = mergedGroup;
        }

        foreach (var pathNameIndex in ExportGroupsByPathIndex
                     .Where(pair => ReferenceEquals(pair.Value, existingGroup))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            ExportGroupsByPathIndex[pathNameIndex] = mergedGroup;
        }
    }
}
