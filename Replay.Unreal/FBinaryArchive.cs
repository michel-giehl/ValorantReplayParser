using Replay.Encoding.Archives;

namespace Replay.Unreal;

public sealed class FBinaryArchive : ByteArchiveReader
{
    public FBinaryArchive(Stream input) : base(input)
    {
    }

    public FBinaryArchive(ReadOnlyMemory<byte> input) : base(input)
    {
    }

    public FBinaryArchive(ReadOnlySpan<byte> input) : base(input)
    {
    }

    public string ReadFString()
    {
        var length = ReadInt32();
        if (length == 0)
        {
            return string.Empty;
        }

        var encoding = System.Text.Encoding.UTF8;
        int byteCount;
        if (length < 0)
        {
            if (length == int.MinValue)
            {
                throw new ArchiveReadException(ArchiveErrorCode.InvalidCount, nameof(ReadFString), Position, Length,
                    length);
            }

            encoding = System.Text.Encoding.Unicode;
            byteCount = checked(-length * 2);
        }
        else
        {
            byteCount = length;
        }

        var bytes = ReadBytes(byteCount);
        return encoding.GetString(bytes.Span).TrimEnd('\0');
    }

    public Guid ReadGuid() => new(ReadBytes(16).Span);

    public TEnum ReadUInt32AsEnum<TEnum>() where TEnum : struct, Enum =>
        (TEnum)Enum.ToObject(typeof(TEnum), ReadUInt32());

    public TEnum ReadByteAsEnum<TEnum>() where TEnum : struct, Enum =>
        (TEnum)Enum.ToObject(typeof(TEnum), ReadByte());

    public T[] ReadArray<T>(Func<T> readValue)
    {
        var count = ReadInt32();
        ValidateArrayCount(count, nameof(ReadArray));

        var values = new T[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = readValue();
        }

        return values;
    }

    public (T First, U Second)[] ReadTupleArray<T, U>(Func<T> readFirst, Func<U> readSecond)
    {
        var count = ReadInt32();
        ValidateArrayCount(count, nameof(ReadTupleArray));

        var values = new (T First, U Second)[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (readFirst(), readSecond());
        }

        return values;
    }

    private void ValidateArrayCount(int count, string operation)
    {
        if (count < 0)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidCount, operation, Position, Length, count);
        }
    }
}