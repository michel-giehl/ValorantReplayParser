using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors;

internal sealed class ValorantPathAliasProvider : IReplayPathAliasProvider
{
    public static ValorantPathAliasProvider Instance { get; } = new();

    private ValorantPathAliasProvider()
    {
    }

    public string? GetAlternatePath(string path)
    {
        const string coreSegment = "/_Core/";
        const string charactersRoot = "/Game/Characters/";

        var coreSegmentIndex = path.IndexOf(coreSegment, StringComparison.Ordinal);
        if (coreSegmentIndex >= 0)
        {
            return string.Concat(
                path.AsSpan(0, coreSegmentIndex),
                "/",
                path.AsSpan(coreSegmentIndex + coreSegment.Length));
        }

        if (path.StartsWith(charactersRoot, StringComparison.Ordinal))
        {
            return string.Concat(
                charactersRoot,
                "_Core/",
                path.AsSpan(charactersRoot.Length));
        }

        return null;
    }
}
