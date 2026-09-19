namespace Replay.Models.Results;

/// <summary>Immutable snapshot of bunch, payload, and content-block counters.</summary>
/// <remarks>Counter meanings follow the parser stages that produce them; related counters are not interchangeable.</remarks>
/// <param name="PacketCount">Number of packets processed by the bunch pipeline.</param>
/// <param name="BunchCount">Total bunches encountered.</param>
/// <param name="PayloadBunchCount">Bunches containing payload data.</param>
/// <param name="PackageMapExportBunchCount">Package-map export bunches encountered.</param>
/// <param name="ExportedNetGuidCount">Net GUID exports encountered.</param>
/// <param name="MustBeMappedGuidCount">Must-be-mapped GUID entries encountered.</param>
/// <param name="PartialFragmentCount">Partial bunch fragments encountered.</param>
/// <param name="CompletedPartialBunchCount">Partial bunch assemblies completed.</param>
/// <param name="PartialErrorCount">Partial-bunch sequence errors.</param>
/// <param name="ActorChannelOpenCount">Actor-channel open bunches encountered.</param>
/// <param name="ActorChannelCloseCount">Actor-channel close bunches encountered.</param>
/// <param name="ActorSerializeNewActorCount">New-actor serialization records encountered.</param>
/// <param name="DynamicOpenPayloadBunchCount">Dynamic actor-channel open payload bunches encountered.</param>
/// <param name="DynamicOpenPayloadBitsSkipped">Dynamic open payload bits intentionally skipped.</param>
/// <param name="ContentBlockCount">Content blocks framed.</param>
/// <param name="ActorContentBlockCount">Actor content blocks framed.</param>
/// <param name="SubobjectContentBlockCount">Subobject content blocks framed.</param>
/// <param name="DeletedContentBlockCount">Deleted-object content blocks framed.</param>
/// <param name="RepLayoutContentBlockCount">Rep-layout content blocks framed.</param>
/// <param name="ContentPayloadBitsSkipped">Content payload bits intentionally skipped.</param>
/// <param name="ContentPayloadBitsParsed">Content payload bits parsed.</param>
/// <param name="MalformedPayloadCount">Malformed payload conditions counted by the bunch pipeline.</param>
/// <param name="MalformedPayloadExceptionCount">Malformed payload exceptions observed.</param>
/// <param name="MalformedMustBeMappedGuidCount">Malformed must-be-mapped GUID payloads.</param>
/// <param name="MalformedActorOpenCount">Malformed actor-channel open payloads.</param>
/// <param name="MalformedContentBlockCount">Malformed content blocks.</param>
/// <param name="TrailingPayloadCount">Unexpected trailing payload conditions counted by the pipeline.</param>
public sealed record ReplayBunchStatistics(
    int PacketCount,
    int BunchCount,
    int PayloadBunchCount,
    int PackageMapExportBunchCount,
    int ExportedNetGuidCount,
    int MustBeMappedGuidCount,
    int PartialFragmentCount,
    int CompletedPartialBunchCount,
    int PartialErrorCount,
    int ActorChannelOpenCount,
    int ActorChannelCloseCount,
    int ActorSerializeNewActorCount,
    int DynamicOpenPayloadBunchCount,
    long DynamicOpenPayloadBitsSkipped,
    int ContentBlockCount,
    int ActorContentBlockCount,
    int SubobjectContentBlockCount,
    int DeletedContentBlockCount,
    int RepLayoutContentBlockCount,
    long ContentPayloadBitsSkipped,
    long ContentPayloadBitsParsed,
    int MalformedPayloadCount,
    int MalformedPayloadExceptionCount,
    int MalformedMustBeMappedGuidCount,
    int MalformedActorOpenCount,
    int MalformedContentBlockCount,
    int TrailingPayloadCount);
