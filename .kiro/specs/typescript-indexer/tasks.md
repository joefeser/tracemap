# TypeScript Indexer Tasks

## Implementation Plan

### Phase 0: Scope Lock

- [x] 0.1 Confirm MVP decisions in `requirements.md` and `design.md`.
- [x] 0.2 Treat JavaScript, workspace discovery, field/property aliasing, parameter-forward edges, broad integrations, and root CLI dispatch as follow-up.
- [x] 0.3 Confirm the existing .NET reducer remains the MVP reduction path for TypeScript indexes.
- [x] 0.4 Confirm TypeScript `buildStatus = "Succeeded"` means semantic analysis succeeded with no known gaps, not target repo build/test success.

### Phase 1: Scaffold `src/typescript`

- [x] 1.1 Create `src/typescript/package.json`, `tsconfig.json`, and test config.
- [x] 1.2 Choose SQLite dependency explicitly; prefer a low-friction option and document native build requirements if using a native package.
- [x] 1.3 Add `tracemap-ts scan --help`.
- [x] 1.4 Add deterministic build/test scripts.
- [x] 1.5 Add package docs for standalone `tracemap-ts` usage.

### Phase 2: Fact Model and Reducer Contract Spike

- [x] 2.1 Define TypeScript models matching the existing TraceMap fact envelope and JSON casing.
- [x] 2.2 Implement deterministic hashing and sorted-property fact creation.
- [x] 2.3 Document and test that extractor version is excluded from fact ID hash input.
- [x] 2.4 Implement `JsonlFactWriter`.
- [x] 2.5 Implement a minimal SQLite writer for `scan_manifest` and `facts`.
- [x] 2.6 Create a tiny TypeScript index fixture with hand-written `TypeDeclared`, `PropertyAccessed`, and `MethodInvoked` facts.
- [x] 2.7 Run the existing .NET reducer against the fixture index and prove `DefiniteImpact`.
- [x] 2.8 Add tests for reducer-compatible fact types and required camelCase property keys.

### Phase 3: Repo Metadata, Manifest, and Inventory

- [x] 3.1 Implement bounded Git metadata detection with concurrent stdout/stderr draining and timeout.
- [x] 3.2 Implement manifest writing with exact full/reduced coverage semantics.
- [x] 3.3 Implement file inventory for MVP TypeScript files, config files, and `package.json`.
- [x] 3.4 Implement default excludes: `.git`, output path, `node_modules`, package-manager caches, and common build outputs.
- [x] 3.5 Implement include/exclude/project scope filtering.
- [x] 3.6 Implement max file byte-size parsing and skip gaps.
- [x] 3.7 Add tests for path normalization, excluded directories, stable scan IDs, and full/reduced manifest gates.

### Phase 4: Package, Config, and Gap Infrastructure

- [x] 4.1 Implement package identity lookup from nearest `package.json`, including missing version fallback, parse-error fallback, and recursive parent lookup.
- [x] 4.2 Parse `package.json` and emit package/dependency facts.
- [x] 4.3 Hash package script command values instead of storing raw text.
- [x] 4.4 Parse JSON config key paths with line spans and sensitive value hashes.
- [x] 4.5 Add `AnalysisGapCollector.ts`.
- [x] 4.6 Add `DiagnosticAggregator.ts` with grouping by diagnostic code/category/project and a per-project cap plus summary count.
- [x] 4.7 Add tests for repeated JSON property names, package fallback identity, config parse gaps, and diagnostic aggregation caps.

### Phase 5: TypeScript Project Loading

- [x] 5.1 Discover explicit `tsconfig.json` files and explicit `--project` paths.
- [x] 5.2 Load project references in dependency order and de-duplicate scanned projects.
- [x] 5.3 Implement `util/CompilerHost.ts` with parsed-command-line/source-file caching inspired by `scip-typescript`.
- [x] 5.4 Integrate file-size skip behavior with compiler host/source indexing.
- [x] 5.5 Create `ts.Program` and `TypeChecker`.
- [x] 5.6 Convert missing dependency/module diagnostics into bounded `AnalysisGap` facts.
- [x] 5.7 Record ordinary TypeScript diagnostics as reduced-coverage `AnalysisGap` facts without aborting recoverable semantic extraction.
- [x] 5.8 Add tests for explicit projects, project references, missing dependency reduced coverage, and no-config syntax fallback.

### Phase 6: Syntax Fallback Extractor

- [x] 6.1 Emit syntax declaration facts using existing reducer-compatible fact types where applicable.
- [x] 6.2 Emit member access facts with receiver names/hashes, not raw expressions.
- [x] 6.3 Emit invocation and syntax call-edge facts.
- [x] 6.4 Emit object creation facts.
- [x] 6.5 Emit import/export/decorator facts without claiming semantic identity.
- [x] 6.6 Emit Tier3 logic-shape and boilerplate facts.
- [x] 6.7 Add tests against broken TypeScript code and TSX syntax.

### Phase 7: Symbol Identity and Semantic MVP

