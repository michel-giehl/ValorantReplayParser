using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.MapDoors;

/// <summary>Wire fields validated against 13.05/13.06 replay payloads and 12.07 native types. Gameplay lifecycle belongs to the analyser.</summary>
public sealed class MapDoorDescriptor(string path) : ExportGroupDescriptor<MapDoorDescriptor>
{
    public const string SummitDoor = "/Game/Environment/Plummet/Blueprints/DroppableDoor/DescentBox_v5.DescentBox_v5_C";
    public const string SummitTrigger = "/Game/Environment/Plummet/Blueprints/RespawningPlummetShootable.RespawningPlummetShootable_C";
    public const string SwitchDoor = "/Game/Interactable/WindowShield.WindowShield_C";
    public const string DoorSwitch = "/Game/Interactable/Switch_BlackMarket_2.Switch_BlackMarket_2_C";
    public const string LotusDoor = "/Game/Maps/Jam/Drawbridge.Drawbridge_C";
    public const string LotusSwitch = "/Game/Maps/Jam/Switch_HiddenTemple.Switch_HiddenTemple_C";
    public const string LotusWall = "/Game/Interactable/WallPlates/RespawningWallPlate.RespawningWallPlate_C";
    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;
    public override object CreatePayloadInstance() => new MapDoorDescriptor(path);
    public float LocalDoorPos { get; set; }
    public byte DoorState { get; set; }
    public byte Transition { get; set; }
    public bool IsAlive { get; set; }
    public bool Hidden { get; set; }
    public double LastUsedTime { get; set; }
    public double GameplayStartTime { get; set; }
    public bool HasPlayed { get; set; }
    public bool IsDisabled { get; set; }
    public bool LeverDown { get; set; }
    public float DoorOpenStartTime { get; set; }
    public float DoorCloseStartTime { get; set; }
    public bool Rotated { get; set; }

    protected override void Configure()
    {
        if (path is SummitDoor or SwitchDoor or LotusDoor)
        {
            AddPropertyHandle(17, "DoorState", x => x.DoorState).EnumRemainingBits();
            AddPropertyHandle(18, "Transition", x => x.Transition).EnumRemainingBits();
            if (path != LotusDoor) AddPropertyHandle(16, "LocalDoorPos", x => x.LocalDoorPos).Float();
            else
            {
                AddPropertyHandle(19, "DoorOpenStartTime", x => x.DoorOpenStartTime).Float();
                AddPropertyHandle(20, "DoorCloseStartTime", x => x.DoorCloseStartTime).Float();
                AddPropertyHandle(24, "Rotated", x => x.Rotated).Bool();
            }
        }
        else if (path is SummitTrigger or LotusWall)
        {
            AddPropertyHandle(15, "IsAlive", x => x.IsAlive).Bool();
            if (path == LotusWall) AddPropertyHandle(1, "bHidden", x => x.Hidden).Bool();
        }
        else if (path is DoorSwitch or LotusSwitch)
        {
            AddPropertyHandle(path == DoorSwitch ? 15u : 17u, "LastUsedTime", x => x.LastUsedTime).Double();
            AddPropertyHandle(path == DoorSwitch ? 16u : 19u, "HasPlayed", x => x.HasPlayed).Bool();
            if (path == DoorSwitch)
            {
                AddPropertyHandle(17, "IsDisabled", x => x.IsDisabled).Bool();
                AddPropertyHandle(18, "GameplayStartTime", x => x.GameplayStartTime).Double();
                AddPropertyHandle(19, "LeverDown", x => x.LeverDown).Bool();
            }
        }
    }
}

