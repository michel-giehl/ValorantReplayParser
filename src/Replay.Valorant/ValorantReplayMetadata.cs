using Replay.Models.Replay;

namespace Replay.Valorant;

/// <summary>Indicates whether a replay's recorded versions match the full parser's supported version set.</summary>
public enum ValorantReplaySupportStatus
{
    /// <summary>Replay and transform versions match those currently supported by the full reader.</summary>
    Supported,
    /// <summary>The metadata was readable, but the full reader does not support one or more recorded versions.</summary>
    UnsupportedVersion,
}

/// <summary>Replay metadata detached from a parse operation.</summary>
/// <remarks>
/// The contained metadata DTOs and collections are consumer-owned mutable objects. They do not reference the input
/// archive or the reader's parsing session. Metadata can be returned for versions that the full reader does not support.
/// </remarks>
/// <param name="ReplayInfo">Replay file information, including declared chunks.</param>
/// <param name="ReplayInfoSerializationMetadata">Serialization details and custom versions from replay info.</param>
/// <param name="ReplayHeader">Decoded replay header.</param>
/// <param name="ReplayVersion">Replay version and branch recorded in the header.</param>
/// <param name="UEVersion">Unreal version values recorded by the replay.</param>
/// <param name="FullParseSupportStatus">Whether the current full reader accepts all required version values.</param>
/// <param name="FullParseUnsupportedReason">Reason full parsing is unsupported, or <see langword="null"/> when supported.</param>
public sealed record ValorantReplayMetadata(
    ReplayInfo ReplayInfo,
    ReplayInfoSerializationMetadata ReplayInfoSerializationMetadata,
    ReplayHeader ReplayHeader,
    ReplayVersion ReplayVersion,
    UEVersion UEVersion,
    ValorantReplaySupportStatus FullParseSupportStatus,
    string? FullParseUnsupportedReason);
