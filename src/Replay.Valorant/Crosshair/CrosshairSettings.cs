namespace Replay.Valorant.Crosshair;

/// <summary>
/// A player's replicated crosshair: <c>FCrosshairSettings</c> of <c>AShooterPlayerState</c>.
/// </summary>
/// <remarks>
/// Every member starts at the class default object of the replicated struct, which is what a client holds before the
/// first update and what an update that omits a member leaves in place. Measured over 5128 players of 536 13.04 to
/// 13.09 replays: every number and flag defaults to zero/false (a value of 1, 2 or true is always sent), the three colour
/// members that hold a user colour (<c>Color</c>, <c>ColorCustom</c>, sniper colours) default to opaque white (a channel of 255
/// is never sent, 0 is the commonly sent value), and <c>OutlineColor</c> defaults to opaque black (255 is sent, 0 never).
/// Unlike the share code, nothing here is "default on"; the alpha channel default of 255 is inferred because alpha is
/// sent for one colour in about 20000.
/// The classes mirror the struct layout of the 12.07 dump (<c>ScriptStruct ShooterGame.CrosshairSettings</c> and
/// its nested structs); <see cref="CrosshairWireLayout"/> describes how they are replicated.
/// </remarks>
public sealed record CrosshairSettings
{
    public LineCrosshairSettings Primary { get; init; } = new();
    public LineCrosshairSettings Ads { get; init; } = new();
    public LineCrosshairSettings FocusMode { get; init; } = new();
    public SniperCrosshairSettings Sniper { get; init; } = new();
    public bool UsePrimaryCrosshairForAds { get; set; }
    public bool UsePrimaryCrosshairForFocusMode { get; set; }
    public bool UseCustomCrosshairOnAllPrimary { get; set; }
    public bool UseAdvancedOptions { get; set; }
    public bool ScaleToResolution { get; set; }
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>True while nothing was ever replicated for this player (every member is still the class default).</summary>
    public bool IsUnset => this == new CrosshairSettings();

    /// <summary>Deep copy (a snapshot); the nested settings are mutable, so a snapshot must not share them.</summary>
    public CrosshairSettings Snapshot() => this with
    {
        Primary = Primary.Snapshot(),
        Ads = Ads.Snapshot(),
        FocusMode = FocusMode.Snapshot(),
        Sniper = Sniper.Snapshot(),
    };
}

/// <summary><c>FLineCrosshairSettings</c>: one complete line crosshair (primary, ADS or focus mode).</summary>
public sealed record LineCrosshairSettings
{
    public CrosshairColor Color { get; set; } = CrosshairColor.White;
    public CrosshairColor ColorCustom { get; set; } = CrosshairColor.White;
    public bool UseCustomColor { get; set; }
    public bool HasOutline { get; set; }
    public float OutlineThickness { get; set; }
    public CrosshairColor OutlineColor { get; set; } = CrosshairColor.Black;
    public float OutlineOpacity { get; set; }
    public float CenterDotSize { get; set; }
    public float CenterDotOpacity { get; set; }
    public bool DisplayCenterDot { get; set; }
    public bool FadeCrosshairWithFiringError { get; set; }
    public bool ShowSpectatedPlayerCrosshair { get; set; }
    public bool HideCrosshair { get; set; }
    public bool FixMinErrorAcrossWeapons { get; set; }
    public LineCrosshairSectionSettings InnerLines { get; init; } = new();
    public LineCrosshairSectionSettings OuterLines { get; init; } = new();

    public LineCrosshairSettings Snapshot() => this with
    {
        InnerLines = InnerLines with { },
        OuterLines = OuterLines with { },
    };
}

/// <summary><c>FLineCrosshairSectionSettings</c>: the inner or outer group of four arms.</summary>
public sealed record LineCrosshairSectionSettings
{
    public float LineThickness { get; set; }
    public float LineLength { get; set; }
    public float LineLengthVertical { get; set; }
    public bool AllowVertScaling { get; set; }
    public float LineOffset { get; set; }
    public bool ShowMovementError { get; set; }
    public bool ShowShootingError { get; set; }
    public bool ShowMinError { get; set; }
    public float Opacity { get; set; }
    public bool ShowLines { get; set; }
    public float FiringErrorScale { get; set; }
    public float MovementErrorScale { get; set; }
}

/// <summary><c>FSniperCrosshairSettings</c>: the scope centre dot.</summary>
public sealed record SniperCrosshairSettings
{
    public CrosshairColor CenterDotColor { get; set; } = CrosshairColor.White;
    public CrosshairColor CenterDotColorCustom { get; set; } = CrosshairColor.White;
    public bool UseCustomCenterDotColor { get; set; }
    public float CenterDotSize { get; set; }
    public float CenterDotOpacity { get; set; }
    public bool DisplayCenterDot { get; set; }

    public SniperCrosshairSettings Snapshot() => this with { };
}
