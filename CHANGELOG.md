# Release notes

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
