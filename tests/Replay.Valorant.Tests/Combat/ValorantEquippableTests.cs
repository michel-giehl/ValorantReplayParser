using Replay.Encoding.Net;
using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;

namespace Replay.Valorant.Tests.Combat;

public class ValorantEquippableTests
{
    [TestCase("/Game/Characters/_Core/Equippable_Unarmed.Equippable_Unarmed_C", "Unarmed", ValorantEquippableCategory.Unarmed)]
    [TestCase("/Game/Equippables/Melee/Ability_Melee_Base.Ability_Melee_Base_C", "Melee", ValorantEquippableCategory.Melee)]
    [TestCase("/Game/Equippables/Bomb/BombEquippable.BombEquippable_C", "Spike", ValorantEquippableCategory.Bomb)]
    [TestCase("/Game/Equippables/Guns/Sidearms/BasePistol/BasePistol.BasePistol_C", "Classic", ValorantEquippableCategory.Sidearm)]
    [TestCase("/Game/Equippables/Guns/Sidearms/Slim/SawedOffShotgun.SawedOffShotgun_C", "Shorty", ValorantEquippableCategory.Sidearm)]
    [TestCase("/Game/Equippables/Guns/Sidearms/AutoPistol/AutomaticPistol.AutomaticPistol_C", "Frenzy", ValorantEquippableCategory.Sidearm)]
    [TestCase("/Game/Equippables/Guns/Sidearms/Luger/LugerPistol.LugerPistol_C", "Ghost", ValorantEquippableCategory.Sidearm)]
    [TestCase("/Game/Equippables/Guns/Sidearms/Compact/CompactPistol.CompactPistol_C", "Compact Pistol", ValorantEquippableCategory.Sidearm)]
    [TestCase("/Game/Equippables/Guns/Sidearms/Revolver/RevolverPistol.RevolverPistol_C", "Sheriff", ValorantEquippableCategory.Sidearm)]
    [TestCase("/Game/Equippables/Guns/SubMachineGuns/Vector/Vector.Vector_C", "Stinger", ValorantEquippableCategory.Smg)]
    [TestCase("/Game/Equippables/Guns/SubMachineGuns/MP5/SubMachineGun_MP5.SubMachineGun_MP5_C", "Spectre", ValorantEquippableCategory.Smg)]
    [TestCase("/Game/Equippables/Guns/Shotguns/PumpShotgun/PumpShotgun.PumpShotgun_C", "Bucky", ValorantEquippableCategory.Shotgun)]
    [TestCase("/Game/Equippables/Guns/Shotguns/AutoShotgun/AutomaticShotgun.AutomaticShotgun_C", "Judge", ValorantEquippableCategory.Shotgun)]
    [TestCase("/Game/Equippables/Guns/Rifles/Burst/AssaultRifle_Burst.AssaultRifle_Burst_C", "Bulldog", ValorantEquippableCategory.Rifle)]
    [TestCase("/Game/Equippables/Guns/Rifles/BattleRifle/BattleRifle.BattleRifle_C", "Warden", ValorantEquippableCategory.Rifle)]
    [TestCase("/Game/Equippables/Guns/SniperRifles/Dmr/DMR.DMR_C", "Guardian", ValorantEquippableCategory.Rifle)]
    [TestCase("/Game/Equippables/Guns/Rifles/Carbine/AssaultRifle_ACR.AssaultRifle_ACR_C", "Phantom", ValorantEquippableCategory.Rifle)]
    [TestCase("/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C", "Vandal", ValorantEquippableCategory.Rifle)]
    [TestCase("/Game/Equippables/Guns/SniperRifles/Leversniper/LeverSniperRifle.LeverSniperRifle_C", "Marshal", ValorantEquippableCategory.SniperRifle)]
    [TestCase("/Game/Equippables/Guns/SniperRifles/Boltsniper/BoltSniper.BoltSniper_C", "Operator", ValorantEquippableCategory.SniperRifle)]
    [TestCase("/Game/Equippables/Guns/SniperRifles/Doublesniper/DS_Gun.DS_Gun_C", "Outlaw", ValorantEquippableCategory.SniperRifle)]
    [TestCase("/Game/Equippables/Guns/HvyMachineGuns/LMG/LightMachineGun.LightMachineGun_C", "Ares", ValorantEquippableCategory.MachineGun)]
    [TestCase("/Game/Equippables/Guns/HvyMachineGuns/HMG/HeavyMachineGun.HeavyMachineGun_C", "Odin", ValorantEquippableCategory.MachineGun)]
    [TestCase("/Game/Characters/Deadeye/S0/Ability_Q/Gun/Gun_Deadeye_Q_Pistol.Gun_Deadeye_Q_Pistol_C", "Headhunter", ValorantEquippableCategory.Ability )]
    public void Resolve_KnownEquippablePath_ReturnsCanonicalWeapon(
        string classPath,
        string expectedName,
        ValorantEquippableCategory expectedCategory)
    {
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(17, classPath);

        var equippable = ValorantEquippableResolver.Resolve(17, cache);

        Assert.Multiple(() =>
        {
            Assert.That(equippable.NetGuid, Is.EqualTo(17));
            Assert.That(equippable.Name, Is.EqualTo(expectedName));
            Assert.That(equippable.Category, Is.EqualTo(expectedCategory));
            Assert.That(equippable.ClassPath, Is.EqualTo(classPath));
        });
    }

