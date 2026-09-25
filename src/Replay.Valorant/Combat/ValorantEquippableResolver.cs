using Replay.Encoding.Net;
using Replay.Models.Net;

namespace Replay.Valorant.Combat;

public static class ValorantEquippableResolver
{
    private const int MaxOuterDepth = 16;

    private static readonly Definition[] CanonicalDefinitions =
    [
        Define("/Game/Characters/_Core/Equippable_Unarmed.Equippable_Unarmed_C", "Unarmed", ValorantEquippableCategory.Unarmed),
        Define("/Game/Equippables/Melee/Ability_Melee_Base.Ability_Melee_Base_C", "Melee", ValorantEquippableCategory.Melee),
        Define("/Game/Equippables/Bomb/BombEquippable.BombEquippable_C", "Spike", ValorantEquippableCategory.Bomb),
        Define("/Game/Equippables/Guns/Sidearms/BasePistol/BasePistol.BasePistol_C", "Classic", ValorantEquippableCategory.Sidearm),
        Define("/Game/Equippables/Guns/Sidearms/Slim/SawedOffShotgun.SawedOffShotgun_C", "Shorty", ValorantEquippableCategory.Sidearm),
        Define("/Game/Equippables/Guns/Sidearms/AutoPistol/AutomaticPistol.AutomaticPistol_C", "Frenzy", ValorantEquippableCategory.Sidearm),
        Define("/Game/Equippables/Guns/Sidearms/Luger/LugerPistol.LugerPistol_C", "Ghost", ValorantEquippableCategory.Sidearm),
        Define("/Game/Equippables/Guns/Sidearms/Compact/CompactPistol.CompactPistol_C", "Compact Pistol", ValorantEquippableCategory.Sidearm),
        Define("/Game/Equippables/Guns/Sidearms/Revolver/RevolverPistol.RevolverPistol_C", "Sheriff", ValorantEquippableCategory.Sidearm),
        Define("/Game/Equippables/Guns/SubMachineGuns/Vector/Vector.Vector_C", "Stinger", ValorantEquippableCategory.Smg),
        Define("/Game/Equippables/Guns/SubMachineGuns/MP5/SubMachineGun_MP5.SubMachineGun_MP5_C", "Spectre", ValorantEquippableCategory.Smg),
        Define("/Game/Equippables/Guns/Shotguns/PumpShotgun/PumpShotgun.PumpShotgun_C", "Bucky", ValorantEquippableCategory.Shotgun),
        Define("/Game/Equippables/Guns/Shotguns/AutoShotgun/AutomaticShotgun.AutomaticShotgun_C", "Judge", ValorantEquippableCategory.Shotgun),
        Define("/Game/Equippables/Guns/Rifles/Burst/AssaultRifle_Burst.AssaultRifle_Burst_C", "Bulldog", ValorantEquippableCategory.Rifle),
        Define("/Game/Equippables/Guns/Rifles/BattleRifle/BattleRifle.BattleRifle_C", "Warden", ValorantEquippableCategory.Rifle),
        Define("/Game/Equippables/Guns/SniperRifles/Dmr/DMR.DMR_C", "Guardian", ValorantEquippableCategory.Rifle),
        Define("/Game/Equippables/Guns/Rifles/Carbine/AssaultRifle_ACR.AssaultRifle_ACR_C", "Phantom", ValorantEquippableCategory.Rifle),
        Define("/Game/Equippables/Guns/Rifles/AK/AssaultRifle_AK.AssaultRifle_AK_C", "Vandal", ValorantEquippableCategory.Rifle),
        Define("/Game/Equippables/Guns/SniperRifles/Leversniper/LeverSniperRifle.LeverSniperRifle_C", "Marshal", ValorantEquippableCategory.SniperRifle),
        Define("/Game/Equippables/Guns/SniperRifles/Boltsniper/BoltSniper.BoltSniper_C", "Operator", ValorantEquippableCategory.SniperRifle),
        Define("/Game/Equippables/Guns/SniperRifles/Doublesniper/DS_Gun.DS_Gun_C", "Outlaw", ValorantEquippableCategory.SniperRifle),
        Define("/Game/Equippables/Guns/HvyMachineGuns/LMG/LightMachineGun.LightMachineGun_C", "Ares", ValorantEquippableCategory.MachineGun),
        Define("/Game/Equippables/Guns/HvyMachineGuns/HMG/HeavyMachineGun.HeavyMachineGun_C", "Odin", ValorantEquippableCategory.MachineGun),
        Define("/Game/Characters/Deadeye/S0/Ability_Q/Gun/Gun_Deadeye_Q_Pistol.Gun_Deadeye_Q_Pistol_C", "Headhunter", ValorantEquippableCategory.Ability),
        Define("/Game/Characters/Deadeye/S0/Ability_X/Gun_Giantslayer/Gun_Deadeye_X_Giantslayer_Prototype_FIreRatePrototype.Gun_Deadeye_X_Giantslayer_Prototype_FireRatePrototype_C", "Tour de Force", ValorantEquippableCategory.Ability),
    ];

