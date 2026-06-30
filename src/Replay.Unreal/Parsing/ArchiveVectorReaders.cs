using Replay.Encoding.Archives;
using Replay.Models.Unreal;

namespace Replay.Unreal.Parsing;

internal static class ArchiveVectorReaders
{
    private const int FixedNormalBitCount = 16;
    private const int FixedNormalBias = 1 << (FixedNormalBitCount - 1);
    private const int FixedNormalSerializedMax = 1 << FixedNormalBitCount;
    private const int FixedNormalScale = FixedNormalBias - 1;

    public static FVector ReadOptionalQuantizedVector(
        FBitArchive archive,
        FVector defaultVector,
        int scaleFactor)
    {
        if (!archive.ReadBit())
        {
            return defaultVector;
        }

        return archive.ReadBit()
            ? ReadQuantizedVector(archive, scaleFactor)
            : ReadDoubleVector(archive);
    }

    public static FVector ReadQuantizedVector(FBitArchive archive, int scaleFactor)
    {
        var componentBitCountAndExtraInfo = archive.ReadSerializedInt(1 << 7);
        var componentBitCount = (int)(componentBitCountAndExtraInfo & 63U);
        var extraInfo = componentBitCountAndExtraInfo >> 6;

        if (componentBitCount > 0)
        {
            return ReadPackedQuantizedVector(archive, componentBitCount, extraInfo, scaleFactor);
        }

        return extraInfo == 0
            ? ReadFloatVector(archive, scaleFactor)
            : ReadDoubleVector(archive, scaleFactor);
    }

    public static FVector ReadDoubleVector(FBitArchive archive) =>
        new(archive.ReadDouble(), archive.ReadDouble(), archive.ReadDouble())
        {
            Bits = 64,
        };

    public static FVector ReadFloatVector(FBitArchive archive) =>
        new(archive.ReadSingle(), archive.ReadSingle(), archive.ReadSingle())
        {
            Bits = 32,
        };

    public static FVector ReadFixedVectorNormal(FBitArchive archive) =>
        new(ReadFixedNormalComponent(archive), ReadFixedNormalComponent(archive), ReadFixedNormalComponent(archive))
        {
            Bits = FixedNormalBitCount,
            ScaleFactor = FixedNormalScale,
        };

    public static FRotator ReadRotationShort(FBitArchive archive) =>
        new(
            ReadCompressedShortRotatorComponent(archive),
            ReadCompressedShortRotatorComponent(archive),
            ReadCompressedShortRotatorComponent(archive));

    private static FVector ReadPackedQuantizedVector(
        FBitArchive archive,
        int componentBitCount,
        uint extraInfo,
        int scaleFactor)
    {
        var x = archive.ReadBitsToUInt64(componentBitCount);
        var y = archive.ReadBitsToUInt64(componentBitCount);
        var z = archive.ReadBitsToUInt64(componentBitCount);
        var signBit = 1UL << componentBitCount - 1;

        double fX = (long)(x ^ signBit) - (long)signBit;
        double fY = (long)(y ^ signBit) - (long)signBit;
        double fZ = (long)(z ^ signBit) - (long)signBit;

        if (extraInfo > 0)
        {
            fX /= scaleFactor;
            fY /= scaleFactor;
            fZ /= scaleFactor;
        }

        return new FVector(fX, fY, fZ)
        {
            Bits = componentBitCount,
            ScaleFactor = scaleFactor,
        };
    }

    private static FVector ReadFloatVector(FBitArchive archive, int scaleFactor) =>
        new(archive.ReadSingle(), archive.ReadSingle(), archive.ReadSingle())
        {
            Bits = 32,
            ScaleFactor = scaleFactor,
        };

    private static FVector ReadDoubleVector(FBitArchive archive, int scaleFactor) =>
        new(archive.ReadDouble(), archive.ReadDouble(), archive.ReadDouble())
        {
            Bits = 64,
            ScaleFactor = scaleFactor,
        };

    private static double ReadFixedNormalComponent(FBitArchive archive)
    {
        var delta = archive.ReadSerializedInt(FixedNormalSerializedMax);
        return ((int)delta - FixedNormalBias) / (double)FixedNormalScale;
    }

    private static float ReadCompressedShortRotatorComponent(FBitArchive archive) =>
        archive.ReadBit() ? DecompressShortAngle(archive.ReadUInt16()) : 0.0f;

    private static float DecompressShortAngle(ushort value)
    {
        const float scale = 360.0f / 65536.0f;
        return value * scale;
    }
}