- [x] 7.1 Implement `TypeScriptSymbolIdentityProvider`.
- [x] 7.2 Implement and test `localSymbolKey(sourceFile, node, name)`.
- [x] 7.3 Include package name/version, module path, descriptors, overload disambiguators, and local spans in IDs.
- [x] 7.4 Emit semantic `TypeDeclared` facts.
- [x] 7.5 Emit semantic `PropertyAccessed` facts with reducer-required property keys.
- [x] 7.6 Emit semantic `MethodInvoked` facts with reducer-required property keys.
- [x] 7.7 Emit semantic call edges and object creation facts when symbol identity is available.
- [x] 7.8 Emit direct `ArgumentPassed` facts with parameter metadata, expression kind, and expression hash.
- [x] 7.9 Emit simple local alias facts for aliases assigned from parameters or resolved symbols.
- [x] 7.10 Add tests for declarations, property reads/writes, function calls, overloads, constructor calls, local aliases, and reducer classification.

### Phase 8: SQLite Symbols and Report

- [x] 8.1 Add `symbols` and `symbol_occurrences` tables.
- [x] 8.2 Insert symbol rows for semantic facts.
- [x] 8.3 Add `fact_symbols` only if the MVP implementation needs it for queryability; otherwise defer with derived tables.
- [x] 8.4 Add SQLite indexes matching expected reducer/report queries.
- [x] 8.5 Generate `report.md` with metadata, coverage, gaps, fact counts, and limitations.
- [x] 8.6 Add SQLite row-count and report snapshot tests.
- [x] 8.7 Resolve `sql.js` wasm assets deterministically and fail with a clear message when assets are missing.

### Phase 9: Symbol Relationships

- [x] 9.1 Emit direct `ExtendsClass` facts.
- [x] 9.2 Emit direct `ExtendsInterface` facts.
- [x] 9.3 Emit direct `ImplementsInterface` facts.
- [x] 9.4 Emit `Overrides` only when the TypeScript `override` keyword is present and the base member resolves.
- [x] 9.5 Defer `ImplementsInterfaceMember` if structural typing makes the evidence ambiguous.
- [x] 9.6 Add relationship fixture tests.

### Phase 10: MVP Integration Extractors

- [x] 10.1 Design integration extraction as either a resolved call-edge post-process or a checker-aware AST pass with explicit tier decisions.
- [x] 10.2 Emit HTTP facts for `fetch` and `axios`.
- [x] 10.3 Emit route facts for Express route methods.
- [x] 10.4 Emit serializer facts for `JSON.parse` and `JSON.stringify`.
- [x] 10.5 Emit schema/DTO facts for Zod.
- [x] 10.6 Emit database facts for Prisma client calls.
- [x] 10.7 Emit config facts for `process.env` reads.
- [x] 10.8 Add fixture tests for each MVP integration family and verify tiers.

### Phase 11: Follow-Up Flow Tables

- [x] 11.1 Design field/property aliasing separately before implementation.
- [x] 11.2 Design `parameter_forward_edges` derivation as either in-memory post-processing or a two-pass SQLite operation.
- [x] 11.3 Add derived tables only when the facts that populate them exist.
- [x] 11.4 Add `tracemap flow` compatibility tests after derived tables exist.

### Phase 12: Samples, Smoke, and Docs

- [x] 12.1 Add `samples/typescript-modern-sample`.
- [x] 12.2 Add `samples/typescript-broken-sample`.
- [x] 12.3 Add a route/schema/database sample for MVP integrations.
- [x] 12.4 Add TypeScript sample contract deltas.
- [x] 12.5 Smoke-test at least one external TypeScript repo and record the result.
- [x] 12.6 Update `README.md` with TypeScript scan/reduce examples.
- [x] 12.7 Update `docs/ACCEPTANCE.md` with TypeScript acceptance scenarios.
- [x] 12.8 Add TypeScript rule IDs and limitations to `rules/rule-catalog.yml`.

## Follow-Up Backlog

- JavaScript and inferred config support.
- Workspace discovery from package-json, pnpm, and Yarn workspace files.
- Root `tracemap` CLI language dispatch.
- Nest, Fastify, Koa, Next.js, Remix, GraphQL, Yup, io-ts, class-validator, TypeBox, OpenAPI, TypeORM, Sequelize, Knex, Drizzle, and SQL template detectors.
- Field/property aliasing.
- Derived `parameter_forward_edges`.
- Multi-index cross-language reducer workflow.

## Definition of Done

- [x] TypeScript package builds.
- [x] TypeScript tests pass.
- [x] Existing .NET `dotnet build src/dotnet/TraceMap.sln` passes after repo changes.
- [x] Existing .NET `dotnet test src/dotnet/TraceMap.sln` passes after repo changes.
- [x] `tracemap-ts scan` writes required artifacts for TypeScript modern and broken samples.
- [x] Existing .NET `tracemap reduce` can classify a TypeScript semantic property/type/method match.
- [x] Facts contain rule IDs, evidence tiers, repo/commit SHA, line spans, extractor versions, and no raw snippets.
- [x] Reduced scans are labeled reduced and never reported as clean.
- [x] Rule catalog limitations are updated for every new rule ID.
