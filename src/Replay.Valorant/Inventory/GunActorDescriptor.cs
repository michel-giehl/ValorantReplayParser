using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Inventory;

/// <summary>
/// Cosmetic and ownership fields replicated by every gun actor. Validated against 12.09-13.08 replays: the skin,
/// skin level and chroma assets are set once at spawn and never change, including after the gun is dropped and picked
/// up by another player. Chamber's ability guns replicate no cosmetics and keep their own descriptors.
/// </summary>
public sealed class GunActorDescriptor(string path) : ExportGroupDescriptor<GunActorDescriptor>
{
    /// <summary>Purchasable gun class paths as replicated on the wire. The Guardian uses <c>DMR/DMR</c>, not <c>Dmr/DMR</c>.</summary>
    public static IReadOnlyList<string> ClassPaths { get; } =
    [
        "/Game/Equippables/Guns/Sidearms/BasePistol/BasePistol.BasePistol_C",
        "/Game/Equippables/Guns/Sidearms/Slim/SawedOffShotgun.SawedOffShotgun_C",
        "/Game/Equippables/Guns/Sidearms/AutoPistol/AutomaticPistol.AutomaticPistol_C",
        "/Game/Equippables/Guns/Sidearms/Luger/LugerPistol.LugerPistol_C",
        "/Game/Equippables/Guns/Sidearms/Compact/CompactPistol.CompactPistol_C",
        "/Game/Equippables/Guns/Sidearms/Revolver/RevolverPistol.RevolverPistol_C",
        "/Game/Equippables/Guns/SubMachineGuns/Vector/Vector.Vector_C",
        "/Game/Equippables/Guns/SubMachineGuns/MP5/SubMachineGun_MP5.SubMachineGun_MP5_C",
        "/Game/Equippables/Guns/Shotguns/PumpShotgun/PumpShotgun.PumpShotgun_C",
        "/Game/Equippables/Guns/Shotguns/AutoShotgun/AutomaticShotgun.AutomaticShotgun_C",
        "/Game/Equippables/Guns/Rifles/Burst/AssaultRifle_Burst.AssaultRifle_Burst_C",
        "/Game/Equippables/Guns/Rifles/BattleRifle/BattleRifle.BattleRifle_C",
        "/Game/Equippables/Guns/SniperRifles/DMR/DMR.DMR_C",
        "/Game/Equippables/Guns/Rifles/Carbine/AssaultRifle_ACR.AssaultRifle_ACR_C",
        "/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C",
        "/Game/Equippables/Guns/SniperRifles/Leversniper/LeverSniperRifle.LeverSniperRifle_C",
        "/Game/Equippables/Guns/SniperRifles/Boltsniper/BoltSniper.BoltSniper_C",
        "/Game/Equippables/Guns/SniperRifles/Doublesniper/DS_Gun.DS_Gun_C",
        "/Game/Equippables/Guns/HvyMachineGuns/LMG/LightMachineGun.LightMachineGun_C",
        "/Game/Equippables/Guns/HvyMachineGuns/HMG/HeavyMachineGun.HeavyMachineGun_C",
    ];

    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.Inventory;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public override object CreatePayloadInstance() => new GunActorDescriptor(path);

    public NetObjectReference? Owner { get; set; }
    public NetObjectReference? OwningPrimaryDataAsset { get; set; }
    public NetObjectReference? SkinDataAsset { get; set; }
    public NetObjectReference? SkinLevelDataAsset { get; set; }
    public NetObjectReference? ChromaDataAsset { get; set; }
    public NetObjectReference? CharmDataAsset { get; set; }
    public NetObjectReference? CharmLevelDataAsset { get; set; }
    public NetObjectReference? PreventPickupCharacter { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.OwningPrimaryDataAsset).ObjectNetGuid();
        AddProperty(x => x.SkinDataAsset).ObjectNetGuid();
        AddProperty(x => x.SkinLevelDataAsset).ObjectNetGuid();
        AddProperty(x => x.ChromaDataAsset).ObjectNetGuid();
        AddProperty(x => x.CharmDataAsset).ObjectNetGuid();
        AddProperty(x => x.CharmLevelDataAsset).ObjectNetGuid();
        AddProperty(x => x.PreventPickupCharacter).ObjectNetGuid();
    }
}
