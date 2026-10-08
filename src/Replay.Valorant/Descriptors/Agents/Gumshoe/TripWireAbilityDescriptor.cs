using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

public sealed class TripWireAbilityDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.TripwireAbility;
    protected override bool IsEquipment => true;
}
