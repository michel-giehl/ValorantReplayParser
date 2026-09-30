using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Valorant;
using Replay.Valorant.Descriptors.Agents.Pandemic.SmokeScreen;

namespace Test.Integration;

[Category("Integration")]
public class ViperWallTests
{
    private class Sink : IReplayEventSink
    {
        private Action<MulticastAddSmokeScreenPointParameters> MulticastAddSmokeScreenPointConsumer { get; set; }

        public Sink(Action<MulticastAddSmokeScreenPointParameters> multicastAddSmokeScreenPointConsumer)
        {
            MulticastAddSmokeScreenPointConsumer = multicastAddSmokeScreenPointConsumer;
        }

        public void Emit(ReplayEvent replayEvent)
        {
            switch (replayEvent)
            {
                case RpcReceived rpc:
                    if (rpc.Payload is MulticastAddSmokeScreenPointParameters multicastAddSmokeScreenPointParameters)
                    {
                        MulticastAddSmokeScreenPointConsumer(multicastAddSmokeScreenPointParameters);
                    }

                    break;
            }
        }
    }

    [Test]
    public void MulticastAddSmokeScreenPoint_GetsCalled30Times_WhenAbilitiesEnabled()
    {
        var archive = new FBinaryArchive(TestHelper.ReadReplayBytes("viper_wall.vrf"));

        var multicastAddSmokeScreenPointConsumer = Substitute.For<Action<MulticastAddSmokeScreenPointParameters>>();
        var parseNothingProfile = new ParseProfile
        {
            EnabledCategories = ExportCategory.Ability,
        };
        var eventSink = new Sink(multicastAddSmokeScreenPointConsumer);

        ValorantReplayReader.CreateDefault(new NullLoggerFactory(), eventSink, parseNothingProfile).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(multicastAddSmokeScreenPointConsumer.ReceivedCalls().Count(), Is.EqualTo(30));
            Assert.That(archive.AtEnd, Is.True);
        });
    }
    
    [Test]
    public void MulticastAddSmokeScreenPoint_DoesntGetCalled_WhenAbilitiesDisabled()
    {
        var archive = new FBinaryArchive(TestHelper.ReadReplayBytes("viper_wall.vrf"));

        var multicastAddSmokeScreenPointConsumer = Substitute.For<Action<MulticastAddSmokeScreenPointParameters>>();
        var parseNothingProfile = new ParseProfile
        {
            EnabledCategories = ExportCategory.None, // No enabled categories
        };
        var eventSink = new Sink(multicastAddSmokeScreenPointConsumer);

        ValorantReplayReader.CreateDefault(new NullLoggerFactory(), eventSink, parseNothingProfile).Read(archive);

        Assert.Multiple(() =>
        {
            Assert.That(multicastAddSmokeScreenPointConsumer.ReceivedCalls().Count(), Is.Zero);
            Assert.That(archive.AtEnd, Is.True);
        });
    }
}