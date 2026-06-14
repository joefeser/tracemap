# JVM Indexer Tasks

## Implementation Plan

### Phase 0: Scope Lock

- [x] 0.1 Confirm Java and Kotlin live together under `src/jvm`.
- [x] 0.2 Confirm MVP Java semantic extraction comes before Kotlin semantic extraction.
- [x] 0.3 Confirm scanners parse Maven/Gradle local files but do not run target build commands during `scan`.
- [x] 0.4 Confirm existing .NET `tracemap reduce`, `tracemap export`, and `tracemap combine` remain the MVP downstream commands for JVM indexes.
- [x] 0.5 Confirm route, SQL, serializer, config, relationship, call, object creation, and argument-flow fact types reuse the existing fact schema where possible.
- [x] 0.6 Confirm MVP Kotlin support is inventory plus Tier3 syntax fallback only.
- [x] 0.7 Confirm field/property aliasing and derived `parameter_forward_edges` are follow-up, not MVP.
- [x] 0.8 Confirm route paths, SQL hashes, and JVM descriptors are report/export evidence unless a fact also carries reducer-matched plain names or `contractElement`.

### Phase 1: Scaffold `src/jvm`

- [x] 1.1 Create `src/jvm` package, build file, formatter/lint config, and test config.
- [x] 1.2 Choose JVM implementation language and build tool; document why. Recommended default: Java implementation on Gradle so future Kotlin compiler dependencies can be isolated.
- [x] 1.3 Decide Java compiler/syntax API and target JDK baseline; document whether MVP uses `JavacTask`/`Trees`, Eclipse JDT, or JavaParser semantic support.
- [x] 1.4 Decide Kotlin syntax strategy and document dependency size/license tradeoff; keep Kotlin compiler dependencies out of Java-only MVP if possible.
- [x] 1.5 Add `tracemap-jvm scan --help` and `tracemap-jvm --version`.
- [x] 1.6 Add deterministic build/test scripts.
- [x] 1.7 Add package docs for standalone `tracemap-jvm` usage and first-scan minimum input.
- [x] 1.8 Ensure repo private-path guard covers new JVM docs and smoke outputs.
- [x] 1.9 Add a common extractor interface and rule-catalog validation hook so emitted rule IDs must exist before tests pass.

### Phase 2: Fact Model and SQLite Contract

- [x] 2.1 Define JVM models matching the existing TraceMap fact envelope and JSON casing.
- [x] 2.2 Implement deterministic hashing and sorted-property fact creation.
- [x] 2.3 Document and test that extractor version is excluded from fact ID hash input.
- [x] 2.4 Implement `JsonlFactWriter`.
- [x] 2.5 Implement minimal SQLite writer for `scan_manifest` and `facts`.
- [x] 2.6 Create a tiny JVM index fixture with hand-written `TypeDeclared`, `PropertyAccessed`, and `MethodInvoked` facts.
- [x] 2.7 Run the real existing .NET `tracemap reduce` command against the fixture index and prove `DefiniteImpact`, including `Type.member` matching with dotted/plain JVM display names.
- [x] 2.8 Verify `SerializationLogic` does not get documented as reducer-probable and `SerializerContractMember` does.
- [x] 2.9 Verify `QueryPatternDetected` is reducer-probable only through `Tier2Structural`.
- [x] 2.10 Add tests for reducer-compatible fact types and required camelCase property keys.
- [x] 2.11 Add `symbols`, `symbol_occurrences`, and `fact_symbols` tables after reducer compatibility is proven.
- [x] 2.12 Add `symbol_relationships`, `call_edges`, `object_creations`, `argument_flows`, and `local_aliases` only as their source facts exist.
- [x] 2.13 Verify the real existing .NET `tracemap export` reads a JVM fixture index.
- [x] 2.14 Verify the real existing .NET `tracemap combine` can merge a JVM fixture index and a .NET fixture index without fact-ID collision.

### Phase 3: Repo Metadata, Manifest, and Inventory

- [x] 3.1 Implement bounded Git metadata detection with concurrent stdout/stderr draining and timeout.
- [x] 3.2 Implement manifest writing with exact full/reduced coverage semantics.
- [x] 3.3 Implement file inventory for `.java`, `.kt`, `.kts`, Maven, Gradle, config, SQL, and resource files.
- [x] 3.4 Implement default excludes: `.git`, output path, `target`, `build`, `.gradle`, generated outputs, dependency caches, and wrappers where appropriate.
- [x] 3.5 Implement include/exclude/project scope filtering.
- [x] 3.6 Implement max file byte-size parsing and skip gaps.
- [x] 3.7 Add tests for path normalization, excluded directories, stable scan IDs, and full/reduced manifest gates.

### Phase 4: Maven, Gradle, Config, and Gap Infrastructure

