using Replay.Models.Descriptors;
using Replay.Models.Events;

namespace CliReader.JsonExport;

internal sealed class ReplayExportStatistics
{
    private readonly Dictionary<(string Path, ExportGroupKind Kind, bool WasDecoded), FilteredExportGroupSummary>
        _filteredExportGroups = [];

    public int ActorSpawnedCount { get; internal set; }
    public int ActorClosedCount { get; internal set; }
    public int ExportGroupCount { get; internal set; }
    public int FilteredExportGroupCount { get; private set; }
    public int UndecodedExportGroupCount { get; private set; }
    public int EmptyDecodedExportGroupCount { get; private set; }
    public int RpcCount { get; internal set; }
    public int ValorantShotReceivedCount { get; internal set; }
    public int ValorantFlashCastCount { get; internal set; }
    public int ValorantFlashPathUpdatedCount { get; internal set; }
    public int ValorantFlashExplodedCount { get; internal set; }
    public int ValorantFlashPlayerHitCount { get; internal set; }
    public int ValorantNearsightCastCount { get; internal set; }
    public int ValorantNearsightPathUpdatedCount { get; internal set; }
    public int ValorantNearsightActivatedCount { get; internal set; }
    public int ValorantNearsightPlayerHitCount { get; internal set; }
    public int ValorantNearsightPlayerEffectEndedCount { get; internal set; }
    public int ValorantWallPlacedCount { get; internal set; }
    public int ValorantWallSegmentSpawnedCount { get; internal set; }
    public int ValorantWallActivatedCount { get; internal set; }
    public int ValorantWallSegmentDestroyedCount { get; internal set; }
    public int ValorantWallDestroyedCount { get; internal set; }
    public int MovementCount { get; internal set; }

    public int EventCount =>
        ActorSpawnedCount + ActorClosedCount + ExportGroupCount + RpcCount + ValorantShotReceivedCount +
        ValorantFlashCastCount + ValorantFlashPathUpdatedCount + ValorantFlashExplodedCount +
        ValorantFlashPlayerHitCount + ValorantNearsightCastCount + ValorantNearsightPathUpdatedCount +
        ValorantNearsightActivatedCount + ValorantNearsightPlayerHitCount +
        ValorantNearsightPlayerEffectEndedCount + ValorantWallPlacedCount +
        ValorantWallSegmentSpawnedCount + ValorantWallActivatedCount +
        ValorantWallSegmentDestroyedCount + ValorantWallDestroyedCount;

    public IReadOnlyCollection<FilteredExportGroupSummary> FilteredExportGroups =>
        _filteredExportGroups.Values;

    public void RecordFilteredExportGroup(ExportGroupReceived exportGroup)
    {
        var path = exportGroup.ExportGroupPath ?? exportGroup.ClassPath ?? "<unresolved>";
        var key = (path, exportGroup.Kind, exportGroup.WasDecoded);
        if (!_filteredExportGroups.TryGetValue(key, out var summary))
        {
            summary = new FilteredExportGroupSummary(path, exportGroup.Kind, exportGroup.WasDecoded);
            _filteredExportGroups.Add(key, summary);
        }

        summary.Add(exportGroup);
        FilteredExportGroupCount++;
        if (exportGroup.WasDecoded)
        {
            EmptyDecodedExportGroupCount++;
        }
        else
        {
            UndecodedExportGroupCount++;
        }
    }
}

internal sealed class FilteredExportGroupSummary(
    string path,
    ExportGroupKind kind,
    bool wasDecoded)
{
    public string Path { get; } = path;
    public ExportGroupKind Kind { get; } = kind;
    public bool WasDecoded { get; } = wasDecoded;
    public int Count { get; private set; }
    public long PayloadBits { get; private set; }
    public uint ActorNetGuid { get; private set; }
    public uint ObjectNetGuid { get; private set; }
    public uint ClassNetGuid { get; private set; }
    public uint OuterNetGuid { get; private set; }
    public string? ObjectPath { get; private set; }
    public string? ClassPath { get; private set; }
    public string? OuterPath { get; private set; }

    public void Add(ExportGroupReceived exportGroup)
    {
        Count++;
        PayloadBits += exportGroup.PayloadBits;
        if (Count != 1)
        {
            return;
        }

        ActorNetGuid = exportGroup.ActorNetGuid;
        ObjectNetGuid = exportGroup.ObjectNetGuid;
        ClassNetGuid = exportGroup.ClassNetGuid;
        OuterNetGuid = exportGroup.OuterNetGuid;
        ObjectPath = exportGroup.ObjectPath;
        ClassPath = exportGroup.ClassPath;
        OuterPath = exportGroup.OuterPath;
    }
}
