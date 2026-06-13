# JVM Indexer Design

## Overview

Add a JVM TraceMap scanner under `src/jvm`. The package will produce TraceMap-compatible artifacts for Java and Kotlin repositories:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

The scanner will use Java compiler APIs for Tier1 Java semantic extraction, JVM build-file metadata for structural evidence, and Java/Kotlin syntax traversal for fallback. Kotlin semantic extraction is a follow-up slice; MVP Kotlin support is inventory plus syntax fallback.

This is a sibling implementation, not a rewrite of the .NET or TypeScript scanners:

```text
src/
  dotnet/
  typescript/
  jvm/
```

## Goals

- Preserve TraceMap's deterministic, evidence-backed behavior for JVM repositories.
- Support Java and Kotlin source inventories in one adapter.
- Parse Maven and Gradle metadata without running target builds or downloading dependencies.
- Emit enough facts for contract reduction, dependency traversal, endpoint alignment, persistence review, serializer review, relationship reporting, and business-logic routing.
- Keep all limitations explicit in rule catalog entries and reports.

## Non-Goals

- No LLM calls or embeddings.
- No runtime execution of target repository code.
- No automatic Maven/Gradle restore, compile, test, wrapper execution, or dependency download as part of `scan`.
- No claim of runtime reachability, branch feasibility, dependency-injection binding, dynamic proxy targets, reflection targets, annotation-processor output, serializer contract mapping, or profile-specific configuration beyond evidence.
- No raw source snippets by default.
- No direct dependency on SCIP output as the canonical TraceMap artifact.
- No bytecode-only decompilation in MVP.
- No Kotlin semantic claims until a compiler-backed extractor exists.
- No claim that `Level1SemanticAnalysis` means the target repository builds, tests, starts, or runs its annotation processors successfully.
- No field/property aliasing, derived parameter-forward edges, reflection-target inference, dependency-injection target inference, generated-source execution, or runtime route/serializer configuration inference in MVP.

## Locked Decisions To Review

- Initial CLI is standalone `tracemap-jvm`; root `tracemap` dispatch is out of scope unless review asks for it.
- Existing .NET `tracemap reduce` is the MVP reduction path for JVM indexes.
- Java and Kotlin live together under `src/jvm`.
- Java semantic extraction comes after the always-on syntax fallback baseline; Kotlin starts with syntax/structural evidence and receives semantic extraction in a follow-up slice.
- Build tools are parsed from local files; target build commands are not run during scan.
- JVM facts that should influence reducer classification reuse existing `FactTypes` strings and reducer-matching camelCase property keys.
- `tracemap combine` is the cross-index dependency home; JVM scan output remains a normal single-language index.

## Package Structure

Proposed package shape:

```text
src/jvm/
  README.md
  build.gradle.kts or pom.xml
  settings.gradle.kts
  src/main/java/com/tracemap/jvm/
    cli/
      TraceMapJvmCli.java
      ScanCommand.java
    scan/
      ScanEngine.java
      AnalysisGapCollector.java
      DiagnosticAggregator.java
      FileInventory.java
      GitMetadataProvider.java
      ManifestWriter.java
    facts/
      FactFactory.java
      FactModels.java
      FactTypes.java
      RuleIds.java
    buildtools/
      MavenProjectReader.java
      GradleProjectReader.java
      ProjectModel.java
      DependencyModel.java
    extractors/
      JavaProjectLoader.java
      JavaSemanticExtractor.java
      JavaSyntaxExtractor.java
      KotlinSyntaxExtractor.java
      ConfigExtractor.java
      IntegrationExtractor.java
      LogicShapeExtractor.java
      Extractor.java
    storage/
      JsonlFactWriter.java
      SqliteIndexWriter.java
    reporting/
      MarkdownReportWriter.java
    symbols/
      JvmSymbolIdentityProvider.java
      JvmDescriptorFormatter.java
    util/
      Paths.java
      Hashes.java
      LineMap.java
      SizeParser.java
      RuleCatalogValidator.java
      GeneratedSourceDetector.java
      ClasspathResolver.java
  src/test/
```

Gradle is a natural fit because it can build Java/Kotlin implementation code later if we choose to add Kotlin compiler dependencies. Maven is also viable. The implementation choice should favor deterministic local tests and low setup friction over target-repo build fidelity.

## CLI Shape

Initial JVM CLI:

```bash
tracemap-jvm scan --repo <path> --out <path>
```

Options:

