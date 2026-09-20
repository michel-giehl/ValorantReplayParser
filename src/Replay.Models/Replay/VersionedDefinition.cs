namespace Replay.Models.Replay;

/// <summary>
/// Selects a definition by inclusive VALORANT release boundaries.
/// </summary>
public sealed class VersionedDefinition<T>
{
    private readonly KeyValuePair<ReplayReleaseVersion, T>[] _boundaries;

    public VersionedDefinition(T baseline)
        : this(baseline, [])
    {
    }

    private VersionedDefinition(
        T baseline,
        KeyValuePair<ReplayReleaseVersion, T>[] boundaries)
    {
        Baseline = baseline;
        _boundaries = boundaries;
    }

    public T Baseline { get; }

    public bool HasVersionBoundaries => _boundaries.Length != 0;

    public VersionedDefinition<T> From(ReplayReleaseVersion release, T definition)
    {
        if (_boundaries.Any(boundary => boundary.Key == release))
        {
            throw new ArgumentException(
                $"A definition is already registered from release {release}.",
                nameof(release));
        }

        var boundaries = new KeyValuePair<ReplayReleaseVersion, T>[_boundaries.Length + 1];
        _boundaries.CopyTo(boundaries, 0);
        boundaries[^1] = new KeyValuePair<ReplayReleaseVersion, T>(release, definition);
        Array.Sort(boundaries, static (left, right) => left.Key.CompareTo(right.Key));
        return new VersionedDefinition<T>(Baseline, boundaries);
    }

    public T Resolve(ReplayReleaseVersion release)
    {
        var selected = Baseline;
        foreach (var boundary in _boundaries)
        {
            if (boundary.Key > release)
            {
                break;
            }

            selected = boundary.Value;
        }

        return selected;
    }

    public T Resolve(ReplayReleaseVersion? release, string definitionName)
    {
        if (release is { } value)
        {
            return Resolve(value);
        }

        if (!HasVersionBoundaries)
        {
            return Baseline;
        }

        throw new InvalidOperationException(
            $"Versioned {definitionName} requires an explicit replay release.");
    }

    public VersionedDefinition<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var result = new VersionedDefinition<TResult>(selector(Baseline));
        foreach (var boundary in _boundaries)
        {
            result = result.From(boundary.Key, selector(boundary.Value));
        }

        return result;
    }
}
