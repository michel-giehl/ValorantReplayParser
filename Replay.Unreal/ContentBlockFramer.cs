using Replay.Encoding.Archives;

namespace Replay.Unreal;

public class ContentBlockFramer
{
    private readonly PackageMapReader _packageMapReader;

    public ContentBlockFramer(PackageMapReader packageMapReader)
    {
        _packageMapReader = packageMapReader;
    }

    public void FrameContentBlocks(FBitArchive payload, ActorChannelState channel, BunchPayloadStats stats)
    {
        while (!payload.AtEnd)
        {
            var bHasRepLayout = payload.ReadBit();
            if (bHasRepLayout)
            {
                stats.RepLayoutContentBlockCount++;
            }

            var bIsActor = payload.ReadBit();

            if (bIsActor)
            {
                stats.ActorContentBlockCount++;
            }
            else
            {
                _ = _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);
                var bStablyNamed = payload.ReadBit();

                if (!bStablyNamed)
                {
                    var bIsDestroyMessage = payload.ReadBit();
                    if (bIsDestroyMessage)
                    {
                        _ = payload.ReadByte();
                        stats.DeletedContentBlockCount++;
                        stats.ContentBlockCount++;
                        continue;
                    }

                    var classNetGuid = _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);
                    if (!classNetGuid.IsValid)
                    {
                        stats.DeletedContentBlockCount++;
                        stats.ContentBlockCount++;
                        continue;
                    }

                    var bActorIsOuter = payload.ReadBit();
                    if (!bActorIsOuter)
                    {
                        _ = _packageMapReader.InternalLoadObject(payload, isExportingNetGuidBunch: false, recursionDepth: 0);
                    }
                }

                stats.SubobjectContentBlockCount++;
            }

            var payloadBits = (int)payload.ReadIntPacked();
            if (payloadBits < 0 || payloadBits > payload.BitsRemaining)
            {
                stats.MalformedPayloadCount++;
                stats.MalformedContentBlockCount++;
                return;
            }

            var contentPayload = payload.ReadSubArchive(payloadBits);
            contentPayload.SkipRemaining();
            stats.ContentPayloadBitsSkipped += payloadBits;
            stats.ContentBlockCount++;
        }
    }
}
