# JVM Indexer Tasks

## Implementation Plan

### Phase 0: Scope Lock

- [ ] 0.1 Confirm Java and Kotlin live together under `src/jvm`.
- [ ] 0.2 Confirm MVP Java semantic extraction comes before Kotlin semantic extraction.
- [ ] 0.3 Confirm scanners parse Maven/Gradle local files but do not run target build commands during `scan`.
- [ ] 0.4 Confirm existing .NET `tracemap reduce`, `tracemap export`, and `tracemap combine` remain the MVP downstream commands for JVM indexes.
- [ ] 0.5 Confirm route, SQL, serializer, config, relationship, call, object creation, and argument-flow fact types reuse the existing fact schema where possible.

### Phase 1: Scaffold `src/jvm`

- [ ] 1.1 Create `src/jvm` package, build file, formatter/lint config, and test config.
- [ ] 1.2 Choose JVM implementation language and build tool; document why.
- [ ] 1.3 Add `tracemap-jvm scan --help`.
- [ ] 1.4 Add deterministic build/test scripts.
- [ ] 1.5 Add package docs for standalone `tracemap-jvm` usage.
- [ ] 1.6 Ensure repo private-path guard covers new JVM docs and smoke outputs.

### Phase 2: Fact Model and SQLite Contract

- [ ] 2.1 Define JVM models matching the existing TraceMap fact envelope and JSON casing.
- [ ] 2.2 Implement deterministic hashing and sorted-property fact creation.
- [ ] 2.3 Document and test that extractor version is excluded from fact ID hash input.
- [ ] 2.4 Implement `JsonlFactWriter`.
- [ ] 2.5 Implement SQLite writer for `scan_manifest`, `facts`, `symbols`, `symbol_occurrences`, `fact_symbols`, and derived tables already supported by combine/export.
- [ ] 2.6 Create a tiny JVM index fixture with hand-written `TypeDeclared`, `PropertyAccessed`, and `MethodInvoked` facts.
- [ ] 2.7 Run the existing .NET reducer against the fixture index and prove `DefiniteImpact`.
- [ ] 2.8 Add tests for reducer-compatible fact types and required camelCase property keys.

### Phase 3: Repo Metadata, Manifest, and Inventory

- [ ] 3.1 Implement bounded Git metadata detection with concurrent stdout/stderr draining and timeout.
- [ ] 3.2 Implement manifest writing with exact full/reduced coverage semantics.
- [ ] 3.3 Implement file inventory for `.java`, `.kt`, `.kts`, Maven, Gradle, config, SQL, and resource files.
- [ ] 3.4 Implement default excludes: `.git`, output path, `target`, `build`, `.gradle`, generated outputs, dependency caches, and wrappers where appropriate.
- [ ] 3.5 Implement include/exclude/project scope filtering.
- [ ] 3.6 Implement max file byte-size parsing and skip gaps.
- [ ] 3.7 Add tests for path normalization, excluded directories, stable scan IDs, and full/reduced manifest gates.

### Phase 4: Maven, Gradle, Config, and Gap Infrastructure

- [ ] 4.1 Parse Maven `pom.xml` for group/artifact/version, parent fallback, modules, dependencies, dependency management, source roots, and compiler source/target/release properties.
- [ ] 4.2 Parse Gradle settings/build files for literal project includes, group/version, plugins, dependencies, and standard source roots.
- [ ] 4.3 Emit package/module identity and dependency facts.
- [ ] 4.4 Emit `AnalysisGap` facts for dynamic Maven/Gradle metadata rather than guessing.
- [ ] 4.5 Parse `.properties`, YAML, JSON, XML, and env-like config key paths with line spans and sensitive value hashes.
- [ ] 4.6 Add `AnalysisGapCollector`.
- [ ] 4.7 Add `DiagnosticAggregator` with grouping by category/project and bounded per-project output.
- [ ] 4.8 Add tests for parent POM fallback, Gradle literal extraction, dynamic build gaps, repeated config keys, parse gaps, and diagnostic aggregation caps.

### Phase 5: Java Project Loading and Semantic MVP

