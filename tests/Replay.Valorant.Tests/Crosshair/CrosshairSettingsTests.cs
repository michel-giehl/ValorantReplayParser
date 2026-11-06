using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Crosshair;
using Replay.Valorant.Descriptors;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Tests.Crosshair;

public class CrosshairSettingsTests
{
    private const string Path = "/Game/GameModes/Bomb/BombPlayerState.BombPlayerState_C";

    // Handle of the first crosshair leaf in 13.04 to 13.09 replays: AFKDetectionComponent is handle 38.
    private const int RealBlockStart = 39;

    [Test]
    public void Layout_ListsEveryFlattenedLeafOnceInStructOrder()
    {
        var leaves = CrosshairWireLayout.Leaves;

        Assert.Multiple(() =>
        {
            Assert.That(leaves, Has.Count.EqualTo(CrosshairWireLayout.LeafCount));
            Assert.That(leaves.Select(l => l.Index), Is.EqualTo(Enumerable.Range(0, leaves.Count)));
            Assert.That(leaves.Select(l => l.Path), Is.Unique);
        });
    }

    // Replay handle minus RealBlockStart, taken from the 13.04 to 13.09 export tables of 536 recorded replays.
    [TestCase(0, "B", "Primary.Color.B")]
    [TestCase(3, "A", "Primary.Color.A")]
    [TestCase(8, "bUseCustomColor", "Primary.bUseCustomColor")]
    [TestCase(10, "OutlineThickness", "Primary.OutlineThickness")]
    [TestCase(15, "OutlineOpacity", "Primary.OutlineOpacity")]
    [TestCase(22, "bFixMinErrorAcrossWeapons", "Primary.bFixMinErrorAcrossWeapons")]
    [TestCase(23, "LineThickness", "Primary.InnerLines.LineThickness")]
    [TestCase(26, "LineOffset", "Primary.InnerLines.LineOffset")]
    [TestCase(27, "bAllowVertScaling", "Primary.InnerLines.bAllowVertScaling")]
    [TestCase(28, "bShowMovementError", "Primary.InnerLines.bShowMovementError")]
    [TestCase(35, "LineThickness", "Primary.OuterLines.LineThickness")]
    [TestCase(46, "MovementErrorScale", "Primary.OuterLines.MovementErrorScale")]
    [TestCase(47, "B", "ADS.Color.B")]
    [TestCase(56, "bHasOutline", "ADS.bHasOutline")]
    [TestCase(70, "LineThickness", "ADS.InnerLines.LineThickness")]
    [TestCase(93, "MovementErrorScale", "ADS.OuterLines.MovementErrorScale")]
    [TestCase(94, "B", "FocusMode.Color.B")]
    [TestCase(140, "MovementErrorScale", "FocusMode.OuterLines.MovementErrorScale")]
    [TestCase(141, "B", "Sniper.CenterDotColor.B")]
    [TestCase(149, "CenterDotSize", "Sniper.CenterDotSize")]
    [TestCase(151, "bDisplayCenterDot", "Sniper.bDisplayCenterDot")]
    [TestCase(152, "bUseCustomCenterDotColor", "Sniper.bUseCustomCenterDotColor")]
    [TestCase(153, "bUsePrimaryCrosshairForADS", "bUsePrimaryCrosshairForADS")]
    [TestCase(155, "bUseCustomCrosshairOnAllPrimary", "bUseCustomCrosshairOnAllPrimary")]
    [TestCase(156, "bUseAdvancedOptions", "bUseAdvancedOptions")]
    [TestCase(158, "ProfileName", "ProfileName")]
    public void Layout_MatchesTheRecordedExportNames(int index, string exportName, string path)
    {
        Assert.Multiple(() =>
        {
            Assert.That(CrosshairWireLayout.Leaves[index].ExportName, Is.EqualTo(exportName));
            Assert.That(CrosshairWireLayout.Leaves[index].Path, Is.EqualTo(path));
        });
    }

