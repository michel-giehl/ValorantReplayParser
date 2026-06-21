namespace Replay.Models.Unreal;

public sealed record CustomVersionEntry(Guid Key, int Version, string FriendlyName);

public sealed class CustomVersionContainer
{
    public List<CustomVersionEntry> Versions { get; } = [];

    public int? GetVersion(Guid key)
    {
        foreach (var version in Versions)
        {
            if (version.Key == key)
            {
                return version.Version;
            }
        }

        return null;
    }

    public void SetVersion(Guid key, int version, string friendlyName)
    {
        for (var i = 0; i < Versions.Count; i++)
        {
            if (Versions[i].Key == key)
            {
                Versions[i] = new CustomVersionEntry(key, version, friendlyName);
                return;
            }
        }

        Versions.Add(new CustomVersionEntry(key, version, friendlyName));
    }

    public void Clear() => Versions.Clear();

    public CustomVersionContainer Clone()
    {
        var clone = new CustomVersionContainer();
        clone.Versions.AddRange(Versions);
        return clone;
    }
}
