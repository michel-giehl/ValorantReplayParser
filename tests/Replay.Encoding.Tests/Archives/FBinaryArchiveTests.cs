using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Models.Unreal;

namespace Replay.Encoding.Tests.Archives;

public class FBinaryArchiveTests
{
    [Test]
    public void ReadFString_ReadsUtf8NullTerminatedString()
    {
        var bytes = new byte[]
        {
            0x06, 0x00, 0x00, 0x00,
            (byte)'L', (byte)'e', (byte)'v', (byte)'e', (byte)'l', 0x00
        };
        var archive = new FBinaryArchive(bytes);

        Assert.That(archive.ReadFString(), Is.EqualTo("Level"));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void ReadFString_ReadsUtf16NullTerminatedString()
    {
        const string value = "WasPartReplicatedFlags";
        var textBytes = System.Text.Encoding.Unicode.GetBytes(value + '\0');
        var bytes = new byte[4 + textBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), -(value.Length + 1));
        textBytes.CopyTo(bytes.AsSpan(4));
        var archive = new FBinaryArchive(bytes);

        Assert.That(archive.ReadFString(), Is.EqualTo(value));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void ReadGuid_ReadsSixteenByteGuid()
    {
        var expected = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var bytes = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), 0x00112233u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 0x44556677u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), 0x8899AABBu);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), 0xCCDDEEFFu);
        var archive = new FBinaryArchive(bytes);

        Assert.That(archive.ReadGuid(), Is.EqualTo(expected));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void ReadFVector_ReadsThreeFloats()
    {
        var bytes = new byte[12];
        WriteSingle(bytes, 0, 1.25f);
        WriteSingle(bytes, 4, -2.5f);
        WriteSingle(bytes, 8, 3.75f);
        var archive = new FBinaryArchive(bytes);

        Assert.That(archive.ReadFVector(), Is.EqualTo(new FVector(1.25f, -2.5f, 3.75f)));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void ReadFQuat_ReadsFourFloats()
    {
        var bytes = new byte[16];
        WriteSingle(bytes, 0, 0.1f);
        WriteSingle(bytes, 4, 0.2f);
        WriteSingle(bytes, 8, 0.3f);
        WriteSingle(bytes, 12, 0.4f);
        var archive = new FBinaryArchive(bytes);

        Assert.That(archive.ReadFQuat(), Is.EqualTo(new FQuat(0.1f, 0.2f, 0.3f, 0.4f)));
        Assert.That(archive.AtEnd, Is.True);
    }

    [Test]
    public void ReadFTransform_ReadsRotationTranslationAndScale()
    {
        var bytes = new byte[40];
        WriteSingle(bytes, 0, 0.1f);
        WriteSingle(bytes, 4, 0.2f);
        WriteSingle(bytes, 8, 0.3f);
        WriteSingle(bytes, 12, 0.4f);
        WriteSingle(bytes, 16, 10f);
        WriteSingle(bytes, 20, 20f);
        WriteSingle(bytes, 24, 30f);
        WriteSingle(bytes, 28, 1f);
        WriteSingle(bytes, 32, 2f);
        WriteSingle(bytes, 36, 3f);
        var archive = new FBinaryArchive(bytes);

        var transform = archive.ReadFTransform();

        Assert.Multiple(() =>
        {
            Assert.That(transform.Rotation, Is.EqualTo(new FQuat(0.1f, 0.2f, 0.3f, 0.4f)));
            Assert.That(transform.Translation, Is.EqualTo(new FVector(10f, 20f, 30f)));
            Assert.That(transform.Scale3D, Is.EqualTo(new FVector(1f, 2f, 3f)));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    private static void WriteSingle(byte[] bytes, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, sizeof(float)), value);
}
