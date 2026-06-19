using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models;

namespace Replay.Unreal.Tests;

public class ExportDataReaderTests
{
    [Test]
    public void ReadNetFieldExports_RegistersExportedGroup()
    {
        var cache = new NetGuidCache();
        var bytes = BuildNetFieldExports(
            writer => AddGroup(writer, 11, "/Game/Test.Test_C", 3, addField: false));

        new ExportDataReader(new FBinaryArchive(bytes), cache).ReadNetFieldExports();

        var group = cache.GetExportGroup(11);
        Assert.Multiple(() =>
        {
            Assert.That(group.PathName, Is.EqualTo("/Game/Test.Test_C"));
            Assert.That(group.PathNameIndex, Is.EqualTo(11));
            Assert.That(group.NetFieldExportsLength, Is.EqualTo(3));
            Assert.That(cache.ExportGroupsByPath[group.PathName], Is.SameAs(group));
        });
    }

    [Test]
    public void ReadNetFieldExports_StoresExportByHandle()
    {
        var cache = new NetGuidCache();
        var bytes = BuildNetFieldExports(
            writer => AddGroup(writer, 11, "/Game/Test.Test_C", 3, addField: true));

        new ExportDataReader(new FBinaryArchive(bytes), cache).ReadNetFieldExports();

        var netFieldExport = cache.GetExportGroup(11).NetFieldExports[2];
        Assert.Multiple(() =>
        {
            Assert.That(netFieldExport, Is.Not.Null);
            Assert.That(netFieldExport!.Handle, Is.EqualTo(2));
            Assert.That(netFieldExport.CompatibleChecksum, Is.EqualTo(0xAABBCCDD));
            Assert.That(netFieldExport.Name, Is.EqualTo("FieldName"));
        });
    }

    [Test]
    public void ReadNetFieldExports_ExistingPathIndexUpdatesGroup()
    {
        var cache = new NetGuidCache();
        var bytes = BuildNetFieldExports(
            writer =>
            {
                AddGroup(writer, 11, "/Game/Test.Test_C", 3, addField: false);
                AddExistingGroupField(writer, 11, handle: 1, name: "LaterField");
            },
            exportCount: 2);

        new ExportDataReader(new FBinaryArchive(bytes), cache).ReadNetFieldExports();

        var group = cache.GetExportGroup(11);
        Assert.That(group.NetFieldExports[1]?.Name, Is.EqualTo("LaterField"));
    }

    [Test]
    public void ReadNetFieldExports_ReExportedGroupExpandsWithoutLosingExistingField()
    {
        var cache = new NetGuidCache();
        var bytes = BuildNetFieldExports(
            writer =>
            {
                AddIntPacked(writer, 11);
                AddIntPacked(writer, 1);
                AddFString(writer, "/Game/Test.Test_C");
                AddIntPacked(writer, 2);
                AddNetFieldExport(writer, handle: 1, name: "ExistingField");

                AddIntPacked(writer, 11);
                AddIntPacked(writer, 1);
                AddFString(writer, "/Game/Test.Test_C");
                AddIntPacked(writer, 4);
                AddNetFieldExport(writer, handle: 3, name: "ExpandedField");
            },
            exportCount: 2);

        new ExportDataReader(new FBinaryArchive(bytes), cache).ReadNetFieldExports();

        var group = cache.GetExportGroup(11);
        Assert.Multiple(() =>
        {
            Assert.That(group.NetFieldExportsLength, Is.EqualTo(4));
            Assert.That(group.NetFieldExports[1]?.Name, Is.EqualTo("ExistingField"));
            Assert.That(group.NetFieldExports[3]?.Name, Is.EqualTo("ExpandedField"));
        });
    }

    [Test]
    public void ReadNetFieldExports_UnknownPathIndexThrowsInvalidReplayInfoException()
    {
        var cache = new NetGuidCache();
        var bytes = BuildNetFieldExports(
            writer =>
            {
                AddIntPacked(writer, 42);
                AddIntPacked(writer, 0);
            });

        var exception = Assert.Throws<InvalidReplayInfoException>(() =>
            new ExportDataReader(new FBinaryArchive(bytes), cache).ReadNetFieldExports());

        Assert.That(exception!.Message, Does.Contain("unknown path index 42"));
    }

