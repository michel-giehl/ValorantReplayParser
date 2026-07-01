using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Movement;

namespace Replay.Valorant.Tests.Movement;

public class RemoteCharacterMovementTests
{
    [Test]
    public void ComponentDataStream_DecodesMovementSection()
    {
        var payload = BuildComponentDataStreamPayload(useByteWrapper: false);
        using var archive = new BitArchiveReader(payload.Bytes, payload.BitCount);

        var stream = ComponentDataStream.Decode(archive);

        Assert.Multiple(() =>
        {
            Assert.That(stream.HasMovementSection, Is.True);
            Assert.That(stream.HasValidMovementMagic, Is.True);
            Assert.That(stream.MovementParseError, Is.Null);
            Assert.That(stream.MoveCount, Is.EqualTo(1));
            Assert.That(stream.HasLatestMove, Is.True);
            Assert.That(stream.LatestMove.MoveType, Is.EqualTo(0));
            Assert.That(stream.LatestMove.Timestamp, Is.EqualTo(42));
            Assert.That(stream.LatestMove.Position.X, Is.EqualTo(1.25f).Within(0.001));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void ComponentDataStream_DecodesByteWrappedPayload()
    {
        var payload = BuildComponentDataStreamPayload(useByteWrapper: true);
        using var archive = new BitArchiveReader(payload.Bytes, payload.BitCount);

        var stream = ComponentDataStream.Decode(archive);

        Assert.Multiple(() =>
        {
            Assert.That(stream.HasValidMovementMagic, Is.True);
            Assert.That(stream.MoveCount, Is.EqualTo(1));
            Assert.That(stream.HasLatestMove, Is.True);
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void ComponentDataStream_InvalidMagic_RecordsParseError()
    {
        var movement = new BitWriter();
        movement.WriteByte(0x00);
        var payload = new BitWriter();
        payload.WriteUInt16((ushort)movement.BitCount);
        payload.WriteBits(movement);
        using var archive = new BitArchiveReader(payload.ToArray(), payload.BitCount);

        var stream = ComponentDataStream.Decode(archive);

        Assert.Multiple(() =>
        {
            Assert.That(stream.HasMovementSection, Is.True);
            Assert.That(stream.HasValidMovementMagic, Is.False);
            Assert.That(stream.MovementParseError, Is.EqualTo("Invalid movement magic 0x00"));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void ComponentDataStream_DecodesVariant1QuantizedVelocity()
    {
        var payload = BuildComponentDataStreamPayload(useByteWrapper: false, useVariant1: true);
        using var archive = new BitArchiveReader(payload.Bytes, payload.BitCount);

        var stream = ComponentDataStream.Decode(archive);

        Assert.Multiple(() =>
        {
            Assert.That(stream.MovementParseError, Is.Null);
            Assert.That(stream.MoveCount, Is.EqualTo(1));
            Assert.That(stream.LatestMove.MoveType, Is.EqualTo(1));
            Assert.That(stream.LatestMove.Variant1Flag, Is.True);
            Assert.That(stream.LatestMove.Velocity!.Value.X, Is.EqualTo(4.0).Within(0.001));
            Assert.That(stream.LatestMove.Velocity!.Value.Y, Is.EqualTo(5.0).Within(0.001));
            Assert.That(stream.LatestMove.Velocity!.Value.Z, Is.EqualTo(6.0).Within(0.001));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void ComponentDataStream_DecodesAllMovesButKeepsOnlyLatestMove()
    {
        var payload = BuildComponentDataStreamPayload(useByteWrapper: false, includeSecondMove: true);
        using var archive = new BitArchiveReader(payload.Bytes, payload.BitCount);

        var stream = ComponentDataStream.Decode(archive);

        Assert.Multiple(() =>
        {
            Assert.That(stream.MovementParseError, Is.Null);
            Assert.That(stream.MoveCount, Is.EqualTo(2));
            Assert.That(stream.HasLatestMove, Is.True);
            Assert.That(stream.LatestMove.Marker, Is.EqualTo(2));
            Assert.That(stream.LatestMove.Timestamp, Is.EqualTo(84));
            Assert.That(stream.LatestMove.Position.X, Is.EqualTo(10.0f).Within(0.001));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void RemoteCharacterUpdatesRpcDecoder_DecodesBatchAndEmitsMovementEvent()
    {
        var decoder = GetRemoteCharacterUpdatesDecoder();
        var payload = BuildRemoteCharacterUpdatesRpcPayload();
        using var archive = new BitArchiveReader(payload.Bytes, payload.BitCount);
        var eventSink = new CapturingReplayEventSink();
        var context = new FieldDecodeContext
        {
            EventSink = eventSink,
            CurrentTimeSeconds = 12.5f,
            CurrentPacketId = 99,
            ChannelIndex = 7,
            ActorNetGuid = new NetworkGuid(100),
            ObjectNetGuid = new NetworkGuid(101),
        };

        var fields = decoder.Decode(ref context, archive);
        var movementEvents = eventSink.Events.OfType<RemoteCharacterMovementReceived>().ToArray();
        var batch = (RemoteCharacterUpdateBatch)fields[0].Value.ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(fields, Has.Count.EqualTo(1));
            Assert.That(fields[0].Name, Is.EqualTo("RemoteCharacterUpdates"));
            Assert.That(fields[0].Value.Kind, Is.EqualTo(DecodedFieldValueKind.Object));
            Assert.That(batch.Updates, Has.Count.EqualTo(1));
            Assert.That(batch.Updates[0].ShooterCharacterNetGuidValue, Is.EqualTo(1234));
            Assert.That(batch.Updates[0].ComponentDataStream!.MoveCount, Is.EqualTo(1));
            Assert.That(batch.Updates[0].ComponentDataStream!.HasLatestMove, Is.True);
            Assert.That(movementEvents, Has.Length.EqualTo(1));
            Assert.That(movementEvents[0].ShooterCharacterNetGuidValue, Is.EqualTo(1234));
            Assert.That(movementEvents[0].Move.Timestamp, Is.EqualTo(42));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    private static IRpcDecoder GetRemoteCharacterUpdatesDecoder()
    {
        var descriptor = ValorantDescriptors.CreateCatalog()
            .ClassNetCacheDescriptors
            .Single(descriptor => descriptor.Path == "/Game/Characters/BaseReplayController.BaseReplayController_C_ClassNetCache");
        var rpc = descriptor.FunctionFields.Single(function =>
            function.Name == "ReplaysClientReceiveRemoteCharacterUpdatesSingleArrayNoAutonomous");
        return (IRpcDecoder)rpc.Decoder!;
    }

    private static PayloadData BuildRemoteCharacterUpdatesRpcPayload()
    {
        var componentDataStream = BuildComponentDataStreamPayload(useByteWrapper: false);

        var update = new BitWriter();
        update.WriteIntPacked(3);
        update.WriteIntPacked(32);
        update.WriteUInt32(1234);
        update.WriteIntPacked(4);
        update.WriteIntPacked((uint)componentDataStream.BitCount);
        update.WriteBits(componentDataStream.Bytes, componentDataStream.BitCount);
        update.WriteIntPacked(0);

        var array = new BitWriter();
        array.WriteIntPacked(1);
        array.WriteIntPacked(1);
        array.WriteBits(update);
        array.WriteIntPacked(0);

        var rpc = new BitWriter();
        rpc.WriteBit(false);
        rpc.WriteIntPacked(2);
        rpc.WriteIntPacked((uint)array.BitCount);
        rpc.WriteBits(array);
        rpc.WriteIntPacked(0);
        return new PayloadData(rpc.ToArray(), rpc.BitCount);
    }

    private static PayloadData BuildComponentDataStreamPayload(
        bool useByteWrapper,
        bool useVariant1 = false,
        bool includeSecondMove = false)
    {
        var movement = new BitWriter();
        movement.WriteByte(0x52);
        movement.WriteBits(1, 3);
        WriteMove(movement, useVariant1, timestamp: 42, x: 1.25f, y: 2.5f, z: 3.75f);
        if (includeSecondMove)
        {
            movement.WriteBits(2, 3);
            WriteMove(movement, useVariant1: false, timestamp: 84, x: 10.0f, y: 11.0f, z: 12.0f);
        }

        var payload = new BitWriter();
        payload.WriteUInt16((ushort)movement.BitCount);
        payload.WriteBits(movement);
        var payloadData = new PayloadData(payload.ToArray(), payload.BitCount);

        if (!useByteWrapper)
        {
            return payloadData;
        }

        payload.PadToByte();
        payloadData = new PayloadData(payload.ToArray(), payload.BitCount);
        var wrapped = new BitWriter();
        wrapped.WriteUInt16((ushort)payloadData.Bytes.Length);
        wrapped.WriteBits(payloadData.Bytes, payloadData.BitCount);
        return new PayloadData(wrapped.ToArray(), wrapped.BitCount);
    }

    private static void WriteMove(
        BitWriter movement,
        bool useVariant1,
        uint timestamp,
        float x,
        float y,
        float z)
    {
        movement.WriteBit(useVariant1);
        movement.WriteByte(2);
        movement.WriteByte(3);
        movement.WriteByte(0);
        movement.WriteSerializedInt(0x8000, 0x10000);
        movement.WriteSerializedInt(0x8000, 0x10000);
        movement.WriteSerializedInt(0x8000, 0x10000);
        movement.WriteIntPacked(timestamp);
        movement.WriteSerializedInt(0, 1 << 7);
        movement.WriteSingle(x);
        movement.WriteSingle(y);
        movement.WriteSingle(z);
        movement.WriteBit(false);
        movement.WriteBit(false);
        movement.WriteUInt32(0);

        if (useVariant1)
        {
            movement.WriteBit(true);
            movement.WriteQuantizedVectorScaled(4.0, 5.0, 6.0, scaleFactor: 10, componentBitCount: 10);
        }
        else
        {
            movement.WriteBit(false);
            movement.WriteUInt32(0);
        }

        movement.WriteBit(false);
    }

    private readonly record struct PayloadData(byte[] Bytes, int BitCount);

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }

    private sealed class BitWriter
    {
        private readonly List<bool> _bits = [];

        public int BitCount => _bits.Count;

        public void WriteBit(bool value) => _bits.Add(value);

        public void WriteBits(int value, int bitCount)
        {
            for (var i = 0; i < bitCount; i++)
            {
                WriteBit((value & (1 << i)) != 0);
            }
        }

        public void WriteBits(BitWriter writer) => WriteBits(writer.ToArray(), writer.BitCount);

        public void WriteBits(byte[] bytes, int bitCount)
        {
            for (var i = 0; i < bitCount; i++)
            {
                WriteBit((bytes[i >> 3] & (1 << (i & 7))) != 0);
            }
        }

        public void WriteByte(byte value) => WriteBits(value, 8);

        public void WriteUInt16(ushort value)
        {
            WriteByte((byte)value);
            WriteByte((byte)(value >> 8));
        }

        public void WriteUInt32(uint value)
        {
            WriteByte((byte)value);
            WriteByte((byte)(value >> 8));
            WriteByte((byte)(value >> 16));
            WriteByte((byte)(value >> 24));
        }

        public void WriteSingle(float value) => WriteUInt32(BitConverter.SingleToUInt32Bits(value));

        public void WriteIntPacked(uint value)
        {
            do
            {
                var byteValue = (byte)((value & 0x7F) << 1);
                value >>= 7;
                if (value != 0)
                {
                    byteValue |= 1;
                }

                WriteByte(byteValue);
            } while (value != 0);
        }

        public void WriteSerializedInt(uint value, int maxValue)
        {
            uint writtenValue = 0;
            for (uint mask = 1; writtenValue + mask < maxValue; mask <<= 1)
            {
                var bit = (value & mask) != 0;
                WriteBit(bit);
                if (bit)
                {
                    writtenValue |= mask;
                }
            }
        }

        public void WriteQuantizedVectorScaled(
            double x,
            double y,
            double z,
            int scaleFactor,
            int componentBitCount)
        {
            var info = (uint)(componentBitCount | (1 << 6));
            WriteSerializedInt(info, 1 << 7);
            WriteSignedBits((long)Math.Round(x * scaleFactor), componentBitCount);
            WriteSignedBits((long)Math.Round(y * scaleFactor), componentBitCount);
            WriteSignedBits((long)Math.Round(z * scaleFactor), componentBitCount);
        }

        private void WriteSignedBits(long value, int bitCount)
        {
            var mask = bitCount == 64 ? ulong.MaxValue : (1UL << bitCount) - 1;
            WriteBits((ulong)value & mask, bitCount);
        }

        private void WriteBits(ulong value, int bitCount)
        {
            for (var i = 0; i < bitCount; i++)
            {
                WriteBit((value & (1UL << i)) != 0);
            }
        }

        public void PadToByte()
        {
            while (_bits.Count % 8 != 0)
            {
                WriteBit(false);
            }
        }

        public byte[] ToArray()
        {
            var bytes = new byte[(_bits.Count + 7) / 8];
            for (var i = 0; i < _bits.Count; i++)
            {
                if (_bits[i])
                {
                    bytes[i >> 3] |= (byte)(1 << (i & 7));
                }
            }

            return bytes;
        }
    }
}
