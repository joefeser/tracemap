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

### Legacy WCF Metadata Facts

The .NET adapter emits two WCF-specific metadata fact types for checked-in service-reference evidence:

| Fact type | Purpose | Safe matching keys |
| --- | --- | --- |
| `WcfServiceReferenceMetadataDeclared` | Inventories parseable `.svcmap`, gated `.wsdl`, `.disco`, and service-reference `.xsd` metadata. | `metadataKind`, `metadataHash`, `metadataFileName`, `serviceReferenceFolder`, `sourceFormat` |
| `WcfMetadataOperationDeclared` | Records safe WSDL `portType/operation` declarations from checked-in metadata. | `operationName`, `portTypeName`, `contractName`, `metadataHash`, `metadataFileName`, `serviceReferenceFolder` |

These facts are static design-time evidence only. They do not prove runtime reachability, deployment, service version compatibility, authorization, binding compatibility, or generated proxy freshness. Raw URLs, SOAP actions, schema locations, namespace URIs, local absolute paths, raw schemas, and snippets must be hashed or omitted.

### Legacy WebForms Event Facts

The .NET adapter emits WebForms-specific evidence for static event entry points:

| Fact type | Purpose | Safe matching keys |
| --- | --- | --- |
| `WebFormsPageDeclared` | Inventories `.aspx`, `.ascx`, and `.master` directives and safe code-behind linkage. | `pageTypeName`, `linkedCodePath`, `directiveKind`, `autoEventWireup` |
| `WebFormsControlDeclared` | Records static server controls from markup. | `pageTypeName`, `controlId`, `controlType`, `designerFactId` |
| `WebFormsEventBindingDeclared` | Records supported static event attributes and handler identifiers. | `pageTypeName`, `controlId`, `eventName`, `handlerName` |
| `WebFormsDesignerControlDeclared` | Records designer partial-class control fields as supporting evidence. | `pageTypeName`, `fieldName`, `controlType` |
| `WebFormsHandlerResolved` | Links event bindings to scoped code-behind methods. | `handlerName`, `handlerSymbol`, `sourceSymbolId`, `bindingFactId`, `resolutionKind` |
| `WebFormsEventFlowProjected` | Projects resolved handlers to direct WCF, HTTP, SQL/query, config, or dependency-surface evidence. | `flowClassification`, `terminalSurfaceKind`, `terminalSurfaceNameHash`, `supportingFactIds`, `supportingEdgeIds`, `coverage` |
| `WebFormsLogicSignalDetected` | Emits bounded static logic or UI-boilerplate signals for handlers. | `handlerName`, `signalKind`, `staticLogicSignal`, `uiBoilerplateSignal` |

These facts are static evidence only. They do not prove runtime page lifecycle execution, postbacks, event bubbling, user reachability, service reachability, SQL execution, deployment, branch feasibility, or production usage. Markup snippets, raw SQL, config values, raw URLs, local absolute paths, repository remotes, and private sample names must not appear in properties or reports.

### Legacy Data Metadata Facts

The .NET adapter emits legacy data metadata facts for checked-in DBML, EDMX, typed DataSet XSD/TableAdapter, data-provider config, and deterministic generated-code linkage.

| Fact type | Purpose | Safe matching keys |
| --- | --- | --- |
| `LegacyDataMetadataDeclared` | Inventories parseable legacy data metadata documents and generated-designer candidates. | `metadataKind`, `metadataHash`, `inventoryKind`, `path` |
| `LegacyDataEntityDeclared` | Records static conceptual/generated entity, context, DataSet, row, or TableAdapter descriptors. | `metadataKind`, `entityKind`, `entityName`, `typeName`, `entityNameHash`, `typeNameHash` |
| `LegacyDataStorageObjectDeclared` | Records static table, view, routine, DataTable, or storage entity-set descriptors. | `metadataKind`, `storageObjectKind`, `storageObjectName`, `tableName`, `storageObjectHash`, `tableNameHash` |
| `LegacyDataColumnDeclared` | Records static property/field/column descriptors from metadata. | `metadataKind`, `columnKind`, `ownerName`, `propertyName`, `fieldName`, `columnName`, hash variants |
| `LegacyDataMappingDeclared` | Records unambiguous descriptor-to-descriptor mappings such as entity-table or property-column. | `metadataKind`, `mappingKind`, `entityName`, `tableName`, `propertyName`, `columnName`, hash variants |
| `LegacyDataProviderConfigDeclared` | Records safe provider, connection-name, provider factory, and EF provider metadata without raw values. | `configKind`, `connectionName`, `providerName`, `connectionNameHash`, `providerNameHash`, `valueHash` |
| `LegacyDataGeneratedCodeLinked` | Links metadata descriptors to generated files or compiler-resolved symbols when deterministic. | `linkKind`, `symbolRole`, `typeName`, `generatedCodeFileName`, `supportingFactIds` |

