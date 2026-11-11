# Release notes

## Unreleased

### Added

- Payload transform for `++Ares-Core+release-china-13.09`.
- Payload transform for `++Ares-Core+release-13.10`.
- Combat reports decode on 13.10, which added `DamageIcon` after `DamageType` in every combat interaction and moved
  every later handle. `CombatInteractionUpdate.DamageIcon` exposes the new object reference.
- Damage and temporary-death RPCs select the 13.10 damage-response layout, including `DamageIcon` and shifted
  life-change, point-damage, assist, and death fields. Earlier releases retain their existing layouts.
- `ValorantRoundPhaseChanged` precedes each round-boundary RPC, so consumers no longer match RPC names.
- `ActorOwnerReferences.Read` is the one way to read an actor's Owner, Instigator and CreatedByCharacter, and
  `ValorantActorOwnerObserved` precedes each export that names one.
- Player crosshairs. `BombPlayerStateDescriptor.CrosshairSettings` exposes the crosshair members present in an update
  (`CrosshairSettingsUpdate`), `CrosshairSettings` is the merged state, and `ValorantEventEnricher` emits
  `ValorantCrosshairUpdated` with a full snapshot per player state whenever it changes. The struct is replicated as 159
  flattened leaves; `CrosshairWireLayout` describes them and binds them by position relative to the other player-state
  properties, because leaf names repeat and the export table only lists leaves some player sent. Unsent leaves hold the
  class default object: zero/false, white for the colours, black for the outline colour. Decoded on 12.08 to 13.09.
  Two wire orderings exist and a replay is bound with the one whose names match the most exports of its crosshair block
  (`CrosshairWireLayout.Current` for 12.11 to 13.09, `CrosshairWireLayout.Legacy` for 12.08 and 12.10, where
  `bAllowVertScaling` precedes `LineOffset` in every line section and the sniper dot leaves are
  `bUseCustomCenterDotColor`, `CenterDotSize`, `CenterDotOpacity`, `bDisplayCenterDot`). `CrosshairWireLayout.Inspect`
  reports the chosen ordering and every export inside the block whose name fits no leaf; such a leaf is not bound. The
  count reaches consumers as `CrosshairSettingsUpdate.UnmatchedLeafCount` and `ValorantCrosshairUpdated.UnmatchedLeafCount`
  (`IsPartial` when above zero, sticky per player state), so a patch that inserts or reorders a leaf is reported instead
  of silently losing its value.
- `FieldDescriptor.HandleResolver` and `FieldDescriptor.Assigner` (`ExportGroupDescriptor<T>.AddResolvedField`) let a
  descriptor bind a field by a function of the replay export table and route its value to custom code instead of a CLR
  property. `FieldDescriptor.BindingStateResolver` computes per-replay state when the group is bound and passes it to
  the assigner.

### Fixed

- Damage and temporary-death RPCs before 12.10 decode: `FNetworkedDamageResponseData` had no
  `KillCreditPlayerState`, so every later handle sits one lower. 12.10 and 12.11 are unverified.
- RoundResults uses the 12.11 handle layout from 12.09, where 12.09 and 12.10 replays already send it.

### Breaking changes: separate native parsing and gameplay events

These changes describe the event-project refactor in the current development version.

`ValorantReplayReader`, including `CreateDefault`, now emits only native decoded events: `ActorSpawned`, `ActorClosed`, `ExportGroupReceived`, `RpcReceived`, and `NetGuidExported`. Existing readers still compile, but their sinks no longer receive derived movement, shot, flash, nearsight, or wall events automatically. Consumers that only need native decoding can keep using the core package.

#### Package, assembly, and namespace migration

Consumers of derived events must reference the new `Replay.Valorant.Events` project or its `ValorantReplayParser.Events` package. For source consumers, add a project reference to `src/Replay.Valorant.Events/Replay.Valorant.Events.csproj`. For package consumers, use matching parser and Events package versions from a release containing this refactor; adding the Events package brings in the core parser transitively.

The following public types moved from `Replay.Valorant.dll` to `Replay.Valorant.Events.dll`:

| Types | Previous namespace | New namespace |
| --- | --- | --- |
| `ValorantShot`, `ValorantShotReceived`, `ValorantShotFireMode` | `Replay.Valorant.Combat` | `Replay.Valorant.Events.Combat` |
| `ValorantEquippable`, `ValorantEquippableCategory`, `ValorantEquippableResolver` | `Replay.Valorant.Combat` | `Replay.Valorant.Events.Combat` |
| `RemoteCharacterMovementReceived`, `IRemoteCharacterMovementSink` | `Replay.Valorant.Movement` | `Replay.Valorant.Events.Movement` |
| All flash event records and their enums | `Replay.Valorant.Flashes` | `Replay.Valorant.Events.Flashes` |
| All nearsight event records and their enums | `Replay.Valorant.Nearsights` | `Replay.Valorant.Events.Nearsights` |
| All wall event records and their enums | `Replay.Valorant.Walls` | `Replay.Valorant.Events.Walls` |

