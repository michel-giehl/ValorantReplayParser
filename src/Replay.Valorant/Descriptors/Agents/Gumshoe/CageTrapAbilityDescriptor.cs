using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

public sealed class CageTrapAbilityDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.CageAbility;
    protected override bool IsEquipment => true;
}
