using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.Descriptors.Agents.Cable;
using Replay.Valorant.Descriptors.Agents.Deadeye;
using Replay.Valorant.Descriptors.Agents.Killjoy;
using Replay.Valorant.Descriptors.Agents.Nox;
using Replay.Valorant.Descriptors.Agents.Thorne;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class SentinelDescriptorTests
{

    private static readonly string[] Agents = ["Cable", "Deadeye", "Killjoy", "Nox", "Thorne"];

    [TestCase("Cable", 18)]
    [TestCase("Deadeye", 8)]
    [TestCase("Killjoy", 11)]
    [TestCase("Nox", 8)]
    [TestCase("Thorne", 6)]
    public void AgentCatalogOwnsItsActorAndFunctionRegistrations(string agent, int sparseActors)
    {
        Assert.That(ActorsFor(agent).OfType<AbilityActorDescriptor>().Count(), Is.EqualTo(sparseActors));
        Assert.That(ActorsFor(agent).All(d => d.Path.StartsWith("/Game/Characters/" + agent + "/", StringComparison.Ordinal)), Is.True);
        Assert.That(CachesFor(agent).All(d => d.Path.StartsWith("/Game/Characters/" + agent + "/", StringComparison.Ordinal)), Is.True);
    }

    private static IReadOnlyList<ExportGroupDescriptor> ActorsFor(string agent) => agent switch
    {
        "Cable" => CableDescriptors.CreateDescriptors(),
        "Deadeye" => DeadeyeDescriptors.CreateDescriptors(),
        "Killjoy" => KilljoyDescriptors.CreateDescriptors(),
        "Nox" => NoxDescriptors.CreateDescriptors(),
        "Thorne" => ThorneDescriptors.CreateDescriptors(),
        _ => throw new ArgumentOutOfRangeException(nameof(agent)),
    };

    private static IReadOnlyList<ClassNetCacheDescriptor> CachesFor(string agent) => agent switch
    {
        "Cable" => CableDescriptors.CreateClassNetCacheDescriptors(),
        "Deadeye" => DeadeyeDescriptors.CreateClassNetCacheDescriptors(),
        "Killjoy" => KilljoyDescriptors.CreateClassNetCacheDescriptors(),
        "Nox" => NoxDescriptors.CreateClassNetCacheDescriptors(),
        "Thorne" => ThorneDescriptors.CreateClassNetCacheDescriptors(),
        _ => throw new ArgumentOutOfRangeException(nameof(agent)),
    };

    [Test]
    public void CatalogRegistersSparseActorsAndMergedFunctionCachesExactlyOnce()
    {
        var catalog = ValorantDescriptors.CreateCatalog();
        var descriptors = Agents.SelectMany(ActorsFor).OfType<AbilityActorDescriptor>().ToArray();
        Assert.That(descriptors, Has.Length.EqualTo(51));
        foreach (var descriptor in descriptors)
        {
            Assert.That(catalog.ExportGroupDescriptors.Count(d => d.Path == descriptor.Path), Is.EqualTo(1));
            var fresh = (ExportGroupDescriptor)descriptor.CreatePayloadInstance();
            Assert.That(fresh.Fields.Select(f => f.ExportName), Is.EqualTo(descriptor.Fields.Select(f => f.ExportName)));
            Assert.That(fresh.DecodedProperties, Is.Empty);
        }
        foreach (var cache in Agents.SelectMany(CachesFor))
        {
            Assert.That(catalog.ClassNetCacheDescriptors.Count(c => c.Path == cache.Path), Is.EqualTo(1));
            Assert.That(cache.FunctionFields.Select(f => f.Handle).Distinct().Count(), Is.EqualTo(cache.FunctionFields.Count));
        }
    }

    [Test]
    public void NamedFlagsAndObjectReferencesPreserveMissingAndFalse()
    {
        var actor = new AbilityActorDescriptor("/Game/Test", new HashSet<string> { "IsBurrowed", "Target" });
        Assert.That(actor.IsBurrowed, Is.Null);
        Assert.That(actor.Target, Is.Null);
        Assert.That(actor.Fields.Any(f => f.PropertyName == "Owner"), Is.False);
        Assert.That(DecodeField(actor, "IsBurrowed", "AA==", 1).BoolValue, Is.False);
        Assert.That(DecodeField(actor, "IsBurrowed", "AQ==", 1).BoolValue, Is.True);
        Assert.That(DecodeField(actor, "Target", "FRY=", 16).NetGuidValue, Is.EqualTo(1418));
        Assert.Throws<ArchiveReadException>(() => DecodeField(actor, "Target", "FRY=", 8));
        Assert.That(actor.HasDecoded(nameof(actor.IsBurrowed)), Is.False);
    }

    [Test]
    public void RecordedDeadlockSplineDecodesNestedPointsAndConsumesExactFunctionBoundary()
    {
        // d9a807b2, packet 360044. Array elements retain the exported handle 2.
        const string raw = "BGBSrgUIIlAsBAwCBQAAAMDRquGAAAAAgKQRY4GT9yTL2cDrgAAIDAIFlv+T7YPK4YDO857KzNdigaGxy1wvH+yAAAwMAgXuZG2tbhPjgPozF8GPf2CBUQcn9Rvy74AAEAwCBdSx0iXjXeSAwLGEWQdJXIFxWB+qnMnzgAAUDAIFCrtuuhro5ICI7QOIkFBagcE+KoX5ZPWAABgMAgX0d1I2TB/igAocQgaVsFeBzYADB1gx9oAAHAwCBSKP91s8fdqAxppowWoXU4GxXFk7Wpf3gAAgDAIF+Nnd23uK0IBoNkZk+GZOgY1eRSxxBPmAACQMAgXQbMWgLKfGgLYhxXTNvUmBKROiZFFv+oAAKAwCBQLb2BdKRbmAVJyb4PkERYF9UiDB9N77gAAsDAIFAAAAgLTrpYAAAACAgXVAgQEAAEAEQv2AAAAAAA==";
        var descriptor = new CableGeometryParameters("spline-function", "spline");
        var payload = ParseFunction(descriptor, raw, 2641);
        Assert.That(payload.Points, Has.Length.EqualTo(11));
        Assert.That(payload.HasDecoded(nameof(payload.Points)), Is.True);
        Assert.That(payload.Points![0].Position!.Value.X, Is.EqualTo(269.3381042480469).Within(0.001));
        Assert.That(payload.Points[0].Position!.Value.Y, Is.EqualTo(-4488.8212890625).Within(0.001));
        Assert.That(payload.Points.All(p => p.HasDecoded(nameof(p.Position))), Is.True);
        Assert.That(payload.Pulling, Is.Null);
        Assert.Throws<Replay.Models.Errors.InvalidReplayDataException>(() => ParseFunction(descriptor, raw, 2541));
    }

    private static CableGeometryParameters ParseFunction(CableGeometryParameters descriptor, string raw, int bits)
    {
        var fields = new FieldBinding[descriptor.Fields.Max(f => (int)f.Handle!.Value) + 1];
        foreach (var field in descriptor.Fields)
            fields[field.Handle!.Value] = new() { Enabled = true, Decoder = (IFieldDecoder)field.Decoder!,
                Name = field.PropertyName, TargetProperty = field.TargetProperty, Categories = ExportCategory.Ability };
        var group = new BoundExportGroup { SourceDescriptor = descriptor, FieldsByHandle = fields,
            Grammar = FieldStreamGrammar.FunctionParameters, Enabled = true };
        var context = new FieldDecodeContext();
        using var archive = new BitArchiveReader(Convert.FromBase64String(raw), bits);
        var result = new FieldPayloadParser().ParseRepLayoutProperties(archive, group, ref context);
        Assert.That(archive.AtEnd, Is.True);
        return (CableGeometryParameters)result.Payload!;
    }
    private static DecodedFieldValue DecodeField(ExportGroupDescriptor descriptor, string field, string raw, int bits)
    {
        using var archive = new BitArchiveReader(Convert.FromBase64String(raw), bits);
        var context = new FieldDecodeContext();
        var value = ((IFieldDecoder)descriptor.Fields.Single(f => f.PropertyName == field).Decoder!).Decode(ref context, archive);
        Assert.That(archive.AtEnd, Is.True);
        return value;
    }
}