Update imports and fully qualified type names, then rebuild dependent assemblies. There are no compatibility aliases or type forwarders. Update reflection lookups and persisted assembly-qualified type names if your application uses them. Native descriptors and wire models, including `MovementMove`, `ComponentDataStream`, `RemoteCharacterUpdateBatch`, and `ReplayPlayContinuousEffectAtLocationParameters`, remain in `Replay.Valorant`; do not replace all `Replay.Valorant.*` imports indiscriminately.

#### Opt into gameplay events

Previously, passing your sink directly to `ValorantReplayReader` also enabled the built-in enrichers:

```csharp
var reader = new ValorantReplayReader(eventSink: sink);
var result = reader.Read(stream);
```

To retain derived events, wrap your existing `IReplayEventSink` with the public `ValorantEventEnricher`:

```csharp
using Replay.Valorant;
using Replay.Valorant.Events;

// sink is your existing IReplayEventSink; stream is positioned at replay start.
var events = new ValorantEventEnricher(sink);
var reader = new ValorantReplayReader(eventSink: events);
var result = reader.Read(stream);
events.Complete();
```

- Create a fresh enricher for each replay. The core reader still supports sequential reuse, but an enricher retains replay-specific state and cannot be reused after completion. Create a new reader with the fresh enricher for the next replay.
- Call `Complete()` only after `Read` returns successfully, including a result with recoverable warnings. It flushes deferred flash, nearsight, and wall events. Do not put it in a `finally` block after a failed parse. Keep the downstream sink open until completion finishes.
- Repeated `Complete()` calls have no effect. Calling `Emit` after completion throws `InvalidOperationException`. Consumer exceptions can also propagate from `Complete()`.
- The enricher forwards native events as well as derived events. Pass the full native stream to it and filter at the downstream sink so correlation receives actor, property, RPC, and GUID updates. Parse profiles must still enable the fields needed by the events you consume.
- Treat events as provisional until both reading and completion succeed. Deferred events can have earlier timestamps than events already delivered. Movement and shot events are now derived when their decoded `RpcReceived` reaches the enricher; do not rely on their former position relative to other native events.

The individual enrichers remain implementation details; `ValorantEventEnricher` is the public entry point. CLI logging and JSON export already opt into it.

#### Native payload and decoder API changes

- Fields decoded with `ObjectNetGuid()` now use `Replay.Models.Net.NetObjectReference` (or its nullable form) instead of `uint`. Read `.NetGuid` for the recorded identifier and `.Path` for the full asset path or subobject name resolved from the GUID cache at decode time. Unknown paths are null; previously emitted values are not updated when a later export supplies a path. For nullable fields, null means absent in the decoded update, while `NetGuid == 0` is a recorded null object reference. Inventory slots and `EquippableStateMachineComponentDescriptor.CurrentState` follow this contract.
- `DamageParameters<T>.EquippableUsed` changed from `ValorantEquippable?` to `NetObjectReference?`. `.EquippableUsed?.NetGuid` still returns the identifier; `.Path` exposes the native resolved path. `Name`, `Category`, and `ClassPath` are no longer populated, even when using `ValorantEventEnricher`. Consumers needing equipment names can use the moved `ValorantEquippableResolver` with their own GUID/path cache.
- `ValorantShot.WaitOnReplicationActor`, `FiringPlayerState`, `FiringState`, `EffectEquippable`, and `EffectContainer` now use `NetObjectReference?`. Access their GUIDs through `.NetGuid`; shot correlation still uses the recorded identifiers.
- Diagnostic object-reference values now use `DecodedFieldValueKind.ObjectReference`, `ObjectReferenceValue`, and `DecodedFieldValue.FromObjectReference`, replacing the former `NetGuid` kind, `NetGuidValue`, and `FromNetGuid`. Damage `EquippableUsed` diagnostics also use this representation instead of an equipment object in `ObjectValue`.
- CLI JSON schema version increased from 8 to 9. Decoded object references in payloads, diagnostics, and shot fields now serialize as `{"net_guid": 123, "path": "..."}`; an unknown path is null. Update consumers that expected a GUID number in those fields.
- `ReplayPlayContinuousEffectAtLocationParameters.CreateShot()` and `EmitDecodedEvents(ref FieldDecodeContext)` were removed, and the descriptor no longer implements `IDecodedPayloadEventEmitter`. Consume its native values from `RpcReceived.Payload`, or consume `ValorantShotReceived` through `ValorantEventEnricher`. Shot mapping is internal to the Events project; there is no public replacement factory on the descriptor.
- Remote-character RPC decoding returns a native `RemoteCharacterUpdateBatch` without emitting `RemoteCharacterMovementReceived` as a decoding side effect. Low-level decoder consumers should read that batch directly or pass its enclosing decoded `RpcReceived` through the enricher. Derived movement still selects the latest decoded move for each update with a known character GUID.