These facts are static design-time metadata evidence. DBML, EDMX, typed DataSet, TableAdapter, and config descriptor facts are capped at `Tier2Structural`; generated-code links may be `Tier1Semantic` only when compiler-resolved symbol evidence is available, and that link does not upgrade descriptor facts. Raw SQL, connection strings, config values, namespace URIs, provider secrets, URLs, local paths, remotes, source snippets, and secret-looking values must be hashed or omitted. Metadata facts must not emit `DatabaseColumnMapping` without code-level mapping evidence owned by another rule.

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

Combined path queries work best when adapters attach integration facts to the containing source symbol and emit direct call/object/argument/relationship evidence with stable display symbols. In practice, endpoint-to-surface paths need:

- endpoint facts with `normalizedPathKey`, method properties, source labels, and a containing method symbol when statically visible.
- dependency surface facts such as SQL/config/package/HTTP evidence with `sourceSymbol` or role properties pointing to the containing method/type when statically visible.
- call, object creation, argument-flow, relationship, and parameter-forward rows that stay source-local unless a derived boundary rule such as endpoint matching connects sources.

Adapters should leave surfaces unlinked when containing symbols are not credible. The combined paths report will surface those as review gaps instead of inventing a path.

## Value-Origin Flow Contract

Value-origin evidence is a bounded static explanation layer over direct argument, alias, field/member, constructor, and dependency-surface facts. It is not taint analysis and must not claim runtime execution, branch feasibility, mutation semantics, collection contents, concrete DI state, reflection targets, serializer-created object identity, or dynamic dispatch targets.

Adapters that emit value-origin evidence should use these shared fact/table roles:

| Evidence | Preferred fact/table shape |
| --- | --- |
| call-site argument to callee parameter | `ArgumentPassed` facts and `argument_flows` rows |
| direct same-method local alias | `LocalAlias` facts and `local_aliases` rows |
| direct field/member alias | `FieldAlias` facts and `field_aliases` rows |
| derived parameter-to-parameter forwarding | `parameter_forward_edges` rows derived from rule-backed facts |
| runtime-sensitive stop point | `FlowBoundary`-style facts such as `CallbackBoundary`/`AsyncBoundary`, or `AnalysisGap` facts with safe `boundaryKind` metadata |

Every value-origin fact should preserve rule ID, evidence tier, file span, source/target/argument/parameter/origin role properties, scan ID, commit SHA, extractor ID, and extractor version through the normal fact contract.

Current adapter alignment:

- TypeScript semantic `ArgumentPassed` facts include shared `argument` and `parameter` role metadata when the compiler resolves those symbols; syntax fallback remains lower-tier and does not invent parameter identities.
- Java semantic `ArgumentPassed` facts include shared `parameter` role metadata and include `argument` role metadata when javac resolves the argument expression to a symbol.
- Python AST `ArgumentPassed`, `LocalAlias`, and `FieldAlias` facts include shared role metadata for syntax-visible names. Callee parameters remain `unresolvedOrdinalPlaceholder` symbols such as `arg0` because the AST adapter does not resolve callable signatures.

Derived parameter-forwarding rows may use direct parameter arguments, same-method local/field aliases, and unique constructor field origins only when every hop has deterministic evidence. The current .NET storage derivation follows same-method alias chains up to 3 alias hops. A field argument may resolve through constructor initialization only when exactly one visible constructor assignment in the analyzed containing type assigns that field from a constructor parameter. Multiple constructors, multiple visible assignments, mutation boundaries, property setter side effects, collection mutation, factory construction, DI activation, serializer construction, reflection, and dynamic dispatch must stop or downgrade value-origin evidence rather than inventing a path.

Callback and async boundary evidence is additive review context. The .NET adapter emits `CallbackBoundary` facts for syntactically visible lambdas, anonymous methods, delegate arguments on invocations and object creation, delegate creation, expression-tree lambdas, captured outer parameters/locals, and event subscriptions. It emits `AsyncBoundary` facts for `await`, `await foreach`, `await using`, task scheduling/continuation calls, thread-pool queueing calls, and iterator `yield` statements. These facts may sit beside direct `ArgumentPassed` rows from calls inside callback bodies, but they do not prove delegate invocation, event firing, expression-tree execution, runtime scheduling, execution ordering, closure lifetime, object mutation safety, async disposal, async-stream enumeration, or task completion.

