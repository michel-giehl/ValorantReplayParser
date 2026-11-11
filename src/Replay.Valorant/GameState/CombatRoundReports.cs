using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Diagnostics;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

/// <summary>A RepLayout array length and its sparse element updates; omitted fields remain null.</summary>
public sealed record CombatReportArrayUpdate<T>(int DeclaredCount, IReadOnlyList<T> Updates);

public sealed record CombatRoundReportUpdate(
    int Index,
    int? RoundNumber,
    CombatReportArrayUpdate<CharacterCombatReportUpdate>? Reports);

public sealed record CharacterCombatReportUpdate(
    int Index,
    int? RoundNumber,
    CombatReportArrayUpdate<ParticipantInteractionUpdate>? Interactions,
    uint? ResurrectorPlayerState,
    bool? Died)
{
    public CombatTimestamp? LastInteractionTime { get; init; }
    public CombatTimestamp? RespawnTime { get; init; }
    public FVector? DeathLocation { get; init; }
    public CombatTimestamp? DeathTime { get; init; }
}

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
    CombatReportArrayUpdate<CombatInteractionUpdate>? DealtInteractions,
    CombatReportArrayUpdate<CombatInteractionUpdate>? ReceivedInteractions,
    IReadOnlyList<uint> DestroyedArmor)
{
    public CombatTimestamp? LastInteractionTime { get; init; }
}

public sealed record CombatInteractionUpdate(
    int Index,
    uint? DamageType,
    CombatReportArrayUpdate<RegionalDamageInteractionUpdate>? Regions)
{
    public CombatTimestamp? StartTime { get; init; }
    public CombatTimestamp? EndTime { get; init; }

    /// <summary>Damage icon object reference, replicated since 13.10.</summary>
    public uint? DamageIcon { get; init; }
}

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
        string? fallbackReason;
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
            $"Field '{context.FieldName}' fell back to raw payload: {fallbackReason}",
            context.CurrentPacketId,
            context.ChannelIndex,
            context.CurrentTimeSeconds,
            context.ExportGroupPath,
            context.FieldName));
        return _fallback.Decode(ref context, archive);
    }
}

public sealed record CombatTimestamp(uint? HudConfig, float? StateRemainingTime, float? GameTime, byte? GamePhase);

/// <summary>Combat report handles that move between releases; handles up to DealtIteractions' DamageType are shared.</summary>
internal readonly record struct CombatReportHandles(
    bool HasDamageIcon,
    int ReceivedInteractions,
    int CombatReportIndex,
    int ResurrectorPlayerState)
{
    public const int DealtInteractions = 26;

    public static CombatReportHandles Release1309 { get; } = new(false, 61, 96, 98);

    /// <summary>13.10 added DamageIcon after DamageType in every combat interaction, shifting every later handle.</summary>
    public static CombatReportHandles Release1310 { get; } = new(true, 62, 98, 100);

    /// <summary>Handle of the RegionalDamageInteractions array of the combat interactions at <paramref name="interactionsHandle"/>.</summary>
    public int RegionsOf(int interactionsHandle) => interactionsHandle + (HasDamageIcon ? 19 : 18);
}

public sealed class CombatRoundReportsDecoder : IFieldDecoder
{
    private readonly CombatReportHandles _handles;

    public CombatRoundReportsDecoder()
        : this(CombatReportHandles.Release1309)
    {
    }

    internal CombatRoundReportsDecoder(CombatReportHandles handles)
    {
        _handles = handles;
        Reports = new NestedDecoder(a => ReadArray(a, ReadCharacterReport));
        Interactions = new NestedDecoder(a => ReadArray(a, ReadParticipant));
        DealtInteractions = new NestedDecoder(a => ReadCombatInteractions(a, CombatReportHandles.DealtInteractions));
        ReceivedInteractions = new NestedDecoder(a => ReadCombatInteractions(a, handles.ReceivedInteractions));
        RegionalInteractions = new NestedDecoder(
            a => ReadRegionalInteractions(a, handles.RegionsOf(CombatReportHandles.DealtInteractions) + 1));
    }