/// <summary>Exact sparse function parameters; omitted zero parameters retain their native default.</summary>
public sealed class MapDoorRpcParameters(string path) : ExportGroupDescriptor<MapDoorRpcParameters>
{
    public const string DeathPath = "/Game/Interactable/RespawningDestructible.RespawningDestructible_C:OnDie";
    public override string Path => path;
    public override ExportCategory Categories => ExportCategory.GameState;
    public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
    public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;
    public override object CreatePayloadInstance() => new MapDoorRpcParameters(path);
    public byte NewState { get; set; }
    public byte OldState { get; set; }
    public bool IsMoveTo { get; set; }
    public byte NewPhase { get; set; }
    public uint DamageCauser { get; set; }
    protected override void Configure()
    {
        if (path.EndsWith(":OnPhaseChangeEvent", StringComparison.Ordinal))
            AddPropertyHandle(0, "NewPhase", x => x.NewPhase).EnumRemainingBits();
        else if (path == DeathPath) AddPropertyHandle(0, "DamageCauser", x => x.DamageCauser).ObjectNetGuid();
        else
        {
            var sounds = path.EndsWith(":PlayDoorSounds", StringComparison.Ordinal);
            AddPropertyHandle(0, sounds ? "New State" : "NewState", x => x.NewState).EnumRemainingBits();
            AddPropertyHandle(1, sounds ? "Old State" : "OldState", x => x.OldState).EnumRemainingBits();
            if (sounds) AddPropertyHandle(2, "isMoveTo", x => x.IsMoveTo).Bool();
        }
    }
}

public static class MapDoorDescriptors
{
    public static void AddTo(DescriptorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var path in new[] { MapDoorDescriptor.SummitDoor, MapDoorDescriptor.SummitTrigger,
                     MapDoorDescriptor.SwitchDoor, MapDoorDescriptor.DoorSwitch, MapDoorDescriptor.LotusDoor,
                     MapDoorDescriptor.LotusSwitch, MapDoorDescriptor.LotusWall })
            catalog.Add(new MapDoorDescriptor(path));
        var death = new MapDoorRpcParameters(MapDoorRpcParameters.DeathPath);
        catalog.Add(death);
        AddCache(MapDoorDescriptor.SummitTrigger, [
            Empty(0, "MulticastRoundBegin", MapDoorDescriptor.SummitTrigger),
            Parameters(1, "OnPhaseChangeEvent", MapDoorDescriptor.SummitTrigger),
            Parameters(2, "OnDie", MapDoorDescriptor.SummitTrigger, death),
            Empty(3, "RoundBeginBroadcast", MapDoorDescriptor.SummitTrigger)]);
        AddCache(MapDoorDescriptor.LotusWall, [
            Parameters(0, "OnPhaseChangeEvent", MapDoorDescriptor.LotusWall),
            Parameters(1, "OnDie", MapDoorDescriptor.LotusWall, death),
            Empty(2, "RoundBeginBroadcast", MapDoorDescriptor.LotusWall)]);
        AddCache(MapDoorDescriptor.SwitchDoor, [
            Empty(0, "HandleDoorDestroyed", MapDoorDescriptor.SwitchDoor),
            Empty(1, "Multicast Round End", MapDoorDescriptor.SwitchDoor),
            Empty(2, "MulticastRoundBegin", MapDoorDescriptor.SwitchDoor),
            Parameters(3, "PlayDoorSounds", MapDoorDescriptor.SwitchDoor),
            Parameters(4, "UpdateNavLinks", MapDoorDescriptor.SwitchDoor)]);
        foreach (var (path, off, on) in new[] { (MapDoorDescriptor.DoorSwitch, 2u, 3u), (MapDoorDescriptor.LotusSwitch, 4u, 5u) })
            AddCache(path, [Empty(0, " MulticastPlayAnimation", path), Empty(1, "MulticastResetAnimation", path),
                Empty(off, "ToggleLightOff", path), Empty(on, "ToggleLightOn", path)]);

        void AddCache(string path, RpcDescriptor[] functions) => catalog.Add(new ClassNetCacheDescriptor(path + "_ClassNetCache", functions));
        RpcDescriptor Parameters(uint handle, string name, string actor, MapDoorRpcParameters? parameter = null)
        {
            parameter ??= new(actor + ":" + name);
            if (parameter != death) catalog.Add(parameter);
            return new() { Handle = handle, Name = name, FunctionExportPath = parameter.Path,
                Categories = ExportCategory.GameState, ParameterDescriptor = parameter };
        }
        static RpcDescriptor Empty(uint handle, string name, string actor) => new()
        {
            Handle = handle, Name = name, FunctionExportPath = actor + ":" + name,
            Categories = ExportCategory.GameState, Decoder = ValorantPayloadDecoders.NoParametersRpc,
        };
    }
}
