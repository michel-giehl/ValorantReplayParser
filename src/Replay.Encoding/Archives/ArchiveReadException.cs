namespace Replay.Encoding.Archives;

public sealed class ArchiveReadException(
    ArchiveErrorCode errorCode,
    string operation,
    long position,
    long length,
    long requested,
    string? message = null)
    : Exception(message ?? BuildMessage(errorCode, operation, position, length, requested))
{
    public ArchiveErrorCode ErrorCode { get; } = errorCode;

    public string Operation { get; } = operation;

    public long Position { get; } = position;

    public long Length { get; } = length;

    public long Requested { get; } = requested;

    public long Remaining { get; } = Math.Max(0, length - position);

    private static string BuildMessage(ArchiveErrorCode errorCode, string operation, long position, long length,
        long requested) =>
        $"{operation} failed with {errorCode}. Position={position}, Length={length}, Requested={requested}, Remaining={Math.Max(0, length - position)}.";
}