namespace Replay.Models;

public static class Constants
{
    public const uint NetworkMagic = 0x2CF5A13D;

    public const uint ExpectedNetworkVersion = 19;

    public const uint ExpectedEngineNetworkProtocolVersion = 32;

    public const uint MetadataMagic = 0x3D06B24E;

    public const int MaxPacketSizeInBits = 2 * 1024 * 8;

    public const int MaxGuidCount = 2048;

    public const int MaxCustomVersionCount = 1024;
}
