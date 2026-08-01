using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors.Agents.Mage.TidalWave;

internal static class TidalWavePaths
{
    public const string Chunk =
        "/Game/Characters/Mage/S0/Ability_X/GameObject_Mage_X_TidalWave_Chunk.GameObject_Mage_X_TidalWave_Chunk_C";

    public const string ChunkInitialize = Chunk + ":MulticastInitialize";
    public const string ChunkWallStartLinger = Chunk + ":MulticastWallStartLinger";
    public const string ChunkClassNetCache = Chunk + "_ClassNetCache";

    public const string Wave =
        "/Game/Characters/Mage/S0/Ability_X/GameObject_Mage_X_TidalWave.GameObject_Mage_X_TidalWave_C";

    public const string WaveStop = Wave + ":MulticastStopWave";
    public const string WaveClassNetCache = Wave + "_ClassNetCache";
}

public static class TidalWaveDescriptors
{
    public static IReadOnlyList<ExportGroupDescriptor> CreateExportDescriptors() =>
    [
        new TidalWaveChunkDescriptor(),
        new SplineMovementComponentDescriptor(),
        new ActorListTransitionContextDescriptor(),
        new TransformTransitionContextDescriptor(),
        new ReadyingStateComponentDescriptor(),
        new ActivatableActorComponentDescriptor(),
    ];

    public static IReadOnlyList<ClassNetCacheDescriptor> CreateClassNetCacheDescriptors() =>
    [
        new TidalWaveChunkClassNetCacheDescriptor(),
        new TidalWaveClassNetCacheDescriptor(),
        new CountdownStateClassNetCacheDescriptor(),
        new ForceModuleManagerComponentClassNetCacheDescriptor(),
    ];
}
