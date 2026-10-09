using System.Text.Json;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Patches;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class CellularPatchDescriptorTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private static RecordedGrid[] ReadGrids() => JsonSerializer.Deserialize<RecordedGrid[]>(File.ReadAllText(
        System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "CellularPatch", "sage-node-grids.json")), Options)!;
    public static IEnumerable<TestCaseData> RecordedGrids() => ReadGrids().Select(g =>
        new TestCaseData(g).SetName($"SageNodeGrid_Packet_{g.PacketId}"));

    [TestCaseSource(nameof(RecordedGrids))]
    public void RecordedGridsDecodeEveryNodeAndConsumeExactBoundaries(RecordedGrid recorded)
    {
        var grid = Parse(recorded.Base64, recorded.Bits);
        var nodes = grid.Nodes ?? throw new AssertionException("Recorded node array is absent.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(grid.IsValid, Is.True);
            Assert.That(grid.MinX, Is.EqualTo(recorded.MinX));
            Assert.That(grid.MinY, Is.EqualTo(recorded.MinY));
            Assert.That(grid.MaxX, Is.EqualTo(recorded.MaxX));
            Assert.That(grid.MaxY, Is.EqualTo(recorded.MaxY));
            Assert.That(grid.LineSize, Is.EqualTo(recorded.MaxX - recorded.MinX + 1));
            Assert.That(grid.Nodes, Has.Length.EqualTo(recorded.Count));
            Assert.That(grid.HasDecoded(nameof(grid.Nodes)), Is.True);
            Assert.That(nodes.Count(n => n.Status == 3), Is.EqualTo(15));
            Assert.That(nodes.All(n => n.DecodedProperties.Count == 6), Is.True);
            Assert.That(nodes.All(n => n.Status <= 4 && n.Normal.HasValue && n.EdgePoint.HasValue), Is.True);
            Assert.That(nodes.Where(n => n.Status == 3).All(n => float.IsFinite(n.FloorZ!.Value) && n.CeilingZ >= n.FloorZ), Is.True);
        }
    }
    [Test]
    public void CatalogBindsSagesInheritedFunctionOnceWithItsNativeExportPath()
    {
        var cache = ValorantDescriptors.CreateCatalog().ClassNetCacheDescriptors.Single(c => c.Path ==
            "/Game/Characters/Thorne/S0/Ability_4/Patch_Thorne_4_SlowField_Production.Patch_Thorne_4_SlowField_Production_C_ClassNetCache");
        var rpc = cache.FunctionFields.Single(f => f.Name == "NetMulticastUpdateNodeGrid");
        Assert.That(rpc.Handle, Is.EqualTo(3));
        Assert.That(rpc.FunctionExportPath, Is.EqualTo(CellularPatchNodeGridParameters.ExportPath));
        Assert.That(rpc.ParameterDescriptor, Is.TypeOf<CellularPatchNodeGridParameters>());
    }
    [Test]
    public void MissingAndDecodedFalseRemainDistinct()
    {
        var empty = Parse("AAA=", 9);
        Assert.That(empty.IsValid, Is.Null);
        Assert.That(empty.Nodes, Is.Null);
        Assert.That(empty.HasDecoded(nameof(empty.IsValid)), Is.False);
        var decodedFalse = Parse("QAQAAA==", 26);
        Assert.That(decodedFalse.IsValid, Is.False);
        Assert.That(decodedFalse.HasDecoded(nameof(decodedFalse.IsValid)), Is.True);
    }
    [Test]
    public void TruncatedRecordedGridIsRejected()
    {
        var recorded = ReadGrids()[0];
        Assert.Throws<InvalidReplayDataException>(() => Parse(recorded.Base64, recorded.Bits - 16));
    }
    private static CellularPatchNodeGridParameters Parse(string raw, int bits)
    {
        var descriptor = new CellularPatchNodeGridParameters();
        var fields = new FieldBinding[17];
        foreach (var field in descriptor.Fields)
            fields[field.Handle!.Value] = new() { Enabled = true, Decoder = (IFieldDecoder)field.Decoder!,
                Name = field.PropertyName, TargetProperty = field.TargetProperty, Categories = ExportCategory.Ability };
        var group = new BoundExportGroup { SourceDescriptor = descriptor, FieldsByHandle = fields,
            Grammar = FieldStreamGrammar.FunctionParameters, Enabled = true };
        var context = new FieldDecodeContext();
        using var archive = new BitArchiveReader(Convert.FromBase64String(raw), bits);
        var result = new FieldPayloadParser().ParseRepLayoutProperties(archive, group, ref context);
        Assert.That(archive.AtEnd, Is.True);
        return (CellularPatchNodeGridParameters)result.Payload!;
    }
    public sealed record RecordedGrid(int PacketId, int Bits, string Base64, int MinX, int MinY, int MaxX, int MaxY, int Count);
}
