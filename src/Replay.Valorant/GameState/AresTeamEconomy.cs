using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed record AresTeamEconomyUpdate(
    int Index,
    uint? ReplicationId,
    int? LoadoutValue,
    int? AverageLoadoutValue);

internal sealed class AresTeamEconomyDecoder : IFieldDecoder
{
    private const int MaxTeams = 8;
    private const int MaxFields = 4;
    private const int MaxFieldPayloadBits = 64 * 1024;

    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        var count = checked((int)archive.ReadIntPacked());
        if (count > MaxTeams) throw InvalidCount(archive, count);
        var values = ReadUpdates(archive, count);
        archive.EnsureFullyConsumed(nameof(AresTeamEconomyDecoder));
        return DecodedFieldValue.FromObject(values);
    }

    private static AresTeamEconomyUpdate[] ReadUpdates(FBitArchive archive, int count)
    {
        var result = new List<AresTeamEconomyUpdate>();
        while (ReadIndex(archive, count) is { } index)
        {
            result.Add(ReadUpdate(archive, index));
        }
        return result.ToArray();
    }

    private static int? ReadIndex(FBitArchive archive, int count)
    {
        var encoded = archive.ReadIntPacked();
        if (encoded == 0) return null;
        var index = checked((int)encoded - 1);
        if (index < count) return index;
        throw InvalidCount(archive, encoded);
    }

    private static AresTeamEconomyUpdate ReadUpdate(FBitArchive archive, int index)
    {
        uint? replicationId = null;
        int? loadout = null;
        int? average = null;
        for (var count = 0; count < MaxFields; count++)
        {
            var encoded = archive.ReadIntPacked();
            if (encoded == 0)
            {
                return new(index, replicationId, loadout, average);
            }
            using var field = ReadField(archive);
            switch (encoded - 1)
            {
                case 56: replicationId = field.ReadIntPacked(); break;
                case 57: loadout = field.ReadInt32(); break;
                case 58: average = field.ReadInt32(); break;
                default: throw new UnsupportedTeamEconomyLayoutException(encoded - 1);
            }
            field.EnsureFullyConsumed($"FAresTeamEconomy field {encoded - 1}");
        }
        throw InvalidCount(archive, MaxFields);
    }

    private static FBitArchive ReadField(FBitArchive archive)
    {
        var bits = archive.ReadIntPacked();
        if (bits <= MaxFieldPayloadBits && bits <= archive.BitsRemaining)
        {
            return archive.ReadSubArchive(checked((int)bits));
        }
        throw new ArchiveReadException(
            ArchiveErrorCode.InvalidBitCount,
            nameof(AresTeamEconomyDecoder),
            archive.Position,
            archive.Length,
            bits);
    }

    private static ArchiveReadException InvalidCount(FBitArchive archive, long count) =>
        new(
            ArchiveErrorCode.InvalidCount,
            nameof(AresTeamEconomyDecoder),
            archive.Position,
            archive.Length,
            count);
}

internal sealed class CompatibleAresTeamEconomyDecoder : IFieldDecoder
{
    private readonly AresTeamEconomyDecoder _decoder = new();
    private readonly IFieldDecoder _fallback =
        ValorantPayloadDecoders.RawPayload("TArray<FAresTeamEconomy>");

    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        string? fallbackReason = null;
        using (var checkpoint = archive.CreateCheckpoint())
        {
            try
            {
                var value = _decoder.Decode(ref context, archive);
                checkpoint.Commit();
                return value;
            }
            catch (ArchiveReadException exception)
            {
                fallbackReason = exception.Message;
            }
            catch (UnsupportedTeamEconomyLayoutException exception)
            {
                fallbackReason = exception.Message;
            }
        }
        context.Diagnostics?.Add(new ReplayDiagnostic(
            ReplayDiagnosticCode.RawPayloadFallback,
            $"Field '{context.FieldName}' fell back to raw payload: {fallbackReason ?? "unsupported field layout"}",
            context.CurrentPacketId,
            context.ChannelIndex,
            context.CurrentTimeSeconds,
            context.ExportGroupPath,
            context.FieldName));
        return _fallback.Decode(ref context, archive);
    }
}

internal sealed class UnsupportedTeamEconomyLayoutException(uint handle)
    : Exception($"Unknown FAresTeamEconomy field handle {handle}.");
