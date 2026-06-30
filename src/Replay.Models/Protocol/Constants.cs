namespace Replay.Models.Protocol;

public static class Constants
{
    public const uint NetworkMagic = 0x2CF5A13D;

    public const uint ExpectedNetworkVersion = 19;

    public const uint ExpectedEngineNetworkProtocolVersion = 32;

    public const int MaxPacketSizeInBits = 2 * 1024 * 8;

    public const int MaxFStringSerializedBytes = 1024 * 1024;

    public const int MaxGuidCount = 2048;

    public const int MaxCustomVersionCount = 1024;
}
