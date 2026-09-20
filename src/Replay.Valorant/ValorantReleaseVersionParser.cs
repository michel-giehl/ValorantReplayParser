using System.Globalization;
using Replay.Models.Errors;
using Replay.Models.Replay;

namespace Replay.Valorant;

internal static class ValorantReleaseVersionParser
{
    private const string BranchPrefix = "++Ares-Core+release-";

    public static ReplayReleaseVersion ParseRequired(string branch)
    {
        if (!branch.StartsWith(BranchPrefix, StringComparison.Ordinal))
        {
            throw InvalidBranch(branch);
        }

        var version = branch.AsSpan(BranchPrefix.Length);
        var separator = version.IndexOf('.');
        if (separator <= 0 ||
            separator == version.Length - 1 ||
            version[(separator + 1)..].Contains('.') ||
            !ushort.TryParse(version[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !ushort.TryParse(version[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            throw InvalidBranch(branch);
        }

        return new ReplayReleaseVersion(major, minor);
    }

    private static InvalidReplayInfoException InvalidBranch(string branch) =>
        new($"Replay branch '{branch}' does not contain a valid VALORANT release version.");
}
