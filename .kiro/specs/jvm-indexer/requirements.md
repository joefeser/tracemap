# JVM Indexer Requirements

## Introduction

TraceMap needs a deterministic JVM repository scanner for Java and Kotlin projects. The scanner should produce the same evidence-backed artifacts as the .NET and TypeScript adapters while using JVM-native compilation metadata where available and syntax fallback when classpath or build-tool loading is incomplete.

This spec intentionally does not add LLMs, embeddings, vector databases, runtime execution, traffic capture, or prompt-based classification. The JVM scanner must preserve TraceMap's core contract: no conclusion without evidence, no evidence without a rule ID, and no raw source snippets by default.

The implementation target is a new sibling package under `src/jvm`. Java and Kotlin should start together because they share Maven/Gradle metadata, JVM bytecode/signature concepts, package naming, classpath handling, and downstream dependency analysis.

## MVP Scope Decisions

- MVP language scope is `.java`, `.kt`, and `.kts` inventory; semantic extraction should start with Java where compiler APIs are lower friction and add Kotlin semantic extraction as the second implementation slice.
- MVP build-tool scope is Maven and Gradle project discovery by local files. The scanner may parse `pom.xml`, `build.gradle`, `build.gradle.kts`, `settings.gradle`, and `settings.gradle.kts`; it must not run Maven, Gradle, or package download commands during `scan`.
- MVP semantic scope uses Java compiler APIs when a project can be loaded from discovered source roots and available classpath metadata. Kotlin compiler integration is follow-up unless implemented deterministically without invoking target builds.
- MVP fallback scope parses Java/Kotlin syntax enough to emit declaration, member access, call, object creation, relationship, endpoint, SQL, config, and logic-shape evidence.
- MVP integration detectors target Spring MVC/WebFlux route annotations, JAX-RS annotations, Ktor routes, Retrofit client calls, JDBC SQL usage, JPA repository/entity metadata, Jackson/Gson/Moshi serialization boundary signals, and common config reads.
- MVP flow scope includes direct argument-to-parameter facts and simple local aliases only. Field/property aliasing and derived `parameter_forward_edges` should be included only when the facts are already proven and bounded.
- MVP reduction is performed by the existing .NET command: `tracemap reduce --index <jvm index.sqlite> --contract-delta <delta.json> --out <report.md>`.

## Borrowed Lessons From `scip-java`

The local `scip-java` repo shows several useful implementation lessons:

- Compiler-backed Java extraction is the strongest source of semantic identity because it sees the same source/classpath model as `javac`.
- Per-file extraction is memory-friendly, but TraceMap should emit native facts and SQLite rows rather than SCIP as the canonical artifact.
- Build-tool integration is hard to make automatic; reduced coverage must be normal, useful, and clearly labeled.
- Source symbols and dependency symbols should use package/module identity where available, and external dependency coordinates should be captured when Maven/Gradle metadata proves them.
- Classpath-only symbols can be valuable for dependency identity, but they do not prove source-level implementation details.
- Syntax-only indexing remains useful when compilation cannot be configured.

TraceMap should not copy SCIP output as its primary product. SCIP is symbol-index oriented; TraceMap is evidence/reducer oriented.

## Requirements

### Requirement 1: JVM Scan CLI

**User Story:** As a reviewer, I want to scan a Java/Kotlin repository with TraceMap so that contract-change reduction and dependency reports can use deterministic JVM evidence.

#### Acceptance Criteria