Value-origin path classifications, when added to reports, must be additive notes or metadata such as `StrongStaticValuePath`, `ProbableStaticValuePath`, `NeedsReviewValuePath`, `UnknownAnalysisGap`, or `NoValuePathEvidence`. They must not replace existing canonical path classifications unless a future compatibility spec changes the public path contract.

Endpoint boundary facts should use a shared path key when possible:

- `normalizedPathKey` is path-only; HTTP method belongs in `httpMethod` and should not be prefixed into the key.
- Query strings and fragments should be removed.
- Literal path segments should be lowercased.
- Route parameters should be normalized to `{}`. Optional parameters may use `{?}` where the source framework exposes optional route segments.
- Duplicate slashes should be collapsed and a trailing slash should be removed except for `/`.
- Base paths, router prefixes, reverse proxies, and deployment roots should be included only when statically visible in the same evidence scope; otherwise emit the local route path and a reduced-coverage gap.

This key is for deterministic matching, not proof of runtime reachability.

## SQL Evidence Contract

SQL evidence is shared cross-language data dependency evidence. Adapters emit `SqlTextUsed` as hash/length evidence for complete statically visible SQL text, and emit SQL-shape `QueryPatternDetected` as separate static shape evidence when a lightweight deterministic extractor can safely derive a query shape.

Current shared minimum:

| Property | Meaning |
| --- | --- |
| `textHash` | SHA-256 over the exact raw SQL string bytes encoded as UTF-8, truncated to 32 lowercase hex chars |
| `textLength` | Length of the raw SQL string |
| `targetSymbol` | Reducer-friendly display symbol for the containing SQL boundary |

Recommended properties:

| Property | Meaning |
| --- | --- |
| `operationName` | Uppercase visible leading verb when the literal starts with an allowed verb; empty or omitted otherwise |
| `sqlSourceKind` | One of the shared source-kind values below |

Allowed leading verbs are `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `CREATE`, `ALTER`, `DROP`, `TRUNCATE`, `CALL`, `EXEC`, and `EXECUTE`. Adapters should trim leading whitespace only for `operationName`. `WITH`/CTE text should leave `operationName` empty in v1 while still allowing shape-hash-only `QueryPatternDetected` when the text is SQL-like. `CALL`/`EXEC`/`EXECUTE` should expose the operation only and must not place routine names in table fields. `SELECT` table extraction should only claim visible top-level `FROM`/`JOIN` identifiers; subquery table positions should remain without table metadata in v1.

Shared `sqlSourceKind` values:

| Value | Meaning |
| --- | --- |
| `literal-string` | SQL text literal embedded in application code |
| `sql-file` | Standalone `.sql` resource |
| `migration-file` | Migration artifact containing SQL |
| `orm-text` | ORM helper that directly wraps SQL text, such as SQLAlchemy `text(...)` |
| `dbapi-execute` | Direct DB API execute call with literal SQL |
| `dynamic-boundary` | Dynamic SQL construction was detected and concrete SQL was not claimed |

`SqlTextUsed` should remain hash/text-length evidence and should not carry guessed table or column fields. SQL-shape `QueryPatternDetected` uses `queryShapeHash`, `sqlSourceKind`, and the safest available `operationName`, `tableName`, `tableNames`, `columnNames`, or `fieldNames`. Shape-hash-only facts are valid for SQL-like `WITH`/CTE text and must omit unsupported operation/table/column properties rather than inventing them.

The v1 `queryShapeHash` reference is Python's normalized masked SQL text: strip `--` and `/* */` comments, mask single- and double-quoted contents, collapse whitespace, trim, strip trailing semicolons, SHA-256 hash, and truncate to 32 lowercase hex chars. Caveats: double-quoted identifiers may collapse like strings, `--` inside string literals follows the reference comment-stripping behavior, and static SQL-shape evidence does not prove dialect validity, runtime execution, schema existence, generated SQL equivalence, permissions, transaction behavior, or branch feasibility.

Dynamic SQL construction where complete static text is not visible should emit an `AnalysisGap` or adapter-equivalent reduced-coverage fact with `sqlSourceKind=dynamic-boundary`, not complete `SqlTextUsed`.

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
