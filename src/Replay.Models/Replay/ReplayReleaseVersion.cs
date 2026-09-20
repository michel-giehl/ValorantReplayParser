namespace Replay.Models.Replay;

/// <summary>
/// Identifies a VALORANT game release independently of the Unreal replay version.
/// </summary>
public readonly record struct ReplayReleaseVersion(ushort Major, ushort Minor)
    : IComparable<ReplayReleaseVersion>
{
    public int CompareTo(ReplayReleaseVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }

    public override string ToString() => $"{Major}.{Minor:D2}";

    public static bool operator <(ReplayReleaseVersion left, ReplayReleaseVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(ReplayReleaseVersion left, ReplayReleaseVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(ReplayReleaseVersion left, ReplayReleaseVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(ReplayReleaseVersion left, ReplayReleaseVersion right) =>
        left.CompareTo(right) >= 0;
}
