namespace Replay.Model;

public class UEVersion
{
    public uint UE4Version { get; set; }
    public uint UE5Version { get; set; }
    public uint PackageVersionLicense { get; set; }
}

public sealed class UninitializedUEVersion : UEVersion;