- [ ] 5.1 Discover Java source roots from project models and conventional paths.
- [ ] 5.2 Create Java compiler tasks using `javax.tools.JavaCompiler` or `JavacTask`.
- [ ] 5.3 Disable annotation processing where possible and record generated-source uncertainty.
- [ ] 5.4 Integrate file-size skip behavior with semantic extraction.
- [ ] 5.5 Convert missing dependency/classpath diagnostics into bounded `AnalysisGap` facts.
- [ ] 5.6 Record ordinary compiler diagnostics as reduced-coverage `AnalysisGap` facts without aborting recoverable extraction.
- [ ] 5.7 Emit semantic `TypeDeclared`, `FieldDeclared`, `ParameterDeclared`, `PropertyAccessed`, and `MethodInvoked` facts.
- [ ] 5.8 Emit semantic `CallEdge`, `ObjectCreated`, and direct `ArgumentPassed` facts.
- [ ] 5.9 Add tests for declarations, field/member reads/writes, method calls, overloads, constructors, local symbols, external dependency symbols, and reducer classification.

### Phase 6: Syntax Fallback Extractors

- [ ] 6.1 Emit Java syntax declaration facts using existing reducer-compatible fact types where applicable.
- [ ] 6.2 Emit Kotlin syntax declaration facts using existing reducer-compatible fact types where applicable.
- [ ] 6.3 Emit annotation facts with safe names and hashed literal arguments.
- [ ] 6.4 Emit member access facts with receiver names/hashes, not raw expressions.
- [ ] 6.5 Emit invocation and syntax call-edge facts.
- [ ] 6.6 Emit object creation facts.
- [ ] 6.7 Emit Tier3 logic-shape and boilerplate facts.
- [ ] 6.8 Add tests against broken Java, broken Kotlin, Java records, Java annotations, Kotlin data classes, Kotlin extension functions, and Kotlin route-builder syntax.

### Phase 7: JVM Symbol Identity and Relationships

- [ ] 7.1 Implement `JvmSymbolIdentityProvider`.
- [ ] 7.2 Implement JVM descriptor formatting for methods, constructors, fields, arrays, generics erasure where appropriate, and source fallback descriptors.
- [ ] 7.3 Include group/artifact/version, module, package, owner, member name, descriptor, and local spans in IDs.
- [ ] 7.4 Emit `ExtendsClass`, `ExtendsInterface`, and `ImplementsInterface`.
- [ ] 7.5 Emit `Overrides` only when compiler evidence proves the overridden target.
- [ ] 7.6 Add relationship rows to SQLite and export/combine compatibility tests.
- [ ] 7.7 Add relationship fixture tests for Java classes, interfaces, records, abstract classes, default interface methods, and cross-module source roots.

### Phase 8: JVM Integration Extractors

- [ ] 8.1 Design integration extraction as either semantic post-processing or checker-aware syntax traversal with explicit tier decisions.
- [ ] 8.2 Emit route facts for Spring MVC/WebFlux annotations.
- [ ] 8.3 Emit route facts for JAX-RS annotations.
- [ ] 8.4 Emit route facts for Ktor route builders as Kotlin syntax/structural evidence.
- [ ] 8.5 Emit HTTP client facts for Retrofit interfaces.
- [ ] 8.6 Emit SQL facts for JDBC calls, JPA `@Query`, repository query-method patterns, and `.sql` resources.
- [ ] 8.7 Emit JPA entity/table/column/repository mapping facts.
- [ ] 8.8 Emit serialization facts for Jackson, Gson, Moshi, kotlinx.serialization, and Java serialization markers.
- [ ] 8.9 Emit config-use facts for Spring `@Value`, `Environment.getProperty`, MicroProfile Config, `System.getenv`, and `System.getProperty`.
- [ ] 8.10 Add fixture tests for each MVP integration family and verify tiers.

### Phase 9: Flow and Logic Shape

- [ ] 9.1 Emit direct `ArgumentPassed` facts from semantic Java calls.
- [ ] 9.2 Emit syntax-only argument shape facts where semantic binding is absent.
- [ ] 9.3 Emit simple local alias facts for Java semantic locals.
- [ ] 9.4 Decide whether bounded field aliasing is safe for MVP; if not, document as follow-up.
- [ ] 9.5 Emit logic-shape facts for arithmetic, comparisons, branching, validation, transformations, retries, collection processing, and date/time logic.
- [ ] 9.6 Emit infrastructure/boilerplate facts for generated code, Spring Boot entrypoints, dependency-injection config, DTO-only classes, and test fixtures.
- [ ] 9.7 Add tests for direct parameter forwarding, local aliases, logic-shape detection, and boilerplate classification.

