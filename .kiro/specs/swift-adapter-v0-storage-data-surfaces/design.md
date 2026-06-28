# Swift Adapter v0 Storage And Data Surfaces Design

Issue: [#385](https://github.com/joefeser/tracemap/issues/385)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

## Overview

This slice defines how a future Swift adapter should emit static storage and
data-surface evidence. It is intentionally limited to checked-in repository
evidence and SwiftSyntax/text/XML/plist-style parsing. It does not run app code,
inspect simulators/devices, execute Xcode builds, connect to databases, query
Keychain, open Realm files, or prove runtime persistence behavior.

Outputs stay adapter-contract compatible:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

## Intended Branch

Spec PR branch: `codex/swift-adapter-v0-storage-data-surfaces`.

Future implementation branches may use a new `codex/` branch name, but should
reference this spec and issue #385.

## Relationship To Companion Swift Specs

This issue #385 spec owns Swift storage/data surface rules. It should consume:

- scaffold/output behavior from the Swift scaffold spec;
- file inventory and metadata discovery from Swift inventory/project discovery;
- stable Swift symbol IDs and source occurrences from the symbol identity spec;
- HTTP, UI, dependency, and toolchain diagnostics from their separate slices.

Storage/data facts should not introduce a second symbol ID scheme, second scan
manifest, or divergent SQLite schema. New fact types are allowed when the rule
catalog documents them, but existing shared fact types should be reused where
they fit: `SqlTextUsed`, `QueryPatternDetected`, `DatabaseColumnMapping`, and
`AnalysisGap`.

## Non-Goals

- No Swift analyzer/runtime implementation in this spec PR.
- No runtime proof of database access, schema existence, migrations, CoreData
  store loading, UserDefaults values, Keychain item contents, Realm object
  graphs, or SQL execution.
- No required successful Xcode build.
- No simulator, device, database, Keychain, or Realm file inspection.
- No raw source snippets or raw secret/config/storage values by default.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Evidence Tiers

| Tier | Swift storage/data v0 use |
| --- | --- |
| `Tier1Semantic` | Reserved for future deterministic compiler/SourceKit evidence. This v0 storage/data design does not require or emit Tier1 evidence. |
| `Tier2Structural` | Checked-in CoreData model metadata, SQL resource files, package/project metadata that proves file membership, and deterministic data-model descriptors. |
| `Tier3SyntaxOrTextual` | SwiftSyntax or textual source evidence such as UserDefaults keys, Keychain query dictionaries, GRDB/FMDatabase calls, Realm declarations, and inline SQL literals. |
| `Tier4Unknown` | Dynamic keys/queries, unsupported wrappers, unsafe values, schema composition, generated code, malformed files, parser failures, and unable-to-prove boundaries. |

Do not upgrade evidence because a framework pattern looks likely. A static
Realm model declaration or UserDefaults key literal is useful evidence, but it
is not runtime persistence proof.

## Proposed Fact Vocabulary

The implementation should add rule catalog entries before emitting any new rule
or fact shape.

| Framework family | Rule ID | Fact type | Key properties |
| --- | --- | --- | --- |
| CoreData | `swift.storage.coredata.metadata.v1` | `SwiftCoreDataModelDeclared` | `frameworkFamily=coredata`, `modelName` or `modelNameHash`, `modelVersion` or hash, `modelHash`, `configurationName` or hash, `coverageLabel` |
| CoreData | `swift.storage.coredata.metadata.v1` | `SwiftCoreDataEntityDeclared` | `entityName` or hash, `managedClassName` or hash, `abstract`, `parentEntityName` or hash, `stableModelKey` |
| CoreData | `swift.storage.coredata.metadata.v1` | `SwiftCoreDataPropertyDeclared` | `entityName` or hash, `propertyName` or hash, `propertyKind`, `attributeType`, `relationshipDestinationName` or hash, `optional`, `toMany`, `stableModelKey` |
| UserDefaults | `swift.storage.userdefaults.key.v1` | `SwiftUserDefaultsKeyAccessed` | `frameworkFamily=userdefaults`, `operationDirection`, `apiName`, `normalizedKey` or `keyHash`, `keyIdentityStatus`, `suiteNameHash`, `targetSymbol` |
| Keychain | `swift.storage.keychain.access.v1` | `SwiftKeychainAccessPattern` | `frameworkFamily=keychain`, `operationDirection`, `apiName`, `keychainClass`, role-separated descriptor hashes, `targetSymbol` |
| SQLite/GRDB/FMDatabase | `swift.storage.sqlite.sql.v1` | `SqlTextUsed` | `frameworkFamily`, `sqlSourceKind`, `textHash`, `textLength`, `operationName`, `targetSymbol` |
| SQLite/GRDB/FMDatabase | `swift.storage.sqlite.sql.v1` | `QueryPatternDetected` | `frameworkFamily`, `sqlSourceKind`, `queryShapeHash`, `operationName`, safe table/column metadata |
| SQLite/GRDB/FMDatabase | `swift.storage.sqlite.table.v1` | `DatabaseColumnMapping` or future Swift table fact | `frameworkFamily`, `tableName` or hash, `columnName` or hash, `propertyName`, `mappingKind`, `targetSymbol` |
| Realm | `swift.storage.realm.model.v1` | `SwiftRealmModelDeclared` | `frameworkFamily=realm`, `typeName`, `realmModelKind`, `primaryKeyName` or hash, `targetSymbol` |
| Realm | `swift.storage.realm.model.v1` | `DatabaseColumnMapping` or future Realm property fact | `frameworkFamily=realm`, `typeName`, `propertyName`, `propertyKind`, `columnName` or hash, `targetSymbol` |
| Any | `swift.storage.analysis-gap.v1` | `AnalysisGap` | `gapKind=storage-data`, `gapReason`, `frameworkFamily`, `operationDirection`, `identityStatus`, safe hashes |

Names above are implementation guidance, not analyzer output in this spec PR.
If implementation chooses different names, it must update this spec or its
implementation-state note and keep issue #385 linked.

## Extraction Pipeline

The storage/data extractor should run after Swift file inventory and symbol
prepass, using deterministic sorted repo-relative paths:

1. Load scan manifest context, commit SHA, extractor versions, safe-value policy,
   and file inventory.
2. Parse supported metadata files: `.xcdatamodel`, `.xcdatamodeld`, `.sql`, and
   supported project/package hints.
3. Parse Swift files with SwiftSyntax or the existing Swift syntax layer.
4. Collect candidate source evidence for UserDefaults, Keychain, SQLite, GRDB,
   FMDatabase, and Realm.
5. Resolve bounded literal aliases and constants only where deterministic.
6. Attach evidence to known Swift symbols where existing symbol identity can do
   so without ambiguity.
7. Emit facts, SQLite rows, gaps, report summaries, and logs in deterministic
   path/span/fact-ID order.

The extractor should prefer partial output with gaps over failing the whole
scan when one framework family cannot be parsed.

## CoreData Design

Supported structural inputs:

- checked-in `.xcdatamodel` XML files;
- `.xcdatamodeld` version directories and model version markers when parseable;
- generated or hand-written Swift `NSManagedObject` subclasses only as optional
  linkage evidence.

Static model descriptors should derive stable keys from:

```text
swift-coredata/v1|
repoCommit=<commit-sha>|
path=<repo-relative-model-path>|
modelHash=<metadata-hash>|
descriptorKind=<model|entity|attribute|relationship|fetch-request|configuration>|
safeNameOrHash=<role-separated-name-or-hash>
```

The model metadata hash should hash the normalized model descriptor content or a
stable structural subset. It must not store raw XML or snippets in facts.

Limitations to document:

- model metadata is design-time checked-in evidence only;
- `.xcdatamodeld` current-version metadata does not prove runtime model choice;
- generated classes and Swift source linkage may be stale;
- CoreData mappings do not prove SQLite tables or columns;
- runtime store paths, migration state, predicates, and production data remain
  unknown.

## UserDefaults Design

Supported source patterns should include:

- `UserDefaults.standard.*(forKey:)`;
- `UserDefaults(suiteName:)` when suite identity can be safely hashed;
- `set(_:forKey:)`, typed getters, `removeObject(forKey:)`,
  `register(defaults:)`, and observation-style key uses where statically
  visible;
- bounded constants such as `static let key = "..."`, enum cases with raw
  string values, and same-file local aliases where the existing Swift adapter
  can evaluate a literal without control-flow speculation.

Do not store values passed to `set`, default values in `register(defaults:)`, or
plist/config-loaded defaults. Store key identities as:

- `normalizedKey` only when the key passes the shared safe-value policy;
- otherwise `keyHash`, `keyLength`, `keyIdentityStatus=hashed` or
  `unsafe-omitted`.

Dynamic keys, interpolation, concatenation, localization, remote/config values,
reflection, or unsupported wrappers should produce `swift.storage.analysis-gap.v1`.

## Keychain Design

Supported source patterns should include:

- `SecItemAdd`, `SecItemCopyMatching`, `SecItemUpdate`, and `SecItemDelete`;
- Swift dictionaries that use Security constants such as `kSecClass`,
  `kSecAttrService`, `kSecAttrAccount`, `kSecAttrAccessGroup`,
  `kSecAttrAccessible`, `kSecAttrSynchronizable`, and `kSecReturnData`;
- wrappers only when they pass literal values to recognized Security APIs or
  expose a documented static API shape in the same scan.

Descriptor values are sensitive by default. Use role-separated hashes such as:

```text
swift-keychain/v1|service|<raw-value>
swift-keychain/v1|account|<raw-value>
swift-keychain/v1|access-group|<raw-value>
```

Closed enum-like Security constants such as `kSecClassGenericPassword` may be
stored as safe descriptors. Credentials, tokens, passwords, item data, labels,
raw query dictionaries, entitlement values, provisioning values, and snippets
must not be stored.

## SQLite, GRDB, And FMDatabase Design

Supported direct SQL sources:

- SQLite C API wrapper calls where the SQL argument is a complete literal;
- FMDatabase/FMDB calls such as `executeQuery` or `executeUpdate` with complete
  literal SQL;
- GRDB `execute(sql:)`, `fetchAll`, `fetchOne`, or equivalent raw SQL APIs with
  complete literal SQL;
- checked-in `.sql` files or migration resources selected by file inventory.

For complete static SQL:

- emit `SqlTextUsed` with `textHash` and `textLength`;
- emit `QueryPatternDetected` only when the shared SQL shape helper safely
  derives operation/table/column metadata;
- use shared `sqlSourceKind` values such as `literal-string`, `sql-file`,
  `migration-file`, or `dynamic-boundary`;
- cap Swift source SQL at `Tier3SyntaxOrTextual`, and SQL resource files at
  `Tier2Structural` where file inventory proves structure.

Dynamic SQL and query-builder evidence should not become `SqlTextUsed`.
GRDB table declarations and FMDatabase wrapper table names may become table
surface or `DatabaseColumnMapping` evidence only when the implementation rule
documents the exact static mapping and redaction behavior.

## Realm Design

Supported source evidence:

- model classes inheriting from recognized Realm model base types;
- persisted property wrappers and collection types where SwiftSyntax exposes
  the declaration;
- static `primaryKey()` and indexed-property declarations when they return
  literal property names;
- explicit persistence APIs such as add/delete/write/query calls when object
  type or predicate evidence is statically visible.

Realm model identity should derive from existing Swift symbol IDs where
available. Property evidence should preserve property names only when safe;
predicate strings and values are hashed or omitted.

Limitations:

- no live Realm schema proof;
- no Realm file path, encryption key, migration, sync, thread, or object
  existence proof;
- no Objective-C dynamic member or property-wrapper synthesis proof beyond
  visible static syntax;
- dynamic predicates and generated schema code emit gaps.

## Gap Model

Use `swift.storage.analysis-gap.v1` with `AnalysisGap` facts for:

- `dynamic-userdefaults-key`;
- `dynamic-keychain-query`;
- `dynamic-sql`;
- `sql-schema-composition`;
- `realm-dynamic-query`;
- `realm-generated-schema`;
- `coredata-model-unsupported`;
- `coredata-model-malformed`;
- `storage-wrapper-unsupported`;
- `unsafe-storage-value-omitted`;
- `storage-symbol-attachment-ambiguous`;
- `storage-toolchain-unavailable`;
- `storage-parser-failed`.

Gaps should include `frameworkFamily`, `gapReason`, `identityStatus`,
`candidateCount` where helpful, safe hashes, and file/span evidence. Gaps should
not include raw values or snippets.

## Redaction Model

Public-safe persisted properties:

- closed framework names and operation directions;
- safe identifiers that pass the shared safe-value helper;
- repo-relative paths;
- line spans;
- hashes, lengths, kinds, counts, evidence tiers, rule IDs, and extractor
  versions.

Always omit or hash:

- raw SQL and predicate strings;
- SQL literal values;
- UserDefaults values and unsafe keys;
- Keychain credentials, services/accounts/access groups when unsafe, item data,
  query dictionaries, entitlement/provisioning values;
- config/plist values;
- Realm file paths and encryption keys;
- database paths, connection strings, URLs, hostnames, raw remotes, local
  absolute paths, source snippets, private labels, credentials, and tokens.

Use role-separated hash inputs so a value hashed as a Keychain service does not
match the same value hashed as a UserDefaults key or SQL text.

## SQLite And Report Output

The implementation should write storage/data facts to the shared `facts` table
and attach symbols through `fact_symbols` where available. It should not add
per-language SQLite tables unless a future shared schema update is documented
and tested.

`report.md` should summarize:

- counts by framework family and fact type;
- counts by rule ID and evidence tier;
- reduced coverage and gap counts;
- safe display names or hash prefixes only;
- limitations for each framework family.

Reports must avoid "impacted" language unless a reducer has produced a
separate evidence-backed result.

## Validation Strategy

Future implementation should add a fixture such as
`samples/swift-storage-data-surfaces` with public-safe examples for:

- parseable CoreData metadata;
- UserDefaults literal/static keys and dynamic-key gaps;
- Keychain Security dictionary access and dynamic-query gaps;
- SQLite/GRDB/FMDatabase literal SQL and dynamic SQL gaps;
- Realm model declarations, static property surfaces, and unsupported dynamic
  query/schema gaps.

Validation should include:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-storage-data-surfaces --out /tmp/tracemap-swift-storage-data-surfaces
sqlite3 /tmp/tracemap-swift-storage-data-surfaces/index.sqlite "select rule_id, evidence_tier, fact_type from facts order by rule_id, fact_type limit 20;"
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-storage-data-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-storage-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-storage-combined.sqlite --out /tmp/tracemap-swift-storage-report
git diff --check
./scripts/check-private-paths.sh
```

Add focused tests that inspect generated `facts.ndjson`, `index.sqlite`,
`report.md`, and logs for redaction failures.
