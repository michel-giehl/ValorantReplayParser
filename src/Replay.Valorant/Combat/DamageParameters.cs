using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Replay;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Combat;

public static class DamageResponseLayout
{
    /// <summary>
    /// First release known to replicate KillCreditPlayerState. 12.07 and 12.09 do not; 13.00 does.
    /// 12.10 and 12.11 are unverified.
    /// </summary>
    public static ReplayReleaseVersion KillCreditPlayerStateRelease { get; } = new(12, 10);

    /// <summary>KillCreditPlayerState's handle in FNetworkedDamageResponseData. Later handles sit one lower without it.</summary>
    internal const uint KillCreditPlayerStateHandle = 17;

    /// <summary>DamageIcon was inserted after DamageType in the damage response.</summary>
    public static ReplayReleaseVersion DamageIconRelease { get; } = new(13, 10);
}

public abstract class DamageParameters<T> : ExportGroupDescriptor<T>
    where T : DamageParameters<T>
{
    private readonly bool _hasKillCreditPlayerState;
    private readonly bool _hasDamageIcon;

    protected DamageParameters(bool hasKillCreditPlayerState, bool hasDamageIcon = false)
    {
        _hasKillCreditPlayerState = hasKillCreditPlayerState;
        _hasDamageIcon = hasDamageIcon;
    }

    public override ExportCategory Categories => ExportCategory.Gunplay;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public float DamageTaken { get; set; }
    public float DamageDealt { get; set; }
    public bool DamageKilledTarget { get; set; }
    public bool AliveAfterDamage { get; set; }
    public NetObjectReference EventInstigator { get; set; }
    public NetObjectReference DamageCauser { get; set; }
    public FVector? DamageOrigin { get; set; }
    public NetObjectReference? EquippableUsed { get; set; }
    public NetObjectReference DamageType { get; set; }
    public NetObjectReference? DamageIcon { get; set; }
    public DamageLifeChangeEvent?[]? LifeChangeEvents { get; set; }
    public NetObjectReference ChangedComponent { get; set; }
    public float? LifeResult { get; set; }
    public float DeltaLife { get; set; }
    public bool AliveAfterChange { get; set; }
    public NetObjectReference EventInstigatorPawn { get; set; }
    public NetObjectReference DamagerPlayerState { get; set; }
    public NetObjectReference KillCreditPlayerState { get; set; }
    public EAresRegionalDamage? RegionalDamage { get; set; }
    public NetObjectReference Character { get; set; }
    public float NetTimestamp { get; set; }
    public int RespawnNumber { get; set; }
    public int LifeChangeEventIndex { get; set; }
    public int VictimRespawnNumber { get; set; }
    public bool EquippableUsedZoomed { get; set; }
    public bool EquippableUsedInFocusMode { get; set; }

    /// <summary>Maps a 13.09 handle, whose damage response data starts at <paramref name="offset"/>, to this release's handle.</summary>
    protected uint LayoutHandle(uint handle, uint offset = 0)
    {
        var mappedHandle = handle;
        if (!_hasKillCreditPlayerState && handle >= DamageResponseLayout.KillCreditPlayerStateHandle + offset)
            mappedHandle--;
        if (_hasDamageIcon && handle >= 9 + offset)
            mappedHandle++;
        return mappedHandle;
    }

    protected void AddSharedFields(uint offset = 0)
    {
        AddPropertyHandle(offset, x => x.DamageTaken, ExportCategory.Gunplay).Float();
        AddPropertyHandle(1 + offset, x => x.DamageDealt, ExportCategory.Gunplay).Float();
        AddPropertyHandle(2 + offset, "bDamageKilledTarget", x => x.DamageKilledTarget, ExportCategory.Gunplay).Bool();
        AddPropertyHandle(3 + offset, "bAliveAfterDamage", x => x.AliveAfterDamage, ExportCategory.Gunplay).Bool();
        AddPropertyHandle(4 + offset, x => x.EventInstigator, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(5 + offset, x => x.DamageCauser, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(6 + offset, x => x.DamageOrigin, ExportCategory.Gunplay).FVectorNetQuantize100();
        AddPropertyHandle(7 + offset, x => x.EquippableUsed, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(8 + offset, x => x.DamageType, ExportCategory.Gunplay).ObjectNetGuid();
        if (_hasDamageIcon)
            AddPropertyHandle(9 + offset, x => x.DamageIcon, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(LayoutHandle(9 + offset, offset), x => x.LifeChangeEvents, ExportCategory.Gunplay).RepLayoutDynamicArray(
            new VersionedDefinition<DamageLifeChangeEvent>(new(offset + (_hasDamageIcon ? 1u : 0u))));
        AddPropertyHandle(LayoutHandle(10 + offset, offset), x => x.ChangedComponent, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(LayoutHandle(11 + offset, offset), x => x.LifeResult, ExportCategory.Gunplay).Float();
        AddPropertyHandle(LayoutHandle(12 + offset, offset), x => x.DeltaLife, ExportCategory.Gunplay).Float();
        AddPropertyHandle(LayoutHandle(13 + offset, offset), "bAliveAfterChange", x => x.AliveAfterChange, ExportCategory.Gunplay).Bool();
        AddPropertyHandle(LayoutHandle(15 + offset, offset), x => x.EventInstigatorPawn, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(LayoutHandle(16 + offset, offset), x => x.DamagerPlayerState, ExportCategory.Gunplay).ObjectNetGuid();
        if (_hasKillCreditPlayerState)
            AddPropertyHandle(LayoutHandle(17 + offset, offset), x => x.KillCreditPlayerState, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(LayoutHandle(18 + offset, offset), x => x.RegionalDamage, ExportCategory.Gunplay).EnumByte();
        AddPropertyHandle(LayoutHandle(19 + offset, offset), x => x.Character, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(LayoutHandle(20 + offset, offset), x => x.NetTimestamp, ExportCategory.Gunplay).Float();
        AddPropertyHandle(LayoutHandle(21 + offset, offset), x => x.RespawnNumber, ExportCategory.Gunplay).Int32();
        AddPropertyHandle(LayoutHandle(22 + offset, offset), x => x.LifeChangeEventIndex, ExportCategory.Gunplay).Int32();
        AddPropertyHandle(LayoutHandle(23 + offset, offset), x => x.VictimRespawnNumber, ExportCategory.Gunplay).Int32();
        AddPropertyHandle(LayoutHandle(24 + offset, offset), "bEquippableUsedZoomed", x => x.EquippableUsedZoomed, ExportCategory.Gunplay).Bool();
        AddPropertyHandle(LayoutHandle(25 + offset, offset), "bEquippableUsedInFocusMode", x => x.EquippableUsedInFocusMode, ExportCategory.Gunplay).Bool();
    }
}