- [x] 4.1 Parse Maven `pom.xml` for group/artifact/version, local parent fallback, modules, dependencies, dependency management, source roots, and compiler source/target/release properties.
- [x] 4.2 Emit Maven gaps for non-local parent POMs, remote-only parent metadata, dynamic properties, and unresolved dependency versions.
- [x] 4.3 Parse Gradle settings/build files for literal project includes, group/version, plugins, and fully literal dependency coordinates.
- [x] 4.4 Emit Gradle gaps for version catalogs, buildSrc, convention plugins, composite builds, string interpolation, and dynamic build metadata rather than guessing.
- [x] 4.5 Emit package/module identity and dependency facts.
- [x] 4.6 Add `ClasspathResolver` that documents empty/platform classpath, user-provided classpath, and missing-dependency gap behavior.
- [x] 4.7 Parse `.properties`, YAML, JSON, XML, and env-like config key paths with line spans and sensitive value hashes.
- [x] 4.8 Add `AnalysisGapCollector`.
- [x] 4.9 Add `DiagnosticAggregator` with grouping by category/project and bounded per-project output.
- [x] 4.10 Add `GeneratedSourceDetector` for common generated-source directories and generated-source-not-run gaps.
- [x] 4.11 Add tests for parent POM fallback, Gradle literal extraction, dynamic build gaps, repeated config keys, parse gaps, generated-source gaps, and diagnostic aggregation caps.

### Phase 5: Syntax Fallback Baseline

- [x] 5.1 Discover Java and Kotlin source roots from project models and conventional paths.
- [x] 5.2 Emit Java syntax declaration facts using existing reducer-compatible fact types where applicable.
- [x] 5.3 Emit Kotlin syntax declaration facts using existing reducer-compatible fact types where applicable.
- [x] 5.4 Emit annotation facts with safe names and hashed literal arguments.
- [x] 5.5 Emit member access facts with receiver names/hashes, not raw expressions.
- [x] 5.6 Emit invocation and syntax call-edge facts using shared `InvocationName`/`CallEdge` fact types and JVM-specific rule IDs.
- [x] 5.7 Emit object creation facts.
- [x] 5.8 Emit Tier3 logic-shape and boilerplate facts.
- [x] 5.9 Add tests against broken Java, broken Kotlin, Java records, Java annotations, Kotlin data classes, Kotlin extension functions, Ktor-like route-builder syntax, and syntax-only reduced coverage.

### Phase 6: Java Semantic Spike and MVP

- [x] 6.1 Create a Java semantic spike against a single-file no-dependency fixture using the selected compiler API.
- [x] 6.2 Disable annotation processing with `-proc:none` where the selected compiler API supports it.
- [x] 6.3 Integrate file-size skip behavior with semantic extraction.
- [x] 6.4 Convert missing dependency/classpath diagnostics into bounded `AnalysisGap` facts.
- [x] 6.5 Record ordinary compiler diagnostics as reduced-coverage `AnalysisGap` facts without aborting recoverable extraction.
- [x] 6.6 Emit semantic `TypeDeclared`, `FieldDeclared`, `ParameterDeclared`, `PropertyAccessed`, and `MethodInvoked` facts.
- [x] 6.7 Emit semantic `CallEdge`, `ObjectCreated`, and direct `ArgumentPassed` facts.
- [x] 6.8 Suppress weaker syntax duplicate facts when a Tier1 semantic fact exists for the same logical site.
- [x] 6.9 Add tests for declarations, field/member reads/writes, method calls, overloads, constructors, local symbols, external dependency symbols, missing classpath gaps, and reducer classification.

### Phase 7: JVM Symbol Identity and Relationships

- [x] 7.1 Implement `JvmSymbolIdentityProvider`.
- [x] 7.2 Implement JVM descriptor formatting for methods, constructors, fields, arrays, generics erasure where appropriate, and source fallback descriptors.
- [x] 7.3 Include group/artifact/version, module, package, owner, member name, descriptor, and local spans in IDs.
- [x] 7.4 Emit `ExtendsClass`, `ExtendsInterface`, and `ImplementsInterface`.
- [x] 7.5 Emit `Overrides` only when compiler evidence proves the overridden target.
- [x] 7.6 Add relationship rows to SQLite and export/combine compatibility tests.
- [x] 7.7 Add relationship fixture tests for Java classes, interfaces, records, abstract classes, default interface methods, and cross-module source roots.

### Phase 8: MVP Integration Extractors

- [x] 8.1 Design integration extraction as either semantic post-processing or checker-aware syntax traversal with explicit tier decisions.
- [x] 8.2 Emit route facts for Spring MVC/WebFlux annotations.
- [x] 8.3 Emit route facts for JAX-RS annotations.
- [x] 8.4 Emit SQL facts for JDBC calls, JPA `@Query`, repository query-method patterns, and `.sql` resources.
- [x] 8.5 Emit JPA entity/table/column/repository mapping facts.
- [x] 8.6 Emit `SerializerContractMember` facts for Jackson annotations with explicit member names and `SerializationLogic` only as report/export evidence.
- [x] 8.7 Emit config-use facts for Spring `@Value`, `Environment.getProperty`, `System.getenv`, and `System.getProperty`.
- [x] 8.8 Add fixture tests for each MVP integration family and verify tiers.

