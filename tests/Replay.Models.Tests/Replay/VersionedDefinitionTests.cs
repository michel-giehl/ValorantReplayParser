using Replay.Models.Replay;

namespace Replay.Models.Tests.Replay;

public class VersionedDefinitionTests
{
    [Test]
    public void Resolve_SelectsBaselineExactBoundaryAndLatestEarlierBoundary()
    {
        var definition = new VersionedDefinition<string>("baseline")
            .From(new ReplayReleaseVersion(13, 5), "13.05")
            .From(new ReplayReleaseVersion(13, 10), "13.10");

        Assert.Multiple(() =>
        {
            Assert.That(definition.Resolve(new ReplayReleaseVersion(13, 4)), Is.EqualTo("baseline"));
            Assert.That(definition.Resolve(new ReplayReleaseVersion(13, 5)), Is.EqualTo("13.05"));
            Assert.That(definition.Resolve(new ReplayReleaseVersion(13, 9)), Is.EqualTo("13.05"));
            Assert.That(definition.Resolve(new ReplayReleaseVersion(13, 10)), Is.EqualTo("13.10"));
            Assert.That(definition.Resolve(new ReplayReleaseVersion(14, 0)), Is.EqualTo("13.10"));
        });
    }

    [Test]
    public void From_RejectsDuplicateBoundaryWithoutMutatingOriginalDefinition()
    {
        var original = new VersionedDefinition<string>("baseline");
        var versioned = original.From(new ReplayReleaseVersion(13, 5), "13.05");

        Assert.Throws<ArgumentException>(() =>
            versioned.From(new ReplayReleaseVersion(13, 5), "duplicate"));
        Assert.Multiple(() =>
        {
            Assert.That(original.HasVersionBoundaries, Is.False);
            Assert.That(original.Resolve(null, "test definition"), Is.EqualTo("baseline"));
            Assert.Throws<InvalidOperationException>(() =>
                versioned.Resolve(null, "test definition"));
        });
    }

    [Test]
    public void ReplayReleaseVersion_UsesNumericOrderingAndStableFormatting()
    {
        var release1305 = new ReplayReleaseVersion(13, 5);
        var release1310 = new ReplayReleaseVersion(13, 10);

        Assert.Multiple(() =>
        {
            Assert.That(release1305, Is.LessThan(release1310));
            Assert.That(release1305.ToString(), Is.EqualTo("13.05"));
            Assert.That(release1310.ToString(), Is.EqualTo("13.10"));
        });
    }
}
