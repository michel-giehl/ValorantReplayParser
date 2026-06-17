using Replay.Models;

namespace Replay.Unreal;

internal static class LocalFileReplayCustomVersions
{
    public const int CustomVersions = 7;
    private const int LatestVersion = CustomVersions;
    public const string FriendlyName = "LocalFileReplay";

    private static readonly Guid Guid = Guid.Parse("95A4F03E-7E0B-49E4-BA43-D35694FF87D9");

    public static void Validate(CustomVersionContainer container)
    {
        var seen = new HashSet<Guid>();
        var foundLocalReplayVersion = false;

        foreach (var version in container.Versions)
        {
            if (!seen.Add(version.Key))
            {
                throw new InvalidReplayInfoException(
                    $"Replay custom version container has duplicate key {version.Key}.");
            }

            if (version.Key != Guid)
            {
                throw new InvalidReplayInfoException(
                    $"Replay was saved with an unregistered custom version {version.Key}.");
            }

            if (version.Version != LatestVersion)
            {
                throw new InvalidReplayInfoException(
                    $"Replay was saved with unsupported LocalFileReplay custom version {version.Version}; expected {LatestVersion}.");
            }

            foundLocalReplayVersion = true;
        }

        if (!foundLocalReplayVersion)
        {
            throw new InvalidReplayInfoException("Replay custom version container is missing LocalFileReplay.");
        }
    }
}
