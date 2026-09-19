# Initial public NuGet release: implementation specification

Prepared on 2026-09-19. This is a plan, not a claim that the fixes are implemented.

## 1. Goal, baseline, and constraints

Release an explicitly experimental VALORANT-only parser as `0.1.0-alpha.1`. Keep `net10.0`, NUnit, the four library projects, and their existing dependency direction. Do not introduce a generic Unreal compatibility layer or a new replay middleware architecture.

The working tree already contains the requested fix moving five stable-subobject mappings from `ContentBlockPathResolver` into `ValorantDescriptors.CreateCatalog`, through `DescriptorCatalog` and `ExportBindingRegistry`. Preserve that work and its tests. Baseline after the fix: 404 passing tests, including 27 integration tests. Release packing succeeds but produces default metadata.

Implement the phases in this document in order, keeping each phase buildable. Do not modify transform bodies or refactor their duplication. Feature expansion, asynchronous parsing, cancellation, streaming-archive replacement, AOT support, and general performance work are out of scope. No commits, staging, tags, publishing, or external issue changes are implied by this plan.

## 2. Parsing failure contract and diagnostics

### Fixed behavior

There is one policy, not selectable strict/lenient modes:

| Condition | Behavior |
| --- | --- |
| Invalid magic, unsupported versions, duplicate header, data before header | Throw the existing appropriate `ReplayParseException` subtype immediately. |
| Invalid declared length, required-read truncation, arithmetic overflow, malformed packet/content/field framing | Fatal. Throw `InvalidReplayDataException` for replay-data errors and retain archive details in the inner exception. |
| Required typed decoder cannot consume its bounded payload | Fatal, except explicitly speculative semantic decoders below. |
| Unknown field/RPC with valid bounds, profile-excluded data, deliberately skipped checkpoint/event chunks | Skip using the declared bounds. This is supported partial feature coverage, not structural corruption. |
| Partial-bunch sequence anomaly | Discard the affected invalid assembly, record a structured warning, and recover at a valid boundary. Never decode the damaged assembly. |
| Incomplete assembly at EOF | Discard, report `IncompletePartialBunch`, and return completed-with-warnings. Applies to both live and completed recordings. |
| Explicitly speculative typed decoding falls back to raw data | Preserve fallback and checkpoint rollback, but report a structured warning. |
| Resource limit exceeded | Fatal `InvalidReplayDataException` identifying limit, allowed value, requested value, packet/channel where available. |
| Consumer event sink throws | Abort, clean up, and preserve the consumer exception. Do not classify it as invalid replay data. |

Returning means the supplied container was traversed under this policy, not that every VALORANT feature was decoded or the recording contains an entire match. Previously emitted events are provisional if a later error occurs. Do not buffer the whole event stream to provide rollback.

### Public diagnostic DTOs

Add under `Replay.Models/Diagnostics`:

- `ReplayReadStatus`: `Completed`, `CompletedWithWarnings`.
- `ReplayDiagnosticCode`: `PartialSequenceError`, `IncompletePartialBunch`, `RawPayloadFallback`.
- `ReplayDiagnostic`: sealed record with init-only `Code`, `Message`, `int? PacketId`, `uint? ChannelIndex`, `float? TimeSeconds`, `string? ExportGroupPath`, and `string? FieldName`. Include the specific sequence error or fallback reason in Message.

Add an internal collector in `Replay.Unreal`. Retain at most 1,000 diagnostics. Count every attempted warning in `long TotalDiagnosticCount`; expose `long SuppressedDiagnosticCount`. Status depends on total, not retained count. Never retain exceptions, payload bytes, archives, contexts, or callbacks in diagnostics. A logger is optional and never the only way to discover a warning.

### Required code changes

