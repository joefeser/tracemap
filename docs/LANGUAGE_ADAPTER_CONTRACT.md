# Language Adapter Contract

TraceMap language scanners are independent adapters that emit the same evidence contract. New language work should satisfy this contract before adding language-specific depth.

## Required Artifacts

Every successful scanner command must write:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

Scans must require a concrete Git commit SHA. If the scanner cannot determine the commit SHA, it should fail before writing a successful scan artifact set.

## Manifest Contract

The manifest must include:

- scan ID
- repo name
- remote URL when available
- branch when available
- commit SHA
- scanner version
- scan timestamp
- analysis level
- build status
- project/workspace identifiers
- known gaps
- scan root metadata when available: `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash`

`Level1SemanticAnalysis` and `Succeeded` mean the scanner has full semantic evidence for the selected scan scope. Compiler errors, unresolved dependencies, project-load failures, skipped files, or syntax-only fallback must produce reduced coverage.

`scanId` must be deterministic. Adapters should derive it from stable repository identity, commit SHA, and a deterministic adapter-specific signature such as sorted file inventory or normalized scan options. The inputs must be documented by the adapter and must not contain a timestamp, UUID, process ID, or output path. Fact IDs may include `scanId`, so unstable scan IDs cause every fact ID to churn between identical runs.

## Fact Contract

Every fact must include:

- deterministic fact ID
- scan ID
- repo and commit SHA
- fact type
- rule ID
- evidence tier
- source/target symbols when available
- contract element when applicable
- file path and line span
- extractor ID and extractor version
- sorted string properties

Facts must not store raw source snippets by default. Store hashes, lengths, kinds, stable names, and line spans instead.

## Evidence Tiers

Use the existing tiers consistently:

- `Tier1Semantic`: compiler-resolved symbol evidence.
- `Tier2Structural`: framework/package/project structure evidence.
- `Tier3SyntaxOrTextual`: syntax-only or textual evidence.
- `Tier4Unknown`: explicit analysis gap or unable-to-prove state.

Do not upgrade evidence tiers because a pattern is likely. If an implementation depends on naming or syntax shape only, label it as syntax/textual or structural and document the limitation in the rule catalog.

## Reducer Compatibility

Facts intended to participate in existing contract reduction should reuse existing fact types and matching keys where possible:

| Purpose | Preferred fact types and keys |
| --- | --- |
| Type usage/declaration | `TypeDeclared`, `typeName`, `namespace`, `targetSymbol` |
| Property/member usage | `PropertyAccessed`, `propertyName`, `memberName`, `containingType`, `targetSymbol` |
| Method/function usage | `MethodInvoked`, `methodName`, `containingType`, `targetSymbol` |
| Config usage | `ConfigKeyDeclared`, `keyPath`, `name`, `targetSymbol` |
| HTTP client/server boundary | `HttpCallDetected`, `HttpRouteBinding`, `methodName`, `normalizedPathKey` |
| Persistence/query evidence | `QueryPatternDetected`, `DatabaseColumnMapping`, `SqlTextUsed`, field/hash properties |
| Serialization/schema evidence | `SerializationLogic`, `SerializerContractMember` |

Language-specific fact types can be added, but they need rule catalog entries and documented reducer/report behavior.

## Symbol Identity

Each adapter should emit stable symbol IDs for its own ecosystem and include a language discriminator in `symbols`.

Examples:

- .NET: assembly name/version plus fully qualified symbol signature.
- TypeScript: package name/version or `HEAD`, module path, descriptor, overload/local span.
- Future JVM: Gradle/Maven module, group/artifact/version when available, package/class/member descriptor, JVM signature.

Raw symbol equality across languages must not imply identity. Cross-language relationships should be derived from explicit boundary facts, package dependencies, endpoints, schemas, or future combine rules.

Reducer-facing display symbols and stable symbol IDs serve different jobs. Properties such as `sourceSymbol`, `targetSymbol`, `containingType`, `typeName`, and `methodName` should be human-readable display values that reducer matching can compare with contract deltas. Stable IDs should be stored separately in role-specific properties and SQLite symbol tables.

When a fact participates in symbol tables or relationship/flow tables, adapters should emit role properties using the current shared storage convention:

| Role property | Meaning |
| --- | --- |
| `{role}SymbolId` | Stable language-local symbol ID |
| `{role}SymbolLanguage` | Language discriminator, such as `csharp`, `typescript`, `jvm`, or `python` |
| `{role}SymbolKind` | Symbol kind, such as `module`, `class`, `method`, `function`, `field`, or `parameter` |
| `{role}SymbolDisplayName` | Human-readable symbol name |

Supported shared roles are `source`, `target`, `argument`, `parameter`, `origin`, and `constructor`. The current .NET SQLite writer recognizes identities gated by `sourceSymbolId`, `targetSymbolId`, `argumentSymbolId`, `parameterSymbolId`, `originSymbolId`, and `constructorSymbolId`.

