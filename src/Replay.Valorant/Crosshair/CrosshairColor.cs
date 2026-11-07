namespace Replay.Valorant.Crosshair;

/// <summary>An Unreal <c>FColor</c>: four bytes stored B, G, R, A in memory and replicated one byte per channel.</summary>
/// <remarks>
/// Channels are 0 to 255. Replays only carry channels that differ from the class default object, which is opaque white
/// for the colour members (see <see cref="CrosshairSettings"/>).
/// </remarks>
public readonly record struct CrosshairColor(byte R, byte G, byte B, byte A = 0)
{
    public static CrosshairColor White { get; } = new(255, 255, 255, 255);

    public static CrosshairColor Black { get; } = new(0, 0, 0, 255);

    /// <summary>Upper-case <c>RRGGBBAA</c>, the order the share code uses.</summary>
    public string ToRgbaHex() => $"{R:X2}{G:X2}{B:X2}{A:X2}";

    /// <summary>Upper-case <c>RRGGBB</c>.</summary>
    public string ToRgbHex() => $"{R:X2}{G:X2}{B:X2}";
}