    // Wire position minus the block start, taken from the 12.08 and 12.10 export tables: LineOffset sits one
    // position later than in 12.11+ (bAllowVertScaling comes first) and the sniper dot leaves are in dump order.
    [TestCase(26, "bAllowVertScaling", "Primary.InnerLines.bAllowVertScaling")]
    [TestCase(27, "LineOffset", "Primary.InnerLines.LineOffset")]
    [TestCase(38, "bAllowVertScaling", "Primary.OuterLines.bAllowVertScaling")]
    [TestCase(39, "LineOffset", "Primary.OuterLines.LineOffset")]
    [TestCase(73, "bAllowVertScaling", "ADS.InnerLines.bAllowVertScaling")]
    [TestCase(74, "LineOffset", "ADS.InnerLines.LineOffset")]
    [TestCase(85, "bAllowVertScaling", "ADS.OuterLines.bAllowVertScaling")]
    [TestCase(86, "LineOffset", "ADS.OuterLines.LineOffset")]
    [TestCase(120, "bAllowVertScaling", "FocusMode.InnerLines.bAllowVertScaling")]
    [TestCase(121, "LineOffset", "FocusMode.InnerLines.LineOffset")]
    [TestCase(149, "bUseCustomCenterDotColor", "Sniper.bUseCustomCenterDotColor")]
    [TestCase(150, "CenterDotSize", "Sniper.CenterDotSize")]
    [TestCase(151, "CenterDotOpacity", "Sniper.CenterDotOpacity")]
    [TestCase(152, "bDisplayCenterDot", "Sniper.bDisplayCenterDot")]
    [TestCase(153, "bUsePrimaryCrosshairForADS", "bUsePrimaryCrosshairForADS")]
    public void LegacyLayout_MatchesTheRecorded12_08And12_10ExportNames(int position, string exportName, string path)
    {
        Assert.Multiple(() =>
        {
            Assert.That(CrosshairWireLayout.Legacy.ExportNameAt(position), Is.EqualTo(exportName));
            Assert.That(CrosshairWireLayout.Legacy.LeafAt(position).Path, Is.EqualTo(path));
        });
    }

