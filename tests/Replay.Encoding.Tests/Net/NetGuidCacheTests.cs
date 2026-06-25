using Replay.Encoding.Net;

namespace Replay.Encoding.Tests.Net;

public class NetGuidCacheTests
{
    [Test]
    public void AddExportGroup_StoresGroupByPathAndIndex()
    {
        var cache = new NetGuidCache();
        var group = CreateGroup();

        cache.AddExportGroup(group);

        Assert.Multiple(() =>
        {
            Assert.That(cache.ExportGroupsByPath[group.PathName], Is.SameAs(group));
            Assert.That(cache.GetExportGroup(group.PathNameIndex), Is.SameAs(group));
        });
    }

    [Test]
    public void AddExportGroup_ExpandsExistingGroupAndPreservesExports()
    {
        var cache = new NetGuidCache();
        var existingExport = new NetFieldExport
        {
            Handle = 1,
            CompatibleChecksum = 17,
            Name = "ExistingField",
        };
        var existingGroup = CreateGroup();
        existingGroup.NetFieldExports[1] = existingExport;
        cache.AddExportGroup(existingGroup);

        var expandedGroup = cache.AddExportGroup(new NetFieldExportGroup
        {
            PathName = existingGroup.PathName,
            PathNameIndex = existingGroup.PathNameIndex,
            NetFieldExportsLength = 4,
            NetFieldExports = new NetFieldExport?[4],
        });

        Assert.Multiple(() =>
        {
            Assert.That(expandedGroup.NetFieldExportsLength, Is.EqualTo(4));
            Assert.That(expandedGroup.NetFieldExports[1], Is.SameAs(existingExport));
            Assert.That(cache.ExportGroupsByPath[existingGroup.PathName], Is.SameAs(expandedGroup));
            Assert.That(cache.GetExportGroup(existingGroup.PathNameIndex), Is.SameAs(expandedGroup));
        });
    }

    [Test]
    public void SetNetGuidPath_StoresPath()
    {
        var cache = new NetGuidCache();

        cache.SetNetGuidPath(17, "/Game/Test.Test_C");

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryGetPath(17, out var pathName), Is.True);
            Assert.That(pathName, Is.EqualTo("/Game/Test.Test_C"));
        });
    }

    [Test]
    public void SetNetGuidPath_StoresOuterNetGuid()
    {
        var cache = new NetGuidCache();
        var outerNetGuid = new NetworkGuid(11);

        cache.SetNetGuidPath(17, "Default__Test_C", outerNetGuid);
        cache.SetNetGuidPath(outerNetGuid.Value, "/Game/Test.Test_C");

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryGetOuterNetGuid(17, out var resolvedOuter), Is.True);
            Assert.That(resolvedOuter, Is.EqualTo(outerNetGuid));
            Assert.That(cache.TryGetOuterPath(17, out var outerPath), Is.True);
            Assert.That(outerPath, Is.EqualTo("/Game/Test.Test_C"));
        });
    }

    [Test]
    public void Clear_RemovesAllState()
    {
        var cache = new NetGuidCache();
        cache.AddExportGroup(CreateGroup());
        cache.SetNetGuidPath(17, "/Game/Test.Test_C");

        cache.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(cache.ExportGroupsByPath, Is.Empty);
            Assert.That(cache.ExportGroupsByPathIndex, Is.Empty);
            Assert.That(cache.PathByNetGuid, Is.Empty);
            Assert.That(cache.OuterNetGuidByNetGuid, Is.Empty);
        });
    }

    private static NetFieldExportGroup CreateGroup() => new()
    {
        PathName = "/Game/Test.Test_C",
        PathNameIndex = 7,
        NetFieldExportsLength = 2,
        NetFieldExports = new NetFieldExport?[2],
    };
}
