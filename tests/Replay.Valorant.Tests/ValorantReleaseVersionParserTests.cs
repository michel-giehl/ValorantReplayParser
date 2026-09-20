using Replay.Models.Errors;
using Replay.Models.Replay;

namespace Replay.Valorant.Tests;

public class ValorantReleaseVersionParserTests
{
    [TestCase("++Ares-Core+release-13.05", 13, 5)]
    [TestCase("++Ares-Core+release-13.10", 13, 10)]
    public void ParseRequired_ParsesNumericRelease(string branch, int major, int minor)
    {
        Assert.That(
            ValorantReleaseVersionParser.ParseRequired(branch),
            Is.EqualTo(new ReplayReleaseVersion((ushort)major, (ushort)minor)));
    }

    [TestCase("++Ares-Core+release-13")]
    [TestCase("++Ares-Core+release-13.05.1")]
    [TestCase("release-13.05")]
    [TestCase("++Ares-Core+release-thirteen.five")]
    public void ParseRequired_RejectsMalformedBranch(string branch)
    {
        var exception = Assert.Throws<InvalidReplayInfoException>(() =>
            ValorantReleaseVersionParser.ParseRequired(branch));

        Assert.That(exception!.Message, Does.Contain(branch));
    }
}
