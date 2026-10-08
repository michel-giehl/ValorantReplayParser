using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents.Gumshoe;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class GumshoeDescriptorTests
{
    [Test]
    public void CatalogRegistersCurrentCypherActorsAndNamedBindings()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var actors = GumshoeDescriptors.CreateDescriptors().OfType<GumshoeActorDescriptor>().ToArray();
        Assert.That(actors, Has.Length.EqualTo(15));
        foreach (var actor in actors)
        {
            Assert.That(catalog.ExportGroupDescriptors.Count(d => d.Path == actor.Path), Is.EqualTo(1));
            Assert.That(actor.CreatePayloadInstance().GetType(), Is.EqualTo(actor.GetType()));
            var fields = new NetFieldExport?[64];
            fields[11] = new() { CompatibleChecksum = 0, Handle = 11, Name = "Owner" };
            fields[13] = new() { CompatibleChecksum = 0, Handle = 13, Name = "Instigator" };
            fields[10] = new() { CompatibleChecksum = 0, Handle = 10, Name = "ReplicatedMovement" };
            fields[17] = new() { CompatibleChecksum = 0, Handle = 17, Name = "Deployed" };
            fields[58] = new() { CompatibleChecksum = 0, Handle = 58, Name = "CreatedByCharacter" };
            var registry = new ExportBindingRegistry(catalog);
            registry.OnExportGroupAdded(new() { PathName = actor.Path, PathNameIndex = 1, NetFieldExports = fields });
            var bound = registry.GetBoundGroup(actor.Path)!;
            Assert.That(bound.FieldsByHandle[11].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[11].Name, Is.EqualTo("Owner"));
            Assert.That(bound.FieldsByHandle[13].Enabled, Is.True);
        }
        foreach (var cache in GumshoeDescriptors.CreateClassNetCacheDescriptors())
            Assert.That(catalog.ClassNetCacheDescriptors.Count(c => c.Path == cache.Path), Is.EqualTo(1));
        Assert.That(new TripWireGameObjectDescriptor().Path, Is.EqualTo(GumshoePaths.Tripwire));
        Assert.That(new CageTrapProjectileDescriptor().Path, Is.EqualTo(GumshoePaths.CageProjectile));
    }

    [Test]
    public void RecordedOwnersAndPairReferencesConsumeBoundedPayloads()
    {
        // 3f0a3366, packets 5189, 224246, 37836/37838. Exact field payloads.
        Assert.That(Decode(new TripWireGameObjectDescriptor(), "Owner", "FRY=", 16).NetGuidValue, Is.EqualTo(1418));
        Assert.That(Decode(new InterrogateHatDescriptor(), "Owner", "ORY=", 16).NetGuidValue, Is.EqualTo(1436));
        Assert.That(Decode(new TripwireEnemyParameters(), "PairedWire", "6Vg=", 16).NetGuidValue, Is.EqualTo(5748));
        Assert.That(Decode(new TripwireEnemyParameters(), "PairedWire", "sVg=", 16).NetGuidValue, Is.EqualTo(5720));
        Assert.That(new TripwireEnemyParameters().Grammar, Is.EqualTo(FieldStreamGrammar.FunctionParameters));
        Assert.Throws<ArchiveReadException>(() => Decode(new TripwireEnemyParameters(), "PairedWire", "6Q==", 8));
    }

    [Test]
    public void RecordedCameraAndWireFlagsPreserveMissingVersusFalse()
    {
        // Packets 9858 (possession), 3943 (deployment), 5257 (wire deployment).
        var camera = new CameraPawnDescriptor();
        Assert.That(camera.Possessed, Is.Null);
        Assert.That(camera.IsDeployed, Is.Null);
        Assert.That(Decode(camera, "Possessed", "AQ==", 1).BoolValue, Is.True);
        Assert.That(Decode(camera, "Possessed", "AA==", 1).BoolValue, Is.False);
        Assert.That(Decode(camera, "IsDeployed", "AQ==", 1).BoolValue, Is.True);
        Assert.That(Decode(new TripWireGameObjectDescriptor(), "Deployed", "AQ==", 1).BoolValue, Is.True);
        Assert.That(Decode(new CameraTrackingDartDescriptor(), "Target", "l3o=", 16).NetGuidValue, Is.EqualTo(7883));
        Assert.That(camera.HasDecoded(nameof(camera.Possessed)), Is.False);
    }

    [Test]
    public void RecordedProjectileAndZoneMovementUsesByteRotation()
    {
        // Packets 8929 and 183688. Packed locations are meters; analyser converts to Unreal centimeters.
        var projectile = Decode(new CageTrapProjectileDescriptor(), "ReplicatedMovement", "4HSs1k1poX0STGb6LgcH", 115).RepMovementValue;
        Assert.That(projectile.Location!.Value.X, Is.EqualTo(55.18).Within(0.001));
        Assert.That(projectile.Location.Value.ScaleFactor, Is.EqualTo(100));
        var zone = Decode(new CageZoneDescriptor(), "ReplicatedMovement", "4BSp81eWQGBBAA==", 74).RepMovementValue;
        Assert.That(zone.Location!.Value.X, Is.EqualTo(-27.82).Within(0.001));
        Assert.That(zone.Rotation!.Value.Yaw, Is.EqualTo(270));
        Assert.Throws<ArchiveReadException>(() => Decode(new CageZoneDescriptor(), "ReplicatedMovement", "4BSp81eWQGBBAA==", 73));
        var dart = Decode(new CameraDartProjectileDescriptor(), "ReplicatedMovement", "4CysBcrmYvwf0NSARSSGcQ==", 127).RepMovementValue;
        Assert.That(dart.Location!.Value.X, Is.EqualTo(-26.83).Within(0.001));
    }

    [Test]
    public void EffectContainerNamesResolveOnlyDecodedWireReferences()
    {
        var cache = new NetGuidCache();
        cache.SetNetGuidPath(7239, "FXC_Gumshoe_X_Telegraph_Timer_Prototype_Reping_C");
        var context = new FieldDecodeContext { NetGuidCache = cache };
        var payload = new EffectManagerPlayContinuousParameters { EffectContainer = 7239 };
        payload.EmitDecodedEvents(ref context);
        Assert.That(payload.EffectContainerPath, Is.Null);
        payload.MarkDecoded(nameof(payload.EffectContainer));
        payload.EmitDecodedEvents(ref context);
        Assert.That(payload.EffectContainer, Is.EqualTo(7239));
        Assert.That(payload.EffectContainerPath, Is.EqualTo("FXC_Gumshoe_X_Telegraph_Timer_Prototype_Reping_C"));
        var unknown = new EffectManagerPlayOneShotParameters { EffectContainer = 123 };
        unknown.MarkDecoded(nameof(unknown.EffectContainer));
        unknown.EmitDecodedEvents(ref context);
        Assert.That(unknown.EffectContainerPath, Is.Null);
    }

    private static DecodedFieldValue Decode(ExportGroupDescriptor descriptor, string property, string base64, int bits)
    {
        using var archive = new BitArchiveReader(Convert.FromBase64String(base64), bits);
        var context = new FieldDecodeContext();
        var decoder = (IFieldDecoder)descriptor.Fields.Single(f => f.PropertyName == property).Decoder!;
        var value = decoder.Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True, property);
        return value;
    }
}
