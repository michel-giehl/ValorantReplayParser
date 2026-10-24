using Replay.Models.Descriptors;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors.Agents.Clay;
using Replay.Valorant.GameState;
using Replay.Valorant.Inventory;

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
        var shared = new[] { CreateKillRpc(), CreateItemPickedUpRpc(), CreateHalosRpc() };
        if (agent is not ClayAgentDescriptor) return shared;
        var reset = new ClayResetRemoteMovementPredictionParameters();
        return [.. shared, new RpcDescriptor
        {
            Name = "ClientResetRemoteMovementPrediction",
            FunctionExportPath = agent.Path + ":ClientResetRemoteMovementPrediction",
            Handle = 6, Categories = ExportCategory.Agent,
            ParameterDescriptor = reset, Fields = reset.Fields,
        }];
    }

    private static RpcDescriptor CreateItemPickedUpRpc()
    {
        var parameters = new MulticastItemPickedUpParameters();
        return new RpcDescriptor
        {
            Name = "MulticastItemPickedUp",
            FunctionExportPath = MulticastItemPickedUpParameters.ExportPath,
            Categories = ExportCategory.Inventory,
            ParameterDescriptor = parameters,
            Fields = parameters.Fields,
        };
    }

    private static RpcDescriptor CreateHalosRpc()
    {
        var parameters = new UpdateHalosEventParameters();
        return new RpcDescriptor
        {
            Name = "UpdateHalosEvent",
            FunctionExportPath = UpdateHalosEventParameters.ExportPath,
            Categories = ExportCategory.GameState,
            ParameterDescriptor = parameters,
            Fields = parameters.Fields,
        };
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
