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
}

public abstract class DamageParameters<T> : ExportGroupDescriptor<T>
    where T : DamageParameters<T>
{
    private readonly bool _hasKillCreditPlayerState;

    protected DamageParameters(bool hasKillCreditPlayerState)
    {
        _hasKillCreditPlayerState = hasKillCreditPlayerState;
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

    /// <summary>Maps a handle in the current layout, whose damage response data starts at <paramref name="offset"/>, to this release's handle.</summary>
    protected uint LayoutHandle(uint handle, uint offset = 0) =>
        _hasKillCreditPlayerState || handle < DamageResponseLayout.KillCreditPlayerStateHandle + offset ? handle : handle - 1;

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
        AddPropertyHandle(9 + offset, x => x.LifeChangeEvents, ExportCategory.Gunplay).RepLayoutDynamicArray(
            new VersionedDefinition<DamageLifeChangeEvent>(new(offset)));
        AddPropertyHandle(10 + offset, x => x.ChangedComponent, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(11 + offset, x => x.LifeResult, ExportCategory.Gunplay).Float();
        AddPropertyHandle(12 + offset, x => x.DeltaLife, ExportCategory.Gunplay).Float();
        AddPropertyHandle(13 + offset, "bAliveAfterChange", x => x.AliveAfterChange, ExportCategory.Gunplay).Bool();
        AddPropertyHandle(15 + offset, x => x.EventInstigatorPawn, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(16 + offset, x => x.DamagerPlayerState, ExportCategory.Gunplay).ObjectNetGuid();
        if (_hasKillCreditPlayerState)
            AddPropertyHandle(17 + offset, x => x.KillCreditPlayerState, ExportCategory.Gunplay).ObjectNetGuid();
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
