namespace Replay.Unreal;

public sealed class BunchPayloadStats
{
    public int PacketCount { get; set; }
    public int BunchCount { get; set; }
    public int PayloadBunchCount { get; set; }
    public int PackageMapExportBunchCount { get; set; }
    public int ExportedNetGuidCount { get; set; }
    public int MustBeMappedGuidCount { get; set; }
    public int PartialFragmentCount { get; set; }
    public int CompletedPartialBunchCount { get; set; }
    public int PartialErrorCount { get; set; }
    public int ActorChannelOpenCount { get; set; }
    public int ActorSerializeNewActorCount { get; set; }
    public int DynamicOpenPayloadBunchCount { get; set; }
    public long DynamicOpenPayloadBitsSkipped { get; set; }
    public int ContentBlockCount { get; set; }
    public int ActorContentBlockCount { get; set; }
    public int SubobjectContentBlockCount { get; set; }
    public int DeletedContentBlockCount { get; set; }
    public int RepLayoutContentBlockCount { get; set; }
    public long ContentPayloadBitsSkipped { get; set; }
    public int MalformedPayloadCount { get; set; }
    public int MalformedPayloadExceptionCount { get; set; }
    public int MalformedMustBeMappedGuidCount { get; set; }
    public int MalformedActorOpenCount { get; set; }
    public int MalformedContentBlockCount { get; set; }
    public int TrailingPayloadCount { get; set; }
}