#### Additional native GUID events

`NetGuidExported` in `Replay.Models.Events` exposes `NetGuid`, `Path`, and `OuterNetGuid` along with the event timestamp and packet ID. Update exhaustive event switches, event-count expectations, and packet indexing: frame-level GUID exports have `PacketId == -1`; packet-level exports carry their packet ID.

For offline enrichment, retain GUID exports and preserve the native stream's emission order. Consumers maintaining their own `Replay.Encoding.Net.NetGuidCache` can apply each export with `SetNetGuidPath(export.NetGuid, export.Path, new NetworkGuid(export.OuterNetGuid))`, where `NetworkGuid` is in `Replay.Models.Net`. Reset that cache for each replay. The public enricher maintains its own cache automatically.

## 0.1.0-alpha.2

Changes since the published `0.1.0-alpha.1` package (commit `e6f0a888c9fb12b7d47135acae2ddb5102991ba3`). This is an experimental .NET 10 release with public API changes.

### Added

- Payload transforms for the exact global branches 11.06 through 11.11 and 12.00 through 12.09, plus China 13.05. Unknown branches still fail early; global and China transforms are selected separately.
- Typed inventory slot references, sparse multi-item slot updates, purchase provenance, credits, and gun-request RPC parameters. Inventory/economy profiles retain the relevant identity fields.
- Expanded Cypher descriptors for tripwires, camera/darts, cages, and ultimate actors; corrected tripwire and cage export paths.
- Controllable pawn ownership, possession, ability fuel/resources, ammo, radius, cooldown, armor, Spike pickup/drop/explosion, and ground pickup descriptors. Added Phoenix fire/ultimate and Chamber trap/teleporter ownership descriptors.
- Resolved `EffectContainerPath` on decoded continuous and one-shot effect parameters when the GUID cache already contains the referenced path.

### Fixed

- Unknown `FAresPlayerRoundInfo` layouts fail through the documented `ReplayParseException` contract. Parsing remains strict and stops immediately.
- The Encoding package includes the vrfkit and ValCoach notices and license files; package validation checks their presence.
- Catalog payload-creation regression coverage verifies runtime descriptor types and paths, including descriptors with constructor arguments.

### Migration from 0.1.0-alpha.1

- `AresInventoryDescriptor.ItemSlots` changed from writable `ValorantRawPayload?` to a read-only `IReadOnlyList<uint?>` in `EAresItemSlot` order. Use the named slot properties, `GetItemSlot`, or `GetDecodedItemSlots`. Null means absent from this update; zero clears a reference. Merge updates in the consumer.
- `AresPlayerRoundInfo` now has six positional members: `Index`, nullable `RoundNumber`, and the four nullable credit/loadout values. Update constructors and deconstruction. Use `Index` to merge sparse array entries; `RoundNumber` is the separately replicated value and can be absent.
- `AbilityCooldownComponentDescriptor.CooldownSeconds` and `StartTimeStamp` changed from `float` to `double` to match their 64-bit wire values.
- Cypher actor descriptors now share `GumshoeActorDescriptor`. Ownership references and tripwire `Deployed` flags are nullable; `CageTrapAbilityDescriptor.AttachComponent` is a nullable object GUID instead of raw payload, and projectile `ReplicatedMovement` is `FRepMovement?` instead of an ignored integer. Removed ignored role/seed fields, cage ability `RelativeScale3D`, and projectile `HasStopped`; update consumers that referenced them. Use the corrected descriptor paths or `GumshoePaths` for path filters.
- Inventory and purchase data are raw typed descriptor updates. Merged snapshots, identity joins, and semantic analysis belong to the consumer/analyser. The development-only `ValorantInventoryChanged`, `ValorantInventoryRemoved`, and `ValorantItemPurchaseInfoChanged` events were never in published `alpha.1`; their removal is not a migration break from that package.

### Known limitations

- China 13.05 is covered by recovered payload vectors and branch-selection tests. A complete China replay fixture is not included, so full replay validation remains unverified.
- A supported branch establishes container/transform compatibility, not complete gameplay coverage. The public API remains experimental, and input streams are still buffered in memory.