- `--project <path>` repeatable: explicit `pom.xml`, `build.gradle`, `build.gradle.kts`, source directory, or module directory.
- `--include <glob>` repeatable.
- `--exclude <glob>` repeatable.
- `--max-file-byte-size <value>` default `1mb`.
- `--no-semantic`: force syntax fallback for debugging/reduced scan validation.
- `--language java|kotlin|all` default `all`.

Follow-up options:

- `--classpath-file <path>` for user-supplied classpath metadata.
- `--dependencies-file <path>` for dependency coordinate mapping inspired by `scip-java`.
- root `tracemap` language dispatch.
- optional `--run-build-metadata-command` only if later explicitly designed with safety boundaries.

## Artifact Compatibility

### Manifest

Use the same manifest fields as the existing adapters:

- `scanId`
- `repo`
- `remoteUrl`
- `branch`
- `commitSha`
- `scannerVersion`
- `scannedAt`
- `analysisLevel`
- `buildStatus`
- project/build-file identifiers
- source roots
- target language summaries
- `knownGaps`
- scan root metadata: `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash`

JVM analysis levels:

- `Level1SemanticAnalysis`: all selected semantic scopes loaded without known gaps.
- `Level1SemanticAnalysisReduced`: at least one semantic scope had compiler/classpath/build-tool gaps, syntax fallback still ran.
- `Level3SyntaxAnalysis`: semantic analysis disabled or no semantic project was loadable.

JVM build status:

- `Succeeded`: scanner semantic analysis completed for all selected JVM project scopes with `commitSha != "unknown"` and no known gaps. This does not mean Maven/Gradle/test execution succeeded.
- `FailedOrPartial`: at least one selected project had diagnostics or gaps that reduce confidence.
- `NotRun`: semantic analysis was disabled or no semantic project was loadable.

Manifest JSON must deserialize into the .NET `ScanManifest` contract. Property names must include the .NET-compatible meanings for `RepoName`, `CommitSha`, `AnalysisLevel`, `BuildStatus`, and `KnownGaps`; JSON casing may vary only where the .NET deserializer is already case-insensitive.

### Facts

Use the existing TraceMap fact envelope:

- deterministic `factId`
- `scanId`
- repo and commit SHA
- `factType`
- `ruleId`
- `evidenceTier`
- source/target symbols when available
- optional `contractElement`
- `EvidenceSpan`
- extractor ID/version
- sorted properties

Reducer-compatible fact types:

- `TypeDeclared`
- `PropertyAccessed`
- `MethodInvoked`
- `FieldDeclared`
- `ParameterDeclared`
- `ConfigKeyDeclared`
- `HttpRouteBinding`
- `HttpCallDetected`
- `QueryPatternDetected`
- `DatabaseColumnMapping`
- `SqlTextUsed`
- `SerializerContractMember`
- `SymbolRelationship`
- `CallEdge`
- `ObjectCreated`
- `ArgumentPassed`
- `LocalAlias`
- `AnalysisGap`

No property should store raw source text. Raw string/template/config values become hashes, lengths, kinds, spans, and normalized keys where safe.

Reducer-classification roles are not the same as storage compatibility:

- Definite reducer facts require `Tier1Semantic` plus `TypeDeclared`, `PropertyAccessed`, or `MethodInvoked`.
- Semantic probable reducer facts must use the reducer's current probable set, including `SerializerContractMember` rather than `SerializationLogic`.
- Any `Tier2Structural` fact can become `ProbableImpact`, so Tier2 must be reserved for recognized framework/build structure, not mere name similarity.
- `CallEdge`, `ObjectCreated`, `ArgumentPassed`, `LocalAlias`, `FieldDeclared`, `ParameterDeclared`, `SerializationLogic`, and Tier3 `QueryPatternDetected` are useful for reports/export/combine, but they do not by themselves drive reducer classification.
- Route paths, SQL hashes, query hashes, and JVM descriptors are not matched by the current reducer. They should be treated as evidence details unless a fact also carries a `Type.member`-style `contractElement` or plain matching keys.

### SQLite

MVP tables:

- `scan_manifest`
- `facts`
- `symbols`
- `symbol_occurrences`
- `fact_symbols`
- `symbol_relationships`
- `call_edges`
- `object_creations`
- `argument_flows`
- `local_aliases`

Follow-up tables:

- `field_aliases`
- `parameter_forward_edges`
- JVM-local `package_dependencies`
- JVM-local `routes`
- JVM-local `config_uses`
- JVM-local `database_mappings`

Schema changes must be additive. JVM indexes must remain readable by existing `tracemap reduce`, `tracemap export`, and `tracemap combine` where those commands rely on shared tables. JVM-local tables are allowed only when documented as having no current downstream consumer.