    [Test]
    public void Variants_AreOnePermutationOfTheSameLeaves()
    {
        foreach (var variant in CrosshairWireLayout.Variants)
        {
            var leaves = Enumerable.Range(0, CrosshairWireLayout.LeafCount).Select(variant.LeafAt).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(leaves.Select(l => l.Index), Is.EquivalentTo(Enumerable.Range(0, CrosshairWireLayout.LeafCount)), variant.Name);
                foreach (var leaf in CrosshairWireLayout.Leaves)
                {
                    Assert.That(variant.LeafAt(variant.PositionOf(leaf.Index)), Is.SameAs(leaf), $"{variant.Name} {leaf.Path}");
                }
            });
        }

        Assert.That(CrosshairWireLayout.Variants.Select(v => v.Name), Is.Unique);
    }

    [Test]
    public void Defaults_AreTheClassDefaultObjectOfTheReplicatedStruct()
    {
        var settings = new CrosshairSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IsUnset, Is.True);
            Assert.That(settings.Primary.Color, Is.EqualTo(CrosshairColor.White));
            Assert.That(settings.Primary.ColorCustom.ToRgbaHex(), Is.EqualTo("FFFFFFFF"));
            Assert.That(settings.Primary.OutlineColor, Is.EqualTo(CrosshairColor.Black));
            Assert.That(settings.Primary.InnerLines.LineThickness, Is.Zero);
            Assert.That(settings.Primary.HasOutline, Is.False);
            Assert.That(settings.Sniper.CenterDotColor, Is.EqualTo(CrosshairColor.White));
        });
    }

    [Test]
    public void Snapshot_DoesNotShareNestedState()
    {
        var settings = new CrosshairSettings();
        var snapshot = settings.Snapshot();

        settings.Primary.InnerLines.LineThickness = 4;
        settings.Ads.Color = new CrosshairColor(1, 2, 3);
        settings.Sniper.CenterDotSize = 2;

        Assert.That(snapshot.IsUnset, Is.True);
    }

    [Test]
    public void Binding_FollowsTheBlockWhereverTheReplayPutsIt()
    {
        foreach (var start in new[] { RealBlockStart, 12, 77 })
        {
            var group = BindGroup(ExportTable(start, anchors: true));

            foreach (var leaf in CrosshairWireLayout.Leaves)
            {
                var binding = group.FieldsByHandle[start + leaf.Index];
                Assert.Multiple(() =>
                {
                    Assert.That(binding.Enabled, Is.True, $"{leaf.Path} at start {start}");
                    Assert.That(binding.Assigner, Is.Not.Null, leaf.Path);
                    Assert.That(binding.Decoder, Is.Not.Null, leaf.Path);
                    Assert.That(binding.Name, Is.EqualTo($"CrosshairSettings.{leaf.Path}"));
                    Assert.That(binding.ExportName, Is.EqualTo(leaf.ExportName));
                });
            }
        }
    }

    [Test]
    public void Binding_FollowsTheLegacyOrderOfOlderReplays()
    {
        foreach (var start in new[] { RealBlockStart, 21 })
        {
            var table = ExportTable(start, anchors: true, variant: CrosshairWireLayout.Legacy);
            var group = BindGroup(table);
            var block = CrosshairWireLayout.Inspect(table);

            Assert.That(block, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(block!.Variant, Is.SameAs(CrosshairWireLayout.Legacy));
                Assert.That(block.Start, Is.EqualTo(start));
                Assert.That(block.UnmatchedLeafCount, Is.Zero);
                foreach (var leaf in CrosshairWireLayout.Leaves)
                {
                    var handle = start + CrosshairWireLayout.Legacy.PositionOf(leaf.Index);
                    Assert.That(group.FieldsByHandle[handle].Assigner, Is.Not.Null, $"{leaf.Path} at start {start}");
                    Assert.That(group.FieldsByHandle[handle].Name, Is.EqualTo($"CrosshairSettings.{leaf.Path}"));
                }
            });
        }
    }

    [Test]
    public void Binding_ChoosesTheOrderByTheWholeBlockNotTheAnchors()
    {
        var current = ExportTable(RealBlockStart, anchors: true, variant: CrosshairWireLayout.Current);
        var legacy = ExportTable(RealBlockStart, anchors: true, variant: CrosshairWireLayout.Legacy);
        // Sparse legacy table: only the anchors and the leaves whose position differs between the orders.
        var sparseLegacy = ExportTable(RealBlockStart, anchors: true,
            keep: position => position is 27 or 39 or 74 or 86 or 149 or 150, variant: CrosshairWireLayout.Legacy);

        Assert.Multiple(() =>
        {
            Assert.That(CrosshairWireLayout.Inspect(current)!.Variant, Is.SameAs(CrosshairWireLayout.Current));
            Assert.That(CrosshairWireLayout.Inspect(legacy)!.Variant, Is.SameAs(CrosshairWireLayout.Legacy));
            Assert.That(CrosshairWireLayout.Inspect(sparseLegacy)!.Variant, Is.SameAs(CrosshairWireLayout.Legacy));
            Assert.That(CrosshairWireLayout.Inspect(sparseLegacy)!.UnmatchedLeafCount, Is.Zero);
        });
    }

    [Test]
    public void Binding_ResolvesALegacyLeafAtItsLegacyHandle()
    {
        // 12.08 puts LineOffset at 27. Binding it through the 12.11+ order would put bAllowVertScaling there.
        var table = ExportTable(RealBlockStart, anchors: true, variant: CrosshairWireLayout.Legacy);
        var offset = CrosshairWireLayout.Leaves.Single(l => l.Path == "Primary.InnerLines.LineOffset");

        Assert.That(CrosshairWireLayout.ResolveHandle(table, offset.Index), Is.EqualTo((uint)(RealBlockStart + 27)));
    }

    [Test]
    public void Inspect_CountsExportsInsideTheBlockThatMatchNeitherOrder()
    {
        var table = ExportTable(RealBlockStart, anchors: true);
        table.NetFieldExports[RealBlockStart + 23] = Export(RealBlockStart + 23, "SomethingElse");
        table.NetFieldExports[RealBlockStart + 40] = Export(RealBlockStart + 40, "AnotherOne");
        var group = BindGroup(table);

        var block = CrosshairWireLayout.Inspect(table)!;

        Assert.Multiple(() =>
        {
            Assert.That(block.UnmatchedLeafCount, Is.EqualTo(2));
            Assert.That(block.Unmatched.Select(u => (u.Position, u.ActualName)),
                Is.EqualTo(new[] { (23, "SomethingElse"), (40, "AnotherOne") }));
            Assert.That(block.Unmatched[0].ExpectedName, Is.EqualTo("LineThickness"));
            Assert.That(block.MatchedLeafCount, Is.EqualTo(CrosshairWireLayout.LeafCount - 2));
            Assert.That(group.FieldsByHandle[RealBlockStart + 23].Assigner, Is.Null);
            Assert.That(group.FieldsByHandle[RealBlockStart + 40].Assigner, Is.Null);
        });
    }

    [Test]
    public void Inspect_AbsentExportsAreNotMismatches()
    {
        var table = ExportTable(RealBlockStart, anchors: true, keep: index => index is 0 or 10 or 23);

        Assert.That(CrosshairWireLayout.Inspect(table)!.UnmatchedLeafCount, Is.Zero);
    }

    [Test]
    public void Binding_StillFindsTheBlockWhenMostLeavesAreNotExported()
    {
        // A replay only exports the leaves some player sent. Keep the anchors and three neighbours.
        var exports = ExportTable(RealBlockStart, anchors: true, keep: index => index is 0 or 10 or 23 or 24 or 158);
        var group = BindGroup(exports);

        Assert.Multiple(() =>
        {
            Assert.That(CrosshairWireLayout.FindBlockStart(exports), Is.EqualTo(RealBlockStart));
            Assert.That(group.FieldsByHandle[RealBlockStart + 10].Assigner, Is.Not.Null);
            Assert.That(group.FieldsByHandle[RealBlockStart + 24].Assigner, Is.Not.Null);
            Assert.That(group.FieldsByHandle[RealBlockStart + 25].Assigner, Is.Null);
        });
    }

    [Test]
    public void Binding_DoesNotBindALeafWhoseNameDiffers()
    {
        var table = ExportTable(RealBlockStart, anchors: true);
        table.NetFieldExports[RealBlockStart + 23] = new NetFieldExport
        {
            Handle = (uint)(RealBlockStart + 23), Name = "SomethingElse", CompatibleChecksum = 0,
        };

        var group = BindGroup(table);

        Assert.Multiple(() =>
        {
            Assert.That(group.FieldsByHandle[RealBlockStart + 23].Assigner, Is.Null);
            Assert.That(group.FieldsByHandle[RealBlockStart + 24].Assigner, Is.Not.Null);
        });
    }

    [Test]
    public void Binding_IgnoresATableWithoutCrosshairNames()
    {
        var exports = new NetFieldExport?[40];
        exports[38] = new NetFieldExport { Handle = 38, Name = "AFKDetectionComponent", CompatibleChecksum = 0 };
        exports[39] = new NetFieldExport { Handle = 39, Name = "Unrelated", CompatibleChecksum = 0 };
        var table = new NetFieldExportGroup { PathName = Path, PathNameIndex = 1, NetFieldExports = exports };

        Assert.That(CrosshairWireLayout.FindBlockStart(table), Is.Null);
        Assert.That(BindGroup(table).FieldsByHandle.Count(binding => binding.Assigner is not null), Is.Zero);
    }

    [Test]
    public void Payload_DecodesIntoASparseUpdateAndMergesOntoTheClassDefaults()
    {
        var group = BindGroup(ExportTable(RealBlockStart, anchors: true));
        var writer = new PayloadWriter();
        // Green: the class default is white, so only B and R are sent.
        writer.Byte(RealBlockStart + 0, 0);
        writer.Byte(RealBlockStart + 2, 0);
        writer.Float(RealBlockStart + 10, 1f);
        writer.Bool(RealBlockStart + 18, true);
        writer.Float(RealBlockStart + 23, 3f);
        writer.Float(RealBlockStart + 26, 0f);
        writer.Bool(RealBlockStart + 28, true);
        writer.Float(RealBlockStart + 31, 0.75f);
        writer.Byte(RealBlockStart + 142, 0);
        writer.Bool(RealBlockStart + 156, true);
        writer.String(RealBlockStart + 158, "main dot");

        var payload = Parse(group, writer);
        var update = payload.CrosshairSettings!;
        var settings = new CrosshairSettings();
        update.ApplyTo(settings);

        Assert.Multiple(() =>
        {
            Assert.That(payload.HasDecoded(nameof(BombPlayerStateDescriptor.CrosshairSettings)), Is.True);
            Assert.That(update.Changes, Has.Count.EqualTo(11));
            Assert.That(update.Changes[0].Path, Is.EqualTo("Primary.Color.B"));
            Assert.That(update.Changes[4].Value, Is.EqualTo(3f));
            Assert.That(settings.Primary.Color, Is.EqualTo(new CrosshairColor(0, 255, 0, 255)));
            Assert.That(settings.Primary.OutlineThickness, Is.EqualTo(1f));
            Assert.That(settings.Primary.DisplayCenterDot, Is.True);
            Assert.That(settings.Primary.InnerLines.LineThickness, Is.EqualTo(3f));
            Assert.That(settings.Primary.InnerLines.ShowMovementError, Is.True);
            Assert.That(settings.Primary.InnerLines.Opacity, Is.EqualTo(0.75f));
            Assert.That(settings.Sniper.CenterDotColor, Is.EqualTo(new CrosshairColor(255, 0, 255, 255)));
            Assert.That(settings.UseAdvancedOptions, Is.True);
            Assert.That(settings.ProfileName, Is.EqualTo("main dot"));
            Assert.That(settings.IsUnset, Is.False);
        });
    }

    [Test]
    public void Payload_LegacyReplayWritesLineOffsetAndTheSniperDotIntoTheirOwnMembers()
    {
        var group = BindGroup(ExportTable(RealBlockStart, anchors: true, variant: CrosshairWireLayout.Legacy));
        var writer = new PayloadWriter();
        writer.Float(RealBlockStart + 27, 1f);
        writer.Float(RealBlockStart + 39, 3f);
        writer.Float(RealBlockStart + 74, 3f);
        writer.Bool(RealBlockStart + 26, true);
        writer.Float(RealBlockStart + 150, 0.31f);
        writer.Float(RealBlockStart + 151, 1f);
        writer.Bool(RealBlockStart + 152, true);

        var update = Parse(group, writer).CrosshairSettings!;
        var settings = new CrosshairSettings();
        update.ApplyTo(settings);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Primary.InnerLines.LineOffset, Is.EqualTo(1f));
            Assert.That(settings.Primary.InnerLines.AllowVertScaling, Is.True);
            Assert.That(settings.Primary.OuterLines.LineOffset, Is.EqualTo(3f));
            Assert.That(settings.Ads.InnerLines.LineOffset, Is.EqualTo(3f));
            Assert.That(settings.Sniper.CenterDotSize, Is.EqualTo(0.31f));
            Assert.That(settings.Sniper.CenterDotOpacity, Is.EqualTo(1f));
            Assert.That(settings.Sniper.DisplayCenterDot, Is.True);
            Assert.That(settings.Sniper.UseCustomCenterDotColor, Is.False);
            Assert.That(update.UnmatchedLeafCount, Is.Zero);
            Assert.That(update.IsPartial, Is.False);
            Assert.That(update.WireLayout, Is.EqualTo(CrosshairWireLayout.Legacy.Name));
        });
    }

    [Test]
    public void Payload_UpdateReportsTheExportsItCouldNotBind()
    {
        var table = ExportTable(RealBlockStart, anchors: true);
        table.NetFieldExports[RealBlockStart + 40] = Export(RealBlockStart + 40, "AnotherOne");
        var group = BindGroup(table);
        var writer = new PayloadWriter();
        writer.Float(RealBlockStart + 23, 3f);

        var update = Parse(group, writer).CrosshairSettings!;

        Assert.Multiple(() =>
        {
            Assert.That(update.UnmatchedLeafCount, Is.EqualTo(1));
            Assert.That(update.IsPartial, Is.True);
            Assert.That(update.WireLayout, Is.EqualTo(CrosshairWireLayout.Current.Name));
        });
    }

    [Test]
    public void Payload_LaterUpdatesOverrideEarlierOnesInOrder()
    {
        var group = BindGroup(ExportTable(RealBlockStart, anchors: true));
        var first = new PayloadWriter();
        first.Float(RealBlockStart + 23, 2f);
        first.Bool(RealBlockStart + 18, true);
        var second = new PayloadWriter();
        second.Float(RealBlockStart + 23, 5f);

        var settings = new CrosshairSettings();
        Parse(group, first).CrosshairSettings!.ApplyTo(settings);
        Parse(group, second).CrosshairSettings!.ApplyTo(settings);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Primary.InnerLines.LineThickness, Is.EqualTo(5f));
            Assert.That(settings.Primary.DisplayCenterDot, Is.True);
        });
    }

    [Test]
    public void Payload_WithoutCrosshairLeavesLeavesTheUpdateNull()
    {
        var group = BindGroup(ExportTable(RealBlockStart, anchors: true));
        var writer = new PayloadWriter();

        var payload = Parse(group, writer);

        Assert.Multiple(() =>
        {
            Assert.That(payload.CrosshairSettings, Is.Null);
            Assert.That(payload.HasDecoded(nameof(BombPlayerStateDescriptor.CrosshairSettings)), Is.False);
        });
    }

    private static BombPlayerStateDescriptor Parse(BoundExportGroup group, PayloadWriter writer)
    {
        using var archive = writer.Build();
        var context = new FieldDecodeContext { ExportGroupPath = Path };
        var result = new FieldPayloadParser().ParseContentPayload(archive, group, ref context);
        return (BombPlayerStateDescriptor)result.Payload!;
    }

    private static BoundExportGroup BindGroup(NetFieldExportGroup table)
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog(), releaseVersion: TestReleases.Current);
        registry.OnExportGroupAdded(table);
        return registry.GetBoundGroup(Path)!;
    }

    private static NetFieldExportGroup ExportTable(
        int start,
        bool anchors,
        Func<int, bool>? keep = null,
        CrosshairWireVariant? variant = null)
    {
        variant ??= CrosshairWireLayout.Current;
        var exports = new NetFieldExport?[start + CrosshairWireLayout.LeafCount + 3];
        for (var position = 0; position < CrosshairWireLayout.LeafCount; position++)
        {
            if (keep is not null && !keep(position) && !(anchors && position == 158))
            {
                continue;
            }

            exports[start + position] = Export(start + position, variant.ExportNameAt(position));
        }

        if (anchors)
        {
            exports[start - 1] = Export(start - 1, "AFKDetectionComponent");
            exports[start + CrosshairWireLayout.LeafCount] = Export(start + CrosshairWireLayout.LeafCount, "SpawnedCharacter");
        }

        return new NetFieldExportGroup { PathName = Path, PathNameIndex = 1, NetFieldExports = exports };
    }

    private static NetFieldExport Export(int handle, string name) =>
        new() { Handle = (uint)handle, Name = name, CompatibleChecksum = 0 };

    /// <summary>Writes a RepLayout property stream: checksum bit, then handle+1, bit count and bits per property.</summary>
    private sealed class PayloadWriter
    {
        private readonly List<bool> _bits = [false];

        public void Byte(int handle, byte value) => Field(handle, 8, bits => Bits(bits, value, 8));

        public void Bool(int handle, bool value) => Field(handle, 1, bits => bits.Add(value));

        public void Float(int handle, float value) =>
            Field(handle, 32, bits => Bits(bits, BitConverter.SingleToUInt32Bits(value), 32));

        public void String(int handle, string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value + "\0");
            Field(handle, 32 + bytes.Length * 8, bits =>
            {
                Bits(bits, (uint)bytes.Length, 32);
                foreach (var b in bytes)
                {
                    Bits(bits, b, 8);
                }
            });
        }

        public BitArchiveReader Build()
        {
            var bits = new List<bool>(_bits);
            IntPacked(bits, 0);
            var bytes = new byte[(bits.Count + 7) / 8];
            for (var i = 0; i < bits.Count; i++)
            {
                if (bits[i])
                {
                    bytes[i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return new BitArchiveReader(bytes, bits.Count);
        }

        private void Field(int handle, int bitCount, Action<List<bool>> write)
        {
            IntPacked(_bits, (uint)handle + 1);
            IntPacked(_bits, (uint)bitCount);
            write(_bits);
        }

        private static void IntPacked(List<bool> bits, uint value)
        {
            do
            {
                var next = value >> 7;
                var b = (byte)(((value & 0x7F) << 1) | (next != 0 ? 1u : 0u));
                Bits(bits, b, 8);
                value = next;
            } while (value != 0);
        }

        private static void Bits(List<bool> bits, uint value, int count)
        {
            for (var i = 0; i < count; i++)
            {
                bits.Add(((value >> i) & 1) != 0);
            }
        }
    }
}
