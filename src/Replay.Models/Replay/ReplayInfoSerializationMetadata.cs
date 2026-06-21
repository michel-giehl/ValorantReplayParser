using Replay.Models.Unreal;

namespace Replay.Models.Replay;

public sealed class ReplayInfoSerializationMetadata
{
    public uint FileVersion { get; set; }
    public string FileFriendlyName { get; set; } = string.Empty;
    public CustomVersionContainer FileCustomVersions { get; set; } = new();

    public void Reset()
    {
        FileVersion = 0;
        FileFriendlyName = string.Empty;
        FileCustomVersions = new CustomVersionContainer();
    }
}
