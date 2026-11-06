using System.Runtime.CompilerServices;
using Replay.Models.Descriptors;
using Replay.Models.Net;

namespace Replay.Valorant.Crosshair;

/// <summary>Replicated representation of one flattened member of <see cref="CrosshairSettings"/>.</summary>
public enum CrosshairWireType
{
    /// <summary>One channel of an <c>FColor</c> (8 bits).</summary>
    Byte,
    Float,
    Bool,
    String,
}

/// <summary>One replicated leaf property of <see cref="CrosshairSettings"/>.</summary>
/// <param name="Index">
/// Identity of the leaf: its position in the 12.11+ order, 0-based. Other orderings
/// (<see cref="CrosshairWireVariant"/>) place the same leaf elsewhere; <see cref="CrosshairWireVariant.PositionOf"/> maps it.
/// </param>
/// <param name="ExportName">Name the replay's export table gives this leaf (leaf names repeat across sections).</param>
/// <param name="Path">Struct path, for example <c>Primary.InnerLines.LineThickness</c>.</param>
/// <param name="Type">Wire representation of the leaf.</param>
/// <param name="Apply">Writes a decoded value into the matching member of a <see cref="CrosshairSettings"/>.</param>
public sealed record CrosshairWireLeaf(
    int Index,
    string ExportName,
    string Path,
    CrosshairWireType Type,
    Action<CrosshairSettings, object> Apply);

/// <summary>
/// The replicated shape of <c>AShooterPlayerState.CrosshairSettings</c>.
/// </summary>
/// <remarks>
/// Replays do not export the struct as one property. RepLayout flattens it into one handle per leaf, in struct
/// order, with <c>FColor</c> expanded into its four channel bytes. Leaf names repeat (every section has a
/// <c>LineThickness</c>), and a leaf only appears in a replay's export table once some player sent it, so the
/// table is sparse and the position of the block cannot be read off any single name.
/// Two orderings exist. <see cref="Current"/> is the 12.11 to 13.09 order. <see cref="Legacy"/> is the 12.08 and
/// 12.10 order, which is the 12.07 dump order: <c>bAllowVertScaling</c> comes before <c>LineOffset</c> in every line
/// section, and the sniper dot leaves are <c>bUseCustomCenterDotColor</c>, <c>CenterDotSize</c>,
/// <c>CenterDotOpacity</c>, <c>bDisplayCenterDot</c>. A replay is bound with the ordering whose names match the most
/// exports of its block; <see cref="Inspect"/> reports which one, and which exports fit neither.
/// A property that equals the class default object is not replicated, and the default is all zeros.
/// </remarks>
public static class CrosshairWireLayout
{
    /// <summary>Number of leaves in the flattened struct.</summary>
    public const int LeafCount = 159;

    // Names that occur once in the player state and sit at a fixed distance from the block start. They are the
    // same in every ordering.
    private static readonly (string Name, int Index)[] Anchors =
    [
        ("AFKDetectionComponent", -1),
        ("bUsePrimaryCrosshairForADS", 153),
        ("bUsePrimaryCrosshairForFocusMode", 154),
        ("bUseCustomCrosshairOnAllPrimary", 155),
        ("bUseAdvancedOptions", 156),
        ("bScaleToResolution", 157),
        ("ProfileName", 158),
        ("SpawnedCharacter", 159),
    ];

    private const int MinimumNameMatches = 4;
    private static readonly ConditionalWeakTable<NetFieldExportGroup, BindingCache> BindingByGroup = new();

    public static IReadOnlyList<CrosshairWireLeaf> Leaves { get; } = BuildLeaves();

    /// <summary>The 12.11 to 13.09 ordering; <see cref="CrosshairWireLeaf.Index"/> is the position in it.</summary>
    public static CrosshairWireVariant Current { get; } = new("12.11+", Enumerable.Range(0, LeafCount).ToArray());

    /// <summary>The 12.08 and 12.10 ordering.</summary>
    public static CrosshairWireVariant Legacy { get; } = BuildLegacy();

    /// <summary>Every known ordering, <see cref="Current"/> first (it wins ties).</summary>
    public static IReadOnlyList<CrosshairWireVariant> Variants { get; } = [Current, Legacy];

    /// <summary>
    /// Finds the handle of the first leaf in a replay's export table, or null when the table carries no
    /// crosshair. See <see cref="Inspect"/>.
    /// </summary>
    public static int? FindBlockStart(NetFieldExportGroup group) => Inspect(group)?.Start;

