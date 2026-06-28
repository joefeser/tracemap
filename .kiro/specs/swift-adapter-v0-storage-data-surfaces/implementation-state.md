# Swift Adapter v0 Storage And Data Surfaces Implementation State

Status: implemented

Issue: [#385](https://github.com/joefeser/tracemap/issues/385)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Implementation branch: `codex/implement-swift-storage-data-surfaces`

## Implemented Scope

This slice adds deterministic static Swift storage/data surface evidence for:

- checked-in CoreData model/entity/property metadata;
- UserDefaults literal and same-file static key references;
- Security framework Keychain access patterns with role-hashed descriptors;
- SQLite/GRDB/FMDatabase complete static SQL literals and checked-in `.sql`
  resources;
- Realm model declarations and simple persisted property metadata;
- explicit storage/data analysis gaps for dynamic keys, dynamic SQL, dynamic
  Keychain query shapes, malformed metadata, and dynamic Realm query evidence.

The scanner emits `swift.storage.*` rule IDs, stable fact properties, evidence
tiers, spans, commit SHA, extractor version, and reduced coverage labels when
storage gaps are present. Reports summarize Swift storage/data counts by
framework, fact type, rule ID, and gap kind without raw snippets or runtime
proof language.

## Public Claim Level

Implemented claim level: static evidence only.

Safe language:

- deterministic static Swift storage/data evidence;
- checked-in CoreData model metadata;
- static UserDefaults key evidence;
- static Keychain access-pattern evidence;
- hashed SQL text and derived SQL shape evidence;
- static Realm model/property evidence;
- reduced coverage;
- explicit storage/data gap.

Unsafe language:

- runtime data flow;
- database proved;
- SQL executed;
- schema exists;
- migration succeeded;
- Keychain item exists;
- UserDefaults value exists;
- Realm live schema proved;
- production data touched;
- app behavior impacted;
- AI impact analysis.

## Source Material Paths

- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- `rules/rule-catalog.yml`
- `.kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/`
- `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/`
- GitHub issue #377 Swift adapter v0 runway
- GitHub issue #385 Swift adapter v0 storage and data surfaces

## Files And Fixtures

- `src/swift/Sources/TraceMapSwift/TraceMapSwift.swift`
- `src/swift/Sources/tracemap-swift-smoke-tests/main.swift`
- `samples/swift-storage-data-surfaces/`
- `rules/rule-catalog.yml`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`

## Rule IDs

- `swift.storage.coredata.metadata.v1`
- `swift.storage.userdefaults.key.v1`
- `swift.storage.keychain.access.v1`
- `swift.storage.sqlite.sql.v1`
- `swift.storage.sqlite.table.v1`
- `swift.storage.realm.model.v1`
- `swift.storage.analysis-gap.v1`

## Validation

Latest local validation passed:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-storage-data-surfaces --out /tmp/tracemap-swift-storage-data-surfaces
test -f /tmp/tracemap-swift-storage-data-surfaces/scan-manifest.json
test -f /tmp/tracemap-swift-storage-data-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-storage-data-surfaces/index.sqlite
test -f /tmp/tracemap-swift-storage-data-surfaces/report.md
test -f /tmp/tracemap-swift-storage-data-surfaces/logs/analyzer.log
/usr/bin/sqlite3 /tmp/tracemap-swift-storage-data-surfaces/index.sqlite "select rule_id, evidence_tier, fact_type from facts where rule_id like 'swift.storage.%' order by rule_id, fact_type limit 20;"
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-storage-data-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-storage-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-storage-combined.sqlite --out /tmp/tracemap-swift-storage-report
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Observed full .NET test result: `697 passed, 0 failed`.

## Deferred Follow-Ups

- SourceKit/compiler semantic enrichment for storage symbol attachment.
- Full CoreData `.xcdatamodel/contents` and version-selection semantics.
- Advanced wrapper/data-flow resolution for UserDefaults, Keychain, GRDB,
  FMDatabase, SQLite, and Realm.
- Runtime instrumentation, simulator/device checks, database connections,
  Keychain access, Realm file inspection, or production-data proof.