1. `FieldPayloadParser`: replace malformed-length warning/skip/return paths for properties and RPCs with `ArchiveReadException`, code `InvalidBitCount`. Keep legitimate existing terminator rules; do not invent padding rules from zero bits. Include group, field/function, handle, packet, and channel when wrapping archive failures. Remove log-and-rethrow duplication. Programming/configuration exceptions remain programming errors.
2. `ContentBlockFramer.TryReadContentPayloadBitCount`: replace the Try/false contract with a required read that throws for invalid counts. Update its caller so malformed framing cannot return normally.
3. `ContentBlocksBunchStage`, `ActorChannelOpenBunchStage`, `MustBeMappedGuidsBunchStage`: remove catch-and-stop recovery of required archive reads. Counters cannot substitute for exceptions. Preserve context through an enclosing bunch boundary or contextual wrapper.
4. `TrailingPayloadBunchStage`: unexpected remaining bits throw `UnexpectedTrailingData`. Keep explicit skip paths for unhandled channels and excluded payloads; intentionally skipped data must not trigger this check.
5. `PlaybackPacketReader`: invalid packet sizes and malformed packets throw `InvalidReplayDataException`, not `InvalidReplayInfoException`. Normalize archive/overflow failures at the data boundary once. Do not repeatedly wrap an existing `ReplayParseException`.
6. `BunchPayloadPipeline`: add packet/channel context to archive errors before they escape; preserve owned-payload disposal in `finally`.
7. Audit remaining catches, malformed counters, and `SkipRemaining` paths in Unreal against the behavior table. Required-read failures are fatal; bounded unknown-payload skips remain supported.

Do not catch arbitrary `Exception` as a parse failure. To preserve the identity of sink exceptions even when a sink throws `ArchiveReadException` or `OverflowException`, wrap consumer sink invocation in an internal exception carrying `ExceptionDispatchInfo`, let it cross parser normalization boundaries, and rethrow the original at the public reader boundary. Ordinary sink exceptions follow the same path. Session cleanup still runs.

### Speculative semantic decoders

Keep checkpoint rollback in `CompatibleAresRoundResultsDecoder`, `CompatibleAresTeamEconomyDecoder`, `CompatibleCombatRoundReportsDecoder`, and `PrimitiveOrRawDecoder`. Add an internal diagnostics reference to `FieldDecodeContext`, populated from the session. Grant `Replay.Valorant` friend access when needed. When a typed attempt falls back, report exactly one `RawPayloadFallback`, including the reason, before invoking the raw fallback. Initially configured raw/skip decoders do not produce a warning on every invocation.

Keep internal layout-control exceptions internal: they must be caught inside the speculative decoder and never escape the public reader. Required framing outside the speculative field is still fatal. Document existing `WasDecoded` as an indication of decoder processing, not a completeness guarantee; `HasDecoded` remains the typed property-presence contract. Do not redesign event DTOs in this phase.

### Acceptance tests

Add NUnit cases for oversized field/RPC lengths, truncated actor-open data, truncated must-be-mapped GUIDs, invalid content-block length, required decoder under/over-read, and unexpected trailing payload. Assert the public read fails with the expected exception chain and no successful event for the malformed payload. Include a valid field followed by a malformed field to detect accidental partial-result success.

Keep unknown-but-bounded and zero-bit-field tests passing. Cover checkpoint rollback and diagnostics for all four speculative decoder families, no-logger behavior, exactly 1,000 retained warnings, suppressed counts, and ordinary/archive-shaped sink exceptions. Update logger tests to validate contextual exceptions/warnings rather than duplicate error logs.

## 3. Partial assembly limits and resource ownership

### Limits

Add internal immutable `PartialBunchLimits` with defaults:

- 16 MiB assembled payload per channel.
- 128 MiB aggregate retained buffer capacity across pending assemblies.
- 1,024 simultaneous pending assemblies.

These are supported-input limits, not claimed wire-protocol maxima. No public tuning API for this release. An internal constructor accepts smaller limits and a `MemoryPool<byte>` for tests. Keep the existing 256 MiB decompressed-chunk limit. Whole-file buffering in `FBinaryArchive(Stream)` remains and must be documented; these limits do not bound total process memory.

### Implementation

