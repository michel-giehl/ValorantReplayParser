using JetBrains.Annotations;
using Replay.Models.Descriptors;

namespace Replay.Models.Events;

[UsedImplicitly]
public sealed record DecodedReplayField(
    int Handle,
    string? Name,
    string? ExportName,
    ExportCategory Categories,
    DecodedFieldValue Value);