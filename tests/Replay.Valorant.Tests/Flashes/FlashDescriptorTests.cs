using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Tests.Flashes;

public class FlashDescriptorTests
{
    [Test]
    public void CreateCatalog_RegistersAllFlashProjectileVariantsAndStopRpcs()
    {
        string[] projectilePaths =
        [
            FlashPaths.SkyeProjectile,
            FlashPaths.KayoOverhandProjectile,
            FlashPaths.KayoUnderhandProjectile,
            FlashPaths.BreachProjectile,
            FlashPaths.PhoenixLeftProjectile,
            FlashPaths.PhoenixRightProjectile,
            FlashPaths.YoruProjectile,
        ];
        var catalog = ValorantDescriptors.CreateCatalog();

        Assert.Multiple(() =>
        {
            foreach (var path in projectilePaths)
            {
                var descriptor = catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == path);
                AssertFieldHandles(descriptor, ("ReplicatedMovement", 10), ("Owner", 11), ("Instigator", 13));
            }

            foreach (var path in projectilePaths.Skip(1))
            {
                var stop = catalog.ClassNetCacheDescriptors
                    .Single(cache => cache.Path == path + "_ClassNetCache")
                    .FunctionFields.Single(function => function.Name == "MulticastStopProjectile");
                Assert.That(stop.Handle, Is.EqualTo(3), path);
                Assert.That(stop.Decoder, Is.Not.Null, path);
                Assert.That(stop.Fields, Is.Empty, path);
            }

            Assert.That(catalog.ClassNetCacheDescriptors.Any(cache =>
                cache.Path == FlashPaths.SkyeProjectile + "_ClassNetCache"), Is.False);
        });
    }

    [Test]
    public void CreateCatalog_RegistersSkyeFlashSourceAndDoubleDurationRpc()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var source = catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == FlashPaths.SkyeFlashSource);
        var duration = catalog.ClassNetCacheDescriptors
            .Single(cache => cache.Path == FlashPaths.SkyeFlashSource + "_ClassNetCache")
            .FunctionFields.Single(function => function.Name == "Multicast Set Flash Duration");

        Assert.Multiple(() =>
        {
            AssertFieldHandles(source, ("Owner", 11), ("Instigator", 13));
            Assert.That(duration.Handle, Is.EqualTo(0));
            Assert.That(duration.ParameterDescriptor, Is.TypeOf<SkyeSetFlashDurationParameters>());
            Assert.That(duration.Fields.Single().Handle, Is.EqualTo(0));
            Assert.That(duration.Fields.Single().TargetProperty!.PropertyType, Is.EqualTo(typeof(double?)));
        });
    }

    [Test]
    public void CreateCatalog_RegistersVyseFlashSourceAndDeployRpc()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var source = catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == FlashPaths.VyseFlashTrap);
        var deploy = catalog.ClassNetCacheDescriptors
            .Single(cache => cache.Path == FlashPaths.VyseFlashTrap + "_ClassNetCache")
            .FunctionFields.Single(function => function.Name == "MulticastDeployTrap");

        Assert.Multiple(() =>
        {
            AssertFieldHandles(source, ("Owner", 11), ("Instigator", 13));
            Assert.That(deploy.Handle, Is.EqualTo(0));
            Assert.That(deploy.ParameterDescriptor, Is.TypeOf<VyseDeployTrapParameters>());
            Assert.That(deploy.Fields.Single().Handle, Is.EqualTo(0));
            Assert.That(deploy.Fields.Single().TargetProperty!.PropertyType, Is.EqualTo(typeof(double?)));
        });
    }

    [Test]
    public void CreateCatalog_RegistersPrecalculatedProjectilePath()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var component = catalog.ExportGroupDescriptors.Single(candidate =>
            candidate.Path == PrecalculatedProjectilePathDescriptors.ComponentPath);
        var setPath = catalog.ClassNetCacheDescriptors
            .Single(cache => cache.Path == PrecalculatedProjectilePathDescriptors.ComponentPath + "_ClassNetCache")
            .FunctionFields.Single(function =>
                function.Name == PrecalculatedProjectilePathDescriptors.FunctionName);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.SubobjectClassPaths["PrecalculatedProjectileMovement"],
                Is.EqualTo(PrecalculatedProjectilePathDescriptors.ComponentPath));
            Assert.That(component.Kind, Is.EqualTo(ExportGroupKind.Component));
            Assert.That(setPath.Handle, Is.EqualTo(0));
            Assert.That(setPath.ParameterDescriptor,
                Is.TypeOf<PrecalculatedProjectileSetPathParameters>());
            AssertFieldHandles((ExportGroupDescriptor)setPath.ParameterDescriptor!,
                ("NetworkedProjectilePath", 0));
            AssertFieldHandles(new PrecalculatedProjectilePathPoint(),
                ("ElapsedSeconds", 1), ("Location", 2), ("Velocity", 3));
        });
    }

    [Test]
    public void CreateCatalog_RegistersBlindManagerActiveBlindLayout()
    {
        var manager = ValorantDescriptors.CreateCatalog().ExportGroupDescriptors
            .Single(candidate => candidate.Path == "/Script/ShooterGame.BlindManagerComponent");
        var activeBlind = new ActiveBlindDescriptor();

        Assert.Multiple(() =>
        {
            AssertFieldHandles(manager, ("ActiveBlinds", 2), ("LongestActiveBlindDuration", 13));
            Assert.That(manager.Fields.Single(field => field.PropertyName == "ActiveBlinds")
                .TargetProperty!.PropertyType, Is.EqualTo(typeof(ActiveBlindDescriptor[])));
            AssertFieldHandles(
                activeBlind,
                ("BlindId", 3),
                ("EffectId", 4),
                ("SourceId", 5),
                ("IsLocalEffect", 6),
                ("IsTransient", 7),
                ("InitialDuration", 8),
                ("StartNetMovementTime", 9),
                ("BlindConfig", 10),
                ("CausingActor", 11));
        });
    }

    [Test]
    public void CreateCatalog_RegistersIndependentEffectManagerLayouts()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var manager = catalog.ExportGroupDescriptors
            .Single(candidate => candidate.Path == "/Script/ShooterGame.EffectManagerComponent");
        var functions = catalog.ClassNetCacheDescriptors
            .Single(cache => cache.Path == "/Script/ShooterGame.EffectManagerComponent_ClassNetCache")
            .FunctionFields.ToDictionary(function => function.Name, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            AssertFieldHandles(manager, ("ServerActiveEffects", 2));
            Assert.That(functions["MulticastPlayContinuousEffect"].Handle, Is.EqualTo(0));
            Assert.That(functions["MulticastPlayOneShotEffect"].Handle, Is.EqualTo(1));
            Assert.That(functions["MulticastStopContinuousEffect"].Handle, Is.EqualTo(2));
            Assert.That(functions["MulticastUpdateContinuousEffect"].Handle, Is.EqualTo(3));
            AssertFieldHandles(new EffectManagerFunctionFloatValue(), ("Name", 3), ("Value", 4));
            AssertFieldHandles(new EffectManagerFunctionVectorValue(), ("Name", 7), ("Value", 8));
            AssertFieldHandles(new EffectManagerFunctionObjectValue(), ("Name", 11), ("Value", 12));
            AssertFieldHandles(new EffectManagerUpdateFloatValue(), ("Name", 6), ("Value", 7));
            AssertFieldHandles(new ActiveEffectFloatValue(), ("Name", 10), ("Value", 11));
            AssertFieldHandles(new ActiveEffectObjectValue(), ("Name", 18), ("Value", 19));
            AssertFieldHandles(
                new ActiveEffectInfoDescriptor(),
                ("EffectId", 3),
                ("EffectType", 7),
                ("ActiveFloatValues", 9),
                ("ActiveObjectValues", 17),
                ("StartTimeStamp", 33));
        });
    }

    private static void AssertFieldHandles(
        ExportGroupDescriptor descriptor,
        params (string PropertyName, uint Handle)[] expected)
    {
        var fields = descriptor.Fields
            .Where(field => field.PropertyName is not null)
            .ToDictionary(field => field.PropertyName!, StringComparer.Ordinal);
        foreach (var (propertyName, handle) in expected)
        {
            Assert.That(fields.ContainsKey(propertyName), Is.True, $"{descriptor.Path}:{propertyName}");
            Assert.That(fields[propertyName].Handle, Is.EqualTo(handle), $"{descriptor.Path}:{propertyName}");
            Assert.That(fields[propertyName].Decoder, Is.Not.Null, $"{descriptor.Path}:{propertyName}");
        }
    }
}
