using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Gumshoe;

public sealed class CageTrapProjectileDescriptor : GumshoeActorDescriptor
{
    public override string Path => GumshoePaths.CageProjectile;
    protected override bool IsMoving => true;
}
