using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Clay;

public static class ClayDescriptors
{
    public static List<ExportGroupDescriptor> CreateDescriptors() =>
    [
        new ClayAgentDescriptor(), new ClayBoomBotAbilityDescriptor(), new ClayBoomBotPawnDescriptor(),
        new ClaySatchelAbilityDescriptor(), new ClaySatchelProjectileDescriptor(), new ClaySatchelExplosionDescriptor(),
        new ClayPaintShellsAbilityDescriptor(), new ClayPaintShellsPrimaryDescriptor(),
        new ClayPaintShellsSecondaryDescriptor(), new ClayPaintShellsSpawnerDescriptor(),
        new ClayShowstopperAbilityDescriptor(), new ClayRocketDescriptor(),
    ];

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        Rpc(ClayPaths.BoomBotAbility,
            Function(ClayPaths.BoomBotAbility, "MulticastOnBoombaNoLongerActivatable", 0),
            Function(ClayPaths.BoomBotAbility, "MulticastOnItemMovedToPersistentData", 1)),
        new ClayBoomBotClassNetCacheDescriptor(),
        Rpc(ClayPaths.SatchelAbility, Function(ClayPaths.SatchelAbility, "MulticastOnItemMovedToPersistentData", 0)),
        Rpc(ClayPaths.PaintShellsAbility, Function(ClayPaths.PaintShellsAbility, "MulticastOnItemMovedToPersistentData", 0)),
        Rpc(ClayPaths.ShowstopperAbility, Function(ClayPaths.ShowstopperAbility, "MulticastOnItemMovedToPersistentData", 0)),
        Rpc(ClayPaths.SatchelProjectile, Function(ClayPaths.SatchelProjectile, "MulticastStopProjectile", 4)),
        Rpc(ClayPaths.PaintShellsSecondary, Function(ClayPaths.PaintShellsSecondary, "MulticastStopProjectile", 3)),
        Rpc(ClayPaths.PaintShellsSpawner, Function(ClayPaths.PaintShellsSpawner, "MulticastStopProjectile", 3)),
        Rpc(ClayPaths.Rocket, Function(ClayPaths.Rocket, "MulticastStopProjectile", 3)),
    ];

    private static ClassNetCacheDescriptor Rpc(string path, params RpcDescriptor[] functions) =>
        new(path + "_ClassNetCache", functions);
    private static RpcDescriptor Function(string path, string name, uint handle) => new()
    {
        Name = name, FunctionExportPath = path + ":" + name, Handle = handle,
        Categories = ExportCategory.Ability | ExportCategory.Effects,
        Decoder = ValorantPayloadDecoders.NoParametersRpc,
    };
}
