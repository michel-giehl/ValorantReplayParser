using System.Buffers.Binary;

namespace Replay.Unreal.Tests;

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
        var archive = new FBinaryArchive(expected.ToByteArray());

        Assert.That(archive.ReadGuid(), Is.EqualTo(expected));
        Assert.That(archive.AtEnd, Is.True);
    }
}