using Replay.Models.Protocol;
using Replay.Models.Unreal;

namespace Replay.Encoding.Archives;

public class FBinaryArchive : ByteArchiveReader
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

    public string ReadFString() => ReadFStringCore(null);

    public string ReadFString(int maxSerializedBytes) => ReadFStringCore(maxSerializedBytes);

    private string ReadFStringCore(int? maxSerializedBytes)
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

        if (maxSerializedBytes is not null && byteCount > maxSerializedBytes.Value)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidCount, nameof(ReadFString), Position, Length,
                byteCount, $"Serialized FString byte count {byteCount} exceeds maximum {maxSerializedBytes.Value}.");
        }

        var bytes = ReadBytes(byteCount);
        return encoding.GetString(bytes.Span).TrimEnd('\0');
    }

    public Guid ReadGuid()
    {
        var a = ReadUInt32();
        var b = ReadUInt32();
        var c = ReadUInt32();
        var d = ReadUInt32();

        return Guid.Parse(
            $"{a:X8}-{b >> 16:X4}-{b & 0xFFFF:X4}-{c >> 16:X4}-{c & 0xFFFF:X4}{d:X8}");
    }

    public FVector ReadFVector() => new(ReadSingle(), ReadSingle(), ReadSingle());

    public FQuat ReadFQuat() => new(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());

    public FTransform ReadFTransform() => new(ReadFQuat(), ReadFVector(), ReadFVector());

    public FTransform ReadFTransfrom() => ReadFTransform();

    public bool ReadUInt32AsBool() => ReadUInt32() != 0;

    public bool ReadBoolean() => ReadByte() != 0;

    public string ReadFName()
    {
        var isHardcoded = ReadBoolean();
        if (isHardcoded)
        {
            var nameIndex = ReadIntPacked();
            return nameIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var name = ReadFString();
        _ = ReadInt32();
        return name;
    }

    public byte[] ReadByteArray(int maxCount)
    {
        var count = ReadInt32();
        if (count < 0 || count > maxCount)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidCount, nameof(ReadByteArray), Position, Length,
                count, $"Serialized byte array count {count} is outside the valid range 0..{maxCount}.");
        }

        return ReadBytes(count).ToArray();
    }

    public CustomVersionContainer ReadCustomVersionContainer()
    {
        var count = ReadInt32();
        ValidateArrayCount(count, Constants.MaxCustomVersionCount, nameof(ReadCustomVersionContainer));

        var container = new CustomVersionContainer();
        for (var i = 0; i < count; i++)
        {
            container.Versions.Add(new CustomVersionEntry(ReadGuid(), ReadInt32(), string.Empty));
        }

        return container;
    }

    public TEnum ReadUInt32AsEnum<TEnum>() where TEnum : struct, Enum =>
        (TEnum)Enum.ToObject(typeof(TEnum), ReadUInt32());

    public TEnum ReadByteAsEnum<TEnum>() where TEnum : struct, Enum =>
        (TEnum)Enum.ToObject(typeof(TEnum), ReadByte());

    public T[] ReadArray<T>(Func<T> readValue) => ReadArray(readValue, int.MaxValue);

    public T[] ReadArray<T>(Func<T> readValue, int maxCount)
    {
        var count = ReadInt32();
        ValidateArrayCount(count, maxCount, nameof(ReadArray));

        var values = new T[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = readValue();
        }

        return values;
    }

    public (T First, U Second)[] ReadTupleArray<T, U>(Func<T> readFirst, Func<U> readSecond) =>
        ReadTupleArray(readFirst, readSecond, int.MaxValue);

    public (T First, U Second)[] ReadTupleArray<T, U>(Func<T> readFirst, Func<U> readSecond, int maxCount)
    {
        var count = ReadInt32();
        ValidateArrayCount(count, maxCount, nameof(ReadTupleArray));

        var values = new (T First, U Second)[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (readFirst(), readSecond());
        }

        return values;
    }

    private void ValidateArrayCount(int count, int maxCount, string operation)
    {
        if (count < 0 || count > maxCount)
        {
            throw new ArchiveReadException(ArchiveErrorCode.InvalidCount, operation, Position, Length,
                count, $"Serialized array count {count} is outside the valid range 0..{maxCount}.");
        }
    }
}

public class UninitializedBinaryArchive : FBinaryArchive
{
    public UninitializedBinaryArchive() : base([])
    {
    }
}
