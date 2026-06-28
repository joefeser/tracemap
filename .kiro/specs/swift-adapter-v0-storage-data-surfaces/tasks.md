# Swift Adapter v0 Storage And Data Surfaces Tasks

Issue: [#385](https://github.com/joefeser/tracemap/issues/385)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

## Phase 0: Spec Scope And Handoff

- [x] Create planning-only Kiro spec files for issue #385.
- [x] Link the spec to issue #385 and parent issue #377.
- [x] Bound claims to deterministic static evidence and explicit gaps.
- [x] Include public-safety constraints for keys, SQL, config, secrets,
  snippets, remotes, and local paths.
- [x] Mark implementation-state status as `ready-for-implementation`.
- [x] Before implementation, re-read `docs/LANGUAGE_ADAPTER_CONTRACT.md`,
  `docs/VALIDATION.md`, `docs/ACCEPTANCE.md`, issue #377, issue #385, and
  this spec.
- [x] Confirm implementation branch name and Swift adapter package path
  `src/swift`.
- [x] Reconcile with companion Swift v0 specs so storage/data facts reuse
  existing manifest, file inventory, safe-value helpers, coverage labels,
  report output, and shared SQL evidence shapes.

## Phase 1: Rule Catalog And Fact Vocabulary

- [x] Add rule catalog entries and limitations for:
  - `swift.storage.coredata.metadata.v1`
  - `swift.storage.userdefaults.key.v1`
  - `swift.storage.keychain.access.v1`
  - `swift.storage.sqlite.sql.v1`
  - `swift.storage.sqlite.table.v1`
  - `swift.storage.realm.model.v1`
  - `swift.storage.analysis-gap.v1`
- [x] Define stable fact property names for CoreData, UserDefaults, Keychain,
  SQLite/GRDB/FMDatabase, Realm, and storage gaps.
- [x] Add smoke guardrails that storage rules do not emit `Tier1Semantic`.
- [x] Reuse shared `SqlTextUsed`, `QueryPatternDetected`,
  `DatabaseColumnMapping`, and `AnalysisGap` where the shared contracts fit.

## Phase 2: CoreData Metadata

- [x] Inventory checked-in `.xcdatamodel` and `.xcdatamodeld` inputs in sorted
  repo-relative order.
- [x] Parse supported CoreData XML metadata without storing raw XML or snippets.
- [x] Emit model, entity, attribute, relationship, fetched-property, and
  fetch-request descriptor facts where statically visible.
- [x] Emit gaps for unreadable, unsupported, or malformed CoreData metadata.
- [x] Add public-safe fixture coverage for parseable metadata and deterministic
  fact IDs.

## Phase 3: UserDefaults Keys

- [x] Detect supported `UserDefaults` read/write/remove/register operations
  with literal keys.
- [x] Resolve bounded static key constants and same-file literal aliases,
  including dotted static constant references.
- [x] Emit public-safe key identities using `normalizedKey` only when allowed,
  otherwise role-separated `keyHash`, `keyLength`, and identity-status
  metadata.
- [x] Emit gaps for dynamic or unsupported UserDefaults keys.
- [x] Add tests proving unsafe key values and source snippets do not leak into
  facts/reports.

## Phase 4: Keychain Access Patterns

- [x] Detect supported Security framework calls:
  `SecItemAdd`, `SecItemCopyMatching`, `SecItemUpdate`, and `SecItemDelete`.
- [x] Extract static query dictionary metadata for operation direction and
  `kSecClass` where visible.
- [x] Hash service/account/access-group descriptors with role-separated hash
  inputs.
- [x] Emit gaps for dynamic/config-derived Keychain query shapes.
- [x] Add tests proving credentials, tokens, service/account values, snippets,
  and local paths are never stored raw.

## Phase 5: SQLite, GRDB, And FMDatabase SQL/Table Surfaces

- [x] Detect complete static SQL literals in supported SQLite, GRDB, and
  FMDatabase-style calls.
- [x] Emit `SqlTextUsed` with `textHash`, `textLength`, `sqlSourceKind`,
  operation metadata where safe, spans, commit SHA, and extractor version.
- [x] Emit SQL-shape `QueryPatternDetected` when deterministic table/operation
  metadata is safely derivable.
- [x] Scan selected checked-in `.sql` resources and emit structural SQL
  text/shape evidence.
- [x] Emit gaps for dynamic SQL, string interpolation, and indirect SQL
  arguments.
- [x] Add tests proving raw SQL, predicate literals, connection strings,
  database paths, URLs, hostnames, unsafe identifiers, snippets, and local
  absolute paths do not leak.

## Phase 6: Realm Model And Persistence Surfaces

- [x] Detect Realm model declarations and supported `@Persisted` property
  patterns from Swift syntax.
- [x] Emit Realm model/property facts with safe type/property identifiers or
  hashes and Tier3 syntax/text evidence.
- [x] Detect static `primaryKey()` declarations when literal property names are
  visible.
- [x] Emit gaps for Realm dynamic predicate/query evidence instead of storing
  raw predicates.
- [x] Add tests proving Realm predicate values, file paths, encryption keys,
  snippets, and local paths are not stored raw.

## Phase 7: Reports, SQLite, Combine, And Reducer Boundaries

- [x] Populate the shared `facts` table with storage/data rows.
- [x] Ensure generated `facts.ndjson` uses stable sorted properties and no raw
  snippets.
- [x] Add `report.md` sections for Swift storage/data counts by framework,
  fact type, rule ID, and gap kind.
- [x] Ensure reports use safe descriptors or hash prefixes only and avoid
  impact/runtime-proof language.
- [x] Validate `tracemap combine` and `tracemap report` can read Swift
  storage/data rows without promoting gaps or hash-only facts to runtime proof.

## Phase 8: Validation

- [x] Add public-safe fixture coverage under
  `samples/swift-storage-data-surfaces`.
- [x] Run Swift build and smoke tests:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
```

- [x] Run the storage/data fixture scan:

```bash
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-storage-data-surfaces --out /tmp/tracemap-swift-storage-data-surfaces
```

- [x] Verify required scan artifacts exist:

```bash
test -f /tmp/tracemap-swift-storage-data-surfaces/scan-manifest.json
test -f /tmp/tracemap-swift-storage-data-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-storage-data-surfaces/index.sqlite
test -f /tmp/tracemap-swift-storage-data-surfaces/report.md
test -f /tmp/tracemap-swift-storage-data-surfaces/logs/analyzer.log
```

- [x] Run SQLite evidence queries for rule IDs, evidence tiers, fact types,
  redaction, and gap coverage.
- [x] Run combine/report smoke:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-storage-data-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-storage-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-storage-combined.sqlite --out /tmp/tracemap-swift-storage-report
```

- [x] Run shared .NET validation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
```

- [x] Run safety checks:

```bash
git diff --check
./scripts/check-private-paths.sh
```

## Deferred Follow-Ups

- SourceKit/compiler semantic enrichment for storage symbol attachment.
- Full CoreData `.xcdatamodel/contents` and version-selection semantics.
- Advanced wrapper/data-flow resolution for UserDefaults, Keychain, GRDB,
  FMDatabase, SQLite, and Realm.
- Runtime instrumentation, simulator/device checks, database connections,
  Keychain access, Realm file inspection, or production-data proof.
