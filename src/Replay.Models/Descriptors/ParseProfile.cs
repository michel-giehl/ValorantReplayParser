namespace Replay.Models.Descriptors;

public sealed class ParseProfile
{
    public ExportCategory EnabledCategories { get; init; }
    public HashSet<string>? IncludedPaths { get; init; }
    public HashSet<string>? ExcludedPaths { get; init; }
    public HashSet<string>? IncludedFields { get; init; }
    public bool EnableUnknownDiagnostics { get; init; }

    public static ParseProfile Default { get; } = new()
    {
        EnabledCategories = ExportCategory.All,
    };

    public static ParseProfile Minimal { get; } = new()
    {
        EnabledCategories = ExportCategory.None,
    };
}
