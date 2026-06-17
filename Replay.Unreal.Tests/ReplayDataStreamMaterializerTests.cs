using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Encoding.Compression;
using Replay.Models;
using Replay.Unreal.Pipeline;

namespace Replay.Unreal.Tests;

public class ReplayDataStreamMaterializerTests
{
    [Test]
    public void Materialize_UncompressedReplayData_CopiesChunksIntoStream()
    {
        var archive = new FBinaryArchive([0xFF, 0x01, 0x02, 0x03, 0x04, 0x05, 0xEE]);
        var info = new ReplayInfo
        {
            TotalDataSizeInBytes = 5,
            Compressed = false,
        };
        info.DataChunks.Add(new ReplayDataChunkInfo
        {
            SizeInBytes = 2,
            MemorySizeInBytes = 2,
            ReplayDataOffset = 1,
            StreamOffset = 0,
        });
        info.DataChunks.Add(new ReplayDataChunkInfo
        {
            SizeInBytes = 3,
            MemorySizeInBytes = 3,
            ReplayDataOffset = 3,
            StreamOffset = 2,
        });

        var decompressedArchive = new ReplayDataStreamMaterializer(archive).Materialize(info);

        Assert.That(decompressedArchive.ReadBytes((int) decompressedArchive.Remaining).ToArray(), Is.EqualTo(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }));
    }

    [Test]
    public void Materialize_CompressedReplayData_DecompressesChunksIntoStream()
    {
        var archive = new FBinaryArchive([
            0x03, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00,
            0x10, 0x11,
            0x02, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x20,
        ]);
        var decompressor = new FakeOodleDecompressor(
            [0xAA, 0xAB, 0xAC],
            [0xBA, 0xBB]);
        var info = new ReplayInfo
        {
            TotalDataSizeInBytes = 5,
            Compressed = true,
        };
        info.DataChunks.Add(new ReplayDataChunkInfo
        {
            SizeInBytes = 10,
            MemorySizeInBytes = 3,
            ReplayDataOffset = 0,
            StreamOffset = 0,
        });
        info.DataChunks.Add(new ReplayDataChunkInfo
        {
            SizeInBytes = 9,
            MemorySizeInBytes = 2,
            ReplayDataOffset = 10,
            StreamOffset = 3,
        });

        var decompressedArchive = new ReplayDataStreamMaterializer(archive, decompressor).Materialize(info);

        Assert.Multiple(() =>
        {
            Assert.That(decompressedArchive.ReadBytes((int) decompressedArchive.Remaining).ToArray(), Is.EqualTo(new byte[] { 0xAA, 0xAB, 0xAC, 0xBA, 0xBB }));
            Assert.That(decompressor.CompressedInputs, Is.EqualTo(new[]
            {
                new byte[] { 0x10, 0x11 },
                new byte[] { 0x20 },
            }));
        });
    }

    [Test]
    public void Materialize_CompressedReplayDataWithoutDecompressor_ThrowsInvalidReplayInfoException()
    {
        var archive = new FBinaryArchive(BuildOodlePayload(1, [0x10]));
        var info = new ReplayInfo
        {
            TotalDataSizeInBytes = 1,
            Compressed = true,
        };
        info.DataChunks.Add(new ReplayDataChunkInfo
        {
            SizeInBytes = 9,
            MemorySizeInBytes = 1,
            ReplayDataOffset = 0,
            StreamOffset = 0,
        });

        Assert.Throws<InvalidReplayInfoException>(() => new ReplayDataStreamMaterializer(archive).Materialize(info));
    }

    [Test]
    public void Materialize_DecompressedSizeMismatch_ThrowsOodleDecompressionException()
    {
        var archive = new FBinaryArchive(BuildOodlePayload(1, [0x10]));
        var decompressor = new FakeOodleDecompressor([0xAA]);
        decompressor.BytesWrittenOverride = 0;
        var info = new ReplayInfo
        {
            TotalDataSizeInBytes = 1,
            Compressed = true,
        };
        info.DataChunks.Add(new ReplayDataChunkInfo
        {
            SizeInBytes = 9,
            MemorySizeInBytes = 1,
            ReplayDataOffset = 0,
            StreamOffset = 0,
        });

        Assert.Throws<OodleDecompressionException>(() =>
            new ReplayDataStreamMaterializer(archive, decompressor).Materialize(info));
    }

    [Test]
    public void Middleware_ValidReplayData_SetsContextAndCallsNext()
    {
        var context = new ReplayReaderContext(new FBinaryArchive(BuildOodlePayload(1, [0x10])));
        context.ReplayInfo.TotalDataSizeInBytes = 1;
        context.ReplayInfo.Compressed = true;
        context.ReplayInfo.DataChunks.Add(new ReplayDataChunkInfo
        {
            SizeInBytes = 9,
            MemorySizeInBytes = 1,
            ReplayDataOffset = 0,
            StreamOffset = 0,
        });
        var middleware = new DecompressReplayData<ReplayReaderContext>(new FakeOodleDecompressor([0xAA]));
        var nextCalled = false;

        middleware.Execute(context, _ => nextCalled = true);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Errors, Is.Empty);
            Assert.That(context.ReplayDataStream.ReadBytes(1).ToArray(), Is.EqualTo(new byte[] { 0xAA }));
        });
    }

    private sealed class FakeOodleDecompressor : IOodleDecompressor
    {
        private readonly Queue<byte[]> _outputs;

        public FakeOodleDecompressor(params byte[][] outputs)
        {
            _outputs = new Queue<byte[]>(outputs);
        }

        public List<byte[]> CompressedInputs { get; } = [];

        public int? BytesWrittenOverride { get; set; }

        public int Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
        {
            CompressedInputs.Add(compressed.ToArray());
            var output = _outputs.Dequeue();
            output.CopyTo(destination);
            return BytesWrittenOverride ?? output.Length;
        }
    }

    private static byte[] BuildOodlePayload(int decompressedSize, byte[] compressed)
    {
        var bytes = new byte[8 + compressed.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), decompressedSize);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), compressed.Length);
        compressed.CopyTo(bytes.AsSpan(8));
        return bytes;
    }
}
