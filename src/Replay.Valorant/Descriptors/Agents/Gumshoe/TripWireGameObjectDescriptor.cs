using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

public sealed class TripWireGameObjectDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.Tripwire;
    public bool? Deployed { get; set; }
    protected override void ConfigureAdditional() => AddProperty(x => ((TripWireGameObjectDescriptor)x).Deployed).Bool();
}
