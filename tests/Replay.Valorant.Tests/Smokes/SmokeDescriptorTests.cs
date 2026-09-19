using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents.Iris;
using Replay.Valorant.Descriptors.Agents.Rift;
using Replay.Valorant.Descriptors.Agents.Sarge;
using Replay.Valorant.Descriptors.Agents.Smonk;
using Replay.Valorant.Descriptors.Agents.Wushu;

namespace Replay.Valorant.Tests.Smokes;

public class SmokeDescriptorTests
{
    [Test]
    public void AgentDescriptorGroups_OwnTheirSmokeDescriptors()
    {
        Assert.Multiple(() =>
        {
            AssertAgentOwns(RiftDescriptors.CreateDescriptors(),
                RiftSmokePaths.Ability,
                RiftSmokePaths.WorldTargetingAbility,
                RiftSmokePaths.SmokeZone,
                RiftSmokePaths.FakeSmokeZone);
            AssertAgentOwns(SmonkDescriptors.CreateDescriptors(),
                SmonkSmokePaths.Ability,
                SmonkSmokePaths.PostDeathAbility,
                SmonkSmokePaths.Smoke,
                SmonkSmokePaths.PersistentSmoke);
            AssertAgentOwns(WushuDescriptors.CreateDescriptors(),
                WushuSmokePaths.Ability,
                WushuSmokePaths.SmokeZone,
                WushuSmokePaths.Projectile);
            AssertAgentOwns(SargeDescriptors.CreateDescriptors(),
                SargeSmokePaths.Ability,
                SargeSmokePaths.SmokeManager,
                SargeSmokePaths.Smoke);
            AssertAgentOwns(IrisDescriptors.CreateDescriptors(),
                IrisSmokePaths.Ability,
                IrisSmokePaths.Smoke);
        });
    }

    [Test]
    public void CreateCatalog_RegistersSmokeOwnershipAndMovementLayouts()
    {
        var catalog = ValorantDescriptors.CreateCatalog();

        Assert.Multiple(() =>
        {
            AssertFields(catalog, RiftSmokePaths.Ability, ("RelativeScale3D", 6), ("Owner", 11),
                ("Instigator", 13), ("IsInPersistentData", 14), ("CreatedByCharacter", 58));
            AssertFields(catalog, RiftSmokePaths.WorldTargetingAbility, ("RelativeScale3D", 6), ("Owner", 11),
                ("Instigator", 13), ("CreatedByCharacter", 58));
            AssertFields(catalog, RiftSmokePaths.SmokeZone, ("Owner", 11), ("Instigator", 13));
            AssertFields(catalog, RiftSmokePaths.FakeSmokeZone, ("Owner", 11), ("Instigator", 13));

            AssertFields(catalog, SmonkSmokePaths.Ability, ("RelativeScale3D", 6), ("Owner", 11),
                ("Instigator", 13), ("IsInPersistentData", 14), ("CreatedByCharacter", 58));
            AssertFields(catalog, SmonkSmokePaths.PostDeathAbility, ("RelativeScale3D", 6), ("Owner", 11),
                ("Instigator", 13), ("CreatedByCharacter", 58));
            AssertMovingSmoke(catalog, SmonkSmokePaths.Smoke);
            AssertMovingSmoke(catalog, SmonkSmokePaths.PersistentSmoke);

            AssertFields(catalog, WushuSmokePaths.Ability, ("RelativeScale3D", 6), ("Owner", 11),
                ("Instigator", 13), ("IsInPersistentData", 14), ("CreatedByCharacter", 58));
            AssertFields(catalog, WushuSmokePaths.SmokeZone, ("Owner", 11), ("Instigator", 13));
            AssertMovingSmoke(catalog, WushuSmokePaths.Projectile);

            AssertFields(catalog, SargeSmokePaths.Ability, ("RelativeScale3D", 6), ("Owner", 11),
                ("Instigator", 13), ("IsInPersistentData", 14), ("CreatedByCharacter", 58));
            AssertFields(catalog, SargeSmokePaths.SmokeManager, ("Owner", 11), ("Instigator", 13));
            AssertFields(catalog, SargeSmokePaths.Smoke, ("Owner", 11), ("Instigator", 13));

            AssertFields(catalog, IrisSmokePaths.Ability, ("RelativeScale3D", 6), ("Owner", 11),
                ("Instigator", 13), ("IsInPersistentData", 14), ("CreatedByCharacter", 58));
            AssertMovingSmoke(catalog, IrisSmokePaths.Smoke);
        });
    }

    [Test]
    public void CreateCatalog_RegistersSmokeClassNetCacheFunctions()
    {
        var catalog = ValorantDescriptors.CreateCatalog();

        Assert.Multiple(() =>
        {
            AssertFunction(catalog, RiftSmokePaths.Ability, "MulticastOnItemMovedToPersistentData", 0);
            AssertFunction(catalog, SmonkSmokePaths.Ability, "MulticastOnItemMovedToPersistentData", 0);
            AssertFunction(catalog, WushuSmokePaths.Ability, "MulticastOnItemMovedToPersistentData", 1);
            AssertFunction(catalog, WushuSmokePaths.Projectile, "MulticastStopProjectile", 3);
            AssertFunction(catalog, SargeSmokePaths.Ability, "MulticastOnItemMovedToPersistentData", 0);
            AssertFunction(catalog, IrisSmokePaths.Ability, "MulticastOnItemMovedToPersistentData", 0);
        });
    }

    private static void AssertAgentOwns(IEnumerable<ExportGroupDescriptor> descriptors, params string[] paths)
    {
        var actualPaths = descriptors.Select(descriptor => descriptor.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            Assert.That(actualPaths.Contains(path), Is.True, path);
        }
    }

    private static void AssertMovingSmoke(DescriptorCatalog catalog, string path) =>
        AssertFields(catalog, path, ("ReplicatedMovement", 10), ("Owner", 11), ("Instigator", 13));

    private static void AssertFields(DescriptorCatalog catalog, string path, params (string Name, uint Handle)[] fields)
    {
        var descriptor = catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == path);
        foreach (var (name, handle) in fields)
        {
            Assert.That(descriptor.Fields.Single(field => field.PropertyName == name).Handle, Is.EqualTo(handle),
                $"{path}:{name}");
        }
    }

    private static void AssertFunction(DescriptorCatalog catalog, string path, string name, uint handle)
    {
        var function = catalog.ClassNetCacheDescriptors
            .Single(candidate => candidate.Path == path + "_ClassNetCache")
            .FunctionFields.Single(candidate => candidate.Name == name);
        Assert.That(function.Handle, Is.EqualTo(handle), $"{path}:{name}");
        Assert.That(function.Decoder, Is.Not.Null, $"{path}:{name}");
    }
}
