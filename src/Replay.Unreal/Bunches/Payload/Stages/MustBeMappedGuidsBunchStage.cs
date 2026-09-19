using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Models.Errors;

namespace Replay.Unreal.Bunches.Payload.Stages;

internal sealed class MustBeMappedGuidsBunchStage : IBunchPayloadStage
{
    public BunchStageResult Process(ref BunchPayloadContext context)
    {
        if (!context.Header.bHasMustBeMappedGUIDs)
        {
            return BunchStageResult.Continue;
        }

        try
        {
            ReadMustBeMappedGuids(context.Payload, context.Stats);
            return BunchStageResult.Continue;
        }
        catch (ArchiveReadException exception)
        {
            throw new InvalidReplayDataException(
                $"Malformed must-be-mapped GUIDs in packet {context.Header.PacketId} on channel {context.Header.ChIndex} " +
                $"at payload position {context.Payload.Position} of {context.Payload.Length} bits: {exception.Message}",
                exception);
        }
    }

    private static void ReadMustBeMappedGuids(FBitArchive payload, BunchPayloadStats stats)
    {
        var count = payload.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
            _ = payload.ReadIntPacked();
            stats.MustBeMappedGuidCount++;
        }
    }
}