## Project Loading

Project loading should be conservative and file-backed:

1. Normalize repo path and scan options.
2. Collect file inventory before semantic load.
3. Discover Maven and Gradle build files.
4. Parse Maven `pom.xml` with XML APIs:
   - parent group/version fallback when literal
   - modules
   - dependencies and dependency management when literal
   - properties
   - standard source/test roots
   - compiler source/target/release properties
5. Parse Gradle files with bounded literal extraction:
   - `settings.gradle*` include declarations
   - literal `group`, `version`, plugins, and dependency coordinates
   - gaps for version catalogs, `buildSrc`, convention plugins, composite builds, string interpolation, and dynamic build logic
6. Build a `ProjectModel` containing modules, source roots, dependency coordinates, and confidence labels.
7. Resolve the Java semantic API before implementation begins. The default recommendation is public `javax.tools` plus `com.sun.source.util.JavacTask`/`Trees` on a pinned JDK baseline, with `-proc:none`; alternatives such as Eclipse JDT or JavaParser semantic support must be documented before Phase 5 work starts.
8. Add a classpath decision layer. MVP may use the JDK platform classpath plus source roots and any user-provided classpath metadata; missing external dependencies become reduced-coverage gaps.
9. Do not execute annotation processors. Disable processing where possible and report generated-source/processor gaps.
10. Record diagnostics through `DiagnosticAggregator` as bounded `AnalysisGap` facts without storing source snippets.
11. Run syntax fallback for selected source files regardless of semantic success. When both a Tier1 semantic fact and a Tier3 syntax fact describe the same logical site, suppress the weaker syntax duplicate using this default key: file path, start line, end line, fact type, containing symbol/display name, and plain member/type name.

Missing dependencies reduce coverage; they do not fail the scan unless no useful analysis can be performed.

## Syntax Fallback

Java syntax decision:

- Pick exactly one Java syntax parser before implementation. The recommended default is javac parse APIs if the Java semantic extractor uses `JavacTask`; JavaParser is acceptable only if spans and duplicate suppression are tested against semantic extraction.
- Emit declarations, annotations, member access, invocations, constructors, call edges, object creation, logic shape, and route/SQL/config patterns.

Kotlin syntax options:

- Pick the Kotlin syntax strategy before implementing Kotlin syntax fixtures. MVP may choose conservative pattern fallback to avoid the full `kotlin-compiler-embeddable` dependency; if PSI is used, dependency size/license and isolation from Java-only builds must be documented.
- Kotlin syntax facts must remain Tier3 unless structural framework evidence supports Tier2.

Syntax fallback must not store raw expressions. Use expression kind, hashes, stable names, and spans.

## Symbol Identity

JVM symbol identity should be stable and precise enough for multi-index dependency queries.

Candidate machine symbol ID shape:

```text
jvm:<language>:<group>:<artifact>:<version-or-HEAD>:<module>:<package>/<ClassName>#<memberName><descriptor>
```

Examples:

```text
jvm:java:com.example:orders:1.2.3:api:com/example/orders/OrderService#submit(Lcom/example/orders/Order;)V
jvm:kotlin:com.example:orders:HEAD:api:com/example/orders/OrderRoutesKt#registerRoutes(Lio/ktor/server/routing/Routing;)V
```

Symbol rows should carry:

- `language`
- `packageName`
- `moduleName`
- Maven/Gradle group/artifact/version when available
- fully qualified JVM owner name
- member name
- JVM descriptor or source fallback descriptor
- source file/span for local declarations

Source fallback symbols are deterministic but weaker than compiler symbols.

Reducer-facing display names must stay plain and dotted. Keep machine IDs and JVM descriptors in `symbols.symbol_id` and JVM-specific properties, but populate `facts.target_symbol`, `facts.source_symbol`, `methodName`, `propertyName`, `memberName`, `typeName`, `namespace`, `containingType`, `sourceSymbolDisplayName`, and `targetSymbolDisplayName` with reducer-friendly values such as `com.example.orders.OrderService.submit`. Do not place slash/descriptor-only strings like `com/example/orders/OrderService#submit(L...)V` in fields the reducer uses for name matching.

Fact IDs must follow the existing adapter formula: hash `scanId`, `factType`, `ruleId`, evidence file path, start line, end line, project path, source symbol, target symbol, contract element, and sorted `key=value` properties. Extractor version is intentionally excluded from the fact ID input.

## Integration Extractors

### HTTP Server

Spring annotations:

- `@RequestMapping`
- `@GetMapping`
- `@PostMapping`
- `@PutMapping`
- `@PatchMapping`
- `@DeleteMapping`

JAX-RS annotations:

- `@Path`
- `@GET`
- `@POST`
- `@PUT`
- `@PATCH`
- `@DELETE`

Follow-up Ktor route builders:

- `route`, `get`, `post`, `put`, `patch`, `delete`, `head`, `options`

HTTP route facts should populate `httpMethod`, `normalizedPathTemplate`, `normalizedPathKey`, `routePatternHash`, `controllerName`, `methodName`, `targetSymbol`, and route parameter metadata when visible. MVP route extraction is Spring plus JAX-RS; Ktor remains follow-up unless implementation review expands the slice.

### HTTP Client

MVP excludes HTTP client integrations unless scope is expanded by review. Retrofit is the first follow-up candidate:

- `@GET`, `@POST`, `@PUT`, `@PATCH`, `@DELETE`, `@HEAD`, `@OPTIONS`, `@HTTP`

Optional follow-up:

- Spring `RestTemplate`
- Spring `WebClient`
- Java `HttpClient`
- OkHttp
- Ktor client

### Persistence

MVP detectors:

- JDBC `prepareStatement`, `createStatement`, `execute`, `executeQuery`, `executeUpdate`
- JPA `@Entity`, `@Table`, `@Column`, `@Id`, relationship annotations
- Spring Data repository interfaces and query-method names
- JPA `@Query`
- `.sql` files

Facts should record operation names, SQL hashes/lengths, table/column names when statically visible, and source symbols.

### Serialization and Schema

MVP detectors:

- Jackson annotations with explicit member names as `SerializerContractMember`
- broad Jackson `ObjectMapper` calls as `SerializationLogic` report/export evidence

Follow-up detectors:

- Gson `Gson` calls and annotations
- Moshi adapter calls
- kotlinx.serialization annotations and compiler plugin metadata
- Java serialization interfaces

Do not claim exact runtime wire contracts unless explicit source evidence exists. `SerializationLogic` is not reducer-probable in the current reducer; `SerializerContractMember` is the reducer-compatible serializer fact.

### Config

MVP detectors:

- Spring `@Value`
- `Environment.getProperty`
- `System.getenv`
- `System.getProperty`
- config files: `.properties`, `.yml`, `.yaml`, `.json`, `.xml`

Follow-up:

- MicroProfile `@ConfigProperty`

Sensitive values remain hashed.

## Sample Repos

Use local external clones for smoke tests, but keep paths out of committed docs and artifacts. Suggested local sample root:

```text
<jvm-sample-repos>/
  scip-java/
  spring-petclinic/
  gson/
  junit4/
  okio/
  ktor-samples/
  turbine/
```

Coverage targets:

- `scip-java`: compiler-indexing reference and mixed Java/Kotlin repo.
- `spring-petclinic`: Spring MVC/API and Maven/Gradle metadata.
- `gson`: Java library, Maven multi-module, serializer domain.
- `junit4`: Java framework/library with substantial tests.
- `okio`: Kotlin-heavy Gradle multiplatform project.
- `ktor-samples`: Ktor endpoint/client/server examples.
- `turbine`: small Kotlin Gradle library.

Any smoke result committed to the repo should use generic sample labels, not absolute local paths.

## Testing Strategy

Test layers:

- unit tests for path normalization, hashing, line spans, fact IDs, and manifest gates
- Maven/Gradle parser fixture tests
- Java semantic fixture tests
- Java syntax fallback fixture tests with broken source
- Kotlin syntax fallback fixture tests
- MVP integration extractor fixtures for Spring, JAX-RS, JDBC, JPA, Jackson `SerializerContractMember`, and config reads
- follow-up integration fixture plans for Ktor, Retrofit, Gson, Moshi, kotlinx.serialization, broader JVM HTTP clients, and MicroProfile Config
- cross-binary compatibility tests that run the real .NET `tracemap reduce`, `tracemap export`, and `tracemap combine` against hand-written JVM fixture indexes
- cross-language fixtures for Java/Kotlin source interaction once Kotlin semantic work begins
- SQLite row-count and reducer compatibility tests
- smoke scripts against external local samples, excluded from private or machine-specific paths

Definition of evidence quality:

- Tier1 only when compiler symbol evidence proves identity.
- Tier2 when framework/build structure proves the pattern but not full symbol identity.
- Tier3 when syntax/text names suggest a match.
- Tier4 for gaps and unable-to-prove states.
