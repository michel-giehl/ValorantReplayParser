using Replay.Models.Replay;

namespace Replay.Valorant.Tests;

/// <summary>Release used by tests that bind the full catalog, which contains release-versioned export groups.</summary>
internal static class TestReleases
{
    public static readonly ReplayReleaseVersion Current = new(13, 9);
}
