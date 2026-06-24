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

### Event And Message Surface Facts

Adapters that emit broker-backed or external event/message evidence must use the
shared fact vocabulary below. In-process mediator, notification, EventEmitter,
or callback patterns are not message dependency surfaces by default.

| Fact type | Purpose | Safe metadata |
| --- | --- | --- |
| `MessagePublisherSurface` | Static publish, send, produce, enqueue, or event publish evidence. | `frameworkFamily`, `frameworkFeature`, `operationDirection=publish`, `operationKind`, `surfaceKind`, `destinationIdentityStatus`, `normalizedDestinationKey`, `destinationHash`, `eventTypeIdentity`, `publisherSymbolId`, `safeMetadataHash`, `stableMessageSurfaceKey` |
| `MessageConsumerSurface` | Static consume, subscribe, receive, handler, listener, or trigger evidence. | `frameworkFamily`, `frameworkFeature`, `operationDirection=consume`, `operationKind`, `surfaceKind`, `destinationIdentityStatus`, `normalizedDestinationKey`, `destinationHash`, `eventTypeIdentity`, `handlerSymbolId`, `safeMetadataHash`, `stableMessageSurfaceKey` |
| `MessageBindingDeclared` | Static framework binding, declaration, annotation, attribute, decorator, config, or manifest evidence that is not itself a publisher or consumer claim. | `frameworkFamily`, `frameworkFeature`, `operationDirection=bind|declare`, `operationKind`, `surfaceKind`, `destinationIdentityStatus`, `normalizedDestinationKey`, `destinationHash`, `safeMetadataHash`, `stableMessageSurfaceKey` |
| `AnalysisGap` | Dynamic, unsupported, ambiguous, unsafe, missing, wildcard, or reduced-coverage message evidence. | `gapKind=message-surface`, `gapReason`, `frameworkFamily`, `frameworkFeature`, `operationDirection`, `operationKind`, `surfaceKind`, `destinationIdentityStatus`, `destinationHash`, `safeMetadataHash` |

Shared message `surfaceKind` values are `message-queue`, `message-topic`,
`message-subscription`, `message-exchange`, `message-stream`, `message-event`,
`message-channel`, and `message-unknown`. `message-publish-consume` is an async
candidate edge kind, not a surface kind, and report/query validators must reject
it where a surface kind is expected. In this slice, `safeMetadataHash` is the
stable-key metadata hash and intentionally excludes handler/publisher symbol
identity for static destination rows.

`destinationIdentityStatus` is one of `static`, `hashed`, `dynamic`, `unknown`,
`ambiguous`, or `unsafe-omitted`. Static identities may render
`normalizedDestinationKey` only when the shared safe-value helper allows it.
Unsafe raw destinations, queue/topic names with secret-like or environment-like
content, URLs, hostnames, connection strings, config values, local absolute
paths, raw remotes, credentials, and source snippets must be omitted or replaced
with a full 64-character lowercase SHA-256 `destinationHash`. Reports may show a
short hash prefix for readability, but persisted properties and stable-key input
must use the full hash.

Message stable keys are derived from `message-surface/v1`, source label,
language, surface kind, framework family, operation direction, operation kind,
destination identity status, the safe destination key or full destination hash,
an occurrence discriminator only when needed, and a safe metadata hash. Handler
and publisher symbol IDs are evidence metadata and must not be used as stable
key material for static destination rows; a handler rename must not churn a
static destination surface key when destination, direction, and operation are
unchanged.

Adapters should attach `handlerSymbolId` and `publisherSymbolId` as properties
only in this slice. They are not portable `fact_symbols` role rows until a future
contract update defines cross-adapter symbol semantics for those roles.

These facts are deterministic static evidence only. They do not prove broker
connection, runtime delivery, live subscriptions, topology, production traffic,
authorization, ordering, retries, retention, dead-letter behavior, deployment
reachability, schema compatibility, payload compatibility, deserialization
success, or runtime impact.

### UI Field And Property Facts

Adapters that emit UI field, form-control, template binding, Razor helper, or
view/model property evidence should use the shared safe metadata below so
`tracemap property-flow` can select and compose roots consistently.

