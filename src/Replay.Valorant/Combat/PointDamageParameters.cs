using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Combat;

public abstract class PointDamageParameters<T> : DamageParameters<T> where T : PointDamageParameters<T>
{
    protected PointDamageParameters(bool hasKillCreditPlayerState, bool hasDamageIcon = false)
        : base(hasKillCreditPlayerState, hasDamageIcon)
    {
    }

    public NetObjectReference DamagedComponent { get; set; }
    public string? DamagedBone { get; set; }
    public bool IsWallPenetration { get; set; }
    public float FalloffMultiplier { get; set; }
    public FVector? DamageDirection { get; set; }
    public FVector? DamageImpactLocation { get; set; }
    public FVector? DamageImpactNormal { get; set; }
    public FVector? DamageImpactBoneRelativeLocation { get; set; }

    protected void AddPointFields(uint offset = 0)
    {
        AddPropertyHandle(LayoutHandle(26 + offset, offset), x => x.DamagedComponent, ExportCategory.Gunplay).ObjectNetGuid();
        AddPropertyHandle(LayoutHandle(27 + offset, offset), x => x.DamagedBone, ExportCategory.Gunplay).FName();
        AddPropertyHandle(LayoutHandle(28 + offset, offset), "bIsWallPenetration", x => x.IsWallPenetration, ExportCategory.Gunplay).Bool();
        AddPropertyHandle(LayoutHandle(29 + offset, offset), x => x.FalloffMultiplier, ExportCategory.Gunplay).Float();
        AddPropertyHandle(LayoutHandle(30 + offset, offset), x => x.DamageDirection, ExportCategory.Gunplay).FVectorNetQuantizeNormal();
        AddPropertyHandle(LayoutHandle(31 + offset, offset), x => x.DamageImpactLocation, ExportCategory.Gunplay).FVectorNetQuantize();
        AddPropertyHandle(LayoutHandle(32 + offset, offset), x => x.DamageImpactNormal, ExportCategory.Gunplay).FVectorNetQuantizeNormal();
        AddPropertyHandle(LayoutHandle(33 + offset, offset), x => x.DamageImpactBoneRelativeLocation, ExportCategory.Gunplay).FVectorNetQuantize();
    }
}
