namespace Replay.Encoding.Net;

[Flags]
public enum ExportFlags : byte
{
    None = 0,
    HasPath = 1 << 0,
    NoLoad = 1 << 1,
    HasNetworkChecksum = 1 << 2,
}
