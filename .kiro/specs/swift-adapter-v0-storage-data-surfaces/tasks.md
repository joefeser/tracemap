# Swift Adapter v0 Storage And Data Surfaces Tasks

Issue: [#385](https://github.com/joefeser/tracemap/issues/385)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Completed tasks describe only this spec PR. Unchecked tasks are future
implementation work. Do not mark implementation tasks complete until analyzer
code, tests, docs, and validation have actually landed.

## Phase 0: Spec Scope And Handoff

- [x] Create planning-only Kiro spec files for issue #385.
- [x] Link the spec to issue #385 and parent issue #377.
- [x] Bound claims to deterministic static evidence and explicit gaps.
- [x] Include public-safety constraints for keys, SQL, config, secrets, snippets,
  remotes, and local paths.
- [x] Mark implementation-state status as `ready-for-implementation`.
- [ ] Before implementation, re-read `docs/LANGUAGE_ADAPTER_CONTRACT.md`,
  `docs/VALIDATION.md`, `docs/ACCEPTANCE.md`, issue #377, issue #385, and this
  spec.
- [ ] Confirm implementation branch name and whether the Swift adapter package
  path remains `src/swift`.
- [ ] Reconcile with companion Swift v0 specs so storage/data facts reuse
  existing manifest, file inventory, symbol IDs, safe-value helpers, and
  coverage labels.

## Phase 1: Rule Catalog And Fact Vocabulary

- [ ] Add rule catalog entries for `swift.storage.coredata.metadata.v1`,
  `swift.storage.userdefaults.key.v1`, `swift.storage.keychain.access.v1`,
  `swift.storage.sqlite.sql.v1`, `swift.storage.sqlite.table.v1`,
  `swift.storage.realm.model.v1`, and `swift.storage.analysis-gap.v1` or
  documented successor rule IDs.
- [ ] Document limitations for every rule before emitting facts.
- [ ] Define stable fact property names for CoreData, UserDefaults, Keychain,
  SQLite/GRDB/FMDatabase, Realm, and storage gaps.
- [ ] Add guardrails/tests that prevent Swift storage/data rules from emitting
  `Tier1Semantic` until a separately validated compiler/SourceKit enrichment
  rule exists.
- [ ] Confirm which Swift-specific fact types need downstream report/export
  handling and which cases should reuse `SqlTextUsed`,
  `QueryPatternDetected`, `DatabaseColumnMapping`, and `AnalysisGap`.

## Phase 2: CoreData Metadata

- [ ] Inventory checked-in `.xcdatamodel` and `.xcdatamodeld` files in sorted
  repo-relative order.
- [ ] Parse supported CoreData XML metadata without storing raw XML or snippets.
- [ ] Emit model, version/configuration, entity, attribute, relationship,
  fetched-property, and fetch-request descriptor facts where statically visible.
- [ ] Emit gaps for malformed, binary, unsupported, oversized, generated, or
  unsafe CoreData metadata.
- [ ] Optionally link CoreData descriptors to Swift `NSManagedObject` symbols
  only when symbol identity is unambiguous.
- [ ] Add fixtures and tests for parseable metadata, model-version ambiguity,
  generated-code linkage, unsupported files, deterministic fact IDs, and
  redaction.

## Phase 3: UserDefaults Keys

- [ ] Detect supported `UserDefaults` read/write/remove/register/observe
  operations with literal keys.
- [ ] Resolve bounded static key constants, enum raw-string keys, and same-file
  literal aliases without control-flow speculation.
- [ ] Emit public-safe key identities using `normalizedKey` only when allowed,
  otherwise role-separated `keyHash`, `keyLength`, and identity-status metadata.
- [ ] Emit gaps for interpolated, concatenated, config-loaded, localized,
  remote, user-input, reflection, and unsupported-wrapper keys.
- [ ] Add tests proving no UserDefaults values, unsafe keys, source snippets,
  config values, local absolute paths, or raw remotes leak into facts/reports.

## Phase 4: Keychain Access Patterns

- [ ] Detect supported Security framework calls: `SecItemAdd`,
  `SecItemCopyMatching`, `SecItemUpdate`, and `SecItemDelete`.
- [ ] Extract static query dictionary metadata for safe closed constants such as
  `kSecClass`, operation direction, accessibility, synchronizable, and return
  flags.
- [ ] Hash service/account/access-group/label/generic descriptors with
  role-separated hash inputs unless the safe-value policy explicitly allows a
  normalized descriptor.
- [ ] Detect wrapper APIs only when they pass static evidence to recognized
  Security APIs or expose a documented static wrapper shape.
- [ ] Emit gaps for dynamic query dictionaries, merged dictionaries, config
  loading, Objective-C bridging, unknown wrappers, and unsupported control flow.
- [ ] Add tests proving credentials, tokens, passwords, item data, raw query
  dictionaries, entitlement values, provisioning values, snippets, and local
  paths are never stored.

## Phase 5: SQLite, GRDB, And FMDatabase SQL/Table Surfaces

- [ ] Detect complete static SQL literals in supported SQLite, GRDB, and
  FMDatabase calls.
- [ ] Emit `SqlTextUsed` with `textHash`, `textLength`, `sqlSourceKind`,
  operation metadata where safe, spans, commit SHA, and extractor version.
- [ ] Emit SQL-shape `QueryPatternDetected` only when the shared SQL evidence
  contract can derive safe operation/table/column metadata.
- [ ] Scan selected checked-in `.sql` or migration resources and emit structural
  SQL text/shape evidence where supported.
- [ ] Add table-surface or `DatabaseColumnMapping` evidence only for documented
  literal table/schema declarations with clear limitations.
- [ ] Emit gaps for dynamic SQL, string interpolation, concatenation,
  query-builders, wrapper-generated SQL, runtime-loaded SQL, and schema
  composition.
- [ ] Add tests proving raw SQL, predicate literals, connection strings,
  database file paths, URLs, hostnames, unsafe identifiers, snippets, and local
  absolute paths do not leak.

## Phase 6: Realm Model And Persistence Surfaces

- [ ] Detect Realm model declarations and supported persisted property patterns
  using existing Swift syntax/symbol evidence.
- [ ] Emit Realm model/property facts with safe type/property identifiers or
  hashes and clear evidence tiers.
- [ ] Detect static `primaryKey()` and indexed-property declarations only when
  literal property names are visible.
- [ ] Detect supported Realm add/delete/write/object/query calls where object
  types or literal predicate identities are statically visible and public-safe.
- [ ] Emit gaps for dynamic object types, dynamic predicates, generated schema,
  migrations, Objective-C bridging, property-wrapper gaps, and runtime config.
- [ ] Add tests proving Realm file paths, encryption keys, raw predicates,
  persisted values, migration snippets, snippets, and local paths are never
  stored.

## Phase 7: Reports, SQLite, Combine, And Reducer Boundaries

- [ ] Populate the shared `facts` table and `fact_symbols` rows where symbol
  attachment is credible.
- [ ] Ensure generated `facts.ndjson` uses stable sorted properties and no raw
  snippets.
- [ ] Add `report.md` sections for Swift storage/data counts by framework,
  evidence tier, rule ID, coverage label, and gap kind.
- [ ] Ensure reports use safe descriptors or hash prefixes only and avoid
  "impacted" language without reducer output.
- [ ] Validate `tracemap combine`, `tracemap report`, and relevant path/reverse
  selectors can read Swift storage/data rows without promoting gaps or
  hash-only facts to runtime proof.
- [ ] Add tests that unknown, dynamic, unsafe, and unsupported storage gaps are
  not projected as clean terminal data surfaces.

## Phase 8: Validation

- [ ] Add public-safe fixture coverage for CoreData, UserDefaults, Keychain,
  SQLite/GRDB/FMDatabase, Realm, and unsupported/dynamic gaps.
- [ ] Run Swift build and smoke tests:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
```

- [ ] Run the storage/data fixture scan:

```bash
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-storage-data-surfaces --out /tmp/tracemap-swift-storage-data-surfaces
```

- [ ] Verify required scan artifacts exist:

```bash
test -f /tmp/tracemap-swift-storage-data-surfaces/scan-manifest.json
test -f /tmp/tracemap-swift-storage-data-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-storage-data-surfaces/index.sqlite
test -f /tmp/tracemap-swift-storage-data-surfaces/report.md
test -f /tmp/tracemap-swift-storage-data-surfaces/logs/analyzer.log
```

- [ ] Run SQLite evidence queries for rule IDs, evidence tiers, fact types,
  redaction, and gap coverage.
- [ ] Run combine/report smoke:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-storage-data-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-storage-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-storage-combined.sqlite --out /tmp/tracemap-swift-storage-report
```

- [ ] Run shared .NET validation if storage, combine, report, reducer, or shared
  SQL behavior changes:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
```

- [ ] Run safety checks:

```bash
git diff --check
./scripts/check-private-paths.sh
```

## Spec PR Review Commands

These commands are for this spec-only PR:

```bash
git diff --check
./scripts/check-private-paths.sh
```