### Phase 10: Report, Export, Combine, and Docs

- [ ] 10.1 Generate `report.md` with metadata, coverage, gaps, fact counts, integration counts, and JVM limitations.
- [ ] 10.2 Verify existing `tracemap export` reads JVM symbols, relationships, and call edges.
- [ ] 10.3 Verify existing `tracemap combine` imports JVM indexes and namespaces symbols by source index/language.
- [ ] 10.4 Update `README.md` with JVM scan/reduce/combine examples.
- [ ] 10.5 Update `docs/ACCEPTANCE.md` with JVM acceptance scenarios.
- [ ] 10.6 Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` only if the JVM implementation discovers contract gaps.
- [ ] 10.7 Add JVM rule IDs and limitations to `rules/rule-catalog.yml`.

### Phase 11: Samples and Smoke

- [ ] 11.1 Add small committed Java sample with Spring, JPA/JDBC, config, serializer, relationship, call, object creation, and argument-flow fixtures.
- [ ] 11.2 Add small committed Kotlin sample with Ktor, serialization/config, relationship, call, object creation, and syntax fallback fixtures.
- [ ] 11.3 Add broken Java/Kotlin sample for reduced coverage.
- [ ] 11.4 Add JVM sample contract deltas.
- [ ] 11.5 Add a local-only smoke script or documented command set for external sample repos using generic placeholder paths.
- [ ] 11.6 Smoke-test at least `scip-java`, `spring-petclinic`, one Java library, one Kotlin library, and one Ktor sample from local external clones.
- [ ] 11.7 Record smoke results without committing absolute local paths or private repo names.

### Phase 12: Kotlin Semantic Follow-Up

- [ ] 12.1 Choose Kotlin compiler analysis API and dependency strategy.
- [ ] 12.2 Implement Kotlin semantic project loading without running target builds.
- [ ] 12.3 Emit semantic Kotlin declarations, calls, object creation, argument passing, relationships, and symbol occurrences.
- [ ] 12.4 Model Kotlin-specific descriptors for top-level functions, companion members, extension functions, properties, suspend functions, and data-class generated members where compiler evidence supports them.
- [ ] 12.5 Add cross Java/Kotlin fixture tests.

## Follow-Up Backlog

- [ ] Bytecode/classpath-only dependency symbol extraction.
- [ ] User-supplied classpath/dependency coordinate files.
- [ ] Spring `RestTemplate`, Spring `WebClient`, Java `HttpClient`, OkHttp, Ktor client detectors.
- [ ] Hibernate Criteria, jOOQ, MyBatis, R2DBC, Exposed, Room, Flyway, Liquibase detectors.
- [ ] Lombok, MapStruct, Dagger, Micronaut, Quarkus, Spring AOT/generated-source awareness.
- [ ] Field/property aliasing beyond bounded local cases.
- [ ] Derived `parameter_forward_edges` for JVM.
- [ ] Root `tracemap` CLI language dispatch.
- [ ] Snapshot diff workflow across two commit SHAs.

## Definition of Done

- [ ] JVM package builds.
- [ ] JVM tests pass.
- [ ] Existing .NET `dotnet build src/dotnet/TraceMap.sln` passes after repo changes.
- [ ] Existing .NET `dotnet test src/dotnet/TraceMap.sln` passes after repo changes.
- [ ] Existing TypeScript tests pass after repo changes if shared docs/schema changed.
- [ ] `tracemap-jvm scan` writes required artifacts for Java, Kotlin, and broken samples.
- [ ] Existing .NET `tracemap reduce` can classify a JVM semantic property/type/method match.
- [ ] Existing `tracemap combine` can import a JVM index.
- [ ] Facts contain rule IDs, evidence tiers, repo/commit SHA, line spans, extractor versions, and no raw snippets.
- [ ] Reduced scans are labeled reduced and never reported as clean.
- [ ] Rule catalog limitations are updated for every new rule ID.
