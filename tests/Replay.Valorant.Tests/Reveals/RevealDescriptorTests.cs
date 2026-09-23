using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Reveals.Descriptors;

namespace Replay.Valorant.Tests.Reveals;

public sealed class RevealDescriptorTests
{
    [Test]
    public void CreateCatalog_RegistersSovaAndFadeRevealLayouts()
    {
        var catalog = ValorantDescriptors.CreateCatalog();

        Assert.Multiple(() =>
        {
            Assert.That(FadeRevealProjectileDescriptor.DescriptorPath, Is.EqualTo(
                "/Game/Characters/BountyHunter/S0/Ability_E/Projectile_E_BountyHunter_Divebomb.Projectile_E_BountyHunter_Divebomb_C"));
            Assert.That(FadeRevealDeviceDescriptor.DescriptorPath, Is.EqualTo(
                "/Game/Characters/BountyHunter/S0/Ability_E/GameObject_BountyHunter_E_LoSReveal_Source_Reactivate.GameObject_BountyHunter_E_LoSReveal_Source_Reactivate_C"));
            AssertFieldHandles(catalog, SovaRevealProjectileDescriptor.DescriptorPath,
                ("ReplicatedMovement", 10), ("Owner", 11), ("Instigator", 13));
            AssertFieldHandles(catalog, SovaRevealDeviceDescriptor.DescriptorPath,
                ("Owner", 11), ("Instigator", 13));
            AssertFieldHandles(catalog, SovaRevealPulseDescriptor.DescriptorPath,
                ("Owner", 11), ("Instigator", 13));
            AssertFieldHandles(catalog, FadeRevealProjectileDescriptor.DescriptorPath,
                ("ReplicatedMovement", 10), ("Owner", 11), ("Instigator", 13));
            AssertFieldHandles(catalog, FadeRevealDeviceDescriptor.DescriptorPath,
                ("Owner", 11), ("Instigator", 13));
        });
    }

    private static void AssertFieldHandles(DescriptorCatalog catalog, string path,
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
}
