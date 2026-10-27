using Replay.Models.Replay;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents.Paprika;

/// <summary>
/// Hush, including replicated focus-projectile references.
/// </summary>
public sealed class PaprikaAgentDescriptor : GenericAgentDescriptor
{
    public override string Path => "/Game/Characters/Paprika/Paprika_PC.Paprika_PC_C";
    public ReplicatedObjectReference?[]? FocusProjectiles { get; set; }

    protected override void Configure()
    {
        base.Configure();
        AddPropertyHandle(45, "FocusProjectiles", x => ((PaprikaAgentDescriptor)x).FocusProjectiles)
            .RepLayoutDynamicArray(new VersionedDefinition<ReplicatedObjectReference>(new(46, "FocusProjectiles")));
    }
}
