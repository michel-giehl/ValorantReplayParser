namespace Replay.Models.Unreal;

public sealed record CustomVersionEntry(Guid Key, int Version, string FriendlyName);

public sealed class CustomVersionContainer
{
    public List<CustomVersionEntry> Versions { get; } = [];

    public int? GetVersion(Guid key) => Versions.FirstOrDefault(version => version.Key == key)?.Version;

    public CustomVersionContainer Clone()
    {
        var clone = new CustomVersionContainer();
        clone.Versions.AddRange(Versions);
        return clone;
    }
}
