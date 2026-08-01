using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Mage.TidalWave;

public sealed class TidalWaveChunkClassNetCacheDescriptor
    : ClassNetCacheDescriptor<TidalWaveChunkClassNetCacheDescriptor>
{
    public override string Path => TidalWavePaths.ChunkClassNetCache;

    protected override void Configure()
    {
        AddFunctionHandle<MulticastInitializeParameters>(
            0,
            "MulticastInitialize",
            TidalWavePaths.ChunkInitialize,
            ExportCategory.Ability);
        AddFunctionHandle(
                1,
                "MulticastPlayFastOutro",
                TidalWavePaths.Chunk + ":MulticastPlayFastOutro",
                ExportCategory.Ability)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);
        AddFunctionHandle<MulticastWallStartLingerParameters>(
            2,
            "MulticastWallStartLinger",
            TidalWavePaths.ChunkWallStartLinger,
            ExportCategory.Ability);
    }
}

public sealed class TidalWaveClassNetCacheDescriptor
    : ClassNetCacheDescriptor<TidalWaveClassNetCacheDescriptor>
{
    public override string Path => TidalWavePaths.WaveClassNetCache;

    protected override void Configure()
    {
        AddFunctionHandle<MulticastStopWaveParameters>(
            0,
            "MulticastStopWave",
            TidalWavePaths.WaveStop,
            ExportCategory.Ability);
    }
}

public sealed class CountdownStateClassNetCacheDescriptor
    : ClassNetCacheDescriptor<CountdownStateClassNetCacheDescriptor>
{
    public override string Path =>
        "/Game/GameModes/HURM/OrbSpawners/CountdownState.CountdownState_C_ClassNetCache";

    protected override void Configure()
    {
        AddFunctionHandle(
                0,
                "OnPreviewTimeStarted",
                "/Game/GameModes/HURM/OrbSpawners/CountdownState.CountdownState_C:OnPreviewTimeStarted",
                ExportCategory.Ability)
            .Decode(ValorantPayloadDecoders.NoParametersRpc);
    }
}

public sealed class ForceModuleManagerComponentClassNetCacheDescriptor
    : ClassNetCacheDescriptor<ForceModuleManagerComponentClassNetCacheDescriptor>
{
    public override string Path => "/Script/ShooterGame.ForceModuleManagerComponent_ClassNetCache";

    protected override void Configure()
    {
        AddFunction<NetMulticastRemoveForceModuleParameters>(
            "NetMulticastRemoveForceModule",
            "/Script/ShooterGame.ForceModuleManagerComponent:NetMulticastRemoveForceModule",
            ExportCategory.Ability);
    }
}