1. Make `PartialBunchAccumulator` and `IPartialBunchAccumulator` disposable. Disposal is idempotent, releases every held owner, clears state/accounting, and does not emit events/diagnostics. Operations after disposal throw `ObjectDisposedException`.
2. Calculate bit totals/byte rounding with checked `long` intermediates. Validate the per-assembly size and pending-count limits before allocating, then checked-convert to int. Never let overflow produce a smaller allocation.
3. Track actual rented capacity and logical payload bytes. Reject/dispose a new rental if aggregate retained capacity would exceed 128 MiB. During replacement, cap temporary old-plus-new capacity at 256 MiB. Dispose a rejected replacement; leave accounting correct if allocation/copy fails. Do not mistake pool bucket capacity for requested length.
4. `TryComplete` removes the assembly/accounting and transfers the owner exactly once. The accumulator must not dispose transferred memory. The existing owned bit archive/bunch context disposes it after processing, including sink failure.
5. Overlapping initial: discard/report the old assembly, then accept the new valid initial. Missing initial: discard/report the continuation. Mismatched continuation: discard/report the affected assembly. Do not carry the old assembly's error flag onto a valid replacement. Concatenate fragments at bit granularity; nonfinal fragments may end at any bit offset. Supported replay fixtures contain nonfinal fragments with non-byte-aligned lengths, and the original accumulator's bitwise copy behavior is required to recover their gameplay events. Invalid declared payload bounds, truncated reads, arithmetic overflow, and configured resource-limit violations remain fatal.
6. Add explicit EOF finalization to report/discard unfinished assemblies. Do not use Dispose for successful finalization: cleanup during exception unwinding must not emit additional output or mask the original error.
7. `BunchPayloadPipeline` owns the accumulator. The session/context owns the pipeline. The public reader disposes the session on all exit paths. Borrowed input archives/streams, sinks, logger factories, and caller-supplied test decompressors are never disposed by the reader.
8. Make owner-backed `ByteArchiveReader` and `BitArchiveReader` disposal idempotent, with reads/seeks after disposal rejected. Do not introduce finalizers.

Keep the existing duplicate sequence tracking in `RawPacketReader` and the accumulator during this release; replacing it is separate work. Only the accumulator emits session sequence warnings. Packet and bunch counters retain their current distinct meanings. Do not change fragment header-merging semantics without a failing wire-format test.

### Acceptance tests

Use a counting fake memory pool, not GC timing. Test exact limits, one-byte-over limits, multiple channels, checked arithmetic near int boundaries through a small size-calculation helper, concatenation across non-byte-aligned boundaries, overlapping initials, missing/mismatched continuations, zero-length final fragments, ownership transfer, EOF cleanup, repeated disposal, and exception unwinding. Every owner must be disposed exactly once and accounting must return to zero. Discarded assemblies emit no payload events.

Existing integration fixtures explicitly expect partial-sequence errors. They must remain supported and return `CompletedWithWarnings` with corresponding diagnostics. Their expected packet error count is not permission to suppress structural archive failures.

## 4. Public reader API and detached results

### Exact API decisions

Keep `ValorantReplayReader` in `Replay.Valorant` with this public constructor:

```csharp
public ValorantReplayReader(
    ILoggerFactory? loggerFactory = null,
    IReplayEventSink? eventSink = null,
    ParseProfile? parseProfile = null,
    DescriptorCatalog? descriptorCatalog = null);
```

Defaults are a no-op sink, `ParseProfile.Default`, a fresh VALORANT catalog, OozSharp, and the normal replay-data handler. Keep `CreateDefault` as a thin factory with its existing argument order and an optional logger. Remove `CreateMinimal`: replace callers with an explicit `ParseProfile.Minimal`. Keep an internal injection constructor for fake decompressors/chunk handlers; make its decompressor argument mandatory to prevent overload ambiguity.

Expose these synchronous methods:

```csharp
public ValorantReplayReadResult Read(FBinaryArchive archive);
public ValorantReplayReadResult Read(Stream stream);
public ValorantReplayMetadata ReadMetadata(FBinaryArchive archive);
public ValorantReplayMetadata ReadMetadata(Stream stream);
```

Stream overloads own only the archive they construct and leave the supplied stream open. Consume from the current position; never implicitly rewind. Archive overloads leave the caller's archive open at the consumed position. ReadMetadata on an archive stops after the header. Because the current stream archive buffers all remaining input up front, either Stream overload leaves the supplied stream at EOF even for metadata-only parsing. Document this explicitly; do not promise header-only stream I/O. Calling full Read afterward requires reopening or explicitly rewinding input, not resuming from either consumed position.

Support sequential reuse, not concurrent/reentrant reads. Use `Interlocked.CompareExchange` for a single active-read guard shared by full/metadata methods; reset it in `finally`. Forwarding overloads call private core methods so they do not acquire the guard twice. Create the default decompressor per read. Snapshot ParseProfile selection sets at read start. Custom catalog/descriptor mutation during a read is unsupported and documented.

### Result contract

Add sealed record `ValorantReplayReadResult` in `Replay.Valorant` with required init-only properties:

