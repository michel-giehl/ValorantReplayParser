namespace Replay.Encoding.Archives;

public enum ArchiveErrorCode
{
    EndOfArchive,
    InvalidSeek,
    InvalidCount,
    MalformedPackedInteger,
    MalformedSerializedInt,
    InvalidBitCount,
    BufferTooSmall,
    UnexpectedTrailingData,
}
