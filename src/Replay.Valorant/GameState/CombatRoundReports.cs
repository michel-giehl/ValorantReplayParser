using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed record CombatRoundReportUpdate(
    int Index,
    int? RoundNumber,
    IReadOnlyList<CharacterCombatReportUpdate> Reports);

public sealed record CharacterCombatReportUpdate(
    int Index,
    int? RoundNumber,
    IReadOnlyList<ParticipantInteractionUpdate> Interactions,
    uint? ResurrectorPlayerState,
    bool? Died);

public sealed record ParticipantInteractionUpdate(
    int Index,
    string? Subject,
    string? Team,
    uint? CharacterIcon,
    float? DamageDealt,
    int? HitsDealt,
    float? DamageReceived,
    int? HitsReceived,
    bool? DidKill,
    byte? AssistType,
    uint? KillerPlayerState,
    bool? WasKiller,
    int? CombatReportIndex,
    IReadOnlyList<CombatInteractionUpdate> DealtInteractions,
    IReadOnlyList<CombatInteractionUpdate> ReceivedInteractions,
    IReadOnlyList<uint> DestroyedArmor);

public sealed record CombatInteractionUpdate(
    int Index,
    uint? DamageType,
    IReadOnlyList<RegionalDamageInteractionUpdate> Regions);

public sealed record RegionalDamageInteractionUpdate(
    int Index,
    byte? Region,
    int? Hits,
    float? Damage,
    bool? IsWallPen,
    bool? IsKill,
    uint? DestroyedArmor);

internal sealed class CompatibleCombatRoundReportsDecoder : IFieldDecoder
{
    private readonly CombatRoundReportsDecoder _decoder = new();
    private readonly IFieldDecoder _fallback =
        ValorantPayloadDecoders.CapturedPayload("TArray<FRoundReports>");

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
            catch (OverflowException exception)
            {
                fallbackReason = exception.Message;
            }
        }
        context.Diagnostics?.Add(new ReplayDiagnostic(
            ReplayDiagnosticCode.RawPayloadFallback,
            $"Field '{context.FieldName}' fell back to raw payload: {fallbackReason ?? "unsupported layout"}",
            context.CurrentPacketId,
            context.ChannelIndex,
            context.CurrentTimeSeconds,
            context.ExportGroupPath,
            context.FieldName));
        return _fallback.Decode(ref context, archive);
    }
}

