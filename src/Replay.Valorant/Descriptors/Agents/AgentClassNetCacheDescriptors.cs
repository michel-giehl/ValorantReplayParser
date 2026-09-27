using Replay.Models.Descriptors;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors.Agents.Clay;

namespace Replay.Valorant.Descriptors.Agents;

internal static class AgentClassNetCacheDescriptors
{
    private const string KillFunctionName = "MulticastNotifyKilledEnemy";
    private const string KillFunctionPath = "/Script/ShooterGame.ShooterCharacter:MulticastNotifyKilledEnemy";

    public static IReadOnlyList<ClassNetCacheDescriptor> Create(IEnumerable<ExportGroupDescriptor> agentDescriptors)
    {
        return agentDescriptors
            .Select(agent => new ClassNetCacheDescriptor(
                agent.Path + "_ClassNetCache",
                CreateFunctions(agent)))
            .ToArray();
    }

    private static IReadOnlyList<RpcDescriptor> CreateFunctions(ExportGroupDescriptor agent)
    {
        if (agent is not ClayAgentDescriptor) return [CreateKillRpc()];
        var reset = new ClayResetRemoteMovementPredictionParameters();
        return [CreateKillRpc(), new RpcDescriptor
        {
            Name = "ClientResetRemoteMovementPrediction",
            FunctionExportPath = agent.Path + ":ClientResetRemoteMovementPrediction",
            Handle = 6, Categories = ExportCategory.Agent,
            ParameterDescriptor = reset, Fields = reset.Fields,
        }];
    }

    private static RpcDescriptor CreateKillRpc()
    {
        var parameters = new MulticastNotifyKilledEnemyParameters();
        return new RpcDescriptor
        {
            Name = KillFunctionName,
            FunctionExportPath = KillFunctionPath,
            Categories = ExportCategory.Gunplay,
            ParameterDescriptor = parameters,
            Fields = parameters.Fields,
        };
    }
}
