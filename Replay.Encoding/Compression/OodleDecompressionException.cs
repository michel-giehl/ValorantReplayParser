namespace Replay.Encoding.Compression;

public sealed class OodleDecompressionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