| Fact type | Purpose | Safe matching keys |
| --- | --- | --- |
| `UiTemplateBinding` | Angular or template binding evidence such as interpolation, property binding, and two-way binding. | `uiFramework`, `bindingKind`, `propertyPath`, `propertyName`, `memberName`, `componentClass`, `templateOrigin`, `expressionKind`, `expressionHash` |
| `UiFormControlBinding` | Static form-control identity evidence such as `formControlName`, form group names, template-driven names, or control names. | `uiFramework`, `bindingKind`, `controlName`, `formControlName`, `formGroupName`, `propertyName`, `componentClass` |
| `UiEventBinding` | Static UI event binding to a handler name when visible. | `uiFramework`, `bindingKind`, `eventName`, `handlerName`, `componentClass`, `expressionHash` |
| `UiTemplateVariable` | Static template variable or local reference evidence. | `uiFramework`, `bindingKind`, `templateVariableName`, `templateVariableExport`, `componentClass` |
| `UiBindingGap` | Dynamic or unsupported template binding evidence boundary. | `uiFramework`, `gapKind`, `expressionHash`, `expressionKind`, `message` |
| `RazorBinding` | Razor `asp-for` or `Html.*For` model-property binding evidence. | `uiFramework`, `bindingKind`, `controlKind`, `propertyPath`, `propertyName`, `modelType` |
| `RazorFormTarget` | Razor static form target metadata. | `uiFramework`, `bindingKind`, `controlKind`, `httpMethod`, `actionName`, `controllerName`, `handlerName`, `pagePathHash` |
| `RazorModelBindingTarget` | Razor Pages/MVC model-binding target evidence when an adapter can prove it. | `uiFramework`, `bindingKind`, `modelType`, `propertyName`, `handlerName`, `actionName`, `controllerName` |
| `RazorBindingGap` | Dynamic Razor model/view-data/partial/template evidence boundary. | `uiFramework`, `gapKind`, `message` |

These facts are static evidence only. They do not prove runtime rendering,
visibility to every user, submitted values, model-binding success, validation
outcome, route selection, authorization, role checks, feature flags, branch
feasibility, browser state, dependency-injection targets, serializer runtime
configuration, SQL execution, deployment, or production use.

UI/property facts must not store raw template snippets, raw form values, raw
URLs, raw SQL, local absolute paths, raw remotes, connection strings, secrets,
credentials, or private data by default. Store safe names, hashes, lengths,
kinds, and line spans instead. Function calls, pipes, dynamic property names,
custom directive semantics, ViewBag/ViewData, partial/template ambiguity, and
runtime-generated forms should emit explicit gap facts or review-tier evidence
rather than invented property paths.

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

### Legacy WinForms Event Navigation Facts

The .NET adapter emits WinForms-specific static evidence for desktop UI entry
points and direct backend context:

| Fact type | Purpose | Safe matching keys |
| --- | --- | --- |
| `WinFormsSurfaceDeclared` | Inventories form, user-control, control, component, and application-context classes. | `typeName`, `surfaceKind`, `baseTypes` |
| `WinFormsControlDeclared` | Records static control/component fields and object creations. | `formTypeName`, `controlId`, `controlType`, `controlKind` |
| `WinFormsEventBindingDeclared` | Records explicit static event subscriptions. | `formTypeName`, `controlId`, `eventName`, `handlerName`, `bindingKind` |
| `WinFormsHandlerResolved` | Links event bindings to scoped partial-class handler methods. | `formTypeName`, `handlerName`, `handlerSymbol`, `sourceSymbolId`, `bindingFactId`, `resolutionKind` |
| `WinFormsNavigationEdgeDeclared` | Records static `Application.Run`, `Show`, `ShowDialog`, owner/parent, or MDI navigation evidence. | `formTypeName`, `sourceMethodName`, `targetFormTypeName`, `navigationKind`, `navigationClassification` |
| `WinFormsCallbackBoundaryDeclared` | Records timer, background worker, UI marshal, async/delegate, or callback boundaries. | `controlId`, `eventName`, `handlerName`, `boundaryClassification` |
| `WinFormsHandlerFlowProjected` | Projects resolved handlers to direct WCF, ASMX, remoting, legacy data, SQL/query, HTTP, config, or dependency-surface evidence. | `flowClassification`, `terminalSurfaceKind`, `terminalSurfaceNameHash`, `supportingFactIds`, `supportingEdgeIds`, `coverage` |
| `WinFormsResourceMetadataDeclared` | Records conservative `.resx` presence, culture suffix, key hashes, and resource kind labels. | `formTypeName`, `cultureSuffix`, `resourceKeyHashes`, `resourceKind` |

