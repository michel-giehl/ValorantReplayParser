using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

public sealed class SecondTripWireGameObjectDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.SecondWire;
    public bool? Deployed { get; set; }
    protected override void ConfigureAdditional() => AddProperty(x => ((SecondTripWireGameObjectDescriptor)x).Deployed).Bool();
}