public sealed class CombatRoundReportsDecoder : IFieldDecoder
{
    private const int MaxItems = 256;
    private const int MaxFields = 128;
    private const int MaxFieldPayloadBits = 256 * 1024;

    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        var reports = ReadArray(archive, ReadRound);
        archive.EnsureFullyConsumed(nameof(CombatRoundReportsDecoder));
        return DecodedFieldValue.FromObject(reports);
    }

    private static CombatRoundReportUpdate ReadRound(int index, FBitArchive archive)
    {
        int? roundNumber = null;
        IReadOnlyList<CharacterCombatReportUpdate> reports = [];
        ReadFields(archive, (handle, field) =>
        {
            if (handle == 3) roundNumber = ReadInt32(field);
            if (handle == 4) reports = ReadArray(field, ReadCharacterReport);
        });
        return new CombatRoundReportUpdate(index, roundNumber, reports);
    }

    private static CharacterCombatReportUpdate ReadCharacterReport(int index, FBitArchive archive)
    {
        int? roundNumber = null;
        IReadOnlyList<ParticipantInteractionUpdate> interactions = [];
        uint? resurrector = null;
        bool? died = null;
        ReadFields(archive, (handle, field) =>
        {
            if (handle == 5) roundNumber = ReadInt32(field);
            if (handle == 10) interactions = ReadArray(field, ReadParticipant);
            if (handle == 98) resurrector = ReadNetGuid(field);
            if (handle == 103) died = ReadBool(field);
        });
        return new CharacterCombatReportUpdate(index, roundNumber, interactions, resurrector, died);
    }

    private static ParticipantInteractionUpdate ReadParticipant(int index, FBitArchive archive)
    {
        var state = new ParticipantState(index);
        ReadFields(archive, state.Read);
        return state.Build();
    }

    private static IReadOnlyList<CombatInteractionUpdate> ReadCombatInteractions(
        FBitArchive archive,
        int regionsHandle,
        int regionHandle)
    {
        return ReadArray(archive, (index, item) =>
        {
            uint? damageType = null;
            IReadOnlyList<RegionalDamageInteractionUpdate> regions = [];
            ReadFields(item, (handle, field) =>
            {
                if (handle == regionsHandle)
                {
                    regions = ReadRegionalInteractions(field, regionHandle);
                }
            });
            return new CombatInteractionUpdate(index, damageType, regions);
        });
    }

    private static IReadOnlyList<RegionalDamageInteractionUpdate> ReadRegionalInteractions(
        FBitArchive archive,
        int regionHandle)
    {
        return ReadArray(archive, (index, item) =>
        {
            byte? region = null;
            int? hits = null;
            float? damage = null;
            bool? wallPen = null;
            bool? kill = null;
            uint? armor = null;
            ReadFields(item, (handle, field) =>
            {
                if (handle == regionHandle) region = TryRead(field, ReadByte);
                if (handle == regionHandle + 1) hits = TryRead(field, ReadInt32);
                if (handle == regionHandle + 2) damage = TryRead(field, ReadFloat);
                if (handle == regionHandle + 3) wallPen = TryRead(field, ReadBool);
                if (handle == regionHandle + 4) kill = TryRead(field, ReadBool);
                if (handle == regionHandle + 5) armor = TryRead(field, ReadNetGuid);
            });
            return new RegionalDamageInteractionUpdate(
                index, region, hits, damage, wallPen, kill, armor);
        });
    }

    private static T[] ReadArray<T>(FBitArchive archive, Func<int, FBitArchive, T> readItem)
    {
        var declaredCount = ReadCount(archive);
        var updates = new List<T>();
        while (ReadIndex(archive, declaredCount) is { } index)
        {
            updates.Add(readItem(index, archive));
        }
        return updates.ToArray();
    }

    private static void ReadFields(FBitArchive archive, Action<int, FBitArchive> readField)
    {
        for (var count = 0; count < MaxFields; count++)
        {
            var encodedHandle = archive.ReadIntPacked();
            if (encodedHandle == 0) return;
            using var field = ReadFieldPayload(archive);
            readField(checked((int)encodedHandle - 1), field);
            field.SkipRemaining();
        }
        throw InvalidCount(archive, MaxFields, "Combat report contains too many fields.");
    }

    private static FBitArchive ReadFieldPayload(FBitArchive archive)
    {
        var bitCount = archive.ReadIntPacked();
        if (bitCount <= MaxFieldPayloadBits && bitCount <= archive.BitsRemaining)
        {
            return archive.ReadSubArchive(checked((int)bitCount));
        }
        throw InvalidCount(archive, bitCount, "Invalid combat report field bit count.");
    }

    private static int ReadCount(FBitArchive archive)
    {
        var count = archive.ReadIntPacked();
        if (count <= MaxItems) return checked((int)count);
        throw InvalidCount(archive, count, "Combat report array is too large.");
    }

    private static int? ReadIndex(FBitArchive archive, int declaredCount)
    {
        var encodedIndex = archive.ReadIntPacked();
        if (encodedIndex == 0) return null;
        var index = checked((int)encodedIndex - 1);
        if (index < declaredCount) return index;
        throw InvalidCount(archive, encodedIndex, "Combat report update index exceeds array size.");
    }

    private static int ReadInt32(FBitArchive archive) => archive.ReadInt32();
    private static float ReadFloat(FBitArchive archive) => archive.ReadSingle();
    private static bool ReadBool(FBitArchive archive) => archive.ReadBit();
    private static byte ReadByte(FBitArchive archive) =>
        checked((byte)archive.ReadBitsToUInt64(checked((int)archive.BitsRemaining)));
    private static uint ReadNetGuid(FBitArchive archive) => archive.ReadIntPacked();
    private static string ReadString(FBitArchive archive) => archive.ReadFString();
    private static string ReadName(FBitArchive archive) => archive.ReadFName();

    private static T? TryRead<T>(FBitArchive archive, Func<FBitArchive, T> read)
        where T : struct
    {
        using var checkpoint = archive.CreateCheckpoint();
        try
        {
            var value = read(archive);
            if (!archive.AtEnd) return null;
            checkpoint.Commit();
            return value;
        }
        catch (ArchiveReadException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static ArchiveReadException InvalidCount(
        FBitArchive archive,
        long requested,
        string message) =>
        new(
            ArchiveErrorCode.InvalidCount,
            nameof(CombatRoundReportsDecoder),
            archive.Position,
            archive.Length,
            requested,
            message);

    private sealed class ParticipantState(int index)
    {
        private IReadOnlyList<CombatInteractionUpdate> _dealtInteractions = [];
        private IReadOnlyList<CombatInteractionUpdate> _receivedInteractions = [];
        private string? _subject;
        private string? _team;
        private uint? _characterIcon;
        private float? _damageDealt;
        private int? _hitsDealt;
        private float? _damageReceived;
        private int? _hitsReceived;
        private bool? _didKill;
        private byte? _assistType;
        private uint? _killerPlayerState;
        private bool? _wasKiller;
        private int? _combatReportIndex;

        public void Read(int handle, FBitArchive field)
        {
            switch (handle)
            {
                case 11: _subject = ReadString(field); break;
                case 12: _team = ReadName(field); break;
                case 13: _characterIcon = ReadNetGuid(field); break;
                case 18: _damageDealt = ReadFloat(field); break;
                case 19: _hitsDealt = ReadInt32(field); break;
                case 20: _damageReceived = ReadFloat(field); break;
                case 21: _hitsReceived = ReadInt32(field); break;
                case 22: _didKill = ReadBool(field); break;
                case 23: _assistType = ReadByte(field); break;
                case 24: _killerPlayerState = ReadNetGuid(field); break;
                case 25: _wasKiller = ReadBool(field); break;
                case 26: _dealtInteractions = ReadCombatInteractions(field, 44, 45); break;
                case 61: _receivedInteractions = ReadCombatInteractions(field, 79, 80); break;
                case 96: _combatReportIndex = ReadInt32(field); break;
            }
        }

        public ParticipantInteractionUpdate Build()
        {
            var armor = _dealtInteractions
                .Concat(_receivedInteractions)
                .SelectMany(interaction => interaction.Regions)
                .Where(region => region.DestroyedArmor.HasValue)
                .Select(region => region.DestroyedArmor!.Value)
                .ToArray();
            return new(
                index,
                _subject,
                _team,
                _characterIcon,
                _damageDealt,
                _hitsDealt,
                _damageReceived,
                _hitsReceived,
                _didKill,
                _assistType,
                _killerPlayerState,
                _wasKiller,
                _combatReportIndex,
                _dealtInteractions,
                _receivedInteractions,
                armor);
        }
    }
}
