using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Tests.Descriptors;

public class ValorantDescriptorsTests
{
    [TestCase("ReplayEffect", "/Script/ShooterGame.ReplayEffectComponent")]
    [TestCase("EffectManager", "/Script/ShooterGame.EffectManagerComponent")]
    [TestCase("BlindManagerComponent", "/Script/ShooterGame.BlindManagerComponent")]
    [TestCase("LocationalEffectManager", "/Script/ShooterGame.LocationalEffectManagerComponent")]
    [TestCase("DamageHandlerComponent", "/Script/ShooterGame.DamageableComponent")]
    public void CreateCatalog_RegistersStableSubobjectClasses(string objectName, string classPath)
    {
        Assert.That(ValorantDescriptors.CreateCatalog().SubobjectClassPaths[objectName], Is.EqualTo(classPath));
    }

    [TestCase("/Game/Characters/Foo/Foo_PC.Foo_PC_C", "/Game/Characters/_Core/Foo/Foo_PC.Foo_PC_C")]
    [TestCase("/Game/Characters/_Core/Foo/Foo_PC.Foo_PC_C", "/Game/Characters/Foo/Foo_PC.Foo_PC_C")]
    [TestCase("/Game/Other/Thing.Thing_C", null)]
    public void CreateCatalog_PathAliasProvider_ResolvesValorantCoreAlias(string path, string? expected)
    {
        Assert.That(ValorantDescriptors.CreateCatalog().PathAliasProvider!.GetAlternatePath(path), Is.EqualTo(expected));
    }

