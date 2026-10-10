using NUnit.Framework;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors;
using Bits = Replay.Valorant.Tests.Combat.DamageLifeChangeEventTests.Bits;

namespace Replay.Valorant.Tests.Combat;

public sealed class LifeChangeRpcDecodingTests
{
    [TestCase(6u, "MulticastNotifyHeal", typeof(MulticastNotifyHealParameters))]
    [TestCase(7u, "MulticastNotifyOverhealDecay", typeof(MulticastNotifyOverhealDecayParameters))]
    [TestCase(8u, "MulticastSectionLifeChange", typeof(MulticastSectionLifeChangeParameters))]
    public void RecordedFunctionHandlesBindTypedParameters(uint handle, string name, Type parameterType)
    {
        var function = new DamageableComponentClassNetCacheDescriptor().FunctionFields.Single(f => f.Handle == handle);
        Assert.That(function.Name, Is.EqualTo(name));
        Assert.That(function.ParameterDescriptor, Is.TypeOf(parameterType));
        Assert.That(function.ParameterDescriptor!.Grammar, Is.EqualTo(FieldStreamGrammar.FunctionParameters));
    }

    [Test]
    public void HealAndDecayArraysKeepSeparatePoolsAndFalseBoolean()
    {
        var writer = new Bits(); writer.Packed(2);
        Entry(writer, 0, 2, 21, 60, 30); Entry(writer, 1, 2, 22, 50, -10); writer.Packed(0);
        var raw = writer.Raw();
        foreach (var descriptor in new ExportGroupDescriptor[] { new MulticastNotifyHealParameters(), new MulticastNotifyOverhealDecayParameters() })
        {
            using var archive = new BitArchiveReader(raw.Data, raw.BitCount);
            var context = new FieldDecodeContext();
            var decoder = (IFieldDecoder)descriptor.Fields.Single(f => f.Handle == 1).Decoder!;
            var entries = (HealLifeChangeEvent?[])decoder.Decode(ref context, archive).ObjectValue!;
            Assert.Multiple(() =>
            {
                Assert.That(archive.AtEnd, Is.True); Assert.That(entries, Has.Length.EqualTo(2));
                Assert.That(entries[0]!.ChangedComponent, Is.EqualTo(21)); Assert.That(entries[0]!.LifeResult, Is.EqualTo(60));
                Assert.That(entries[1]!.ChangedComponent, Is.EqualTo(22)); Assert.That(entries[1]!.DeltaLife, Is.EqualTo(-10));
                Assert.That(entries[1]!.AliveAfterChange, Is.False);
                Assert.That(entries[1]!.HasDecoded(nameof(HealLifeChangeEvent.AliveAfterChange)), Is.True);
            });
        }
    }

    [Test]
    public void SectionLifeUsesItsOwnChildHandleOffsetAndExplicitZero()
    {
        var writer = new Bits(); writer.Packed(1); Entry(writer, 0, 1, 21, 0, -10); writer.Packed(0);
        var raw = writer.Raw(); using var archive = new BitArchiveReader(raw.Data, raw.BitCount);
        var context = new FieldDecodeContext();
        var decoder = (IFieldDecoder)new MulticastSectionLifeChangeParameters().Fields.Single(f => f.Handle == 0).Decoder!;
        var entries = (SectionLifeChangeEvent?[])decoder.Decode(ref context, archive).ObjectValue!;
        Assert.Multiple(() =>
        {
            Assert.That(archive.AtEnd, Is.True); Assert.That(entries[0]!.ChangedComponent, Is.EqualTo(21));
            Assert.That(entries[0]!.LifeResult, Is.Zero); Assert.That(entries[0]!.HasDecoded(nameof(SectionLifeChangeEvent.LifeResult)), Is.True);
        });
    }

    [Test]
    public void MissingNestedValuesStayAbsentAndMalformedFloatIsRejected()
    {
        var sparse = new Bits(); sparse.Packed(2); sparse.Packed(2);
        sparse.Field(5, b => b.Bit(false)); sparse.Packed(0); sparse.Packed(0);
        var raw = sparse.Raw(); using var archive = new BitArchiveReader(raw.Data, raw.BitCount);
        var context = new FieldDecodeContext(); var decoder = RepLayoutArrayDecoders.DynamicArray<HealLifeChangeEvent>();
        var entries = (HealLifeChangeEvent?[])decoder.Decode(ref context, archive).ObjectValue!;
        Assert.That(entries[0], Is.Null); Assert.That(entries[1]!.HasDecoded(nameof(HealLifeChangeEvent.LifeResult)), Is.False);
        var invalid = new Bits(); invalid.Packed(1); invalid.Packed(1);
        invalid.Field(3, b => { for (var i = 0; i < 31; i++) b.Bit(false); }); invalid.Packed(0); invalid.Packed(0);
        var bad = invalid.Raw(); using var malformed = new BitArchiveReader(bad.Data, bad.BitCount);
        Assert.Throws<ArchiveReadException>(() => decoder.Decode(ref context, malformed));
    }

    private static void Entry(Bits writer, uint index, uint firstHandle, uint section, float life, float delta)
    {
        writer.Packed(index + 1); writer.Field(firstHandle, b => b.Packed(section));
        writer.Field(firstHandle + 1, b => b.Float(life)); writer.Field(firstHandle + 2, b => b.Float(delta));
        writer.Field(firstHandle + 3, b => b.Bit(false)); writer.Packed(0);
    }
}
