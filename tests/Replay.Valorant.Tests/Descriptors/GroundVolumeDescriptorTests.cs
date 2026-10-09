using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.DynamicVolume;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class GroundVolumeDescriptorTests
{
    [TestCase(1, 147899, 72, 72)]
    [TestCase(2, 148335, 71, 71)]
    [TestCase(3, 148335, 71, 71)]
    [TestCase(4, 182635, 88, 88)]
    [TestCase(5, 176639, 85, 85)]
    [TestCase(6, 166703, 81, 80)]
    [TestCase(7, 189699, 92, 89)]
    [TestCase(8, 163659, 80, 79)]
    public void RecordedNativePayload_DecodesAllFragmentsAndConsumesEveryBit(int cast, int bits, int count, int active)
    {
        using var archive = new BitArchiveReader(File.ReadAllBytes(Fixture(cast)).AsMemory(), bits);
        var context = new FieldDecodeContext();
        var result = new FieldPayloadParser().ParseRepLayoutProperties(archive, Binding(), ref context);
        var volume = (GroundVolumeComponentDescriptor)result.Payload!;
        Assert.Multiple(() =>
        {
            Assert.That(archive.AtEnd, Is.True);
            Assert.That(volume.FinalCount, Is.EqualTo(count));
            Assert.That(volume.HasDecoded(nameof(volume.FragmentDelta)), Is.True);
            Assert.That(volume.FragmentDelta!.BaseKey, Is.Zero);
            Assert.That(volume.FragmentDelta.Updates, Has.Count.EqualTo(count));
            Assert.That(volume.FragmentDelta.Updates.Count(f => f.IsActive == true && f.Status != 3), Is.EqualTo(active));
            Assert.That(volume.FragmentDelta.Updates.Select(f => f.ReplicationId).Distinct().Count(), Is.EqualTo(count));
        });
        foreach (var fragment in volume.FragmentDelta!.Updates)
        {
            Assert.That(fragment.ConvexHullPoints, Is.Not.Null);
            Assert.That(fragment.ConvexHullPoints!.Length, Is.EqualTo(fragment.ConvexHullCeilings!.Length));
            Assert.That(fragment.ConvexHullPoints.Length, Is.EqualTo(fragment.ConvexHullTravelDistances!.Length));
            Assert.That(fragment.ConvexHullPoints.All(p => p?.Point is not null), Is.True);
        }
        if (cast == 1)
        {
            var point = volume.FragmentDelta.Updates[0].ConvexHullPoints![0]!.Point!.Value;
            Assert.That(point.X, Is.EqualTo(2280.98388671875).Within(1e-6));
            Assert.That(point.Z, Is.EqualTo(485.0000305175781).Within(1e-6));
        }
    }

    [Test]
    public void TruncatedNativePayload_FailsInsteadOfPublishingPartialGeometry()
    {
        using var archive = new BitArchiveReader(File.ReadAllBytes(Fixture(1)).AsMemory(), 147898);
        var context = new FieldDecodeContext();
        Assert.Throws<InvalidReplayDataException>(() => new FieldPayloadParser()
            .ParseRepLayoutProperties(archive, Binding(), ref context));
    }

    private static string Fixture(int cast) => Path.Combine(TestContext.CurrentContext.TestDirectory,
        "Fixtures", "ViperPit", $"cast-{cast}.bin");
    private static BoundExportGroup Binding()
    {
        var descriptor = ValorantDescriptors.CreateCatalog().ExportGroupDescriptors
            .Single(d => d.Path == GroundVolumeComponentDescriptor.ExportPath);
        var fields = new FieldBinding[22];
        foreach (var f in descriptor.Fields) fields[f.Handle!.Value] = new()
        {
            Enabled = true, Name = f.PropertyName, TargetProperty = f.TargetProperty, Decoder = (IFieldDecoder)f.Decoder!,
        };
        return new() { SourceDescriptor = descriptor, FieldsByHandle = fields, Enabled = true,
            Categories = ExportCategory.Ability, Grammar = FieldStreamGrammar.RepLayoutProperties };
    }
}