```text
ValorantReplayMetadata Metadata
ReplayPacketStatistics PacketStats
ReplayBunchStatistics BunchPayloadStats
ReplayReadStatus Status
IReadOnlyList<ReplayDiagnostic> Diagnostics
long TotalDiagnosticCount
long SuppressedDiagnosticCount
IReadOnlyList<ReplayExportGroupSummary> ExportGroups
```

Place statistics/export summary records in `Replay.Models/Results`:

- Packet and bunch snapshots contain exactly the scalar properties currently in `RawPacketStats` and `BunchPayloadStats`, preserving their names/types, with init-only setters and no mutation methods.
- `ReplayExportGroupSummary`: `string PathName`, `uint PathNameIndex`, and `IReadOnlyList<ReplayExportFieldSummary> Fields`.
- `ReplayExportFieldSummary`: `uint Handle`, `string Name`, `uint CompatibleChecksum`.
- Copy non-null fields in handle order and groups in ordinal path order.

Deep-copy existing metadata at the boundary, including nested arrays, lists, custom versions, chunk DTOs, and encryption-key bytes. Do not redesign every metadata model now: document nested metadata as consumer-owned mutable data detached from execution state. Result collection wrappers must not expose session-owned mutable lists. No archive, GUID cache, channel state, callback, logger, binding registry, pipeline, or pooled memory belongs in the result.

Construct the result after dispatch, incomplete-assembly finalization, and semantic enricher completion; dispose the session afterward. Do not invoke enrichers' Complete methods following fatal parse/sink failure. Apply the same detached metadata policy to ReadMetadata.

### Visibility changes and caller migration

Make `ReplayReaderContext`, `BunchPayloadPipeline`, `RawPacketStats`, `BunchPayloadStats`, `IReplayDataChunkHandler`, and its implementations internal. Reduce visibility of methods/classes exposing these session types: dispatcher execution, playback reader, frame readers, and session orchestration. Keep archives, metadata/events, descriptor builders, `IFieldDecoder`, `IRpcDecoder`, and `FieldDecodeContext` public for deliberate extension use. Do not blindly make the entire Unreal assembly internal.

Add Unreal friend assemblies for `Replay.Valorant`, `Replay.Unreal.Tests`, `Replay.Valorant.Tests`, and `Test.Integration`. Add `Test.Integration` as a Valorant friend for the internal test constructor where needed. Do not give CLI projects friend access.

Migrate all callers:

- `ReplayExportRunner` and every `ReplayExportManifestWriter` context parameter use the result.
- `ReplayLogRunner` reads metadata/statistics from the result.
- `NetGuidCacheReader/Program.cs` enumerates `ExportGroups`; it does not receive the live cache.
- Integration helpers return results. Replace public-reader assertions on channel dictionaries with lifecycle event/statistics assertions. Lower-level tests can still create internal contexts through friend access.
- Manifest schema becomes 8: preserve current statistics and export-group JSON fields and add `parse_status`, `diagnostics`, `total_diagnostic_count`, and `suppressed_diagnostic_count`. Serialize diagnostic codes/statuses in snake_case, omit null location fields. Fatal exports still leave no success manifest; warning completions write an explicitly warning-marked manifest.

Do not add a public context-returning method as a workaround for migrations.

### Acceptance tests

Constructor and factory decode the same compressed fixture and emit equivalent events. Explicit Minimal skips descriptor decoding; lifecycle events may still occur, so do not promise an empty event stream. Results remain usable after input disposal and after a second sequential read. Inspect the returned object graph to exclude execution-state types; do not rely on forced-GC timing. Test borrowed ownership, metadata position, reentrancy rejection, guard reset after failures, and original sink exception identity.

## 5. Finish extracting VALORANT path knowledge

Preserve the five explicit stable-subobject mappings in the VALORANT catalog. Do not replace them with name-based inference: instance names and class names are not interchangeable.

Move `/Game/Characters/` and `/_Core/` alias rules from `Replay.Unreal/Parsing/ReplayPath.cs`. These apply to runtime-discovered paths, so a finite table derived only from known descriptors would change behavior. Use one narrowly scoped interface:

```csharp
// Replay.Models.Descriptors
public interface IReplayPathAliasProvider
{
    string? GetAlternatePath(string path);
}
```

Add `IReplayPathAliasProvider? PathAliasProvider { get; init; }` to `DescriptorCatalog`. Implement internal singleton `ValorantPathAliasProvider` under `Replay.Valorant/Descriptors`, moving the current algorithm verbatim:

1. If the first `/_Core/` segment exists, remove it.
2. Otherwise, if the path begins `/Game/Characters/`, insert `_Core/` immediately afterward.
3. Otherwise return null.

Register it in `ValorantDescriptors.CreateCatalog`. No recursion, regex, global mutable registry, or general prefix-plugin system.

`ReplayPath` retains generic Default__ and ClassNetCache suffix handling. Lookup helpers accept the optional provider. Preserve lookup order: exact path, default-object alias, one game alias; apply class-net-cache suffix variants in the current order. Ignore alternate paths identical to the input.

Thread the same provider through `DescriptorCatalogIndex`, `BoundExportStore`, and `ContentBlockPathResolver` via `ExportBindingRegistry`. Set it before indexing a replacement catalog and clear previous bindings. Without a provider, Unreal performs no `_Core` rewrite. Add an internal monotonically increasing catalog revision to the registry; the resolver clears its path caches when the revision changes. This prevents old cached paths surviving catalog replacement through an existing registry.

Tests: actual game rewrite rules in Valorant tests; generic provider plumbing with invented paths in Unreal tests. Cover both directions, no provider, exact-match precedence, class-net-cache suffixes, inline RPC parameter binding, null aliases, and catalog replacement. Acceptance search: no `/Script/ShooterGame`, `/Game/Characters/`, or `/_Core/` production literals remain in Unreal. VALORANT wire-format header/packet parsing stays there, per repository architecture.

## 6. NuGet identities, metadata, and contents

Use these IDs without renaming assemblies or C# namespaces:

| Project | Package ID |
| --- | --- |
| Replay.Valorant | ValorantReplayParser |
| Replay.Models | ValorantReplayParser.Models |
| Replay.Encoding | ValorantReplayParser.Encoding |
| Replay.Unreal | ValorantReplayParser.Unreal |

All four use `0.1.0-alpha.1`; consumers normally reference only the main package. Preserve ProjectReference dependency packaging. Do not embed sibling DLLs, use ILMerge, or create compatibility packages for the old default IDs. Keep current third-party dependency versions unless a concrete release blocker is discovered.

Add `build/LibraryPackage.props`, explicitly imported by the four library projects, for shared metadata. Do not introduce central dependency versioning for this task. Set packability/marker properties before the import; do not rely on project properties being available during the earlier Directory.Build.props import.

Shared properties:

| Property | Value |
| --- | --- |
| Version | `0.1.0-alpha.1`, overridable with `-p:Version=...` |
| Authors | `Michel Giehl` |
| PackageLicenseExpression | `MIT` |
| PackageProjectUrl / RepositoryUrl | `https://github.com/michel-giehl/ValorantReplayParser` |
| RepositoryType | `git` |
| PackageReadmeFile | `README.md` |
| PublishRepositoryUrl | `true` |
| IncludeSymbols | `true` |
| SymbolPackageFormat | `snupkg` |
| DebugType | `portable` |
| GenerateDocumentationFile | `true` |
| ContinuousIntegrationBuild | `true` in CI only |

Use SDK-provided GitHub Source Link; do not add an unnecessary SourceLink dependency. Suppress only CS1591 for the existing large undocumented descriptor surface, with an explanation. Do not weaken global warnings-as-errors. Add XML docs to the reader, result, diagnostics, and ownership contracts.

Main package description: `VALORANT replay (.vrf) parsing with typed gameplay events and replay metadata. Experimental API.` Give the other three packages distinct descriptions identifying models, decoding primitives, and the VALORANT container/network parser.

Pack the repository README and LICENSE at each package root via explicit linked items. Set `IsPackable=false` explicitly on both CLI projects and every test project. Pack only the four library project files, output to `artifacts/packages`, and ignore `artifacts/` in Git.

Add `global.json`: SDK `10.0.301`, `rollForward: latestPatch`, `allowPrerelease: false`, matching the reviewed local SDK. CI reads this file.

Produce four nupkg files and four matching snupkg files. Inspect archives for correct dependency IDs/version, XML documentation, README/LICENSE, repository URL/commit, and portable symbols. Exclude replay fixtures, dumps, CLI/test binaries, and development outputs. Do not ship absolute workspace paths in package metadata.

Package-ID availability/ownership and credentials are external prerequisites, not established by local packing. Check before upload; do not silently rename IDs or publish under another identity. This task prepares artifacts and does not publish.

## 7. CI and isolated package-consumer validation

