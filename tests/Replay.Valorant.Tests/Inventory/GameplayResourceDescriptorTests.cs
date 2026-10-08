using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Control;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.Inventory;

public sealed class GameplayResourceDescriptorTests
{
    [Test]
    public void RecordedFuelRadiusAndSharedSlotScalarsConsumeTheirEntirePayload()
    {
        Assert.That(Decode(new AbilityFuelComponentDescriptor(), "CurrentFuel", "000000000000F03F", 64).DoubleValue, Is.EqualTo(1));
        Assert.That(Decode(new AbilityFuelComponentDescriptor(), "IsFuelDraining", "01", 1).BoolValue, Is.True);
        Assert.That(Decode(new AbilityRadiusComponentDescriptor(), "RuntimeRadius", "0000E144", 32).FloatValue, Is.EqualTo(1800));
        Assert.That(Decode(new ExternalResourceComponentDescriptor(), "ExternalSlot", "04", 8).ByteValue, Is.EqualTo(4));
        Assert.That(Decode(new BlueprintResourceVisualizationDescriptor(), "Ammo", "05000000", 32).Int32Value, Is.EqualTo(5));
        Assert.That(Decode(new AbilityGunAmmoResourceDescriptor(), "AuthResourceAmount", "03000000", 32).Int32Value, Is.EqualTo(3));
        Assert.That(Decode(new AbilityCooldownComponentDescriptor(), "StartTimeStamp", "0000000000004E40", 64).DoubleValue, Is.EqualTo(60));
    }

    [TestCase(ControllablePawnDescriptor.Skye)]
    [TestCase(ControllablePawnDescriptor.Sova)]
    [TestCase(ControllablePawnDescriptor.Tejo)]
    [TestCase(ControllablePawnDescriptor.Gekko)]
    public void PawnPayloadRetainsItsRegisteredPathAndDecodesOwnership(string path)
    {
        var descriptor = new ControllablePawnDescriptor(path);
        Assert.That(((ControllablePawnDescriptor)descriptor.CreatePayloadInstance()).Path, Is.EqualTo(path));
        Assert.That(Decode(descriptor, "PlayerState", "54", 8).NetGuidValue, Is.EqualTo(42));
        Assert.That(ValorantDescriptors.CreateCatalog().ExportGroupDescriptors.Any(d => d.Path == path), Is.True);
    }

    [Test]
    public void ProductionCatalogBindsClasslessGameplaySubobjects()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        foreach (var name in new[] { "PossessableActorComponent", "Comp_AbilityFuelSystem", "MagazineAmmo",
                     "ReserveAmmo", "ExternalResource", "Comp_Ability_GunAmmoResourceComponent", "AbilityRadius" })
        {
            Assert.That(catalog.SubobjectClassPaths.ContainsKey(name), Is.True, name);
            Assert.That(catalog.ExportGroupDescriptors.Any(d => d.Path == catalog.SubobjectClassPaths[name]), Is.True, name);
        }
    }

    [Test]
    public void TruncatedRecordedScalarsFailWithinTheBoundedPayload()
    {
        Assert.Catch(() => Decode(new AbilityFuelComponentDescriptor(), "CurrentFuel", "000000000000F03F", 63));
        Assert.Catch(() => Decode(new AbilityRadiusComponentDescriptor(), "RuntimeRadius", "0000E144", 31));
        Assert.Catch(() => Decode(new ExternalResourceComponentDescriptor(), "ExternalSlot", "04", 7));
    }
    private static DecodedFieldValue Decode(ExportGroupDescriptor descriptor, string field, string hex, int bits)
    {
        var decoder = (IFieldDecoder)descriptor.Fields.Single(f => f.ExportName == field).Decoder!;
        using var archive = new BitArchiveReader(Convert.FromHexString(hex), bits);
        var context = new FieldDecodeContext();
        var value = decoder.Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True);
        return value;
    }
}
