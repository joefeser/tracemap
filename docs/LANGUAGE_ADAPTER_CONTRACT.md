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

## SQLite Contract

At minimum, `index.sqlite` must include reducer-compatible `scan_manifest` and `facts` tables.

When available, adapters should also populate:

- `symbols`
- `symbol_occurrences`
- `fact_symbols`
- derived call/object/flow tables supported by the existing schema

Schema changes should be additive. `tracemap combine` imports multiple indexes, keeps original fact IDs, namespaces source indexes, and leaves room for derived cross-index rows rather than rewriting source facts.

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
