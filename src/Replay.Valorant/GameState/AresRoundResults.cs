using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed record AresRoundResult(
    int RoundNumber,
    string? WinningTeam,
    AresTeamRole? WinningTeamRole,
    AresRoundOutcome? RoundResult);

public enum AresTeamRole : byte
{
    None = 0,
    Attacker = 1,
    Defender = 2,
    FreeForAll = 3,
    Any = 4,
    RoleCount = 5,
}

public enum AresRoundOutcome : byte
{
    Elimination = 0,
    Defuse = 1,
    Detonate = 2,
    TimeExpired = 3,
    Cheat = 4,
    Surrendered = 5,
    RoundOutcomeCount = 6,
    Invalid = 7,
}

internal sealed class AresRoundResultsDecoder : IFieldDecoder
{
    internal const int MaxRoundCount = 128;
    private const int MaxFieldsPerUpdate = 4;
    private const int MaxFieldPayloadBits = 64 * 1024;
    private readonly AresRoundResultHandles _handles;

    public AresRoundResultsDecoder(AresRoundResultHandles handles)
    {
        _handles = handles;
    }

    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        if (archive.AtEnd) return DecodedFieldValue.FromObject(Array.Empty<AresRoundResult>());

        var roundCount = ReadRoundCount(archive);
        var results = ReadUpdates(archive, roundCount);
        archive.EnsureFullyConsumed(nameof(AresRoundResultsDecoder));
        return DecodedFieldValue.FromObject(results);
    }

    private static int ReadRoundCount(FBitArchive archive)
    {
        var count = archive.ReadIntPacked();
        if (count <= MaxRoundCount) return (int)count;

        throw InvalidCount(archive, count, $"RoundResults declared {count} rounds; maximum is {MaxRoundCount}.");
    }

    private AresRoundResult[] ReadUpdates(FBitArchive archive, int roundCount)
    {
        var results = new List<AresRoundResult>();
        while (ReadUpdateIndex(archive, roundCount) is { } roundNumber)
        {
            results.Add(ReadUpdate(archive, roundNumber));
        }

        return results.ToArray();
    }

    private static int? ReadUpdateIndex(FBitArchive archive, int roundCount)
    {
        var encodedIndex = archive.ReadIntPacked();
        if (encodedIndex == 0) return null;

        var roundNumber = checked((int)encodedIndex - 1);
        if (roundNumber < roundCount) return roundNumber;

        throw InvalidCount(archive, encodedIndex, $"RoundResults update index {roundNumber} exceeds count {roundCount}.");
    }

    private AresRoundResult ReadUpdate(FBitArchive archive, int roundNumber)
    {
        string? winningTeam = null;
        AresTeamRole? winningTeamRole = null;
        AresRoundOutcome? roundResult = null;

        for (var fieldCount = 0; fieldCount <= MaxFieldsPerUpdate; fieldCount++)
        {
            var encodedHandle = archive.ReadIntPacked();
            if (encodedHandle == 0)
            {
                return new AresRoundResult(roundNumber, winningTeam, winningTeamRole, roundResult);
            }

            if (fieldCount == MaxFieldsPerUpdate) throw TooManyFields(archive);
            ReadField(archive, encodedHandle - 1, ref winningTeam, ref winningTeamRole, ref roundResult);
        }

        throw TooManyFields(archive);
    }

    private void ReadField(
        FBitArchive archive,
        uint handle,
        ref string? winningTeam,
        ref AresTeamRole? winningTeamRole,
        ref AresRoundOutcome? roundResult)
    {
        var field = ReadFieldPayload(archive);
        if (handle == _handles.WinningTeam)
        {
            winningTeam = field.ReadFName();
        }
        else if (handle == _handles.WinningTeamRole)
        {
            winningTeamRole = (AresTeamRole)ReadEnum(field);
        }
        else if (handle == _handles.RoundResult)
        {
            roundResult = (AresRoundOutcome)ReadEnum(field);
        }
        else if (handle == _handles.EliminatedTeams)
        {
            field.SkipRemaining();
        }
        else
        {
            throw new UnsupportedRoundResultsLayoutException(handle);
        }

        field.EnsureFullyConsumed($"FAresRoundResult field {handle}");
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
            nameof(AresRoundResultsDecoder),
            archive.Position,
            archive.Length,
            bitCount);
    }

    private static byte ReadEnum(FBitArchive archive)
    {
        if (archive.BitsRemaining is > 0 and <= 8)
        {
            return (byte)archive.ReadBitsToUInt64((int)archive.BitsRemaining);
        }

        throw new ArchiveReadException(
            ArchiveErrorCode.InvalidBitCount,
            nameof(AresRoundResultsDecoder),
            archive.Position,
            archive.Length,
            archive.BitsRemaining);
    }

    private static ArchiveReadException TooManyFields(FBitArchive archive) =>
        InvalidCount(archive, MaxFieldsPerUpdate + 1, "FAresRoundResult contains too many fields.");

    private static ArchiveReadException InvalidCount(FBitArchive archive, long requested, string message) =>
        new(
            ArchiveErrorCode.InvalidCount,
            nameof(AresRoundResultsDecoder),
            archive.Position,
            archive.Length,
            requested,
            message);
}

internal sealed class CompatibleAresRoundResultsDecoder : IFieldDecoder
{
    private readonly AresRoundResultHandles _handles;
    private readonly AresRoundResultsDecoder _decoder;

    public CompatibleAresRoundResultsDecoder(AresRoundResultHandles handles)
    {
        _handles = handles;
        _decoder = new AresRoundResultsDecoder(handles);
    }

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
            catch (UnsupportedRoundResultsLayoutException exception)
            {
                fallbackReason = exception.Message;
            }
        }

        var bitCount = checked((int)archive.BitsRemaining);
        var data = archive.ReadBits(bitCount);
        var release = context.ReplayReleaseVersion?.ToString() ?? "unspecified";
        context.Diagnostics?.Add(new ReplayDiagnostic(
            ReplayDiagnosticCode.RawPayloadFallback,
            $"Field '{context.FieldName}' fell back to raw payload for VALORANT release {release} " +
            $"using RoundResults layout '{_handles.LayoutName}': {fallbackReason ?? "unsupported field layout"}",
            context.CurrentPacketId,
            context.ChannelIndex,
            context.CurrentTimeSeconds,
            context.ExportGroupPath,
            context.FieldName));
        return DecodedFieldValue.FromObject(new ValorantRawPayload("TArray<FAresRoundResult>", bitCount, data));
    }
}

internal readonly record struct AresRoundResultHandles(
    string LayoutName,
    uint WinningTeam,
    uint WinningTeamRole,
    uint RoundResult,
    uint EliminatedTeams)
{
    public static AresRoundResultHandles Release1301 { get; } = new("13.01", 93, 94, 95, 96);
    public static AresRoundResultHandles Release1305 { get; } = new("13.05", 82, 83, 84, 85);
}

internal sealed class UnsupportedRoundResultsLayoutException(uint handle)
    : Exception($"Unknown FAresRoundResult field handle {handle}.")
{
    public uint Handle { get; } = handle;
}
