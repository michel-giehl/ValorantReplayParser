using Replay.Encoding.Net;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Valorant.Events.Combat;
using Replay.Valorant.Events.Flashes;
using Replay.Valorant.Events.Footsteps;
using Replay.Valorant.Events.Movement;
using Replay.Valorant.Events.Nearsights;
using Replay.Valorant.Events.Walls;

namespace Replay.Valorant.Events;

/// <summary>Derives gameplay events from native parser events and forwards both to the consumer.</summary>
/// <remarks>Create one instance per replay. Call Complete only after a successful read.</remarks>
public sealed class ValorantEventEnricher : IReplayEventSink
{
    private readonly NetGuidCache _netGuidCache = new();
    private readonly IReplayEventSink _events;
    private readonly ValorantFlashEventEnricher _flashes;
    private readonly ValorantNearsightEventEnricher _nearsights;
    private readonly ValorantWallEventEnricher _walls;
    private bool _completed;

    public ValorantEventEnricher(IReplayEventSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _walls = new ValorantWallEventEnricher(new ValorantFootstepEventEnricher(sink, _netGuidCache));
        _nearsights = new ValorantNearsightEventEnricher(_walls, _netGuidCache);
        _flashes = new ValorantFlashEventEnricher(_nearsights, _netGuidCache);
        _events = new ValorantMovementEventEnricher(new ValorantShotEventEnricher(_flashes, _netGuidCache));
    }

    public void Emit(ReplayEvent replayEvent)
    {
        if (_completed)
            throw new InvalidOperationException("This replay's event enrichment is complete.");

        if (replayEvent is NetGuidExported exported)
        {
            _netGuidCache.SetNetGuidPath(exported.NetGuid, exported.Path, new NetworkGuid(exported.OuterNetGuid));
        }

        _events.Emit(replayEvent);
    }

    /// <summary>Flushes deferred gameplay events after parsing succeeds. Repeated calls have no effect.</summary>
    public void Complete()
    {
        if (_completed) return;
        _completed = true;
        _flashes.Complete();
        _nearsights.Complete();
        _walls.Complete();
    }
}
