using NUnit.Framework;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Tests.Combat;

public sealed class DamageLifeChangeEventTests
{
    [TestCase(177, "02021620C32A18400000C6421A400000D0C11C02010000", 2785u, 99f, -26f, 125f)]
    [TestCase(338, "04021620E32C1840000000001A40000000801C0201082C40F65930800000DA8734800000A0833904020000", 2941u, 474f, -26f, 500f)]
    [TestCase(177, "02021620A32A18400000B4431A40000020C21C02010000", 2769u, 360f, -40f, 400f)]
    public void RecordedPoolsDecodeWithoutLosingRawPayload(int bits, string hex, uint section, float result, float delta, float preHit)
    {
        var raw = new ValorantRawPayload("LifeChangeEvents", bits, Convert.FromHexString(hex));
        Assert.That(DamageLifeChangeEvents.TryDecode(raw, out var entries), Is.True);
        var selected = entries.Single(e => e?.ChangedComponent == section)!;
        Assert.Multiple(() =>
        {
            Assert.That(selected.LifeResult, Is.EqualTo(result));
            Assert.That(selected.DeltaLife, Is.EqualTo(delta));
            Assert.That(selected.LifeResult - selected.DeltaLife, Is.EqualTo(preHit));
            Assert.That(selected.AliveAfterChange, Is.True);
            Assert.That(selected.DecodedProperties.Count, Is.EqualTo(4));
            Assert.That(Convert.ToHexString(raw.Data.Span), Is.EqualTo(hex));
        });
    }

    [TestCase(177, "02021620C32A1840000000001A400000A8C11C02010000")]
    [TestCase(338, "04021620E32C1840000000001A40000000801C0201082C40F6593080000000003480000080833904020000")]
    [TestCase(177, "02021620A32A1840000000001A40000020C21C02010000")]
    public void UtilityZeroLifeDoesNotRewriteRecordedAliveFlag(int bits, string hex)
    {
        Assert.That(DamageLifeChangeEvents.TryDecode(new("LifeChangeEvents", bits, Convert.FromHexString(hex)), out var entries), Is.True);
        Assert.That(entries.Any(e => e is { LifeResult: 0, DeltaLife: < 0, AliveAfterChange: true }), Is.True);
    }

    [Test]
    public void SparseArrayAndFalseBoolKeepDecodedPresence()
    {
        var writer = new Bits(); writer.Packed(2); writer.Packed(2);
        writer.Field(13, p => p.Bit(false)); writer.Packed(0); writer.Packed(0);
        Assert.That(DamageLifeChangeEvents.TryDecode(writer.Raw(), out var entries), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Length.EqualTo(2)); Assert.That(entries[0], Is.Null);
            Assert.That(entries[1]!.HasDecoded(nameof(DamageLifeChangeEvent.AliveAfterChange)), Is.True);
            Assert.That(entries[1]!.AliveAfterChange, Is.False);
            Assert.That(entries[1]!.HasDecoded(nameof(DamageLifeChangeEvent.LifeResult)), Is.False);
        });
    }

    [Test]
    public void TruncatedArrayAndIncorrectFloatWidthAreRejected()
    {
        var valid = new ValorantRawPayload("LifeChangeEvents", 177,
            Convert.FromHexString("02021620C32A18400000C6421A400000D0C11C02010000"));
        Assert.That(DamageLifeChangeEvents.TryDecode(valid with { BitCount = 150, Data = valid.Data[..19] }, out _), Is.False);
        var shortFloat = new Bits(); shortFloat.Packed(1); shortFloat.Packed(1);
        shortFloat.Field(11, p => { for (var i = 0; i < 31; i++) p.Bit(false); });
        shortFloat.Packed(0); shortFloat.Packed(0);
        Assert.That(DamageLifeChangeEvents.TryDecode(shortFloat.Raw(), out _), Is.False);
        var longFloat = new Bits(); longFloat.Packed(1); longFloat.Packed(1);
        longFloat.Field(11, p => { for (var i = 0; i < 33; i++) p.Bit(false); });
        longFloat.Packed(0); longFloat.Packed(0);
        Assert.That(DamageLifeChangeEvents.TryDecode(longFloat.Raw(), out _), Is.False);
    }

    internal sealed class Bits
    {
        private readonly List<bool> _bits = [];
        public void Bit(bool value) => _bits.Add(value);
        public void Packed(uint value)
        {
            do { var b = (byte)((value & 127) << 1); value >>= 7; if (value != 0) b |= 1;
                for (var i = 0; i < 8; i++) Bit((b & (1 << i)) != 0); } while (value != 0);
        }
        public void Field(uint handle, Action<Bits> payload)
        { var p = new Bits(); payload(p); Packed(handle + 1); Packed((uint)p._bits.Count); _bits.AddRange(p._bits); }
        public void Float(float value)
        { foreach (var b in BitConverter.GetBytes(value)) for (var i = 0; i < 8; i++) Bit((b & (1 << i)) != 0); }
        public ValorantRawPayload Raw()
        { var bytes = new byte[(_bits.Count + 7) / 8]; for (var i = 0; i < _bits.Count; i++) if (_bits[i]) bytes[i / 8] |= (byte)(1 << (i % 8));
            return new("LifeChangeEvents", _bits.Count, bytes); }
    }
}
