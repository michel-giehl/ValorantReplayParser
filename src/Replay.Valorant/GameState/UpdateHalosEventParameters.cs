using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.GameState;

public sealed class UpdateHalosEventParameters : ExportGroupDescriptor<UpdateHalosEventParameters>
{
    public const string ExportPath = "/Game/Characters/_Core/BasePlayerCharacter.BasePlayerCharacter_C:UpdateHalosEvent";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

    public byte? CommActionEnum { get; set; }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.CommActionEnum, ExportCategory.GameState).EnumRemainingBits();
    }
}