    [Test]
    public void ReadNetFieldExports_InvalidHandleIsIgnored()
    {
        var cache = new NetGuidCache();
        var bytes = BuildNetFieldExports(
            writer =>
            {
                AddIntPacked(writer, 11);
                AddIntPacked(writer, 1);
                AddFString(writer, "/Game/Test.Test_C");
                AddIntPacked(writer, 1);
                AddNetFieldExport(writer, handle: 2, name: "OutOfRangeField");
            });

        Assert.DoesNotThrow(() =>
            new ExportDataReader(new FBinaryArchive(bytes), cache).ReadNetFieldExports());
        Assert.That(cache.GetExportGroup(11).NetFieldExports, Is.All.Null);
    }

    [Test]
    public void ReadExportGuids_RegistersExportedPath()
    {
        var cache = new NetGuidCache();
        var payload = BuildNetGuidPayload(17, "/Game/Test.Test_C");
        var bytes = new List<byte>();
        AddIntPacked(bytes, 1);
        AddInt32(bytes, payload.Length);
        bytes.AddRange(payload);

        new ExportDataReader(new FBinaryArchive(bytes.ToArray()), cache).ReadExportGuids();

        Assert.Multiple(() =>
        {
            Assert.That(cache.TryGetPath(17, out var pathName), Is.True);
            Assert.That(pathName, Is.EqualTo("/Game/Test.Test_C"));
        });
    }

    [Test]
    public void ReadExportGuids_NegativePayloadSize_Throws()
    {
        var cache = new NetGuidCache();
        var bytes = new List<byte>();
        AddIntPacked(bytes, 1);
        AddInt32(bytes, -1);

        Assert.Throws<InvalidReplayInfoException>(() =>
            new ExportDataReader(new FBinaryArchive(bytes.ToArray()), cache).ReadExportGuids());
    }

    [Test]
    public void ReadExportGuids_TrailingPayloadDataThrows()
    {
        var cache = new NetGuidCache();
        var payload = BuildNetGuidPayload(17, "/Game/Test.Test_C").ToList();
        payload.Add(0xFF);
        var bytes = new List<byte>();
        AddIntPacked(bytes, 1);
        AddInt32(bytes, payload.Count);
        bytes.AddRange(payload);

        var exception = Assert.Throws<ArchiveReadException>(() =>
            new ExportDataReader(new FBinaryArchive(bytes.ToArray()), cache).ReadExportGuids());

        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.UnexpectedTrailingData));
    }

    private static byte[] BuildNetFieldExports(Action<List<byte>> writeExports, uint exportCount = 1)
    {
        var bytes = new List<byte>();
        AddIntPacked(bytes, exportCount);
        writeExports(bytes);
        return bytes.ToArray();
    }

    private static void AddGroup(
        List<byte> bytes,
        uint pathNameIndex,
        string pathName,
        uint groupLength,
        bool addField)
    {
        AddIntPacked(bytes, pathNameIndex);
        AddIntPacked(bytes, 1);
        AddFString(bytes, pathName);
        AddIntPacked(bytes, groupLength);

        if (addField)
        {
            AddNetFieldExport(bytes, 2, "FieldName");
        }
        else
        {
            bytes.Add(0);
        }
    }

    private static void AddExistingGroupField(List<byte> bytes, uint pathNameIndex, uint handle, string name)
    {
        AddIntPacked(bytes, pathNameIndex);
        AddIntPacked(bytes, 0);
        AddNetFieldExport(bytes, handle, name);
    }

    private static void AddNetFieldExport(List<byte> bytes, uint handle, string name)
    {
        bytes.Add(1);
        AddIntPacked(bytes, handle);
        AddUInt32(bytes, 0xAABBCCDD);
        bytes.Add(0);
        AddFString(bytes, name);
        AddInt32(bytes, 0);
    }

    private static byte[] BuildNetGuidPayload(uint netGuid, string pathName)
    {
        var bytes = new List<byte>();
        AddIntPacked(bytes, netGuid);
        bytes.Add((byte)ExportFlags.HasPath);
        AddIntPacked(bytes, 0);
        AddFString(bytes, pathName);
        return bytes.ToArray();
    }

    private static void AddFString(List<byte> bytes, string value)
    {
        var encoded = System.Text.Encoding.UTF8.GetBytes(value + '\0');
        AddInt32(bytes, encoded.Length);
        bytes.AddRange(encoded);
    }

    private static void AddUInt32(List<byte> bytes, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddInt32(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddIntPacked(List<byte> bytes, uint value)
    {
        do
        {
            var nextByte = (byte)((value & 0x7F) << 1);
            value >>= 7;
            if (value != 0)
            {
                nextByte |= 1;
            }

            bytes.Add(nextByte);
        } while (value != 0);
    }
}
