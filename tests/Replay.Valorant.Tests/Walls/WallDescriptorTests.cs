using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Walls.Descriptors;

namespace Replay.Valorant.Tests.Walls;

public class WallDescriptorTests
{
    [Test]
    public void CreateCatalog_RegistersSageAndVyseWallLayouts()
    {
        var catalog = ValorantDescriptors.CreateCatalog();

        Assert.Multiple(() =>
        {
            AssertFieldHandles(catalog, WallPaths.SageWall, ("Owner", 11), ("Instigator", 13));
            AssertFieldHandles(catalog, WallPaths.SageSegment,
                ("Owner", 11), ("Instigator", 13), ("IsAlive", 15));
            AssertFieldHandles(catalog, WallPaths.VyseTrap, ("Owner", 11), ("Instigator", 13));
            AssertFieldHandles(catalog, WallPaths.VyseWall, ("Owner", 11), ("Instigator", 13));

            AssertFunction(catalog, WallPaths.SageSegment, "DisableCollision", 0);
            AssertFunction(catalog, WallPaths.VyseTrap, "MulticastInitializeTrapAnchors", 0);
            AssertFunction(catalog, WallPaths.VyseWall, "MulticastEnableDynamicCollision", 0);
            AssertFunction(catalog, WallPaths.VyseWall, "MulticastInitializeWall", 1);

            AssertParameterFieldHandles(catalog, WallPaths.VyseTrap, "MulticastInitializeTrapAnchors",
                ("WallStartPoint", 0), ("WallEndPoint", 1), ("ImpactPoint", 2), ("ImpactNormal", 3));
            AssertParameterFieldHandles(catalog, WallPaths.VyseWall, "MulticastInitializeWall",
                ("WallStartLocation", 0), ("WallEndLocation", 1), ("WallImpactNormal", 2), ("EnemyTrigger", 3));
        });
    }

    private static void AssertFieldHandles(
        DescriptorCatalog catalog,
        string path,
        params (string PropertyName, uint Handle)[] expected)
    {
        var descriptor = catalog.ExportGroupDescriptors.Single(candidate => candidate.Path == path);
        var fields = descriptor.Fields.Where(field => field.PropertyName is not null)
            .ToDictionary(field => field.PropertyName!, StringComparer.Ordinal);
        foreach (var (propertyName, handle) in expected)
        {
            Assert.That(fields[propertyName].Handle, Is.EqualTo(handle), $"{path}:{propertyName}");
            Assert.That(fields[propertyName].Decoder, Is.Not.Null, $"{path}:{propertyName}");
        }
    }

    private static void AssertFunction(
        DescriptorCatalog catalog,
        string path,
        string functionName,
        uint handle)
    {
        var function = catalog.ClassNetCacheDescriptors
            .Single(candidate => candidate.Path == path + "_ClassNetCache")
            .FunctionFields.Single(candidate => candidate.Name == functionName);
        Assert.That(function.Handle, Is.EqualTo(handle), $"{path}:{functionName}");
        Assert.That(function.Decoder is not null || function.ParameterDescriptor is not null, Is.True,
            $"{path}:{functionName}");
    }

    private static void AssertParameterFieldHandles(
        DescriptorCatalog catalog,
        string path,
        string functionName,
        params (string PropertyName, uint Handle)[] expected)
    {
        var descriptor = catalog.ClassNetCacheDescriptors
            .Single(candidate => candidate.Path == path + "_ClassNetCache")
            .FunctionFields.Single(candidate => candidate.Name == functionName)
            .ParameterDescriptor!;
        var fields = descriptor.Fields.Where(field => field.PropertyName is not null)
            .ToDictionary(field => field.PropertyName!, StringComparer.Ordinal);
        foreach (var (propertyName, handle) in expected)
        {
            Assert.That(fields[propertyName].Handle, Is.EqualTo(handle), $"{path}:{functionName}:{propertyName}");
            Assert.That(fields[propertyName].Decoder, Is.Not.Null, $"{path}:{functionName}:{propertyName}");
        }
    }
}
