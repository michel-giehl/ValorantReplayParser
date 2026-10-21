using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Models.Unreal;
using Replay.Unreal.Parsing;
using Replay.Valorant.Flashes.Descriptors;

namespace Replay.Valorant.Descriptors.Effects.Locational;

public sealed class LocationalEffectManagerClassNetCacheDescriptor
    : ClassNetCacheDescriptor<LocationalEffectManagerClassNetCacheDescriptor>
{
    public const string ComponentPath = "/Script/ShooterGame.LocationalEffectManagerComponent";
    public override string Path => ComponentPath + "_ClassNetCache";
    protected override void Configure()
    {
        AddFunctionHandle<ClientPlayOneShotEffectAtLocationParameters>(2, "ClientPlayOneShotEffectAtLocation",
            ClientPlayOneShotEffectAtLocationParameters.ExportPath, ExportCategory.Effects);
    }
}

public sealed class ClientPlayOneShotEffectAtLocationParameters
    : ExportGroupDescriptor<ClientPlayOneShotEffectAtLocationParameters>, IEffectDataPayload, IDecodedPayloadEventEmitter
{
    public const string ExportPath = LocationalEffectManagerClassNetCacheDescriptor.ComponentPath + ":ClientPlayOneShotEffectAtLocation";
    public override string Path => ExportPath;
    public override ExportCategory Categories => ExportCategory.Effects;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public NetObjectReference? EffectContainer { get; set; }
    public string? EffectContainerPath { get; set; }
    public NetObjectReference? WaitOnReplicationActor { get; set; }
    public EffectManagerFunctionFloatValue[]? FunctionFloatValues { get; set; }
    public EffectManagerFunctionObjectValue[]? FunctionObjectValues { get; set; }
    public FVector? Location { get; set; }
    public FRotator? Rotation { get; set; }
    public float? StartMovementTime { get; set; }

    public IReadOnlyList<EffectManagerFloatValue> FloatValues =>
        EffectManagerValueMapper.Map(FunctionFloatValues);

    public IReadOnlyList<EffectManagerObjectValue> ObjectValues =>
        EffectManagerValueMapper.Map(FunctionObjectValues);

    public void EmitDecodedEvents(ref FieldDecodeContext context)
    {
        if (HasDecoded(nameof(EffectContainer)) && EffectContainer is { } guid &&
            context.NetGuidCache?.TryGetPath(guid.NetGuid, out var path) == true) EffectContainerPath = path;
    }

    protected override void Configure()
    {
        AddPropertyHandle(0, x => x.EffectContainer).ObjectNetGuid();
        AddPropertyHandle(1, x => x.WaitOnReplicationActor).ObjectNetGuid();
        AddPropertyHandle(2, "FloatValues", x => x.FunctionFloatValues)
            .RepLayoutDynamicArray<EffectManagerFunctionFloatValue>();
        AddPropertyHandle(10, "ObjectValues", x => x.FunctionObjectValues)
            .RepLayoutDynamicArray<EffectManagerFunctionObjectValue>();
        AddPropertyHandle(22, x => x.Location).FVector();
        AddPropertyHandle(23, x => x.Rotation).FRotatorShort();
        AddPropertyHandle(24, x => x.StartMovementTime).Float();
    }
}