These facts are static evidence only. They do not prove runtime event firing,
form visibility, user reachability, layout, localization results, auth/role
outcomes, branch feasibility, callback scheduling, service reachability, SQL
execution, database existence, deployment, or production usage. Source snippets,
raw resource values, raw SQL, config values, endpoint addresses, URLs, hostnames,
local absolute paths, repository remotes, and private sample names must not
appear in properties or reports.

### Legacy ASP.NET Route And Navigation Facts

The .NET adapter also emits deterministic classic ASP.NET surface evidence
around WebForms pages and handlers. Existing `WebForms*` facts remain
authoritative for page/control/master event evidence; the ASP.NET facts add
route, config, handler, PageMethod, and navigation context without running IIS
or executing the app.

| Fact type | Purpose | Safe matching keys |
| --- | --- | --- |
| `AspNetSurfaceDeclared` | Inventories checked-in ASP.NET application and handler surface files, while reusing WebForms page/control/master inventory instead of duplicating it. | `surfaceKind`, safe directive metadata or role-separated hashes, `coverageLabel`, `ruleLimitations` |
| `AspNetRouteDeclared` | Records supported static classic ASP.NET route registration candidates such as `MapPageRoute`. | `routeShape`, `routePatternHash`, safe route name or hash, mapped page descriptor or hash, `coverageLabel` |
| `AspNetConfigSurfaceDeclared` | Records safe structures from checked-in handler/module/pages/controls/namespaces/urlMappings/compilation config. | `sectionKind`, safe type/path/verb descriptors or role-separated hashes, `coverageLabel` |
| `AspNetHandlerDeclared` | Records `.ashx`, `IHttpHandler`, `IHttpAsyncHandler`, and handler-factory static evidence. | `handlerKind`, safe type identity, unresolved factory flags when needed, `coverageLabel` |
| `AspNetPageMethodDeclared` | Records `[WebMethod]`, `[ScriptMethod]`, and `[ScriptService]` PageMethod/script-service evidence without reclassifying it as ASMX. | `methodName`, `containingTypeName`, `attributeNames`, `isStatic`, `coverageLabel` |
| `AspNetNavigationReferenceDeclared` | Records static navigation reference candidates from markup, sitemap XML, or supported C# APIs. | `referenceKind`, `sourceSurface`, target descriptor or role-separated hash, `coverageLabel` |
| `AspNetNavigationEdgeDeclared` | Links a navigation reference to checked-in page, route, config, or handler evidence only when non-hash target evidence supports the edge. | `edgeKind`, `referenceFactId`, `targetFactId`, `targetFactType`, `supportingFactIds` |

ASP.NET route/navigation hashes use the scanner-side context shape
`legacy.aspnet.<family>|<propertyRole>|<normalizedValue>` and store the
32-character lowercase hex prefix. The same unsafe raw value in route pattern,
config, and navigation target roles must produce different stored hashes.

These facts are static evidence only. They do not prove route-table execution,
IIS deployment, runtime URL rewriting, authorization, browser behavior,
JavaScript execution, page rendering, request handling, user reachability, or
runtime impact. Raw URLs, hostnames, config values, endpoint values, local
absolute paths, remotes, snippets, credentials, and secrets are omitted or
hashed.

### Legacy Data Metadata Facts

The .NET adapter emits legacy data metadata facts for checked-in DBML, EDMX, typed DataSet XSD/TableAdapter, data-provider config, and deterministic generated-code linkage.