Update `.github/workflows/build.yml` to run on main pushes and pull requests with Windows and Ubuntu jobs. Restore/build/test the solution in Release, including all five existing test projects. Set `SNAPSHOOTER_STRICT_MODE=true` for integration tests so absent snapshots cannot pass by being generated. Keep the nightly integration workflow if useful, but nightly success is not the release gate. Use a 30-minute job timeout and install .NET from global.json.

Add `tests/PackageConsumerSmoke/PackageConsumerSmoke.csproj` as a net10.0 console project outside the solution, with `IsPackable=false`. It has exactly one direct PackageReference to `ValorantReplayParser`, using required MSBuild property `PackageUnderTestVersion`. It must fail early if that property is absent. No ProjectReference, linked source, DLL HintPath, or friend access is allowed. The replay path is a runtime argument, not a copied package asset. Reuse the existing supported fixture `12974d2b-848f-490d-80ba-5f03a033c2d5.13_00.vrf`.

Add `build/Test-Packages.ps1` using PowerShell Core on both platforms. Give it mandatory `-Version` and optional `-ReplayPath`; the default replay path resolves the fixture above relative to the repository root. The script must:

1. Resolve repository and artifact paths explicitly. Clean only its own artifact directories after checking that resolved paths are inside this checkout; use PowerShell LiteralPath operations.
2. Pack Models, Encoding, Unreal, then Valorant in Release with the identical supplied version. Keep build/configuration consistent with the test run.
3. Inspect all four nuspecs and archive contents for the metadata/dependency/content requirements in phase 6. Check the main package actually references the renamed sibling package IDs and that the Encoding package retains OozSharp.
4. Generate isolated NuGet configuration clearing inherited sources, with the local artifact feed and nuget.org. Use source mapping: `ValorantReplayParser*` must resolve exclusively from the local feed; external dependencies use nuget.org. Use the more specific local prefix mapping and a nuget.org wildcard for third-party dependencies.
5. Restore the smoke project using that configuration, an empty packages directory under `artifacts/package-smoke/packages`, and explicit version property. Build/run without a second restore. Check each command's exit code and stop on failure.
6. Run the smoke program against the fixture. Require Supported metadata, nonzero packets, nonzero typed gameplay events, a valid completion status, accessible detached export summaries, and a still-open borrowed input stream. Reopen the input between metadata/full reads. Dispose input, then verify the result still works. Parse invalid magic and require `ReplayParseException`. Exit nonzero on any failed assertion.

Run this script in both CI jobs after solution tests. Upload packages, symbols, and test results as workflow artifacts, not replay fixtures or dependency caches. A successful pack alone is not the acceptance criterion: the isolated consumer proves transitive package identities, dependency installation, public API accessibility, and OozSharp operation on each platform.

Add a manual `release-candidate.yml` workflow accepting a version. Validate prerelease SemVer and require it to equal the intended source release version. Run the same full validation and produce artifacts named with version/commit SHA. It must not push to nuget.org, create tags, create GitHub releases, or introduce publishing credentials. Later explicit publication must use these exact verified artifacts.

## 8. SonarCloud decisions and documentation

The inspected SonarCloud analysis on 2026-09-19 reported 64 unresolved code smells. Its new-code gate failed only on duplicate density, 3.1% versus a 3% threshold. That analysis revision differs from the local checkout; rerun analysis after implementation before comparing issue counts.

Configure only this duplication exclusion in the actual analysis configuration:

```text
sonar.cpd.exclusions=src/Replay.Encoding/PayloadEncryption/VersionedTransforms/**/*.cs
```

Do not use `sonar.exclusions` for these files: correctness/security analysis and transform tests must remain enabled. There is currently no scanner workflow in the repository. If the project uses automatic analysis, set duplication exclusions in SonarCloud project settings and document them in `docs/sonar-triage.md`. Do not create an ignored properties file or duplicate analysis pipeline. If settings access is unavailable, report this exact external action as pending; do not claim the gate is fixed. Plan implementation alone does not authorize external issue status changes.

Triage rules:

