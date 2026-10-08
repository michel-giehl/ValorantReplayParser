using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors;

/// <summary>Observed Owner (11) and Instigator (13) on Phoenix fire patches and ultimate anchors.</summary>
public sealed class PhoenixPresenceDescriptor(string path) : ExportGroupDescriptor<PhoenixPresenceDescriptor>
{
    public const string HotHands = "/Game/Characters/Phoenix/S0/Ability_4/Production/NewMolotov/Patch_Phoenix_MolotovFire.Patch_Phoenix_MolotovFire_C";
    public const string RunItBack = "/Game/Characters/Phoenix/S0/Ability_X/Production/GameObject_Phoenix_X_ResTarget_Production.GameObject_Phoenix_X_ResTarget_Production_C";
    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public override object CreatePayloadInstance() => new PhoenixPresenceDescriptor(path);
    public uint Owner { get; set; }
    public uint Instigator { get; set; }
    protected override void Configure()
    {
        AddProperty(x => x.Owner).ObjectNetGuid();
        AddProperty(x => x.Instigator).ObjectNetGuid();
    }
}
