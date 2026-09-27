using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents.Clay;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class ClayDescriptorTests
{
    [Test]
    public void CatalogRegistersAllObservedRazeActorsAndFunctions()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var actors = ClayDescriptors.CreateDescriptors().OfType<ClayActorDescriptor>().ToArray();
        Assert.That(actors, Has.Length.EqualTo(11));
        foreach (var actor in actors)
        {
            Assert.That(catalog.ExportGroupDescriptors.Count(d => d.Path == actor.Path), Is.EqualTo(1));
            Assert.That(actor.CreatePayloadInstance().GetType(), Is.EqualTo(actor.GetType()));
            Assert.That(actor.Fields.Single(f => f.PropertyName == "Owner").Handle, Is.EqualTo(11));
            Assert.That(actor.Fields.Single(f => f.PropertyName == "Instigator").Handle, Is.EqualTo(13));
            Assert.That(actor.Fields.All(f => f.Decoder is not null), Is.True);
        }
        var caches = ClayDescriptors.CreateClassNetCacheDescriptors();
        Assert.That(caches, Has.Count.EqualTo(9));
        Assert.That(caches.Sum(c => c.FunctionFields.Count), Is.EqualTo(10));
        foreach (var cache in caches)
            Assert.That(catalog.ClassNetCacheDescriptors.Count(c => c.Path == cache.Path), Is.EqualTo(1));
        var character = catalog.ClassNetCacheDescriptors.Single(c => c.Path == new ClayAgentDescriptor().Path + "_ClassNetCache");
        Assert.That(character.FunctionFields.Any(f => f.Name == "ClientResetRemoteMovementPrediction" && f.Handle == 6), Is.True);
        Assert.That(character.FunctionFields.Any(f => f.Name == "MulticastNotifyKilledEnemy"), Is.True);
    }

    [Test]
    public void BoomBotUsesShortRotationAndProjectilesUseByteRotation()
    {
        foreach (var actor in ClayDescriptors.CreateDescriptors().OfType<ClayActorDescriptor>())
        {
            var field = actor.Fields.SingleOrDefault(f => f.PropertyName == "ReplicatedMovement");
            if (field is null) continue;
            Assert.That(field.Decoder, Is.SameAs(actor is ClayBoomBotPawnDescriptor
                ? PrimitiveDecoders.RepMovement : PrimitiveDecoders.RepMovementByte), actor.Path);
        }
    }

    [TestCase("AgJeIEkGAAA=", 420u)] // packet 1456: first satchel
    [TestCase("AgJeIL0GAAA=", 478u)] // packet 1764: second satchel
    public void RecordedFocusArrayDecodesProjectileReference(string base64, uint expected)
    {
        var value = Decode(new ClayAgentDescriptor(), "FocusProjectiles", base64, 64);
        var references = (ClayFocusProjectileReference[])value.ObjectValue!;
        Assert.That(references, Has.Length.EqualTo(1));
        Assert.That(references[0].ActorNetGuid, Is.EqualTo(expected));
    }

    [Test]
    public void RecordedFocusArrayCanBeCleared()
    {
        var value = Decode(new ClayAgentDescriptor(), "FocusProjectiles", "AAA=", 16);
        Assert.That((ClayFocusProjectileReference[])value.ObjectValue!, Is.Empty);
    }

    [Test]
    public void RecordedSatchelAttachmentFieldsConsumeTheirExactBits()
    {
        // Replay 42e03082, packet 1508, actor 420. Actual bounded wire payloads.
        var satchel = new ClaySatchelProjectileDescriptor();
        var location = Decode(satchel, "LocationOffset", "0yBnt6iXSAA=", 64).VectorValue;
        var rotation = Decode(satchel, "RotationOffset", "AYDuJ/f/Bw==", 51).RotatorValue;
        Assert.That(double.IsFinite(location.X) && double.IsFinite(location.Y) && double.IsFinite(location.Z), Is.True);
        Assert.That(double.IsFinite(rotation.Pitch) && double.IsFinite(rotation.Yaw) && double.IsFinite(rotation.Roll), Is.True);
        Assert.That(Decode(satchel, "RemoteRole", "AQ==", 3).UInt32Value, Is.EqualTo(1));
        Assert.That(Decode(satchel, "Role", "Aw==", 3).UInt32Value, Is.EqualTo(3));
        Assert.That(Decode(new ClaySatchelAbilityDescriptor(), "CosmeticRandomSeed", "4elLQA==", 32).Int32Value, Is.EqualTo(1078716897));
    }

    [Test]
    public void TruncatedAttachmentIsRejected()
    {
        Assert.Throws<ArchiveReadException>(() => Decode(new ClaySatchelProjectileDescriptor(), "RotationOffset", "AQ==", 8));
    }

    [Test]
    public void ForcePayloadHasBoundedTypedFieldsAndRetainsNestedMovementTimestamp()
    {
        var descriptor = new RazeForceParameters();
        Assert.That(descriptor.Grammar, Is.EqualTo(FieldStreamGrammar.FunctionParameters));
        Assert.That(descriptor.Fields.Select(f => f.Handle), Is.EqualTo(Enumerable.Range(0, 9)));
        Assert.That(Decode(descriptor, "Duration", Convert.ToBase64String(BitConverter.GetBytes(0.6f)), 32).FloatValue, Is.EqualTo(0.6f));
        Assert.That(Decode(descriptor, "NetTimestamp", Convert.ToBase64String(BitConverter.GetBytes(9.6625f)), 32).FloatValue, Is.EqualTo(9.6625f));
        Assert.That(Decode(descriptor, "RespawnNumber", Convert.ToBase64String(BitConverter.GetBytes(3)), 32).Int32Value, Is.EqualTo(3));
        Assert.Throws<ArchiveReadException>(() => Decode(descriptor, "Duration", "AA==", 8));
        var catalog = ValorantDescriptors.CreateCatalog();
        var force = catalog.ClassNetCacheDescriptors.Single(c => c.Path == "/Script/ShooterGame.ForceModuleManagerComponent_ClassNetCache");
        Assert.That(force.FunctionFields.Any(f => f.Name == "NetMulticastApplyForceModule"), Is.True);
        Assert.That(catalog.ClassNetCacheDescriptors.Any(c => c.Path == RazeProjectileEffectDescriptor.ClassPath + "_ClassNetCache"), Is.True);
    }
    private static DecodedFieldValue Decode(ExportGroupDescriptor descriptor, string property, string base64, int bits)
    {
        using var archive = new BitArchiveReader(Convert.FromBase64String(base64), bits);
        var context = new FieldDecodeContext();
        var decoder = (IFieldDecoder)descriptor.Fields.Single(f => f.PropertyName == property).Decoder!;
        var result = decoder.Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True, property);
        return result;
    }
}

