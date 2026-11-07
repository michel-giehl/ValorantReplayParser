using Replay.Models.Descriptors;
using Replay.Models.Net;
using Replay.Unreal.Parsing;
using Replay.Valorant.Crosshair;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.GameState;

public sealed class BombPlayerStateDescriptor : ExportGroupDescriptor<BombPlayerStateDescriptor>
{
    public override string Path => "/Game/GameModes/Bomb/BombPlayerState.BombPlayerState_C";
    public override ExportCategory Categories => ExportCategory.GameState | ExportCategory.Gunplay | ExportCategory.Inventory | ExportCategory.Economy;
    public override ExportGroupKind Kind => ExportGroupKind.Actor;

    public int PlayerId { get; set; }
    public RiotUniqueNetId? UniqueId { get; set; }
    public uint Ping { get; set; }
    public int CompetitiveTier { get; set; }
    public string? Subject { get; set; }
    public NetObjectReference SpectatedPlayer { get; set; }
    public NetObjectReference PlayerInfo { get; set; }
    public NetObjectReference SpawnedCharacter { get; set; }
    public NetObjectReference PossessedCharacter { get; set; }
    public bool UltimateActive { get; set; }
    public int NumUltimatePoints { get; set; }
    public int TotalAcquiredUltimatePoints { get; set; }

    /// <summary>
    /// Crosshair members present in this update (<c>AShooterPlayerState.CrosshairSettings</c>), or null when the
    /// update carried none. See <see cref="CrosshairWireLayout"/>.
    /// </summary>
    public CrosshairSettingsUpdate? CrosshairSettings { get; set; }

    protected override void Configure()
    {
        AddProperty("PlayerId", x => x.PlayerId, ExportCategory.GameState).Int32();
        AddProperty("PlayerID", x => x.PlayerId, ExportCategory.GameState).Int32();
        AddProperty(x => x.UniqueId, ExportCategory.GameState).Decode(RiotUniqueNetIdDecoder.Instance);
        AddProperty(x => x.Ping, ExportCategory.GameState).UInt16();
        AddProperty(x => x.CompetitiveTier, ExportCategory.GameState).Int32();
        AddProperty(x => x.Subject, ExportCategory.GameState | ExportCategory.Inventory | ExportCategory.Economy).FString();
        AddProperty(x => x.SpectatedPlayer, ExportCategory.GameState).ObjectNetGuid();
        AddProperty(x => x.PlayerInfo, ExportCategory.GameState | ExportCategory.Economy).ObjectNetGuid();
        AddProperty(x => x.SpawnedCharacter, ExportCategory.GameState | ExportCategory.Gunplay | ExportCategory.Inventory).ObjectNetGuid();
        AddProperty(x => x.PossessedCharacter, ExportCategory.GameState | ExportCategory.Gunplay | ExportCategory.Inventory).ObjectNetGuid();
        AddProperty("bUltimateActive", x => x.UltimateActive, ExportCategory.GameState).Bool();
        AddProperty(x => x.NumUltimatePoints, ExportCategory.GameState).Int32();
        AddProperty(x => x.TotalAcquiredUltimatePoints, ExportCategory.GameState).Int32();
        AddCrosshairSettings();
    }

    private void AddCrosshairSettings()
    {
        foreach (var leaf in CrosshairWireLayout.Leaves)
        {
            var index = leaf.Index;
            var field = AddResolvedField(
                $"CrosshairSettings.{leaf.Path}",
                leaf.ExportName,
                group => CrosshairWireLayout.ResolveHandle(group, index),
                (descriptor, value, block) =>
                {
                    var update = descriptor.CrosshairSettings ??= new CrosshairSettingsUpdate();
                    update.Record(index, value);
                    update.Attach(block as CrosshairBlockBinding);
                },
                nameof(CrosshairSettings),
                ExportCategory.GameState,
                CrosshairWireLayout.Inspect);
            _ = leaf.Type switch
            {
                CrosshairWireType.Byte => field.Byte(),
                CrosshairWireType.Float => field.Float(),
                CrosshairWireType.Bool => field.Bool(),
                _ => field.FString(),
            };
        }
    }
}
