using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Nearsights.Descriptors;

namespace Replay.Valorant.Tests.Nearsights;

public class NearsightDescriptorTests
{
    [Test]
    public void CreateCatalog_RegistersProjectileSourceAndStopLayouts()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var projectiles = new[]
        {
            (NearsightPaths.OmenProjectile, StopHandle: 3u),
            (NearsightPaths.ReynaProjectile, StopHandle: 4u),
            (NearsightPaths.HarborProjectile, StopHandle: 3u),
        };

        Assert.Multiple(() =>
        {
            foreach (var (path, stopHandle) in projectiles)
            {
                var descriptor = catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == path);
                AssertFieldHandles(descriptor, ("ReplicatedMovement", 10), ("Owner", 11), ("Instigator", 13));

                var stop = catalog.ClassNetCacheDescriptors
                    .Single(cache => cache.Path == path + "_ClassNetCache")
                    .FunctionFields.Single(function => function.Name == "MulticastStopProjectile");
                Assert.That(stop.Handle, Is.EqualTo(stopHandle), path);
                Assert.That(stop.Decoder, Is.Not.Null, path);
                Assert.That(stop.Fields, Is.Empty, path);
            }

            AssertFieldHandles(
                catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == NearsightPaths.ReynaSource),
                ("Owner", 11),
                ("Instigator", 13));
            AssertFieldHandles(
                catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == NearsightPaths.HarborSource),
                ("Owner", 11),
                ("Instigator", 13));
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
            Assert.That(fields[propertyName].Handle, Is.EqualTo(handle), $"{descriptor.Path}:{propertyName}");
            Assert.That(fields[propertyName].Decoder, Is.Not.Null, $"{descriptor.Path}:{propertyName}");
        }
    }
}
