using Replay.Models.Descriptors;
using static Replay.Valorant.Descriptors.Agents.AbilityDescriptorRegistration;
using Replay.Valorant.Walls.Descriptors;
using Replay.Valorant.Descriptors.Patches;

namespace Replay.Valorant.Descriptors.Agents.Thorne;

public static class ThorneDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors()
    {
        return
        [
            new ThorneAgentDescriptor(),
            Actor("/Game/Characters/Thorne/S0/Ability_4/Ability_Thorne_4_SlowField_Production.Ability_Thorne_4_SlowField_Production_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Thorne/S0/Ability_4/Patch_Thorne_4_SlowField_Production.Patch_Thorne_4_SlowField_Production_C", ["Owner", "Instigator"]),
            Actor("/Game/Characters/Thorne/S0/Ability_4/Projectile_Thorne_4_SlowFIeld_Production.Projectile_Thorne_4_SlowFIeld_Production_C", ["Owner", "Instigator", "ReplicatedMovement"]),
            Actor("/Game/Characters/Thorne/S0/Ability_E/Ability_Thorne_E_Wall_Fortifying.Ability_Thorne_E_Wall_Fortifying_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Thorne/S0/Ability_Q/Ability_Thorne_Q_Heal_Production_New.Ability_Thorne_Q_Heal_Production_New_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            Actor("/Game/Characters/Thorne/S0/Ability_X/Ability_Thorne_X_Resurrect_Production.Ability_Thorne_X_Resurrect_Production_C", ["Owner", "Instigator", "CreatedByCharacter", "AttachParent", "bInPersistentData"]),
            new SageWallDescriptor(),
            new SageWallSegmentDescriptor(),
        ];
    }

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() => MergeClassNetCaches(
    [
        Rpc("/Game/Characters/Thorne/S0/Ability_4/Patch_Thorne_4_SlowField_Production.Patch_Thorne_4_SlowField_Production_C", "MulticastBeginDissipate", 1),
        Rpc("/Game/Characters/Thorne/S0/Ability_4/Projectile_Thorne_4_SlowFIeld_Production.Projectile_Thorne_4_SlowFIeld_Production_C", "MulticastStopProjectile", 3),
        new ClassNetCacheDescriptor("/Game/Characters/Thorne/S0/Ability_4/Patch_Thorne_4_SlowField_Production.Patch_Thorne_4_SlowField_Production_C_ClassNetCache",
            [new RpcDescriptor { Name = "NetMulticastUpdateNodeGrid", Handle = 3, Categories = ExportCategory.Ability,
                FunctionExportPath = CellularPatchNodeGridParameters.ExportPath, ParameterDescriptor = new CellularPatchNodeGridParameters() }]),
        new SageWallSegmentClassNetCacheDescriptor(),
    ]);
}