    /// <summary>
    /// Binds a replay's export table to the crosshair block, or returns null when the table carries no crosshair.
    /// Candidate starts come from the anchors; for each candidate and each ordering the exported names are compared
    /// with the whole block, and the best match wins (ties go to the earlier start, then to <see cref="Current"/>).
    /// A patch that shifts the block is followed; one that reorders it yields a block whose
    /// <see cref="CrosshairBlockBinding.Unmatched"/> exports are listed instead of being dropped silently.
    /// </summary>
    public static CrosshairBlockBinding? Inspect(NetFieldExportGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return BindingByGroup.GetValue(group, static g => new BindingCache(Locate(g))).Binding;
    }

    /// <summary>Handle for <paramref name="leafIndex"/>, or null when the replay does not export that leaf.</summary>
    public static uint? ResolveHandle(NetFieldExportGroup group, int leafIndex)
    {
        if (Inspect(group) is not { } block)
        {
            return null;
        }

        var handle = block.Start + block.Variant.PositionOf(leafIndex);
        var exports = group.NetFieldExports;
        return (uint)handle < (uint)exports.Length
               && exports[handle] is { } export
               && string.Equals(export.Name, Leaves[leafIndex].ExportName, StringComparison.Ordinal)
            ? (uint)handle
            : null;
    }

