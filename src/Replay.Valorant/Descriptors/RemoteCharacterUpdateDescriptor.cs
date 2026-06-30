using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

internal sealed class RemoteCharacterUpdateDescriptor : ExportGroupDescriptor<RemoteCharacterUpdateDescriptor>
{
    public override string Path => "/Script/ShooterGame.RemoteCharacterUpdate";
    public override ExportCategory Categories => ExportCategory.Movement;
    public override ExportGroupKind Kind => ExportGroupKind.FastArray;

    public uint ShooterCharacterNetGuidValue { get; set; }
    public uint ShooterCharacterNetGuid { get; set; }
    public uint ComponentDataStream { get; set; }

    protected override void Configure()
    {
        AddProperty(x => x.ShooterCharacterNetGuidValue).ObjectNetGuid();
        AddProperty(x => x.ShooterCharacterNetGuid).ObjectNetGuid();
        AddProperty(x => x.ComponentDataStream);
    }
}