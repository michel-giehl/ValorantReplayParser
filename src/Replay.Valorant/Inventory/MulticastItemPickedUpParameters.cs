using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Inventory;

public sealed class MulticastItemPickedUpParameters
    : ExportGroupDescriptor<MulticastItemPickedUpParameters>
{
    public const string ExportPath = "/Script/ShooterGame.ShooterCharacter:MulticastItemPickedUp";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.Inventory;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public NetObjectReference? Item { get; set; }
    public bool? CurrentWasDropped { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.Item, ExportCategory.Inventory).ObjectNetGuid();
        AddPropertyHandle(1, "bCurrentWasDropped", x => x.CurrentWasDropped, ExportCategory.Inventory).Bool();
    }
}