### Phase 9: Report, Export, Combine, and Docs

- [x] 9.1 Generate `report.md` with metadata, coverage, gaps, fact counts, integration counts, and JVM limitations.
- [x] 9.2 Verify existing `tracemap export` reads JVM symbols, relationships, and call edges.
- [x] 9.3 Verify existing `tracemap combine` imports JVM indexes and namespaces symbols by source index/language.
- [x] 9.4 Update `README.md` with JVM scan/reduce/combine examples.
- [x] 9.5 Update `docs/ACCEPTANCE.md` with JVM acceptance scenarios.
- [x] 9.6 Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` only if the JVM implementation discovers contract gaps.
- [x] 9.7 Add JVM rule IDs and limitations to `rules/rule-catalog.yml` before any extractor emits those rule IDs.

### Phase 10: Samples and Smoke

- [x] 10.1 Add small committed Java sample with Spring, JPA/JDBC, config, Jackson serializer member, relationship, call, object creation, and argument-flow fixtures.
- [x] 10.2 Add small committed Kotlin sample with syntax fallback fixtures and clear reduced-coverage expectations.
- [x] 10.3 Add broken Java/Kotlin sample for reduced coverage.
- 10.4 Add missing-classpath fixture and verify `AnalysisGap` facts with `gapKind = "MissingDependency"`.
- 10.5 Add cross-language fixture: Java class referencing Kotlin-shaped source and Kotlin source referencing Java source, with syntax-only expectations unless Kotlin semantic exists.
- [x] 10.6 Add JVM sample contract deltas.
- [x] 10.7 Add a local-only smoke script or documented command set for external sample repos using generic placeholder paths.
- 10.8 Smoke-test at least `scip-java`, `spring-petclinic`, one Java library, one Kotlin library, and one Ktor sample from local external clones.
- 10.9 Record smoke results without committing absolute local paths or private repo names.

### Phase 11: Post-MVP Flow and Logic

- 11.1 Emit simple local alias facts for Java semantic locals.
- 11.2 Expand logic-shape facts for arithmetic, comparisons, branching, validation, transformations, retries, collection processing, and date/time logic.
- 11.3 Emit infrastructure/boilerplate facts for generated code, Spring Boot entrypoints, dependency-injection config, DTO-only classes, and test fixtures.
- 11.4 Add tests for local aliases, logic-shape detection, and boilerplate classification.

## Follow-Up Backlog

- Bytecode/classpath-only dependency symbol extraction.
- User-supplied classpath/dependency coordinate files.
- Ktor route builders with Kotlin structural/semantic evidence.
- Retrofit interface HTTP client facts.
- Spring `RestTemplate`, Spring `WebClient`, Java `HttpClient`, OkHttp, Ktor client detectors.
- Gson deep handling, Moshi, kotlinx.serialization, Java serialization markers, and MicroProfile Config.
- Hibernate Criteria, jOOQ, MyBatis, R2DBC, Exposed, Room, Flyway, Liquibase detectors.
- Lombok, MapStruct, Dagger, Micronaut, Quarkus, Spring AOT/generated-source awareness.
- Field/property aliasing beyond bounded local cases.
- Derived `parameter_forward_edges` for JVM.
- Kotlin semantic extraction with Kotlin compiler analysis API or another deterministic compiler-backed approach.
- Kotlin descriptors for top-level functions, companion members, extension functions, properties, suspend functions, and data-class generated members where compiler evidence supports them.
- Root `tracemap` CLI language dispatch.
- Snapshot diff workflow across two commit SHAs.

## Definition of Done

- [x] JVM package builds.
- [x] JVM tests pass.
- [x] Existing .NET `dotnet build src/dotnet/TraceMap.sln` passes after repo changes.
- [x] Existing .NET `dotnet test src/dotnet/TraceMap.sln` passes after repo changes.
- [x] Existing TypeScript tests pass after repo changes if shared docs/schema changed.
- [x] `tracemap-jvm scan` writes required artifacts for Java, Kotlin, and broken samples.
- [x] Existing .NET `tracemap reduce` can classify a JVM semantic property/type/method match.
- [x] Existing `tracemap combine` can import a JVM index.
- [x] Facts contain rule IDs, evidence tiers, repo/commit SHA, line spans, extractor versions, and no raw snippets.
- [x] Reduced scans are labeled reduced and never reported as clean.
- [x] Rule catalog limitations are updated for every new rule ID.
