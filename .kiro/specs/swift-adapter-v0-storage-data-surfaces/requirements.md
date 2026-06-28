# Swift Adapter v0 Storage And Data Surfaces Requirements

Issue: [#385 Swift adapter v0: storage and data surfaces](https://github.com/joefeser/tracemap/issues/385)

Parent: [#377 Swift adapter v0 runway](https://github.com/joefeser/tracemap/issues/377)

Readiness: ready-for-implementation

## Purpose

Define the Swift adapter v0 slice for deterministic static storage and data
surface evidence. This spec is planning-only: it does not implement Swift
analyzer/runtime code, does not change the rule catalog by itself, and does not
create a public product claim by itself.

The implementation must follow TraceMap's language adapter contract:

- no conclusion without evidence;
- no evidence without a rule ID;
- no rule without documented limitations;
- no scan without repo and commit SHA;
- partial Swift analysis is useful, but must be labeled partial or reduced;
- no LLM calls, embeddings, vector databases, or prompt-based classification in
  scanner or reducer behavior;
- no target app execution, simulator/device inspection, Xcode build execution,
  database connection, or runtime persistence proof.

## Public Claim Level

Spec PR claim: TraceMap has an implementation-ready design for Swift static
storage and data-surface evidence.

Future implementation claim, only after validation passes: Swift v0 can emit
deterministic static evidence for supported CoreData metadata, UserDefaults key
uses, Keychain access patterns, literal SQL/table surfaces, and Realm
model/persistence surfaces, with explicit gaps for dynamic or unsupported
storage behavior.

This slice must not claim runtime database access, table existence, schema
compatibility, migration success, CoreData stack configuration, Keychain item
existence, UserDefaults value contents, GRDB/FMDatabase execution, Realm file
creation, production data, branch feasibility, or app impact.

## Source Material

- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- `rules/rule-catalog.yml`
- `.kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/`
- `.kiro/specs/swift-adapter-v0-symbol-identity-relationships/`
- GitHub issue #377 Swift adapter v0 runway
- GitHub issue #385 Swift adapter v0 storage and data surfaces

## Ownership Boundary

This issue #385 spec owns Swift storage/data surface extraction for:

- CoreData model metadata where checked-in model files can be parsed;
- UserDefaults key access patterns where keys are statically visible;
- Keychain access patterns through Security framework dictionaries or wrapper
  APIs where service/class/account/access-group evidence is statically visible;
- SQLite, GRDB, and FMDatabase literal SQL/table surfaces;
- Realm model declarations and explicit persistence API surfaces;
- explicit gaps for dynamic keys, dynamic queries, generated schemas,
  unsupported descriptors, runtime-only stores, and schema composition.

Companion Swift v0 specs own CLI scaffolding, project discovery, declarations,
symbol identity, direct relationships, package/dependency surfaces, HTTP/API
client surfaces, UI surfaces, and toolchain diagnostics. This spec should reuse
their scan manifest, file inventory, symbol IDs, source occurrences, coverage
labels, and public-safety helpers instead of defining competing infrastructure.

## Requirements

### Requirement 1: Shared Storage/Data Evidence Contract

**User Story:** As a reviewer, I want Swift storage/data facts to look like
other TraceMap persistence evidence, with rule IDs, tiers, file spans, commit
SHA, extractor versions, and public-safe properties.

#### Acceptance Criteria

1. WHEN the Swift adapter emits any storage/data fact THEN it SHALL include a
   deterministic fact ID, scan ID, repo identity, concrete commit SHA, fact
   type, rule ID, evidence tier, repo-relative file path, one-based line span
   when available, extractor ID, extractor version, and sorted string
   properties.
2. WHEN storage/data evidence can be attached to a Swift symbol using existing
   Swift symbol identity evidence THEN the fact SHALL include reducer-friendly
   properties such as `targetSymbol`, `sourceSymbolId`, `sourceSymbolKind`,
   `sourceSymbolDisplayName`, and SHALL populate `fact_symbols` where the
   shared contract supports that role.
3. WHEN evidence is not attachable to a symbol, such as a model file or SQL
   resource file, THEN the adapter SHALL still emit source-file evidence and
   SHALL NOT invent a call path or owner symbol.
4. WHEN evidence is static metadata or project structure THEN the evidence tier
   SHALL be no higher than `Tier2Structural` unless a future deterministic
   compiler rule documents stronger proof.
5. WHEN evidence is SwiftSyntax or textual source evidence THEN the evidence
   tier SHALL be no higher than `Tier3SyntaxOrTextual`.
6. WHEN evidence is dynamic, ambiguous, unsupported, unsafe, missing, generated,
   or unable to prove/disprove THEN the adapter SHALL emit `AnalysisGap` with
   `Tier4Unknown` rather than a concrete storage/data surface.
7. WHEN a future implementation adds rule IDs THEN it SHALL add rule catalog
   entries for every emitted rule and SHALL document limitations before the
   scanner emits those facts.

### Requirement 2: CoreData Metadata Surfaces

**User Story:** As a maintainer, I want checked-in CoreData model metadata to
surface entities, attributes, relationships, and model boundaries without
claiming the runtime store or migration state.

#### Acceptance Criteria

1. WHEN a checked-in `.xcdatamodel` or `.xcdatamodeld` model file contains
   parseable CoreData XML metadata THEN the adapter SHALL emit CoreData model,
   entity, attribute, relationship, fetched-property, fetch-request, and
   configuration facts where those descriptors are statically visible.
2. WHEN model version metadata is visible through `.xcdatamodeld` contents or
   model version markers THEN facts SHALL include safe version/configuration
   identity metadata or hashes without claiming which model version is loaded at
   runtime.
3. WHEN NSManagedObject subclass evidence, `@NSManaged` properties, or
   generated-code linkage is visible in Swift source THEN implementation MAY
   link model descriptors to Swift symbols only when deterministic identity
   evidence is unambiguous.
4. WHEN CoreData metadata is binary, malformed, generated, too large, uses an
   unsupported model format, or cannot be safely parsed THEN the adapter SHALL
   emit an `AnalysisGap`.
5. CoreData facts SHALL NOT prove `NSPersistentContainer` initialization,
   persistent store paths, migration success, SQLite table names, runtime fetch
   predicates, production data, or app behavior.

### Requirement 3: UserDefaults Key Surfaces

**User Story:** As a reviewer, I want static UserDefaults key usage evidence
without exposing sensitive keys or claiming runtime reads/writes.

#### Acceptance Criteria

1. WHEN Swift source calls supported UserDefaults APIs with a statically visible
   key literal, enum/static constant initialized from a literal, or bounded
   local alias initialized from a literal THEN the adapter SHALL emit a
   UserDefaults key surface fact.
2. WHEN the operation kind is visible THEN facts SHALL distinguish read, write,
   remove, registration-defaults, observation, or unknown operation direction
   using closed values.
3. WHEN a key value is public-safe under the shared safe-value policy THEN the
   fact MAY include `normalizedKey`; otherwise it SHALL include `keyHash`,
   `keyLength`, and `keyIdentityStatus=hashed` or `unsafe-omitted`.
4. WHEN keys are interpolated, concatenated, derived from runtime config,
   localized resources, environment, remote payloads, user input, reflection, or
   unsupported wrappers THEN the adapter SHALL emit a dynamic-key gap.
5. UserDefaults facts SHALL NOT store default values, raw plist values,
   credential-looking keys, source snippets, or runtime value contents.
6. UserDefaults facts SHALL NOT prove a key exists at runtime, a value is read,
   a write persists, suite selection, synchronization behavior, or production
   usage.

### Requirement 4: Keychain Access Pattern Surfaces

**User Story:** As a security reviewer, I want static Keychain access-pattern
evidence without leaking service/account/access-group secrets or claiming stored
credential presence.

#### Acceptance Criteria

1. WHEN Swift source uses Security framework APIs such as `SecItemAdd`,
   `SecItemCopyMatching`, `SecItemUpdate`, or `SecItemDelete` with a
   statically visible query dictionary THEN the adapter SHALL emit a Keychain
   access surface fact.
2. WHEN wrapper methods or typed helper APIs carry statically visible Keychain
   service, class, account, access group, synchronizable, accessibility, or
   operation evidence THEN implementation MAY emit a Keychain access surface
   fact capped at the evidence tier supported by the wrapper evidence.
3. WHEN `kSecClass` or operation direction is statically visible THEN facts
   SHALL include safe closed metadata such as `keychainClass` and
   `operationDirection`.
4. WHEN service, account, label, generic attribute, access group, or other
   descriptor values are visible THEN facts SHALL store only safe normalized
   identifiers when allowed by the safe-value policy; otherwise they SHALL store
   role-separated hashes.
5. WHEN query dictionaries are dynamically composed, merged, passed through
   unknown wrappers, loaded from config, bridged through Objective-C, or spread
   across unsupported control flow THEN the adapter SHALL emit an `AnalysisGap`.
6. Keychain facts SHALL NOT store credentials, tokens, passwords, raw query
   dictionaries, entitlement values, provisioning profile values, local paths,
   snippets, or Keychain item contents.
7. Keychain facts SHALL NOT prove a Keychain item exists, access succeeds,
   access control prompts, biometric policy, entitlement validity, device state,
   sync behavior, or production usage.

### Requirement 5: SQLite, GRDB, And FMDatabase Literal SQL/Table Surfaces

**User Story:** As a data reviewer, I want Swift SQL evidence to reuse shared
SQL hash and shape contracts without exposing SQL text or inventing runtime DB
behavior.

#### Acceptance Criteria

1. WHEN direct SQL text is found in supported Swift SQLite, GRDB, or FMDatabase
   calls with complete statically visible SQL text THEN the adapter SHALL emit
   `SqlTextUsed` with `textHash`, `textLength`, `sqlSourceKind`, and containing
   symbol metadata where available.
2. WHEN the direct SQL text has a safely derivable operation/table/column shape
   under the shared SQL evidence contract THEN the adapter SHALL also emit
   `QueryPatternDetected` with `queryShapeHash`, `sqlSourceKind`, and safe
   derived metadata.
3. WHEN SQL appears in checked-in `.sql` resources, migrations, or bundled
   schema files selected by the Swift adapter inventory THEN the adapter SHALL
   emit SQL text/shape evidence using repo-relative file paths and structural or
   textual evidence tiers according to the file evidence.
4. WHEN GRDB table-record or database-table-name declarations expose literal
   table names without direct SQL text THEN implementation MAY emit a
   storage/table surface fact or `DatabaseColumnMapping` only when the rule
   documents the exact static mapping evidence and limitations.
5. WHEN SQL is interpolated, concatenated, built by query builders, hidden
   behind wrappers, generated by migrations, loaded from runtime config, or
   assembled from table/column constants without complete text THEN the adapter
   SHALL emit a dynamic-query or schema-composition gap rather than complete
   `SqlTextUsed`.
6. SQL facts SHALL NOT store raw SQL, literal predicate values, source snippets,
   connection strings, file-system database paths, hostnames, URLs, local
   absolute paths, or unsafe identifiers.
7. SQL facts SHALL NOT prove query execution, database existence, schema
   existence, dialect validity, transaction behavior, permissions, migrations,
   branch feasibility, or production data access.

### Requirement 6: Realm Model And Persistence Surfaces

**User Story:** As a maintainer, I want static Realm model and persistence API
evidence without claiming the live Realm schema or object graph.

#### Acceptance Criteria

1. WHEN Swift source declares a Realm model using statically recognizable
   superclass/protocol/property-wrapper patterns such as `Object`, `EmbeddedObject`,
   `RealmSwiftObject`, `Persisted`, `List`, or `MutableSet` THEN the adapter
   SHALL emit Realm model/property surface evidence where identity is
   unambiguous.
2. WHEN Realm object type names, persisted property names, primary-key method
   names, indexed-property method names, or static table/class-name overrides
   are visible THEN facts SHALL include safe normalized identifiers or hashes.
3. WHEN source calls supported Realm APIs such as `realm.add`, `realm.delete`,
   `realm.objects`, `realm.object`, `realm.write`, or query/filter APIs with
   static object types or literal predicate strings THEN the adapter MAY emit
   persistence/query surface evidence capped at the evidence tier supported by
   the static evidence.
4. WHEN Realm schema depends on property wrappers the adapter cannot parse,
   dynamic object types, string predicates, generated code, migrations,
   Objective-C bridging, or runtime configuration THEN the adapter SHALL emit
   explicit gaps.
5. Realm facts SHALL NOT store raw predicate strings, persisted values, Realm
   file paths, encryption keys, migration snippets, source snippets, or local
   absolute paths.
6. Realm facts SHALL NOT prove runtime schema, migration success, live object
   existence, query execution, file encryption, thread confinement, sync state,
   or production usage.

### Requirement 7: Dynamic And Unsupported Gap Coverage

**User Story:** As a reviewer, I want unsupported storage behavior to be visible
as coverage gaps instead of disappearing or being reported as clean absence.

#### Acceptance Criteria

1. WHEN the adapter observes dynamic keys, dynamic SQL, dynamic Realm queries,
   dynamic CoreData model names, schema composition, generated schema code,
   unsupported wrappers, unsupported file formats, unreadable files, toolchain
   absence, or parser failure THEN it SHALL emit `AnalysisGap` facts with
   stable `gapKind`, `gapReason`, `frameworkFamily`, `operationDirection` where
   visible, `identityStatus`, and safe hash metadata.
2. WHEN a supported framework is imported or referenced but no static surface is
   extractable THEN the adapter SHALL emit a reduced-coverage gap instead of a
   clean no-storage conclusion.
3. WHEN an unsafe value is omitted or hashed for public safety THEN the adapter
   SHALL preserve enough role metadata to explain what was omitted without
   exposing the raw value.
4. WHEN scan coverage is reduced because storage/data extraction falls back to
   syntax/textual evidence or skips unsupported files THEN `scan-manifest.json`
   and `report.md` SHALL make that reduced coverage visible.

### Requirement 8: Redaction And Public Safety

**User Story:** As a user sharing TraceMap artifacts, I want storage/data output
to avoid leaking secrets, private paths, local config, raw SQL, or private
schema details by default.

#### Acceptance Criteria

1. Storage/data facts, reports, logs, and SQLite rows SHALL NOT store source
   snippets by default.
2. Raw SQL, SQL predicate literals, UserDefaults values, Keychain values,
   Keychain service/account/access-group strings when unsafe, config values,
   plist values, Realm predicate strings, database file paths, URLs, hostnames,
   connection strings, credentials, tokens, raw remotes, local absolute paths,
   provisioning profile values, entitlements values, and secret-like names
   SHALL be omitted or hashed.
3. Repo paths SHALL be repo-relative where emitted; local scan roots and git
   roots SHALL use existing path-hash metadata.
4. Hashes SHALL be role-separated so the same raw value in different roles such
   as `userdefaults-key`, `keychain-service`, `sql-text`, and `realm-predicate`
   does not produce a misleading shared identity.
5. Reports SHALL render safe descriptors, hash prefixes, counts, evidence
   tiers, rule IDs, coverage labels, and limitations; they SHALL NOT render raw
   values that were omitted or hashed in facts.

### Requirement 9: Tests And Validation

**User Story:** As an implementer, I want fixtures and validation tasks that
prove supported evidence, unsupported gaps, deterministic IDs, and redaction.

#### Acceptance Criteria

1. Tests SHALL cover CoreData metadata parsing, UserDefaults static keys,
   Keychain static query dictionaries, SQLite/GRDB/FMDatabase literal SQL,
   Realm model/persistence evidence, and at least one reduced/unsupported path
   for each framework family.
2. Tests SHALL assert rule IDs, evidence tiers, commit SHA, extractor ID,
   extractor version, file spans, stable fact IDs, sorted properties, and
   SQLite rows.
3. Tests SHALL assert raw SQL, snippets, config values, Keychain values,
   UserDefaults values, unsafe keys, local absolute paths, raw remotes, URLs,
   hostnames, credentials, and tokens do not appear in facts, reports, logs, or
   generated validation summaries.
4. Validation SHALL run Swift package tests or smoke tests once implementation
   exists, a Swift fixture scan, SQLite fact queries, combine/report/path smoke
   for supported terminal surfaces where applicable, `git diff --check`, and
   `./scripts/check-private-paths.sh`.

## Proposed Rule IDs

Future implementation should add rule catalog entries for these planned rule
IDs or document any renamed successor before emitting facts:

| Rule ID | Primary facts | Tier | Limitation summary |
| --- | --- | --- | --- |
| `swift.storage.coredata.metadata.v1` | `SwiftCoreDataModelDeclared`, `SwiftCoreDataEntityDeclared`, `SwiftCoreDataPropertyDeclared`, `AnalysisGap` | `Tier2Structural` | Checked-in model metadata only; no runtime store, migration, or table proof. |
| `swift.storage.userdefaults.key.v1` | `SwiftUserDefaultsKeyAccessed`, `AnalysisGap` | `Tier3SyntaxOrTextual` | Static key/API evidence only; no runtime read/write/value proof. |
| `swift.storage.keychain.access.v1` | `SwiftKeychainAccessPattern`, `AnalysisGap` | `Tier3SyntaxOrTextual` | Static query/access pattern only; no credential, entitlement, prompt, or item-existence proof. |
| `swift.storage.sqlite.sql.v1` | `SqlTextUsed`, `QueryPatternDetected`, `AnalysisGap` | `Tier2Structural` or `Tier3SyntaxOrTextual` | Hash/shape evidence only; no SQL execution or schema proof. |
| `swift.storage.sqlite.table.v1` | `DatabaseColumnMapping` or Swift-specific table surface facts, `AnalysisGap` | `Tier2Structural` or `Tier3SyntaxOrTextual` | Only literal table/schema declarations; no generated schema or migration proof. |
| `swift.storage.realm.model.v1` | `SwiftRealmModelDeclared`, `DatabaseColumnMapping` where justified, `AnalysisGap` | `Tier3SyntaxOrTextual` | Static Realm model/property evidence only; no live Realm schema or object proof. |
| `swift.storage.analysis-gap.v1` | `AnalysisGap` | `Tier4Unknown` | Dynamic, unsafe, unsupported, ambiguous, generated, or reduced-coverage storage boundaries. |

## Non-Goals

- Implementing analyzer code in this spec PR.
- Running app code, Xcode builds, simulators, devices, databases, Keychain, or
  Realm.
- Runtime data-flow proof, database proof, data existence proof, production
  usage proof, or change-impact classification.
- Full SQL parsing, full CoreData migration interpretation, Realm migration
  execution, GRDB query-builder semantics, or FMDatabase wrapper semantics.
- Public copy that says Swift storage/data behavior is impacted without a
  reducer result and supporting evidence.