    [Test]
    public void CreateCatalog_IncludesPlayableAgentDescriptors()
    {
        string[] expectedAgentPaths =
        [
            "/Game/Characters/Aggrobot/Aggrobot_PC.Aggrobot_PC_C",
            "/Game/Characters/BountyHunter/BountyHunter_PC.BountyHunter_PC_C",
            "/Game/Characters/Breach/Breach_PC.Breach_PC_C",
            "/Game/Characters/Cable/Cable_PC.Cable_PC_C",
            "/Game/Characters/Cashew/Cashew_PC.Cashew_PC_C",
            "/Game/Characters/Clay/Clay_PC.Clay_PC_C",
            "/Game/Characters/Deadeye/Deadeye_PC.Deadeye_PC_C",
            "/Game/Characters/Grenadier/Grenadier_PC.Grenadier_PC_C",
            "/Game/Characters/Guide/Guide_PC.Guide_PC_C",
            "/Game/Characters/Gumshoe/Gumshoe_PC.Gumshoe_PC_C",
            "/Game/Characters/Hunter/Hunter_PC.Hunter_PC_C",
            "/Game/Characters/Iris/Iris_PC.Iris_PC_C",
            "/Game/Characters/Killjoy/Killjoy_PC.Killjoy_PC_C",
            "/Game/Characters/Mage/Mage_PC.Mage_PC_C",
            "/Game/Characters/Nox/Nox_PC.Nox_PC_C",
            "/Game/Characters/Pandemic/Pandemic_PC.Pandemic_PC_C",
            "/Game/Characters/Phoenix/Phoenix_PC.Phoenix_PC_C",
            "/Game/Characters/Pine/Pine_PC.Pine_PC_C",
            "/Game/Characters/Rift/Rift_PC.Rift_PC_C",
            "/Game/Characters/Sarge/Sarge_PC.Sarge_PC_C",
            "/Game/Characters/Sequoia/Sequoia_PC.Sequoia_PC_C",
            "/Game/Characters/Smonk/Smonk_PC.Smonk_PC_C",
            "/Game/Characters/Sprinter/Sprinter_PC.Sprinter_PC_C",
            "/Game/Characters/Stealth/Stealth_PC.Stealth_PC_C",
            "/Game/Characters/Terra/Terra_PC.Terra_PC_C",
            "/Game/Characters/Thorne/Thorne_PC.Thorne_PC_C",
            "/Game/Characters/Vampire/Vampire_PC.Vampire_PC_C",
            "/Game/Characters/Wraith/Wraith_PC.Wraith_PC_C",
            "/Game/Characters/Wushu/Wushu_PC.Wushu_PC_C",
        ];

        var actualAgentPaths = ValorantDescriptors.CreateCatalog()
            .ExportGroupDescriptors
            .Where(descriptor => descriptor.Categories.HasFlag(ExportCategory.Agent))
            .Select(descriptor => descriptor.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.That(actualAgentPaths, Is.EqualTo(expectedAgentPaths));
    }

    [Test]
    public void CreateCatalog_IncludesRequestedGameplayExportDescriptors()
    {
        string[] expectedPaths =
        [
            "/Game/GameModes/Bomb/BombPlayerState.BombPlayerState_C",
            "/Game/GameModes/Common/BaseReplayPlayerState.BaseReplayPlayerState_C",
            "/Game/GameModes/Bomb/BombGameState.BombGameState_C",
            "/Game/GameModes/Bomb/Bomb_CombatReportComponent.Bomb_CombatReportComponent_C",
            "/Script/ShooterGame.AresInventory",
            "/Script/ShooterGame.EquippableStateMachineComponent",
            "/Script/ShooterGame.AmmoComponent",
            "/Script/ShooterGame.AresAttributeSet",
            "/Script/ShooterGame.ChildDamageSectionComponent",
            "/Script/ShooterGame.ChildRegionDamageSectionComponent",
            "/Script/ShooterGame.AttachedDamageSectionComponent",
            "/Game/Characters/Mage/S0/Ability_Q/Projectile_Mage_Q_Wall.Projectile_Mage_Q_Wall_C",
            "/Game/Characters/Phoenix/S0/Ability_Q/Production/Projectile_Phoenix_Q_FlameWall_ThroughWall.Projectile_Phoenix_Q_FlameWall_ThroughWall_C",
            "/Game/Characters/Sprinter/S0/Ability_4/Projectile_Neon_C_Tunnel.Projectile_Neon_C_Tunnel_C",
        ];

        var descriptors = ValorantDescriptors.CreateCatalog().ExportGroupDescriptors;
        var descriptorPaths = descriptors
            .Select(descriptor => descriptor.Path)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            foreach (var path in expectedPaths)
            {
                var descriptor = descriptors.Single(d => d.Path == path);
                Assert.That(descriptorPaths.Contains(path), Is.True, path);
                Assert.That(descriptor.Fields, Is.Not.Empty, path);
                Assert.That(descriptor.Fields.All(field => field.Decoder is not null), Is.True, path);
            }
        });
    }

    [Test]
    public void CreateCatalog_IncludesRequestedClassNetCacheFunctions()
    {
        var cacheDescriptors = ValorantDescriptors.CreateCatalog()
            .ClassNetCacheDescriptors
            .ToDictionary(descriptor => descriptor.Path, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            AssertFunctions(
                cacheDescriptors["/Game/Characters/_Core/BaseReplayController.BaseReplayController_C_ClassNetCache"],
                "ReplaysClientReceiveRemoteCharacterUpdatesSingleArrayNoAutonomous",
                "ClientGamePhaseBegin",
                "ClientGamePhaseEnded");

            AssertFunctions(
                cacheDescriptors["/Game/GameModes/Bomb/BombGameState.BombGameState_C_ClassNetCache"],
                "ClientBuyPhaseEnd",
                "ClientRoundStart",
                "Multicast Side Switch Event",
                "ClientResetRound",
                "MulticastEndRound",
                "MulticastEnterPlayspace",
                "MulticastReceivePlayerResurrectEvent",
                "MulticastReceivePlayerTemporaryDeathEvent_Base",
                "MulticastReceivePlayerTemporaryDeathEvent_Point",
                "MulticastSetPhase",
                "MulticastResetForRespawn");

            AssertFunctions(
                cacheDescriptors["/Script/ShooterGame.ReplayEffectComponent_ClassNetCache"],
                "ReplayPlayContinuousEffectAtLocation",
                "ReplayPlayOneShotEffectAtLocation",
                "ReplayStopContinuousEffectAtLocation");

            AssertFunctions(
                cacheDescriptors["/Script/ShooterGame.DamageableComponent_ClassNetCache"],
                "MulticastNotifyDamage_Base",
                "MulticastNotifyDamage_Point");
        });
    }

    [Test]
    public void CreateCatalog_DecodesDamageAndKillRpcFields()
    {
        var catalog = ValorantDescriptors.CreateCatalog();

        AssertDecodedRpcFields(
            catalog.ClassNetCacheDescriptors.Single(cache =>
                    cache.Path == "/Script/ShooterGame.DamageableComponent_ClassNetCache")
                .FunctionFields.Single(function => function.Name == "MulticastNotifyDamage_Base"),
            "DamageTaken", "DamagerPlayerState", "KillCreditPlayerState", "KillsForKiller");
        AssertDecodedRpcFields(
            catalog.ClassNetCacheDescriptors.Single(cache =>
                    cache.Path == "/Script/ShooterGame.DamageableComponent_ClassNetCache")
                .FunctionFields.Single(function => function.Name == "MulticastNotifyDamage_Point"),
            "DamageTaken", "DamagerPlayerState", "DamagedComponent", "DamagedBone", "KillsForKiller");

        var equippableField = catalog.ClassNetCacheDescriptors.Single(cache =>
                cache.Path == "/Script/ShooterGame.DamageableComponent_ClassNetCache")
            .FunctionFields.Single(function => function.Name == "MulticastNotifyDamage_Point")
            .Fields.Single(field => field.PropertyName == "EquippableUsed");
        var damagedBoneField = catalog.ClassNetCacheDescriptors.Single(cache =>
                cache.Path == "/Script/ShooterGame.DamageableComponent_ClassNetCache")
            .FunctionFields.Single(function => function.Name == "MulticastNotifyDamage_Point")
            .Fields.Single(field => field.PropertyName == "DamagedBone");

        var agentCachePaths = catalog.ExportGroupDescriptors
            .Where(descriptor => descriptor.Categories.HasFlag(ExportCategory.Agent))
            .Select(descriptor => descriptor.Path + "_ClassNetCache")
            .ToHashSet(StringComparer.Ordinal);
        var agentCaches = catalog.ClassNetCacheDescriptors
            .Where(cache => agentCachePaths.Contains(cache.Path))
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(agentCaches, Has.Length.EqualTo(agentCachePaths.Count));
            Assert.That(equippableField.TargetProperty!.PropertyType, Is.EqualTo(typeof(ValorantEquippable)));
            Assert.That(damagedBoneField.TargetProperty!.PropertyType, Is.EqualTo(typeof(string)));
            foreach (var cache in agentCaches)
            {
                AssertDecodedRpcFields(
                    cache.FunctionFields.Single(function => function.Name == "MulticastNotifyKilledEnemy"),
                    "KillerCharacter", "KilledCharacter", "MultikillLevel");
            }
        });
    }

    [Test]
    public void CreateCatalog_DecodesReplayContinuousEffectWithParameterDescriptor()
    {
        var function = ValorantDescriptors.CreateCatalog()
            .ClassNetCacheDescriptors
            .Single(descriptor => descriptor.Path == "/Script/ShooterGame.ReplayEffectComponent_ClassNetCache")
            .FunctionFields
            .Single(function => function.Name == "ReplayPlayContinuousEffectAtLocation");

        var fieldsByName = function.Fields
            .Where(field => field.PropertyName is not null)
            .ToDictionary(field => field.PropertyName!, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(function.Decoder, Is.Null);
            Assert.That(function.Handle, Is.EqualTo(0));
            Assert.That(function.FunctionExportPath,
                Is.EqualTo("/Script/ShooterGame.ReplayEffectComponent:ReplayPlayContinuousEffectAtLocation"));
            Assert.That(fieldsByName.ContainsKey("EffectId"), Is.True);
            Assert.That(fieldsByName.ContainsKey("FloatValues"), Is.True);
            Assert.That(fieldsByName.ContainsKey("VectorValues"), Is.True);
            Assert.That(fieldsByName.ContainsKey("ObjectValues"), Is.True);
            Assert.That(fieldsByName.ContainsKey("StartMovementTime"), Is.True);
            Assert.That(fieldsByName.Values.All(field => field.Decoder is not null), Is.True);
        });
    }

    [Test]
    public void CreateCatalog_DecodesRoundLifecycleRpcFields()
    {
        var functions = ValorantDescriptors.CreateCatalog()
            .ClassNetCacheDescriptors
            .Single(descriptor => descriptor.Path == "/Game/GameModes/Bomb/BombGameState.BombGameState_C_ClassNetCache")
            .FunctionFields
            .ToDictionary(function => function.Name, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            AssertDecodedRpcFields(functions["MulticastReceivePlayerResurrectEvent"],
                "ResurrectorPlayer",
                "ResurrectedPlayer");
            AssertDecodedRpcFields(functions["MulticastReceivePlayerTemporaryDeathEvent_Base"],
                "DamagerPlayer",
                "DownedPlayer");
            AssertDecodedRpcFields(functions["MulticastReceivePlayerTemporaryDeathEvent_Point"],
                "DamagerPlayer",
                "DownedPlayer");
            AssertDecodedRpcFields(functions["MulticastResetForRespawn"], "ShooterCharacter");
        });
    }

    [Test]
    public void CreateCatalog_KnownPrimitiveFailure_ThrowsInsteadOfReturningRawPayload()
    {
        var healthField = FindExportField(
            ValorantDescriptors.CreateCatalog(),
            "/Script/ShooterGame.AresAttributeSet",
            "Health");
        Assert.That(healthField.Decoder, Is.AssignableTo<IFieldDecoder>());

        var archive = new BitArchiveReader(new byte[] { 0xA6 }, bitCount: 8);
        var context = new FieldDecodeContext { FieldName = healthField.PropertyName };

        var exception = Assert.Throws<ArchiveReadException>(() =>
            ((IFieldDecoder)healthField.Decoder!).Decode(ref context, archive));

        Assert.That(exception!.ErrorCode, Is.EqualTo(ArchiveErrorCode.EndOfArchive));
    }

    private static FieldDescriptor FindExportField(DescriptorCatalog catalog, string descriptorPath, string fieldName)
    {
        var descriptor = catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == descriptorPath);
        return descriptor.Fields.Single(field => field.PropertyName == fieldName);
    }

    private static void AssertDecodedRpcFields(RpcDescriptor descriptor, params string[] fieldNames)
    {
        Assert.That(descriptor.Decoder, Is.Null, $"{descriptor.Name} should use named function fields");

        var fieldsByName = descriptor.Fields
            .Where(field => field.PropertyName is not null)
            .ToDictionary(field => field.PropertyName!, StringComparer.Ordinal);

        foreach (var fieldName in fieldNames)
        {
            Assert.That(fieldsByName.ContainsKey(fieldName), Is.True, $"{descriptor.Name}:{fieldName}");
            Assert.That(fieldsByName[fieldName].Decoder, Is.Not.Null, $"{descriptor.Name}:{fieldName}");
        }
    }

    private static void AssertFunctions(ClassNetCacheDescriptor descriptor, params string[] functionNames)
    {
        var actualNames = descriptor.FunctionFields
            .Select(function => function.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var functionName in functionNames)
        {
            Assert.That(actualNames.Contains(functionName), Is.True, $"{descriptor.Path}:{functionName}");
        }
    }
}