    internal IFieldDecoder Reports { get; }
    internal IFieldDecoder Interactions { get; }
    internal IFieldDecoder DealtInteractions { get; }
    internal IFieldDecoder ReceivedInteractions { get; }
    internal IFieldDecoder RegionalInteractions { get; }

    private sealed class NestedDecoder(Func<FBitArchive, object> read) : IFieldDecoder
    {
        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            var value = read(archive);
            archive.EnsureFullyConsumed(nameof(CombatRoundReportsDecoder));
            return DecodedFieldValue.FromObject(value);
        }
    }

    private const int MaxItems = 256;
    private const int MaxFields = 128;
    private const int MaxFieldPayloadBits = 256 * 1024;

    public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
    {
        var reports = ReadArray(archive, ReadRound);
        archive.EnsureFullyConsumed(nameof(CombatRoundReportsDecoder));
        return DecodedFieldValue.FromObject(reports);
    }

    private CombatRoundReportUpdate ReadRound(int index, FBitArchive archive)
    {
        int? roundNumber = null;
        CombatReportArrayUpdate<CharacterCombatReportUpdate>? reports = null;
        ReadFields(archive, (handle, field) =>
        {
            if (handle == 3) roundNumber = ReadInt32(field);
            if (handle == 4) reports = ReadArray(field, ReadCharacterReport);
        });
        return new CombatRoundReportUpdate(index, roundNumber, reports);
    }

    private CharacterCombatReportUpdate ReadCharacterReport(int index, FBitArchive archive)
    {
        int? roundNumber = null;
        CombatReportArrayUpdate<ParticipantInteractionUpdate>? interactions = null;
        uint? resurrector = null;
        bool? died = null;
        FVector? deathLocation = null;
        var resurrectorHandle = _handles.ResurrectorPlayerState;
        var interactionTime = new TimestampState(6);
        var respawnTime = new TimestampState(resurrectorHandle + 1);
        var deathTime = new TimestampState(resurrectorHandle + 7);
        ReadFields(archive, (handle, field) =>
        {
            if (handle == 5) roundNumber = ReadInt32(field);
            if (handle == 10) interactions = ReadArray(field, ReadParticipant);
            if (handle == resurrectorHandle) resurrector = ReadNetGuid(field);
            if (handle == resurrectorHandle + 5) died = ReadBool(field);
            if (handle == resurrectorHandle + 6) deathLocation = ReadVector(field);
            interactionTime.Read(handle, field);
            respawnTime.Read(handle, field);
            deathTime.Read(handle, field);
        });
        return new CharacterCombatReportUpdate(index, roundNumber, interactions, resurrector, died)
        {
            LastInteractionTime = interactionTime.Build(), RespawnTime = respawnTime.Build(),
            DeathTime = deathTime.Build(), DeathLocation = deathLocation,
        };
    }

    private ParticipantInteractionUpdate ReadParticipant(int index, FBitArchive archive)
    {
        var state = new ParticipantState(index, this);
        ReadFields(archive, state.Read);
        return state.Build();
    }

    private CombatReportArrayUpdate<CombatInteractionUpdate> ReadCombatInteractions(
        FBitArchive archive,
        int interactionsHandle)
    {
        var regionsHandle = _handles.RegionsOf(interactionsHandle);
        return ReadArray(archive, (index, item) =>
        {
            uint? damageType = null;
            uint? damageIcon = null;
            CombatReportArrayUpdate<RegionalDamageInteractionUpdate>? regions = null;
            var start = new TimestampState(interactionsHandle + 1);
            var end = new TimestampState(interactionsHandle + 5);
            ReadFields(item, (handle, field) =>
            {
                if (handle == interactionsHandle + 9) damageType = ReadNetGuid(field);
                if (_handles.HasDamageIcon && handle == interactionsHandle + 10) damageIcon = ReadNetGuid(field);
                start.Read(handle, field);
                end.Read(handle, field);
                if (handle == regionsHandle)
                {
                    regions = ReadRegionalInteractions(field, regionsHandle + 1);
                }
            });
            return new CombatInteractionUpdate(index, damageType, regions)
            { StartTime = start.Build(), EndTime = end.Build(), DamageIcon = damageIcon };
        });
    }

    private static CombatReportArrayUpdate<RegionalDamageInteractionUpdate> ReadRegionalInteractions(
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
                switch (handle - regionHandle)
                {
                    case 0: region = ReadByte(field); break;
                    case 1: hits = ReadInt32(field); break;
                    case 2: damage = ReadFloat(field); break;
                    case 3: wallPen = ReadBool(field); break;
                    case 4: kill = ReadBool(field); break;
                    case 5: armor = ReadNetGuid(field); break;
                }
            });
            return new RegionalDamageInteractionUpdate(
                index, region, hits, damage, wallPen, kill, armor);
        });
    }

    private static CombatReportArrayUpdate<T> ReadArray<T>(FBitArchive archive, Func<int, FBitArchive, T> readItem)
    {
        var declaredCount = ReadCount(archive);
        var updates = new List<T>();
        while (ReadIndex(archive, declaredCount) is { } index)
        {
            updates.Add(readItem(index, archive));
        }
        return new CombatReportArrayUpdate<T>(declaredCount, updates.ToArray());
    }

    private static void ReadFields(FBitArchive archive, Action<int, FBitArchive> readField)
    {
        for (var count = 0; count < MaxFields; count++)
        {
            var encodedHandle = archive.ReadIntPacked();
            if (encodedHandle == 0) return;
            using var field = ReadFieldPayload(archive);
            readField(checked((int)encodedHandle - 1), field);
            field.EnsureFullyConsumed($"Combat report field {encodedHandle - 1}");
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

    private static FVector ReadVector(FBitArchive archive)
    {
        var context = new FieldDecodeContext();
        return PrimitiveDecoders.Vector.Decode(ref context, archive).VectorValue;
    }

    private sealed class TimestampState(int firstHandle)
    {
        private uint? _hud;
        private float? _remaining;
        private float? _time;
        private byte? _phase;
        public void Read(int handle, FBitArchive field)
        {
            if (handle == firstHandle) _hud = ReadNetGuid(field);
            if (handle == firstHandle + 1) _remaining = ReadFloat(field);
            if (handle == firstHandle + 2) _time = ReadFloat(field);
            if (handle == firstHandle + 3) _phase = ReadByte(field);
        }
        public CombatTimestamp? Build() => _hud.HasValue || _remaining.HasValue || _time.HasValue || _phase.HasValue
            ? new(_hud, _remaining, _time, _phase) : null;
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

    private sealed class ParticipantState(int index, CombatRoundReportsDecoder decoder)
    {
        private CombatReportArrayUpdate<CombatInteractionUpdate>? _dealtInteractions;
        private CombatReportArrayUpdate<CombatInteractionUpdate>? _receivedInteractions;
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
        private readonly TimestampState _lastInteractionTime = new(14);

        public void Read(int handle, FBitArchive field)
        {
            _lastInteractionTime.Read(handle, field);
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
                case CombatReportHandles.DealtInteractions:
                    _dealtInteractions = decoder.ReadCombatInteractions(field, handle);
                    break;
            }

            var handles = decoder._handles;
            if (handle == handles.ReceivedInteractions)
            {
                _receivedInteractions = decoder.ReadCombatInteractions(field, handle);
            }

            if (handle == handles.CombatReportIndex) _combatReportIndex = ReadInt32(field);
        }

        public ParticipantInteractionUpdate Build()
        {
            var armor = (_dealtInteractions?.Updates ?? [])
                .Concat(_receivedInteractions?.Updates ?? [])
                .SelectMany(interaction => interaction.Regions?.Updates ?? [])
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
                armor) { LastInteractionTime = _lastInteractionTime.Build() };
        }
    }
}
