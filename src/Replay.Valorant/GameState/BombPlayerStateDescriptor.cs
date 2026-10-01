using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed class BombPlayerStateDescriptor : ExportGroupDescriptor<BombPlayerStateDescriptor>
{
    public override string Path => "/Game/GameModes/Bomb/BombPlayerState.BombPlayerState_C";
    public override ExportCategory Categories => ExportCategory.GameState | ExportCategory.Gunplay | ExportCategory.Inventory | ExportCategory.Economy;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public int PlayerId { get; set; }
    public ValorantRawPayload? UniqueId { get; set; } // TODO: Implement FUniqueNetIdRepl.
    public int CompetitiveTier { get; set; }
    public string? Subject { get; set; }
    public uint SpectatedPlayer { get; set; }
    public uint PlayerInfo { get; set; }
    public uint SpawnedCharacter { get; set; }
    public uint PossessedCharacter { get; set; }
    public bool UltimateActive { get; set; }
    public int NumUltimatePoints { get; set; }
    public int TotalAcquiredUltimatePoints { get; set; }

    protected override void Configure()
    {
        AddProperty("PlayerId", x => x.PlayerId, ExportCategory.GameState).Int32();
        AddProperty("PlayerID", x => x.PlayerId, ExportCategory.GameState).Int32();
        AddProperty(x => x.UniqueId, ExportCategory.GameState).Decode(ValorantPayloadDecoders.RawPayload("FUniqueNetIdRepl"));
        AddProperty(x => x.CompetitiveTier, ExportCategory.GameState).Int32();
        AddProperty(x => x.Subject, ExportCategory.GameState | ExportCategory.Inventory | ExportCategory.Economy).FString();
        AddProperty(x => x.SpectatedPlayer, ExportCategory.GameState).ObjectNetGuid();
        AddProperty(x => x.PlayerInfo, ExportCategory.GameState | ExportCategory.Economy).ObjectNetGuid();
        AddProperty(x => x.SpawnedCharacter, ExportCategory.GameState | ExportCategory.Gunplay | ExportCategory.Inventory).ObjectNetGuid();
        AddProperty(x => x.PossessedCharacter, ExportCategory.GameState | ExportCategory.Gunplay | ExportCategory.Inventory).ObjectNetGuid();
        AddProperty("bUltimateActive", x => x.UltimateActive, ExportCategory.GameState).Bool();
        AddProperty(x => x.NumUltimatePoints, ExportCategory.GameState).Int32();
        AddProperty(x => x.TotalAcquiredUltimatePoints, ExportCategory.GameState).Int32();
    }
}