| Fact type | Purpose | Safe matching keys |
| --- | --- | --- |
| `LegacyDataMetadataDeclared` | Inventories parseable legacy data metadata documents and generated-designer candidates. | `metadataKind`, `metadataFormat`, `metadataHash`, `inventoryKind`, `path` |
| `LegacyDataEntityDeclared` | Records static conceptual/generated entity, context, DataSet, row, or TableAdapter descriptors. | `metadataKind`, `metadataFormat`, `modelKind`, `descriptorRole`, `stableModelKey`, `entityName`, `typeName`, `mappedTypeName`, hash variants |
| `LegacyDataStorageObjectDeclared` | Records static table, view, routine, DataTable, or storage entity-set descriptors. | `metadataKind`, `metadataFormat`, `modelKind`, `descriptorRole`, `stableModelKey`, `storageObjectKind`, `storageObjectName`, `tableName`, hash variants |
| `LegacyDataColumnDeclared` | Records static property/field/column descriptors from metadata. | `metadataKind`, `metadataFormat`, `modelKind`, `descriptorRole`, `stableModelKey`, `ownerName`, `propertyName`, `fieldName`, `columnName`, hash variants |
| `LegacyDataMappingDeclared` | Records unambiguous descriptor-to-descriptor mappings such as entity-table, property-column, DBML/EDMX associations, and typed DataSet relations. | `metadataKind`, `metadataFormat`, `modelKind`, `descriptorRole`, `stableModelKey`, `mappingKind`, `modelRelationshipKind`, `modelRelationshipRuleId`, `relationshipEndpointCoverage`, `sourceEndpointName`, `targetEndpointName`, `entityName`, `tableName`, `propertyName`, `columnName`, hash variants |
| `LegacyDataProviderConfigDeclared` | Records safe provider, connection-name, provider factory, and EF provider metadata without raw values. | `configKind`, `connectionName`, `providerName`, `connectionNameHash`, `providerNameHash`, `valueHash` |
| `LegacyDataGeneratedCodeLinked` | Links metadata descriptors to generated files, scoped syntax declarations, or compiler-resolved symbols when deterministic. | `linkKind`, `symbolRole`, `metadataFormat`, `stableModelKey`, `mappedTypeName`, `typeName`, `generatedCodeFileName`, `sourceMetadataFactId`, `supportingFactIds`, `coverageLabel`, `limitations` |
| `AnalysisGap` under `legacy.data.orm.unsupported.v1` | Records recognized unsupported old ORM descriptor families without parsing or inventing model facts. | `descriptorFamily`, `descriptorSignal`, `classification`, `coverage`, `unsupportedLegacyOrmDescriptor`, `runtimeProof` |

Model identity properties such as `modelIdentityRuleId`, `modelIdentityEvidenceTier`, `sourceMetadataFactId`, `displayName`, `displayNameHash`, `containerName`, `containerHash`, and `coverageLabel` are additive metadata on the source facts; they do not re-emit DBML, EDMX, or typed DataSet descriptors under a second rule ID. These facts are static design-time metadata evidence. DBML, EDMX, typed DataSet, TableAdapter, and config descriptor facts are capped at `Tier2Structural`; generated-code links may be `Tier1Semantic` only when compiler-resolved symbol evidence is available, and that link does not upgrade descriptor facts. Raw SQL, connection strings, config values, namespace URIs, provider secrets, URLs, local paths, remotes, source snippets, and secret-looking values must be hashed or omitted. Metadata facts must not emit `DatabaseColumnMapping` without code-level mapping evidence owned by another rule.

Combined report, path, and route-flow readers project terminal legacy data model descriptors as the existing `legacy-data` surface kind with `surfaceSubtype = data-model`. The subtype is report/export metadata only; selectors continue to use `legacy-data`, and `AnalysisGap` facts under `legacy.data.*` rules remain gaps or caveats rather than terminal surfaces.

Projected `legacy-data` path nodes may carry an optional `limitations` list of
stable descriptor limitation codes such as `formula-redacted`,
`filter-redacted`, or `query-redacted`. These codes are output metadata for
report/export consumers; they must not include raw SQL, config values, provider
URLs, remotes, local paths, source snippets, or private labels.

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