    private static CrosshairBlockBinding? Locate(NetFieldExportGroup group)
    {
        var exports = group.NetFieldExports;
        var candidates = new HashSet<int>();
        for (var handle = 0; handle < exports.Length; handle++)
        {
            if (exports[handle] is not { } export)
            {
                continue;
            }

            foreach (var (name, index) in Anchors)
            {
                if (string.Equals(export.Name, name, StringComparison.Ordinal) && handle - index >= 0)
                {
                    candidates.Add(handle - index);
                }
            }
        }

        CrosshairBlockBinding? best = null;
        foreach (var candidate in candidates.Order())
        {
            foreach (var variant in Variants)
            {
                var matched = 0;
                var unmatched = new List<CrosshairUnmatchedExport>();
                for (var position = 0; position < LeafCount && candidate + position < exports.Length; position++)
                {
                    if (exports[candidate + position] is not { } export)
                    {
                        continue;
                    }

                    var expected = variant.ExportNameAt(position);
                    if (string.Equals(export.Name, expected, StringComparison.Ordinal))
                    {
                        matched++;
                    }
                    else
                    {
                        unmatched.Add(new CrosshairUnmatchedExport(position, candidate + position, expected, export.Name));
                    }
                }

                if (matched >= MinimumNameMatches
                    && (best is null
                        || matched > best.MatchedLeafCount
                        || (matched == best.MatchedLeafCount && unmatched.Count < best.UnmatchedLeafCount)))
                {
                    best = new CrosshairBlockBinding(candidate, variant, matched, unmatched);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The 12.08 and 12.10 ordering: the 12.11+ order with <c>bAllowVertScaling</c> moved before <c>LineOffset</c> in
    /// every line section and the sniper dot leaves in dump order.
    /// </summary>
    private static CrosshairWireVariant BuildLegacy()
    {
        var paths = Leaves.Select(leaf => leaf.Path).ToList();
        foreach (var section in new[] { "Primary", "ADS", "FocusMode" })
        {
            foreach (var lines in new[] { "InnerLines", "OuterLines" })
            {
                var offset = paths.IndexOf($"{section}.{lines}.LineOffset");
                if (offset < 0 || paths[offset + 1] != $"{section}.{lines}.bAllowVertScaling")
                {
                    throw new InvalidOperationException(
                        $"Crosshair layout has no LineOffset/bAllowVertScaling pair in {section}.{lines}.");
                }

                (paths[offset], paths[offset + 1]) = (paths[offset + 1], paths[offset]);
            }
        }

        string[] sniperDot =
        [
            "Sniper.bUseCustomCenterDotColor", "Sniper.CenterDotSize", "Sniper.CenterDotOpacity", "Sniper.bDisplayCenterDot",
        ];
        var first = paths.IndexOf("Sniper.CenterDotSize");
        if (first < 0 || !paths.GetRange(first, sniperDot.Length).Order().SequenceEqual(sniperDot.Order()))
        {
            throw new InvalidOperationException("Crosshair layout has no sniper dot leaf run.");
        }

        for (var i = 0; i < sniperDot.Length; i++)
        {
            paths[first + i] = sniperDot[i];
        }

        var indexByPath = Leaves.ToDictionary(leaf => leaf.Path, leaf => leaf.Index);
        return new CrosshairWireVariant("12.08-12.10", paths.Select(path => indexByPath[path]).ToArray());
    }

    private static CrosshairWireLeaf[] BuildLeaves()
    {
        var leaves = new List<CrosshairWireLeaf>(LeafCount);
        AddLineSettings(leaves, "Primary", static s => s.Primary);
        AddLineSettings(leaves, "ADS", static s => s.Ads);
        AddLineSettings(leaves, "FocusMode", static s => s.FocusMode);

        void Sniper<T>(string name, CrosshairWireType type, Action<SniperCrosshairSettings, T> set,
            Func<object, T> read) =>
            leaves.Add(new CrosshairWireLeaf(leaves.Count, name, $"Sniper.{name}", type,
                (settings, value) => set(settings.Sniper, read(value))));

        AddColor(leaves, "Sniper.CenterDotColor", static s => s.Sniper.CenterDotColor,
            static (s, c) => s.Sniper.CenterDotColor = c);
        AddColor(leaves, "Sniper.CenterDotColorCustom", static s => s.Sniper.CenterDotColorCustom,
            static (s, c) => s.Sniper.CenterDotColorCustom = c);
        Sniper("CenterDotSize", CrosshairWireType.Float, (s, v) => s.CenterDotSize = v, Float);
        Sniper("CenterDotOpacity", CrosshairWireType.Float, (s, v) => s.CenterDotOpacity = v, Float);
        Sniper("bDisplayCenterDot", CrosshairWireType.Bool, (s, v) => s.DisplayCenterDot = v, Bool);
        Sniper("bUseCustomCenterDotColor", CrosshairWireType.Bool, (s, v) => s.UseCustomCenterDotColor = v, Bool);

        void Top<T>(string name, CrosshairWireType type, Action<CrosshairSettings, T> set, Func<object, T> read) =>
            leaves.Add(new CrosshairWireLeaf(leaves.Count, name, name, type, (settings, value) => set(settings, read(value))));

        Top("bUsePrimaryCrosshairForADS", CrosshairWireType.Bool, (s, v) => s.UsePrimaryCrosshairForAds = v, Bool);
        Top("bUsePrimaryCrosshairForFocusMode", CrosshairWireType.Bool, (s, v) => s.UsePrimaryCrosshairForFocusMode = v, Bool);
        Top("bUseCustomCrosshairOnAllPrimary", CrosshairWireType.Bool, (s, v) => s.UseCustomCrosshairOnAllPrimary = v, Bool);
        Top("bUseAdvancedOptions", CrosshairWireType.Bool, (s, v) => s.UseAdvancedOptions = v, Bool);
        Top("bScaleToResolution", CrosshairWireType.Bool, (s, v) => s.ScaleToResolution = v, Bool);
        Top("ProfileName", CrosshairWireType.String, (s, v) => s.ProfileName = v, String);

        if (leaves.Count != LeafCount)
        {
            throw new InvalidOperationException($"Crosshair layout has {leaves.Count} leaves, expected {LeafCount}.");
        }

        return [.. leaves];
    }

    private static void AddLineSettings(
        List<CrosshairWireLeaf> leaves,
        string section,
        Func<CrosshairSettings, LineCrosshairSettings> select)
    {
        void Leaf<T>(string name, CrosshairWireType type, Action<LineCrosshairSettings, T> set, Func<object, T> read) =>
            leaves.Add(new CrosshairWireLeaf(leaves.Count, name, $"{section}.{name}", type,
                (settings, value) => set(select(settings), read(value))));

        AddColor(leaves, $"{section}.Color", s => select(s).Color, (s, c) => select(s).Color = c);
        AddColor(leaves, $"{section}.ColorCustom", s => select(s).ColorCustom, (s, c) => select(s).ColorCustom = c);
        Leaf("bUseCustomColor", CrosshairWireType.Bool, (s, v) => s.UseCustomColor = v, Bool);
        Leaf("bHasOutline", CrosshairWireType.Bool, (s, v) => s.HasOutline = v, Bool);
        Leaf("OutlineThickness", CrosshairWireType.Float, (s, v) => s.OutlineThickness = v, Float);
        AddColor(leaves, $"{section}.OutlineColor", s => select(s).OutlineColor, (s, c) => select(s).OutlineColor = c);
        Leaf("OutlineOpacity", CrosshairWireType.Float, (s, v) => s.OutlineOpacity = v, Float);
        Leaf("CenterDotSize", CrosshairWireType.Float, (s, v) => s.CenterDotSize = v, Float);
        Leaf("CenterDotOpacity", CrosshairWireType.Float, (s, v) => s.CenterDotOpacity = v, Float);
        Leaf("bDisplayCenterDot", CrosshairWireType.Bool, (s, v) => s.DisplayCenterDot = v, Bool);
        Leaf("bFadeCrosshairWithFiringError", CrosshairWireType.Bool, (s, v) => s.FadeCrosshairWithFiringError = v, Bool);
        Leaf("bShowSpectatedPlayerCrosshair", CrosshairWireType.Bool, (s, v) => s.ShowSpectatedPlayerCrosshair = v, Bool);
        Leaf("bHideCrosshair", CrosshairWireType.Bool, (s, v) => s.HideCrosshair = v, Bool);
        Leaf("bFixMinErrorAcrossWeapons", CrosshairWireType.Bool, (s, v) => s.FixMinErrorAcrossWeapons = v, Bool);
        AddLines(leaves, $"{section}.InnerLines", s => select(s).InnerLines);
        AddLines(leaves, $"{section}.OuterLines", s => select(s).OuterLines);
    }

    private static void AddLines(
        List<CrosshairWireLeaf> leaves,
        string path,
        Func<CrosshairSettings, LineCrosshairSectionSettings> select)
    {
        void Leaf<T>(string name, CrosshairWireType type, Action<LineCrosshairSectionSettings, T> set, Func<object, T> read) =>
            leaves.Add(new CrosshairWireLeaf(leaves.Count, name, $"{path}.{name}", type,
                (settings, value) => set(select(settings), read(value))));

        Leaf("LineThickness", CrosshairWireType.Float, (s, v) => s.LineThickness = v, Float);
        Leaf("LineLength", CrosshairWireType.Float, (s, v) => s.LineLength = v, Float);
        Leaf("LineLengthVertical", CrosshairWireType.Float, (s, v) => s.LineLengthVertical = v, Float);
        Leaf("LineOffset", CrosshairWireType.Float, (s, v) => s.LineOffset = v, Float);
        Leaf("bAllowVertScaling", CrosshairWireType.Bool, (s, v) => s.AllowVertScaling = v, Bool);
        Leaf("bShowMovementError", CrosshairWireType.Bool, (s, v) => s.ShowMovementError = v, Bool);
        Leaf("bShowShootingError", CrosshairWireType.Bool, (s, v) => s.ShowShootingError = v, Bool);
        Leaf("bShowMinError", CrosshairWireType.Bool, (s, v) => s.ShowMinError = v, Bool);
        Leaf("Opacity", CrosshairWireType.Float, (s, v) => s.Opacity = v, Float);
        Leaf("bShowLines", CrosshairWireType.Bool, (s, v) => s.ShowLines = v, Bool);
        Leaf("FiringErrorScale", CrosshairWireType.Float, (s, v) => s.FiringErrorScale = v, Float);
        Leaf("MovementErrorScale", CrosshairWireType.Float, (s, v) => s.MovementErrorScale = v, Float);
    }

    /// <summary>An <c>FColor</c> is flattened into its channel properties in memory order: B, G, R, A.</summary>
    private static void AddColor(
        List<CrosshairWireLeaf> leaves,
        string path,
        Func<CrosshairSettings, CrosshairColor> get,
        Action<CrosshairSettings, CrosshairColor> set)
    {
        void Channel(string name, Func<CrosshairColor, byte, CrosshairColor> with) =>
            leaves.Add(new CrosshairWireLeaf(leaves.Count, name, $"{path}.{name}", CrosshairWireType.Byte,
                (settings, value) => set(settings, with(get(settings), (byte)value))));

        Channel("B", static (c, v) => c with { B = v });
        Channel("G", static (c, v) => c with { G = v });
        Channel("R", static (c, v) => c with { R = v });
        Channel("A", static (c, v) => c with { A = v });
    }

    private static float Float(object value) => (float)value;

    private static bool Bool(object value) => (bool)value;

    private static string String(object value) => (string)value;

    /// <summary>The replicated value of a decoded field as the CLR type <paramref name="type"/> maps to.</summary>
    internal static object ToValue(CrosshairWireType type, DecodedFieldValue value) => type switch
    {
        CrosshairWireType.Byte => value.ByteValue,
        CrosshairWireType.Float => value.FloatValue,
        CrosshairWireType.Bool => value.BoolValue,
        _ => value.StringValue ?? string.Empty,
    };

    private sealed class BindingCache(CrosshairBlockBinding? binding)
    {
        public CrosshairBlockBinding? Binding { get; } = binding;
    }
}