    private static readonly IReadOnlyDictionary<string, Definition> Definitions = CreateDefinitions();

    public static IReadOnlyList<string> GunClassPaths { get; } = CanonicalDefinitions
        .Where(static definition => definition.Category is not ValorantEquippableCategory.Unarmed and not ValorantEquippableCategory.Melee and not ValorantEquippableCategory.Bomb)
        .Select(static definition => definition.ClassPath)
        .ToArray();

    public static ValorantEquippable Resolve(uint netGuid, NetGuidCache? netGuidCache)
    {
        string? firstPath = null;
        foreach (var path in GetPaths(netGuid, netGuidCache))
        {
            firstPath ??= path;
            if (Definitions.TryGetValue(path, out var definition))
            {
                return new ValorantEquippable(netGuid, definition.Name, definition.Category, definition.ClassPath);
            }
        }

        return new ValorantEquippable(netGuid, null, ValorantEquippableCategory.Unknown, firstPath);
    }

    public static bool TryResolve(uint netGuid, IEnumerable<string?> paths, out ValorantEquippable equippable)
    {
        foreach (var path in paths)
        {
            if (path is not null && Definitions.TryGetValue(path, out var definition))
            {
                equippable = new ValorantEquippable(netGuid, definition.Name, definition.Category, definition.ClassPath);
                return true;
            }
        }

        equippable = null!;
        return false;
    }

    public static bool TryResolveClassNetCachePath(string? classNetCachePath, uint netGuid, out ValorantEquippable equippable)
    {
        const string suffix = "_ClassNetCache";
        if (classNetCachePath is not null && classNetCachePath.EndsWith(suffix, StringComparison.Ordinal))
        {
            return TryResolve(netGuid, [classNetCachePath[..^suffix.Length]], out equippable);
        }

        equippable = null!;
        return false;
    }

    private static IEnumerable<string> GetPaths(uint netGuid, NetGuidCache? netGuidCache)
    {
        if (netGuidCache is null || netGuid == 0)
        {
            yield break;
        }

        var current = new NetworkGuid(netGuid);
        for (var depth = 0; depth < MaxOuterDepth && current.IsValid; depth++)
        {
            if (netGuidCache.TryGetPath(current.Value, out var path))
            {
                yield return path;
            }

            if (!netGuidCache.TryGetOuterNetGuid(current.Value, out current))
            {
                yield break;
            }
        }
    }

    private static IReadOnlyDictionary<string, Definition> CreateDefinitions()
    {
        var values = new Dictionary<string, Definition>(StringComparer.Ordinal);
        foreach (var definition in CanonicalDefinitions)
        {
            values.Add(definition.ClassPath, definition);

            var separatorIndex = definition.ClassPath.LastIndexOf('.');
            var packagePath = definition.ClassPath[..separatorIndex];
            var className = definition.ClassPath[(separatorIndex + 1)..];
            values.Add(packagePath, definition);
            values.Add("Default__" + className, definition);
        }

        return values;
    }

    private static Definition Define(string classPath, string name, ValorantEquippableCategory category) =>
        new(classPath, name, category);

    private sealed record Definition(string ClassPath, string Name, ValorantEquippableCategory Category);
}
