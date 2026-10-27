using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents.Mage.TidalWave;
using Replay.Valorant.Descriptors.Agents.Sequoia;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class IsoDescriptorTests
{
    // d9a807b2, moving-cover actor 8914: bounded Trajectory payloads.
    private const string InitialTrajectory =
        "DAIIgQLGUvd4LTJ7wHJHtbsTh7PAAAAAAABAakAABAiBAqCP7T0em3LAgKsslA6Ds8AY+hqQWQRpQAAGCIEC8JjHBR4IZMCOD6RsCX+zwG/3KeMpCmlAAAgIgQIAlaB9/M82wJxzG0UEe7PAB0rpQAoJaUAACgiBAlznvsw9qFxAqteSHf92s8Ck5Kae6gdpQAAMCIEC/PlyXD2Cb0C4Owr2+XKzwAQuS+HYAGlAAAA=";
    private const string TrajectoryDelta =
        "Fg4IgQIlQEPpLVh4QMafgc70brPA6vtthXwAaUAAEAiBAqaBJpKed4BA1AP5pu9qs8DSyZApIABpQAASCIECOmOrLybDhEDiZ3B/6mazwNLJkCkgAGlAABQIgQLORDDNrQ6JQPDL51flYrPA0smQKSAAaUAAFgiBAmImtWo1Wo1A/i9fMOBes8DSyZApIABpQAAA";

    [Test]
    public void RecordedTrajectoryDecodesNestedVectorsAndConsumesEveryBit()
    {
        var points = (SplineTrajectoryPoint?[])Decode(nameof(SplineMovementComponentDescriptor.Trajectory),
            InitialTrajectory, 1408).ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(points, Has.Length.EqualTo(6));
            Assert.That(points.All(point => point?.HasDecoded(nameof(SplineTrajectoryPoint.Position)) == true), Is.True);
            Assert.That(points[0]!.Position!.Value.X, Is.EqualTo(-435.1361016903942).Within(1e-9));
            Assert.That(points[0]!.Position!.Value.Y, Is.EqualTo(-4999.07708294863).Within(1e-9));
            Assert.That(points[0]!.Position!.Value.Z, Is.EqualTo(210));
            Assert.That(points[5]!.Position!.Value.X, Is.EqualTo(252.06999037224148).Within(1e-9));
        });
    }

    [Test]
    public void RecordedTrajectoryDeltaPreservesOmittedEntries()
    {
        var points = (SplineTrajectoryPoint?[])Decode(nameof(SplineMovementComponentDescriptor.Trajectory),
            TrajectoryDelta, 1176).ObjectValue!;

        Assert.Multiple(() =>
        {
            Assert.That(points, Has.Length.EqualTo(11));
            Assert.That(points.Take(6).All(point => point is null), Is.True);
            Assert.That(points.Skip(6).All(point => point?.HasDecoded(nameof(SplineTrajectoryPoint.Position)) == true), Is.True);
            Assert.That(points[6]!.Position!.Value.X, Is.EqualTo(389.5112087847686).Within(1e-9));
            Assert.That(points[10]!.Position!.Value.X, Is.EqualTo(939.2760824348773).Within(1e-9));
        });
    }

    [Test]
    public void TruncatedTrajectoryIsRejected()
    {
        Assert.Throws<ArchiveReadException>(() => Decode(nameof(SplineMovementComponentDescriptor.Trajectory),
            InitialTrajectory, 1407));
    }

    [Test]
    public void RecordedSpeedUnitConsumesItsTwoBitPayload()
    {
        Assert.That(Decode(nameof(SplineMovementComponentDescriptor.SpeedUnit), "AQ==", 2).UInt32Value, Is.EqualTo(1));
    }

    [TestCase("Cover1_GEN_VARIABLE.ChildActor_GEN_VARIABLE_GameObject_Sequoia_X_ArenaCover_C_CAT")]
    [TestCase("Cover2_GEN_VARIABLE.Cover2_GEN_VARIABLE_GameObject_Sequoia_X_ArenaCover_C_CAT")]
    [TestCase("Cover3_GEN_VARIABLE.Cover3_GEN_VARIABLE_GameObject_Sequoia_X_ArenaCover_C_CAT")]
    public void RecordedArenaCoverTemplatesBindPropertiesAndIntroRpc(string templatePath)
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog(), releaseVersion: TestReleases.Current);
        registry.OnExportGroupAdded(Group(SequoiaDescriptors.ArenaCoverPath, 1,
            (1, "bHidden"), (3, "216"), (4, "AttachParent"), (6, "RelativeScale3D"),
            (9, "AttachComponent"), (12, "215")));
        registry.OnExportGroupAdded(Group(SequoiaDescriptors.ArenaCoverPath + "_ClassNetCache", 2,
            (0, "MulticastBeginIntroSequence")));

        var properties = registry.GetBoundGroup(templatePath)!;
        var functions = registry.GetBoundCache(templatePath)!;
        Assert.Multiple(() =>
        {
            Assert.That(properties, Is.SameAs(registry.GetBoundGroup(SequoiaDescriptors.ArenaCoverPath)));
            Assert.That(properties.FieldsByHandle[1].Enabled, Is.True);
            Assert.That(properties.FieldsByHandle[4].Enabled, Is.True);
            Assert.That(properties.FieldsByHandle[6].Enabled, Is.True);
            Assert.That(properties.FieldsByHandle[9].Enabled, Is.True);
            Assert.That(functions, Is.SameAs(registry.GetBoundCache(SequoiaDescriptors.ArenaCoverPath)));
            Assert.That(registry.GetBoundCache(templatePath + "_ClassNetCache"), Is.SameAs(functions));
            Assert.That(functions.FunctionsByHandle[0].Enabled, Is.True);
            Assert.That(functions.FunctionsByHandle[0].Name, Is.EqualTo("MulticastBeginIntroSequence"));
        });
    }

    private static DecodedFieldValue Decode(string property, string base64, int bits)
    {
        var descriptor = new SplineMovementComponentDescriptor();
        var decoder = (IFieldDecoder)descriptor.Fields.Single(field => field.PropertyName == property).Decoder!;
        using var archive = new BitArchiveReader(Convert.FromBase64String(base64), bits);
        var context = new FieldDecodeContext();
        var value = decoder.Decode(ref context, archive);
        archive.EnsureFullyConsumed(property);
        return value;
    }

    private static NetFieldExportGroup Group(string path, uint index, params (uint Handle, string Name)[] fields)
    {
        var exports = new NetFieldExport?[fields.Max(field => field.Handle) + 1];
        foreach (var (handle, name) in fields)
            exports[handle] = new NetFieldExport { Handle = handle, Name = name, CompatibleChecksum = 0 };
        return new NetFieldExportGroup { PathName = path, PathNameIndex = index, NetFieldExports = exports };
    }
}
