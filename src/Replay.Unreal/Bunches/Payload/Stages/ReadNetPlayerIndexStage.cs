using Replay.Models.Descriptors;
using Replay.Unreal.Channels;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class ReadNetPlayerIndexStage : IBunchPayloadStage
{
    public BunchStageResult Process(ref BunchPayloadContext context)
    {
        if (!context.OpenedDynamicActor || context.Payload.AtEnd)
        {
            return BunchStageResult.Continue;
        }

        if (context.Channel is not null && IsPlayerController(context.Channel, context.ReaderContext.ExportBindingRegistry))
        {
            _ = context.Payload.ReadByte();
        }

        return BunchStageResult.Continue;
    }

    private static bool IsPlayerController(ActorChannelState channel, ExportBindingRegistry bindingRegistry)
    {
        return IsPlayerControllerPath(channel.ReplicationClassPath, bindingRegistry)
            || IsPlayerControllerPath(channel.ArchetypePath, bindingRegistry)
            || IsPlayerControllerPath(channel.ActorPath, bindingRegistry);
    }

    private static bool IsPlayerControllerPath(string? path, ExportBindingRegistry bindingRegistry)
    {
        return path is not null && bindingRegistry.GetExportGroupKind(path) == ExportGroupKind.PlayerController;
    }
}