| Sonar issue | Decision |
| --- | --- |
| S2139 log-and-rethrow | Fix through contextual exceptions in phase 2. |
| S3881 disposal | Fix ownership, idempotence, and use-after-dispose behavior in phase 3. |
| S108 empty speculative catches | Preserve intentional fallback, capture its reason, and report a diagnostic. Do not rethrow optional speculative reads just to silence the rule. |
| S3871 private layout exceptions | Keep internal and prove they do not escape. Document as accepted-by-design triage, not a reason to expand the public API. |
| Transform naming/duplication | Keep patch separators and implementations; no cosmetic rewrite. |
| LINQ suggestions, naming, literal extraction, parameter count, cognitive complexity | Not release blockers. Do not rewrite hot paths or split code solely to satisfy thresholds. |

Update README and XML documentation with:

- Installation of `ValorantReplayParser` version `0.1.0-alpha.1` and net10.0 requirement.
- Default-constructor example with a complete event sink; compile the example in the package smoke project or share its actual sample implementation.
- Status/diagnostics handling and `ReplayParseException` handling.
- Supported branches exactly matching `PayloadTransformRegistry` and unsupported-version behavior. Do not add support beyond the registry.
- Explicit Minimal profile semantics and default full descriptor selection.
- Borrowed input ownership, current-position behavior, reopening between metadata/full reads, whole-file buffering, and sequential-only reader reuse.
- Provisional streamed events if parsing later fails. Semantic enrichers can emit deferred events: no guarantee of globally increasing event timestamps.
- `WasDecoded` versus `HasDecoded`, raw fallback warnings, and the distinction between version compatibility and complete semantic coverage.
- Custom catalogs are complete replacements. Show starting with `ValorantDescriptors.CreateCatalog()` to extend defaults, retaining its subobject mappings and path provider.
- Existing MIT license, Riot disclaimer, and experimental API status.

## 9. Implementation order and proof of completion

Use these increments, in order:

1. Diagnostic DTOs/collector and fatal structural-error propagation.
2. Partial-bunch limits, sequence recovery warnings, ownership, and EOF finalization.
3. Detached public result/default constructor, caller migration, and manifest diagnostics.
4. Remaining game-specific alias extraction and resolver cache invalidation.
5. Package metadata and isolated package-consumer smoke.
6. CI, documentation, and Sonar triage instructions.

Each increment must compile and pass focused tests before the next. Run the complete solution after changes crossing assembly boundaries. Do not stage, revert, or overwrite the pre-existing resolver fix. Do not update gameplay snapshots to conceal changed semantics.

Final local commands:

```powershell
dotnet restore ValorantReplayParser.sln
dotnet build ValorantReplayParser.sln -c Release --no-restore
$env:SNAPSHOOTER_STRICT_MODE = 'true'
dotnet test ValorantReplayParser.sln -c Release --no-build --no-restore
pwsh -File build/Test-Packages.ps1 -Version 0.1.0-alpha.1
git diff --check
```

Completion checklist:

- Both CI platforms pass solution tests and the isolated package consumer.
- Original 404 tests remain represented unless explicitly replaced for a documented contract change; report replacements and additions.
- Every supported replay fixture retains gameplay event semantics, or a change is explained and backed by a focused regression test.
- No malformed structural input returns a normal successful result; required archive errors retain contextual details.
- Tolerated anomalies are visible without logging, even when retained diagnostic capacity is exhausted.
- Every pending/transferred buffer has exactly one owner and deterministic cleanup.
- Results retain no parser state/buffers and survive input disposal and later reads.
- No transform implementation bodies changed; transform tests remain enabled.
- No game semantic path literals remain in Unreal; generic parsing still has no dependency on Valorant.
- Four package archives and four symbol archives have the specified IDs/version/contents.
- The package consumer obtains all project packages exclusively from freshly created local artifacts, never project references or stale global packages.
- README examples compile, CLI tools still work through public results, and fatal JSON exports leave no success manifest.

If stricter parsing reveals a supported fixture relying on a swallowed required-read error, add a focused reproduction and correct the parser. Do not add a version wildcard, catch-all recovery mode, or fixture-specific skip to make tests pass. If the wire rule cannot be established from code/tests and the available fixture, report that concrete blocker rather than guessing a new format assumption.

External release prerequisites are package-ID ownership, publication credentials, and Sonar settings access if unavailable. These do not block local implementation/testing. Report them accurately and do not invent successful verification.

## References

- [NuGet pack/MSBuild properties](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets)
- [NuGet symbol packages](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg)
- [SDK-provided Source Link](https://github.com/dotnet/sourcelink)
- [Project SonarCloud dashboard](https://sonarcloud.io/summary/new_code?id=michel-giehl_ValorantReplayParser)
