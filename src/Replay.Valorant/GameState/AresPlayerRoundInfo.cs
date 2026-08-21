using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed record AresPlayerRoundInfo(
    int RoundNumber,
    int? StartOfRoundMoney,
    int? StartOfRoundLoadoutValue,
    int? EndOfRoundMoney,
    int? EndOfRoundLoadoutValue);

internal sealed class AresPlayerRoundInfoDecoder : IFieldDecoder
{
    private const int MaxRoundCount = 128;
    private const int MaxFieldsPerUpdate = 5;
    private const int MaxFieldPayloadBits = 64 * 1024;
    private const uint RoundNumberHandle = 40;
    private const uint StartOfRoundMoneyHandle = 41;
    private const uint StartOfRoundLoadoutValueHandle = 42;
    private const uint EndOfRoundMoneyHandle = 43;
    private const uint EndOfRoundLoadoutValueHandle = 44;

    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        if (archive.AtEnd) return DecodedFieldValue.FromObject(Array.Empty<AresPlayerRoundInfo>());

        var infosCount = ReadRoundCount(archive);
        var infos = ReadUpdates(archive, infosCount);
        archive.EnsureFullyConsumed(nameof(AresPlayerRoundInfoDecoder));
        return DecodedFieldValue.FromObject(infos);
    }

    private static int ReadRoundCount(FBitArchive archive)
    {
        var count = archive.ReadIntPacked();
        if (count <= MaxRoundCount) return (int)count;

        throw InvalidCount(archive, count, $"PlayerRoundInfo declared {count} rounds; maximum is {MaxRoundCount}.");
    }

    private static AresPlayerRoundInfo[] ReadUpdates(FBitArchive archive, int infosCount)
    {
        var infos = new List<AresPlayerRoundInfo>();
        while (ReadUpdateIndex(archive, infosCount) is { } roundNumber)
        {
            infos.Add(ReadUpdate(archive, roundNumber));
        }

        return infos.ToArray();
    }

    private static int? ReadUpdateIndex(FBitArchive archive, int infosCount)
    {
        var encodedIndex = archive.ReadIntPacked();
        if (encodedIndex == 0) return null;

        var infoIndex = checked((int)encodedIndex - 1);
        if (infoIndex < infosCount) return infoIndex;

        throw InvalidCount(archive, encodedIndex, $"PlayerRoundInfo update index {infoIndex} exceeds count {infosCount}.");
    }

    private static AresPlayerRoundInfo ReadUpdate(FBitArchive archive, int roundNumber)
    {
        int? startOfRoundMoney = null;
        int? startOfRoundLoadoutValue = null;
        int? endOfRoundMoney = null;
        int? endOfRoundLoadoutValue = null;

        for (var fieldCount = 0; fieldCount <= MaxFieldsPerUpdate; fieldCount++)
        {
            var encodedHandle = archive.ReadIntPacked();
            if (encodedHandle == 0)
            {
                return new AresPlayerRoundInfo(roundNumber, startOfRoundMoney, startOfRoundLoadoutValue, endOfRoundMoney, endOfRoundLoadoutValue);
            }

            if (fieldCount == MaxFieldsPerUpdate) throw TooManyFields(archive);
            ReadField(archive, encodedHandle - 1, ref startOfRoundMoney, ref startOfRoundLoadoutValue, ref endOfRoundMoney, ref endOfRoundLoadoutValue);
        }

        throw TooManyFields(archive);
    }

    private static void ReadField(
        FBitArchive archive,
        uint handle,
        ref int? startOfRoundMoney,
        ref int? startOfRoundLoadoutValue,
        ref int? endOfRoundMoney,
        ref int? endOfRoundLoadoutValue)
    {
        var field = ReadFieldPayload(archive);
        switch (handle)
        {
            case RoundNumberHandle:
                _ = field.ReadInt32();
                break;
            case StartOfRoundMoneyHandle:
                startOfRoundMoney = field.ReadInt32();
                break;
            case StartOfRoundLoadoutValueHandle:
                startOfRoundLoadoutValue = field.ReadInt32();
                break;
            case EndOfRoundMoneyHandle:
                endOfRoundMoney = field.ReadInt32();
                break;
            case EndOfRoundLoadoutValueHandle:
                endOfRoundLoadoutValue = field.ReadInt32();
                break;
            default:
                throw new UnsupportedPlayerRoundInfoLayoutException(handle);
        }

        field.EnsureFullyConsumed($"FAresPlayerRoundInfo field {handle}");
    }

    private static FBitArchive ReadFieldPayload(FBitArchive archive)
    {
        var bitCount = archive.ReadIntPacked();
        if (bitCount <= MaxFieldPayloadBits && bitCount <= archive.BitsRemaining)
        {
            return archive.ReadSubArchive((int)bitCount);
        }

        throw new ArchiveReadException(
            ArchiveErrorCode.InvalidBitCount,
            nameof(AresPlayerRoundInfoDecoder),
            archive.Position,
            archive.Length,
            bitCount);
    }


    private static ArchiveReadException TooManyFields(FBitArchive archive) =>
        InvalidCount(archive, MaxFieldsPerUpdate + 1, "FAresPlayerRoundInfo contains too many fields.");

    private static ArchiveReadException InvalidCount(FBitArchive archive, long requested, string message) =>
        new(
            ArchiveErrorCode.InvalidCount,
            nameof(AresPlayerRoundInfoDecoder),
            archive.Position,
            archive.Length,
            requested,
            message);
}

internal sealed class CompatibleAresPlayerRoundInfoDecoder : IFieldDecoder
{
    private readonly AresPlayerRoundInfoDecoder _release1301 = new();

    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        using (var checkpoint = archive.CreateCheckpoint())
        {
            try
            {
                var value = _release1301.Decode(ref context, archive);
                checkpoint.Commit();
                return value;
            }
            catch (UnsupportedPlayerRoundInfoLayoutException)
            {
            }
        }

        var bitCount = checked((int)archive.BitsRemaining);
        archive.SkipRemaining();
        return DecodedFieldValue.FromObject(new ValorantRawPayload("TArray<FAresPlayerRoundInfo>", bitCount));
    }
}

internal sealed class UnsupportedPlayerRoundInfoLayoutException(uint handle)
    : Exception($"Unknown FAresPlayerRoundInfo field handle {handle}.");