1. WHEN the user runs `tracemap-jvm scan --repo <path> --out <path>` THEN the scanner SHALL write `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
2. WHEN the repo path does not exist THEN the scanner SHALL exit non-zero and not write a partial success manifest.
3. WHEN the repo is inside a Git checkout THEN the manifest SHALL include repo name, remote URL when available, branch when available, and commit SHA.
4. WHEN Git commit metadata is unavailable THEN the scanner SHALL fail before writing scan artifacts.
5. WHEN output already exists THEN the scanner SHALL overwrite only the requested output path and SHALL not delete files outside that path.
6. WHEN semantic analysis has any project-load, classpath, compiler, parser, skipped-file, or unsupported-language gaps THEN the manifest SHALL set reduced coverage and emit at least one `AnalysisGap` fact.
7. WHEN every selected JVM project loads semantically, `commitSha != "unknown"`, and no known gaps are emitted THEN the manifest SHALL set `analysisLevel = "Level1SemanticAnalysis"` and `buildStatus = "Succeeded"`.
8. WHEN semantic analysis is disabled or unavailable THEN the manifest SHALL set `analysisLevel = "Level3SyntaxAnalysis"` or `Level1SemanticAnalysisReduced` as appropriate.

### Requirement 2: Project and Build-Tool Discovery

**User Story:** As a maintainer, I want the JVM scanner to understand common Maven and Gradle layouts so that mono-repos and multi-module projects can be scanned predictably.

#### Acceptance Criteria

1. WHEN the repo contains Maven `pom.xml` files THEN the scanner SHALL parse group ID, artifact ID, version, modules, source roots, test source roots, compiler source/target/release properties when statically visible, and dependencies.
2. WHEN the repo contains Gradle settings/build files THEN the scanner SHALL parse project names, included projects, plugins, group/version values when statically visible, source-set-like paths when obvious, and dependencies from simple literal declarations.
3. WHEN build files contain dynamic logic, property indirection, plugin convention logic, version catalogs, buildSrc, included builds, or custom source sets THEN the scanner SHALL record an `AnalysisGap` or reduced structural evidence rather than guessing.
4. WHEN both Maven and Gradle files exist THEN the scanner SHALL inventory both and choose the explicit `--project` scope when provided; otherwise it SHALL scan discovered source roots without claiming the target build succeeded.
5. WHEN `--project <path>` is provided THEN project loading SHALL be limited to the selected project or build file scope.
6. WHEN `--include` or `--exclude` globs are provided THEN file inventory, syntax fallback, semantic extraction, and project loading SHALL obey those filters.
7. WHEN files are under `.git`, TraceMap output directories, `target`, `build`, `.gradle`, `.mvn/wrapper`, generated-source output, or dependency caches THEN they SHALL be excluded by default unless explicitly included.

### Requirement 3: File Inventory, Package, and Config Facts

**User Story:** As a reviewer, I want JVM repository structure and config represented as evidence so that structural matches can support reducer findings.

#### Acceptance Criteria

1. WHEN supported files are discovered THEN the scanner SHALL emit deterministic `FileInventoried` facts with relative path, kind, size, rule ID, evidence tier, and extractor version.
2. WHEN Maven or Gradle build files are discovered THEN the scanner SHALL emit package/module identity facts and dependency facts without storing raw build-file snippets.
3. WHEN dependency versions are literal or resolved from local properties in the same parsed file THEN the scanner SHALL store group/artifact/version.
4. WHEN dependency versions are dynamic or externally resolved THEN the scanner SHALL store group/artifact when safe and record a reduced-confidence property or `AnalysisGap`.
5. WHEN properties, YAML, JSON, XML, env-like, or framework config files are scanned THEN config key facts SHALL store key paths, value kind, hashes for sensitive values, and line spans.
6. WHEN config parsing fails THEN the scanner SHALL continue and emit an `AnalysisGap` fact with the parser error category.

### Requirement 4: Syntax Fallback

**User Story:** As a reviewer, I want useful JVM facts even when dependencies or build metadata are missing.

#### Acceptance Criteria

1. WHEN semantic analysis cannot load a project THEN the scanner SHALL parse supported source files with syntax fallback.
2. WHEN syntax parse diagnostics occur THEN the scanner SHALL emit `AnalysisGap` facts and still emit recoverable syntax facts.
3. WHEN a recoverable Java or Kotlin declaration node is parsed THEN syntax fallback SHALL emit declaration facts with declaration kind, safe name, source span, and rule ID.
4. WHEN annotation syntax is parsed THEN syntax fallback SHALL emit annotation facts with safe names and hashed literal arguments.
5. WHEN invocation expressions are found THEN syntax fallback SHALL emit `InvocationName` and syntax `CallEdge` facts with containing function/class when available.
6. WHEN constructor calls are found THEN syntax fallback SHALL emit `ObjectCreated` facts with created type syntax, constructor argument count, and assigned variable when obvious.
7. WHEN member access expressions are found THEN syntax fallback SHALL emit member-name facts without storing raw receiver expressions.
8. WHEN syntax facts need expression identity THEN they SHALL store expression kind and expression hash, not raw source text.

### Requirement 5: Java Semantic Symbol Facts

**User Story:** As a reviewer, I want compiler-resolved Java facts so that contract deltas can be matched with stronger evidence than syntax names.

#### Acceptance Criteria

1. WHEN a Java project can be compiled or parsed with `javac` semantic APIs and symbols resolve THEN semantic facts SHALL use `Tier1Semantic`.
2. WHEN package, type, enum, record, interface, method, constructor, field, property-like accessor, and parameter declarations are resolved THEN the scanner SHALL emit declaration facts with stable JVM symbol IDs and fully qualified display names.
3. WHEN field or accessor reads/writes are resolved THEN the scanner SHALL emit property/member access or mutation facts with source symbol, target symbol, package/module identity, file span, and rule ID.
4. WHEN method calls are resolved THEN the scanner SHALL emit `MethodInvoked` and semantic `CallEdge` facts with caller, callee, JVM descriptor, argument count, and overload/signature identity.
5. WHEN constructor calls are resolved THEN the scanner SHALL emit `ObjectCreated` facts with created type, constructor symbol, caller symbol, module identity, and assignment target when obvious.
6. WHEN call-site arguments are resolved THEN the scanner SHALL emit `ArgumentPassed` facts with parameter name/type, argument symbol when available, expression kind, and expression hash; it SHOULD emit source declaration span when available.
7. WHEN external dependency symbols resolve to Maven/Gradle coordinates THEN symbol identity SHALL include group ID, artifact ID, version when available, and package/class/member descriptor.
8. WHEN symbols are local-only THEN IDs SHALL be deterministic for the same file path and source span.

### Requirement 6: Kotlin Semantic Direction

**User Story:** As a Kotlin reviewer, I want the JVM adapter to support Kotlin without pretending syntax-only evidence is semantic.

#### Acceptance Criteria

1. WHEN Kotlin semantic extraction is not implemented THEN `.kt` files SHALL still receive syntax fallback and reduced coverage SHALL be visible in the manifest.
2. WHEN Kotlin semantic extraction is added THEN it SHALL use Kotlin compiler analysis APIs or another deterministic compiler-backed approach, not source-text guesses.
3. WHEN Kotlin functions, classes, properties, constructors, extension functions, companion members, data classes, and suspend functions resolve semantically THEN facts SHALL use stable JVM/Kotlin-aware symbol IDs and `Tier1Semantic`.
4. WHEN Kotlin-specific features such as reified generics, delegated properties, operator overloads, extension dispatch, coroutine continuations, or synthetic data-class members cannot be modeled directly THEN the scanner SHALL emit documented gaps or conservative syntax evidence.
5. WHEN Kotlin and Java symbols interact in the same project THEN cross-language JVM call and relationship facts SHALL be emitted only when compiler evidence proves the target symbol.

### Requirement 7: Symbol Relationships

**User Story:** As a reviewer, I want JVM type and member relationships so that interface/class impact can be traversed across implementations and overrides.

#### Acceptance Criteria

1. WHEN a class extends another class THEN the scanner SHALL emit a `SymbolRelationship` fact with relationship kind `ExtendsClass`.
2. WHEN a class implements an interface THEN the scanner SHALL emit relationship kind `ImplementsInterface`.
3. WHEN an interface extends another interface THEN the scanner SHALL emit relationship kind `ExtendsInterface`.
4. WHEN a method overrides a superclass or interface method and compiler evidence proves it THEN the scanner SHALL emit relationship kind `Overrides`.
5. WHEN a Kotlin declaration implements/overrides a Java declaration or vice versa THEN the relationship SHALL be emitted only with semantic compiler evidence.
6. WHEN relationships cannot be resolved because dependencies, annotation processors, generated sources, dynamic proxies, or compiler plugins are missing THEN the scanner SHALL not guess and SHOULD emit an analysis gap or limitation note where useful.

### Requirement 8: Integration and Contract-Mapping Evidence

**User Story:** As a platform engineer, I want common JVM service boundaries detected so that contract changes can route to API, persistence, serializer, and config code.

#### Acceptance Criteria

1. WHEN Spring MVC/WebFlux annotations such as `@RequestMapping`, `@GetMapping`, `@PostMapping`, `@PutMapping`, `@PatchMapping`, and `@DeleteMapping` are found THEN the scanner SHALL emit `HttpRouteBinding` facts with method/path hashes or normalized path keys and controller/action symbols when available.
2. WHEN JAX-RS annotations such as `@Path`, `@GET`, `@POST`, `@PUT`, `@PATCH`, and `@DELETE` are found THEN the scanner SHALL emit `HttpRouteBinding` facts with equivalent route evidence.
3. WHEN Ktor route builder calls are found by Kotlin syntax shape THEN the scanner SHALL emit Tier3 or Tier2 `HttpRouteBinding` facts and SHALL not claim runtime plugin route installation.
4. WHEN Retrofit client annotations are found THEN the scanner SHALL emit `HttpCallDetected` facts with method/path evidence and interface method symbols when available.
5. WHEN JDBC `Connection.prepareStatement`, `Statement.execute*`, JPA `@Query`, repository method query patterns, or SQL resource files are found THEN the scanner SHALL emit persistence/query facts with SQL hashes, operation names, and field/table names when statically visible.
6. WHEN JPA entities, embeddables, mapped superclasses, table names, column names, IDs, relationships, or repository interfaces are found THEN the scanner SHALL emit structural database/object-mapping facts with rule IDs and evidence tiers.
7. WHEN Jackson, Gson, Moshi, kotlinx.serialization, or Java serialization APIs/annotations are found THEN the scanner SHALL emit serialization/schema facts without claiming exact runtime serializer configuration unless explicit evidence exists.
8. WHEN Spring `@Value`, `Environment.getProperty`, MicroProfile Config, `System.getenv`, `System.getProperty`, or common config binding annotations are found THEN the scanner SHALL emit config-use facts with key paths/hashes where statically visible.
9. WHEN literal or template values are used for routes, SQL, config, or serialization fields THEN raw text SHALL not be stored; store literal kind, hash, length, and line span. Normalized route path keys may store route shape because they are dependency evidence, not secrets.

### Requirement 9: Flow and Logic Shape

**User Story:** As a reviewer, I want evidence about data flow and business-logic hotspots so that review can distinguish meaningful behavior from glue code.

#### Acceptance Criteria

1. WHEN a parameter is passed directly to another function, method, or constructor THEN the scanner SHALL emit `ArgumentPassed` facts when semantic evidence exists and syntax fallback facts when only syntax is available.
2. WHEN simple local aliases are assigned from parameters or other symbols THEN the scanner SHOULD emit local alias facts.
3. WHEN fields/properties are assigned from parameters and later passed onward in the same method or constructor pattern THEN the scanner MAY emit bounded alias facts only if ambiguity is low and the rule limitations are documented.
4. WHEN arithmetic, comparison-heavy, branch-heavy, retry/backoff, validation, transformation, collection-processing, or date/time logic is found THEN the scanner SHALL emit logic-shape facts with operators/kinds and expression hashes.
5. WHEN files match boilerplate patterns such as generated code, Spring Boot entrypoints, dependency injection configuration, test fixtures, data-transfer-only classes, or build scripts THEN the scanner SHALL emit infrastructure/boilerplate facts.
6. WHEN flow cannot be proven across threads, callbacks, reactive streams, coroutines, reflection, dynamic proxies, dependency injection, annotation processors, serializers, or collection contents THEN the scanner SHALL not infer flow beyond recorded evidence.

### Requirement 10: SQLite and Reducer Compatibility

**User Story:** As a user of TraceMap, I want JVM indexes to work with existing reduction/reporting concepts so that contract deltas can be reduced across languages.

#### Acceptance Criteria

1. WHEN JVM scan completes THEN `facts.ndjson` SHALL use the same top-level fact schema and JSON casing as .NET and TypeScript facts.
2. WHEN a JVM fact is intended to participate in existing reducer classification THEN it SHALL reuse existing `FactTypes` strings verbatim.
3. WHEN a JVM fact is intended to be `DefiniteImpact` eligible THEN it SHALL use one of the reducer-recognized fact types: `PropertyAccessed`, `MethodInvoked`, or `TypeDeclared`.
4. WHEN a JVM fact is intended to be `ProbableImpact` eligible THEN it SHALL use existing structural or probable semantic fact types such as `HttpRouteBinding`, `ConfigKeyDeclared`, `SqlTextUsed`, `HttpCallDetected`, `SerializationLogic`, `QueryPatternDetected`, `DatabaseColumnMapping`, or related recognized fact types.
5. WHEN emitting reducer-compatible facts THEN the scanner SHALL populate existing camelCase matching keys used by the reducer, including `propertyName`, `memberName`, `fieldName`, `methodName`, `keyPath`, `name`, `containingType`, `className`, `typeName`, `namespace`, and `targetSymbol` where applicable.
6. WHEN `index.sqlite` is written THEN it SHALL include `scan_manifest` and `facts` tables readable by the existing .NET reducer without reducer changes.
7. WHEN symbols, relationships, calls, object creation, and flow evidence are emitted THEN SQLite SHALL include `symbols`, `symbol_occurrences`, `fact_symbols`, `symbol_relationships`, `call_edges`, `object_creations`, `argument_flows`, `local_aliases`, and other existing derived tables where supported.
8. WHEN JVM-specific data is needed THEN schema additions SHALL be additive and documented.
9. WHEN facts contain JVM symbol IDs THEN the symbol table SHALL include `language = 'java'`, `language = 'kotlin'`, or `language = 'jvm'` as appropriate.

### Requirement 11: Determinism, Safety, and Performance

**User Story:** As a maintainer, I want JVM scanning to be deterministic, bounded, and safe for large repos.

#### Acceptance Criteria

1. WHEN the same repo commit and scan options are scanned twice THEN stable fact IDs SHALL match except for explicitly time-stamped manifest fields.
2. WHEN dependencies are missing or compiler diagnostics occur THEN the scanner SHALL continue with reduced semantic coverage and syntax fallback.
3. WHEN annotation processors or compiler plugins would be required to generate sources THEN the scanner SHALL not execute them during scan and SHALL report generated-source uncertainty.
4. WHEN a file exceeds the max byte-size threshold THEN the scanner SHALL skip semantic/syntax extraction for that file and emit an `AnalysisGap`.
5. WHEN worker/project indexing fails unexpectedly THEN the scanner SHALL record the failure and continue to remaining projects when possible.
6. WHEN paths are emitted THEN they SHALL be normalized repo-relative paths.
7. WHEN evidence spans are emitted THEN they SHALL use one-based line numbers and deterministic end-line calculation.
8. WHEN scanner logs include diagnostics THEN logs SHALL avoid raw source snippets unless an explicit future raw-snippet option is added.

## Required Property-Key Contract

The JVM scanner SHALL use these existing property keys when emitting reducer-compatible facts:

| Fact type | Required matching keys |
| --- | --- |
| `TypeDeclared` | `name`, `typeName`, `namespace`, `targetSymbol` |
| `PropertyAccessed` | `propertyName`, `memberName`, `containingType`, `targetSymbol` |
| `MethodInvoked` | `methodName`, `containingType`, `targetSymbol` |
| `FieldDeclared` | `fieldName`, `fieldType`, `containingType`, `targetSymbol` |
| `ParameterDeclared` | `parameterName`, `parameterType`, `sourceSymbol` |
| `ConfigKeyDeclared` | `keyPath`, `name`, `targetSymbol` |
| `HttpRouteBinding` | `httpMethod`, `normalizedPathKey`, `methodName`, `targetSymbol`, `contractElement` |
| `HttpCallDetected` | `httpMethod`, `normalizedPathKey`, `methodName`, `targetSymbol`, `contractElement` |
| `SqlTextUsed` | `textHash`, `textLength`, `targetSymbol` |
| `SerializationLogic` | `operationName`, `targetSymbol`, `contractElement` |
| `SymbolRelationship` | `relationshipKind`, `sourceSymbol`, `targetSymbol`, `sourceSymbolId`, `targetSymbolId` |

## JVM-Specific Limitations

- JVM scanner `buildStatus = "Succeeded"` means scanner semantic analysis reached full coverage for the selected scope; it does not mean target repository tests, Gradle tasks, Maven lifecycle goals, annotation processors, or app startup succeeded.
- Missing dependencies, generated sources, annotation processors, Gradle convention plugins, Kotlin compiler plugins, Lombok, MapStruct, Dagger, Micronaut, Quarkus, and Spring AOT can reduce semantic coverage.
- Syntax-only Java/Kotlin facts do not prove overload resolution, receiver type, inheritance, runtime dispatch, dependency injection, serializer configuration, or route registration.
- Java reflection, proxies, service loaders, dependency-injection bindings, runtime profiles, feature flags, classpath shading, and bytecode weaving are not proven by MVP scanning.
- Kotlin data-class synthetic methods, delegated properties, extension dispatch, suspend/coroutine transformations, and compiler-plugin-generated declarations require explicit compiler evidence before semantic claims.
- Symbol IDs are stable for the same scan inputs but are not guaranteed stable across file renames, package renames, module changes, dependency version changes, or compiler binding changes.
