# Swift Adapter v0 Storage And Data Surfaces Implementation State

Status: ready-for-implementation

Issue: [#385](https://github.com/joefeser/tracemap/issues/385)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Current branch: `codex/swift-adapter-v0-storage-data-surfaces`

## Current Scope

This PR is spec-only. It creates Kiro requirements, design, tasks, and
implementation-state notes for the Swift adapter v0 storage/data surface slice.
It does not implement analyzer code, change scanner output, update generated
artifacts, or alter the rule catalog.

## Public Claim Level

Current PR: implementation-ready planning for deterministic static Swift
storage/data surface evidence.

Future implementation may claim static evidence for supported CoreData
metadata, UserDefaults keys, Keychain access patterns, SQLite/GRDB/FMDatabase
literal SQL/table surfaces, and Realm model/persistence surfaces only after
rule catalog entries, tests, fixtures, and validation land.

Unsafe claims remain out of scope: runtime data-flow proof, database proof,
query execution, schema existence, migration success, Keychain item existence,
UserDefaults value contents, Realm live schema, production data, or app impact.

## Source Material Paths

- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- `rules/rule-catalog.yml`
- `.kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/`
- `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/`
- GitHub issue #377 Swift adapter v0 runway
- GitHub issue #385 Swift adapter v0 storage and data surfaces

## Scope Decisions

- Keep this as a storage/data-surface slice only.
- Reuse the shared SQL evidence contract for `SqlTextUsed` and
  `QueryPatternDetected`.
- Reuse `DatabaseColumnMapping` only where future implementation documents
  concrete static mapping evidence; otherwise prefer Swift-specific descriptor
  facts or gaps.
- Cap CoreData checked-in metadata at `Tier2Structural`.
- Cap SwiftSyntax/textual UserDefaults, Keychain, SQLite call, GRDB, FMDatabase,
  and Realm source evidence at `Tier3SyntaxOrTextual`.
- Reserve `Tier1Semantic` for a future deterministic compiler/SourceKit
  enrichment rule; this spec does not need Tier1 evidence.
- Treat dynamic keys, dynamic SQL, generated schemas, runtime config, wrappers,
  Objective-C bridging, migrations, and unsafe values as explicit gaps.
- Store repo-relative paths, line spans, hashes, lengths, kinds, counts, rule
  IDs, evidence tiers, extractor versions, and coverage labels instead of raw
  snippets or unsafe values.

## Planned Rule IDs

- `swift.storage.coredata.metadata.v1`
- `swift.storage.userdefaults.key.v1`
- `swift.storage.keychain.access.v1`
- `swift.storage.sqlite.sql.v1`
- `swift.storage.sqlite.table.v1`
- `swift.storage.realm.model.v1`
- `swift.storage.analysis-gap.v1`

Future implementation may rename these only if it updates the spec/state notes
and keeps issue #385 linkage clear.

## Validation Commands For This Spec PR

```bash
git diff --check
./scripts/check-private-paths.sh
```

Latest local validation:

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.

## Future Implementation Validation

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-storage-data-surfaces --out /tmp/tracemap-swift-storage-data-surfaces
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-storage-data-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-storage-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-storage-combined.sqlite --out /tmp/tracemap-swift-storage-report
git diff --check
./scripts/check-private-paths.sh
```

## Safe / No-Overclaim Boundaries

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

## Follow-Up Items

- Implement the future unchecked tasks in `tasks.md`.
- Add rule catalog entries before emitting storage/data facts.
- Add public-safe Swift storage/data fixtures.
- Update `docs/VALIDATION.md` after implementation adds runnable storage/data
  fixture commands.
- Keep public copy bounded to static evidence, rule IDs, evidence tiers,
  coverage labels, limitations, and generated artifacts.
