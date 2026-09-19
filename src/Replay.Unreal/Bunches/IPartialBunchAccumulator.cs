using System.Buffers;
using Replay.Encoding.Archives;
using Replay.Models.Net;

namespace Replay.Unreal.Bunches;

internal interface IPartialBunchAccumulator
    : IDisposable
{
    PartialBunchResult AddFragment(
        uint chIndex,
        RawBunchHeader header,
        FBitArchive payload,
        BunchPayloadStats stats);

    bool TryComplete(uint chIndex, out IMemoryOwner<byte> buffer, out int bitCount, out RawBunchHeader storedHeader);

    /// <summary>
    /// Discards and reports unfinished assemblies after normal replay traversal.
    /// Dispose remains silent so exception cleanup cannot add diagnostics.
    /// </summary>
    int FinalizePending();
}