    [Test]
    public void Resolve_DefaultObjectOuterPath_ReturnsKnownEquippable()
    {
        const string classPath = "/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C";
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(17, "EquippableInstance", new NetworkGuid(18));
        cache.SetNetGuidPath(18, "Default__AssaultRifle_AK_C");

        var equippable = ValorantEquippableResolver.Resolve(17, cache);

        Assert.Multiple(() =>
        {
            Assert.That(equippable.Name, Is.EqualTo("Vandal"));
            Assert.That(equippable.Category, Is.EqualTo(ValorantEquippableCategory.Rifle));
            Assert.That(equippable.ClassPath, Is.EqualTo(classPath));
        });
    }

    [Test]
    public void TryResolveClassNetCachePath_KnownGun_ReturnsWeapon()
    {
        var resolved = ValorantEquippableResolver.TryResolveClassNetCachePath(
            "/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C_ClassNetCache",
            17,
            out var equippable);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(equippable.NetGuid, Is.EqualTo(17));
            Assert.That(equippable.Name, Is.EqualTo("Vandal"));
        });
    }

    [Test]
    public void Resolve_UnknownOrInvalidGuid_PreservesAvailableIdentity()
    {
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(17, "/Game/Unknown.Unknown_C");

        var unknown = ValorantEquippableResolver.Resolve(17, cache);
        var invalid = ValorantEquippableResolver.Resolve(0, cache);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Name, Is.Null);
            Assert.That(unknown.Category, Is.EqualTo(ValorantEquippableCategory.Unknown));
            Assert.That(unknown.ClassPath, Is.EqualTo("/Game/Unknown.Unknown_C"));
            Assert.That(invalid.NetGuid, Is.Zero);
            Assert.That(invalid.Name, Is.Null);
            Assert.That(invalid.ClassPath, Is.Null);
        });
    }

    [Test]
    public void DamageEquippableFieldDecoder_ResolvesTheEncodedNetGuid()
    {
        const string classPath = "/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C";
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(17, classPath);
        var field = new MulticastNotifyDamagePointParameters().Fields
            .Single(candidate => candidate.PropertyName == "EquippableUsed");
        Assert.That(field.Decoder, Is.AssignableTo<IFieldDecoder>());
        var decoder = (IFieldDecoder)field.Decoder!;
        var context = new FieldDecodeContext { NetGuidCache = cache };
        using var archive = new BitArchiveReader(new byte[] { 0x22 }, bitCount: 8);

        var value = decoder.Decode(ref context, archive);
        var equippable = (ValorantEquippable)value.ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(equippable.NetGuid, Is.EqualTo(17));
            Assert.That(equippable.Name, Is.EqualTo("Vandal"));
            Assert.That(archive.AtEnd, Is.True);
        });
    }
}