Some existing adapters have legacy aliases, such as TypeScript's `{role}DisplayName` and `{role}Symbol` properties. New adapters should emit the shared convention above and may add legacy aliases only when needed for a specific existing reader.

## Relationships and Flow

Adapters should emit direct relationships only when evidence supports them:

- inheritance/interface/override relationships
- call edges
- object creation
- argument-to-parameter facts
- local aliases
- package/dependency references
- endpoint and integration boundary facts

Avoid inferring runtime dependency injection bindings, dynamic dispatch targets, reflection targets, serializer mappings, branch feasibility, or collection contents without explicit rule-backed evidence.

Endpoint boundary facts should use a shared path key when possible:

- `normalizedPathKey` is path-only; HTTP method belongs in `httpMethod` and should not be prefixed into the key.
- Query strings and fragments should be removed.
- Literal path segments should be lowercased.
- Route parameters should be normalized to `{}`. Optional parameters may use `{?}` where the source framework exposes optional route segments.
- Duplicate slashes should be collapsed and a trailing slash should be removed except for `/`.
- Base paths, router prefixes, reverse proxies, and deployment roots should be included only when statically visible in the same evidence scope; otherwise emit the local route path and a reduced-coverage gap.

This key is for deterministic matching, not proof of runtime reachability.

## SQL Evidence Contract

SQL evidence is shared cross-language data dependency evidence. Current adapters all support the minimum `textHash`/`textLength` shape for SQL text evidence where SQL text is detected. Additional properties such as `operationName` and `sqlSourceKind` are additive convergence targets until all adapters backfill them.

Current shared minimum:

| Property | Meaning |
| --- | --- |
| `textHash` | SHA-256 over the exact raw SQL string bytes encoded as UTF-8, truncated to 32 lowercase hex chars |
| `textLength` | Length of the raw SQL string |
| `targetSymbol` | Reducer-friendly display symbol for the containing SQL boundary |

Recommended additive properties:

| Property | Meaning |
| --- | --- |
| `operationName` | Uppercase visible leading verb when the literal starts with an allowed verb; empty or omitted otherwise |
| `sqlSourceKind` | One of the shared source-kind values below |

Allowed leading verbs are `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `CREATE`, `ALTER`, `DROP`, `TRUNCATE`, `CALL`, `EXEC`, and `EXECUTE`. Adapters should trim leading whitespace only and should not implement comment stripping or dialect parsing for this field. `WITH`/CTE text and non-verb starts should leave `operationName` empty until a shared SQL parser exists.

Shared `sqlSourceKind` values:

| Value | Meaning |
| --- | --- |
| `literal-string` | SQL text literal embedded in application code |
| `sql-file` | Standalone `.sql` resource |
| `migration-file` | Migration artifact containing SQL |
| `orm-text` | ORM helper that directly wraps SQL text, such as SQLAlchemy `text(...)` |
| `dbapi-execute` | Direct DB API execute call with literal SQL |
| `dynamic-boundary` | Dynamic SQL construction was detected and concrete SQL was not claimed |

`SqlTextUsed` should remain hash/text-length evidence and should not carry guessed table or column fields. Adapters may emit `QueryPatternDetected` with best-effort `tableName`, `tableNames`, `columnNames`, `fieldNames`, and `queryShapeHash` when a lightweight deterministic extractor finds a simple static SQL shape. Those fields are structural review-routing evidence, not proof of dialect validity, generated SQL, runtime execution, or database schema existence. Adapters should omit `QueryPatternDetected` when no operation/table/column shape is statically visible.

Cross-language SQL matching must not require `operationName` or `sqlSourceKind` until the existing adapters emit those properties consistently.

## SQLite Contract

At minimum, `index.sqlite` must include reducer-compatible `scan_manifest` and `facts` tables.

When available, adapters should also populate:

- `symbols`
- `symbol_occurrences`
- `fact_symbols`
- derived call/object/flow tables supported by the existing schema

Adapters must use the shared SQLite DDL used by the .NET storage layer, including snake_case table and column names. JSON fact properties remain camelCase. Schema changes should be additive. `tracemap combine` imports multiple indexes, keeps original fact IDs, namespaces source indexes, and leaves room for derived cross-index rows rather than rewriting source facts.

Do not hand-maintain divergent per-language schemas. If a new adapter needs a table or column, add it to the shared contract and update the existing schema tests.

## Rule Catalog

Every new rule ID must be added to `rules/rule-catalog.yml` with:

- emitted fact type or derived report type
- evidence tier expectations
- required properties
- limitations
- known false positive/false negative cases

No rule catalog entry means the scanner should not emit that fact.

## JVM Direction

Future Java/Kotlin work should live under `src/jvm` unless implementation discovery shows a strong reason to split Java and Kotlin adapters. The JVM adapter can share package/module discovery, bytecode/JVM signature concepts, Gradle/Maven metadata, and classpath handling across both languages.

`tracemap combine` exists before JVM work so multi-index dependency questions have a stable place to live.
