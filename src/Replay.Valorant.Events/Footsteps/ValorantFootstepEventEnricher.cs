using Replay.Encoding.Net;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.Descriptors.Effects.Locational;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Events.Footsteps;

internal sealed class ValorantFootstepEventEnricher : IReplayEventSink
{
    private readonly IReplayEventSink _inner;
    private readonly PlayerIdentityTracker _players;

    public ValorantFootstepEventEnricher(IReplayEventSink inner, NetGuidCache netGuidCache)
    {
        _inner = inner;
        _players = new PlayerIdentityTracker(netGuidCache);
    }

    public void Emit(ReplayEvent replayEvent)
    {
        switch (replayEvent)
        {
            case ExportGroupReceived exportGroup:
                TrackExportGroup(exportGroup);
                break;
            case RpcReceived { WasDecoded: true, Payload: ClientPlayOneShotEffectAtLocationParameters parameters } rpc:
                if (ResolveKind(parameters) is { } kind)
                {
                    _inner.Emit(Create(rpc, parameters, kind));
                }
                break;
        }

        _inner.Emit(replayEvent);
    }

    private void TrackExportGroup(ExportGroupReceived exportGroup)
    {
        switch (exportGroup.Payload)
        {
            case GenericAgentDescriptor agent:
                _players.TrackAgent(exportGroup.ActorNetGuid, agent);
                break;
            case BombPlayerStateDescriptor playerState:
                _players.TrackPlayerState(exportGroup.ActorNetGuid, playerState);
                break;
        }
    }

    private ValorantFootstepReceived Create(
        RpcReceived rpc,
        ClientPlayOneShotEffectAtLocationParameters parameters,
        ValorantFootstepKind kind)
    {
        var characterNetGuid = GetObject(parameters, "FXC.EffectContext")?.NetGuid;
        var playerStateNetGuid = _players.GetPlayerState(characterNetGuid);
        return new ValorantFootstepReceived(
            rpc.TimeSeconds,
            rpc.PacketId,
            rpc.ActorNetGuid,
            rpc.ObjectNetGuid,
            rpc.ChannelIndex,
            kind,
            characterNetGuid,
            playerStateNetGuid,
            _players.GetSubject(playerStateNetGuid),
            parameters.Location,
            parameters.Rotation,
            GetFloat(parameters, "FXC.Material"),
            GetFloat(parameters, "FXC.Intensity"),
            parameters.StartMovementTime,
            parameters.EffectContainer);
    }

    private static ValorantFootstepKind? ResolveKind(ClientPlayOneShotEffectAtLocationParameters parameters)
    {
        var path = parameters.EffectContainer?.Path ?? parameters.EffectContainerPath;
        if (path is null)
        {
            return null;
        }

        if (path.EndsWith(".FXC_Footstep_C", StringComparison.Ordinal))
        {
            return ValorantFootstepKind.Footstep;
        }

        if (path.EndsWith(".FXC_JumpLaunch_C", StringComparison.Ordinal))
        {
            return ValorantFootstepKind.JumpLaunch;
        }

        return path.EndsWith(".FXC_JumpLand_C", StringComparison.Ordinal) ? ValorantFootstepKind.JumpLand : null;
    }

    private static float? GetFloat(ClientPlayOneShotEffectAtLocationParameters parameters, string tagName) =>
        parameters.FloatValues.FirstOrDefault(value => value.Name?.TagName == tagName)?.Value;

    private static NetObjectReference? GetObject(ClientPlayOneShotEffectAtLocationParameters parameters, string tagName) =>
        parameters.ObjectValues.FirstOrDefault(value => value.Name?.TagName == tagName)?.Value;
}
