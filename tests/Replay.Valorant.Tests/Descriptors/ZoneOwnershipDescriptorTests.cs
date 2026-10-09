using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class ZoneOwnershipDescriptorTests
{
    private const string Meddle = "/Game/Characters/Smonk/S0/Ability_Q/DebuffKnife/DecayLauncher/GameObject_Smonk_Q_DecayExplosion.GameObject_Smonk_Q_DecayExplosion_C";
    private const string Seize = "/Game/Characters/BountyHunter/S0/Ability_Q/GameObject_Q_BountyHunter_Tether_SphereExpansion.GameObject_Q_BountyHunter_Tether_SphereExpansion_C";

    [TestCase(Meddle)]
    [TestCase(Seize)]
    public void CatalogRegistersOnlyTheObservedOwnershipFieldsOnce(string path)
    {
        var descriptor = Descriptor(path);
        Assert.That(descriptor.Kind, Is.EqualTo(ExportGroupKind.Actor));
        Assert.That(descriptor.Fields.Select(f => f.ExportName), Is.EqualTo(new[] { "Owner", "Instigator" }));
        var fresh = (AbilityActorDescriptor)descriptor.CreatePayloadInstance();
        Assert.That(fresh.Path, Is.EqualTo(path));
        Assert.That(fresh.Owner, Is.Null);
        Assert.That(fresh.Instigator, Is.Null);
        Assert.That(fresh.DecodedProperties, Is.Empty);
    }

    [TestCase(Meddle)]
    [TestCase(Seize)]
    public void ObservedHandleBindingsDecodeReferencesAndConsumeTheirExactBoundary(string path)
    {
        // Minimal field stream using the observed Owner=11 / Instigator=13 bindings and
        // the Meddle references from 3f0a3366 (zone 25116). No gameplay interpretation here.
        var actor = Parse(path, "MGCqDwU4QKIdAAA=", 81);
        Assert.That(actor.Owner, Is.EqualTo(25066));
        Assert.That(actor.Instigator, Is.EqualTo(1000));
        Assert.That(actor.HasDecoded(nameof(actor.Owner)), Is.True);
        Assert.That(actor.HasDecoded(nameof(actor.Instigator)), Is.True);
    }

    [TestCase(Meddle)]
    [TestCase(Seize)]
    public void MissingReferencesDifferFromDecodedZeroAndTruncatedFieldsFail(string path)
    {
        var absent = Parse(path, "AAA=", 9);
        Assert.That(absent.Owner, Is.Null);
        Assert.That(absent.Instigator, Is.Null);
        Assert.That(absent.DecodedProperties, Is.Empty);
        var cleared = Parse(path, "OCAAAAA=", 33);
        Assert.That(cleared.Owner, Is.Null);
        Assert.That(cleared.Instigator, Is.Zero);
        Assert.That(cleared.HasDecoded(nameof(cleared.Instigator)), Is.True);
        Assert.Throws<InvalidReplayDataException>(() => Parse(path, "MGCqDwU4QKIdAAA=", 64));
    }

    private static AbilityActorDescriptor Descriptor(string path) =>
        (AbilityActorDescriptor)ValorantDescriptors.CreateCatalog().ExportGroupDescriptors.Single(d => d.Path == path);

    private static AbilityActorDescriptor Parse(string path, string raw, int bits)
    {
        var descriptor = Descriptor(path);
        var fields = new FieldBinding[14];
        foreach (var field in descriptor.Fields)
            fields[field.ExportName == "Owner" ? 11 : 13] = new() { Enabled = true, Decoder = (IFieldDecoder)field.Decoder!,
                Name = field.PropertyName, TargetProperty = field.TargetProperty, Categories = ExportCategory.Ability };
        var group = new BoundExportGroup { SourceDescriptor = descriptor, FieldsByHandle = fields,
            Grammar = FieldStreamGrammar.RepLayoutProperties, Enabled = true };
        var context = new FieldDecodeContext();
        using var archive = new BitArchiveReader(Convert.FromBase64String(raw), bits);
        var result = new FieldPayloadParser().ParseRepLayoutProperties(archive, group, ref context);
        Assert.That(archive.AtEnd, Is.True);
        return (AbilityActorDescriptor)result.Payload!;
    }
}
