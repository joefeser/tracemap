# TypeScript Indexer Requirements

## Introduction

TraceMap needs a deterministic TypeScript repository scanner that produces the same evidence-backed artifacts as the .NET scanner while using the TypeScript compiler API for semantic facts and syntax fallback when project loading is incomplete. JavaScript support is a follow-up because weak or absent type information changes evidence quality and coverage semantics.

This spec intentionally does not add LLMs, embeddings, vector stores, or prompt-based classification to the scanner or reducer. The TypeScript implementation must preserve TraceMap's core contract: no conclusion without evidence, no evidence without a rule ID, and no raw source snippets by default.

The implementation target is a new sibling package under `src/typescript`, with compatible scan artifacts and SQLite schema so the existing .NET reducer can read a TypeScript `index.sqlite`.

## MVP Scope Decisions

- MVP language scope is `.ts` and `.tsx`; `.js`, `.jsx`, `.mjs`, `.cjs`, and inferred JS config are follow-up.
- MVP project scope is explicit `tsconfig.json` discovery, `--project`, and TypeScript project references; workspace discovery is follow-up unless it can be implemented by parsing local workspace files without package-manager commands.
- MVP integration detectors are limited to `fetch`, `axios`, Express routes, Zod schemas, Prisma client calls, and `process.env` reads.
- MVP flow scope includes direct `ArgumentPassed` facts and simple local aliases only; field/property aliasing and derived `parameter_forward_edges` are follow-up.
- MVP SQLite compatibility requires `scan_manifest` and `facts` tables for reducer compatibility, plus `symbols` and `symbol_occurrences` when symbol facts are emitted. Derived flow tables are follow-up.
- MVP reduction is performed by the existing .NET command: `tracemap reduce --index <typescript index.sqlite> --contract-delta <delta.json> --out <report.md>`. There is no `tracemap-ts reduce` in MVP.

## Borrowed Lessons From `scip-typescript`

The local `scip-typescript` repo shows several patterns worth borrowing:

- Use TypeScript's own config loading APIs: `ts.readConfigFile`, `ts.parseJsonConfigFileContent`, `ts.parseCommandLine`, `ts.createProgram`, and `program.getTypeChecker()`.
- Index project references before the referencing project and de-duplicate visited projects.
- SCIP supports `jsconfig.json`, inferred config for JS-only repos, and workspace discovery; TraceMap should treat those as follow-up unless a local-file-only implementation is explicitly added.
- Cache parsed command lines and source files across projects when safe.
- Use package identity from nearest `package.json` as part of stable symbol identity.
- Skip very large files by a deterministic byte-size threshold and report the skip as an analysis gap.
- Keep indexing resilient: one failing file/project should not abort the entire scan.

TraceMap should not copy SCIP output as its primary product. SCIP is symbol-index oriented; TraceMap is evidence/reducer oriented. We should borrow compiler and project-loading techniques, then emit TraceMap facts with rule IDs, evidence tiers, spans, hashes, and known limitations.

## Requirements

### Requirement 1: TypeScript Scan CLI

**User Story:** As a reviewer, I want to scan a TypeScript repository with TraceMap so that contract-change reduction can use deterministic evidence from TypeScript code.

#### Acceptance Criteria

1. WHEN the user runs `tracemap-ts scan --repo <path> --out <path>` THEN the scanner SHALL write `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
2. WHEN the repo path does not exist THEN the scanner SHALL exit non-zero and not write a partial success manifest.
3. WHEN the repo is inside a Git checkout THEN the manifest SHALL include repo name, remote URL when available, branch when available, and commit SHA.
4. WHEN Git metadata is unavailable THEN the manifest SHALL use `commitSha: "unknown"` and emit an `AnalysisGap` fact.
5. WHEN output already exists THEN the scanner SHALL overwrite only the requested output path and SHALL not delete files outside that path.
6. WHEN scan completes with any project-load or semantic gaps THEN the manifest SHALL set `analysisLevel` to `Level1SemanticAnalysisReduced` or `Level3SyntaxAnalysis` as appropriate and SHALL include at least one `AnalysisGap` fact.
7. WHEN every selected TypeScript project loads semantically, `commitSha != "unknown"`, and no known gaps are emitted THEN the manifest SHALL set `analysisLevel = "Level1SemanticAnalysis"` and `buildStatus = "Succeeded"` for compatibility with the existing reducer's full-coverage gate.
8. WHEN semantic analysis is incomplete THEN the manifest SHALL set `buildStatus = "FailedOrPartial"` or `buildStatus = "NotRun"` consistently with the .NET scanner semantics.

### Requirement 2: Project and Workspace Discovery

**User Story:** As a maintainer, I want the TypeScript scanner to understand common Node repository layouts so that monorepos and single packages can be scanned predictably.

#### Acceptance Criteria

1. WHEN the repo contains explicit `tsconfig.json` files THEN the scanner SHALL use those projects for semantic analysis.
2. WHEN a config file declares project references THEN referenced projects SHALL be scanned before the referencing project and each project SHALL be scanned at most once per scan.
3. WHEN the repo contains npm, Yarn, pnpm, or package-json workspaces THEN workspace discovery SHALL be treated as follow-up unless implemented by parsing local workspace files without running external package-manager commands.
4. WHEN no config exists THEN the MVP scanner SHALL still run TypeScript syntax fallback over supported `.ts` and `.tsx` files and emit a project-load `AnalysisGap`.
5. WHEN JavaScript-only files are discovered THEN the MVP scanner SHALL inventory them structurally but SHALL NOT emit Tier1 semantic facts for them.
6. WHEN `--project <path>` is provided THEN semantic project loading SHALL be limited to the selected project(s).
7. WHEN `--include` or `--exclude` globs are provided THEN file inventory, syntax fallback, semantic extraction, and project loading SHALL obey those scope filters.
8. WHEN files are under `node_modules`, `.git`, TraceMap output directories, common build output directories, or package-manager caches THEN they SHALL be excluded by default.

### Requirement 3: File Inventory and Config Facts

**User Story:** As a reviewer, I want TypeScript repository structure, package metadata, and configuration files represented as evidence so that structural matches can support reducer findings.

#### Acceptance Criteria

1. WHEN supported files are discovered THEN the scanner SHALL emit deterministic `FileInventoried` facts with relative path, kind, size, rule ID, evidence tier, and extractor version.
2. WHEN `package.json` is discovered THEN the scanner SHALL emit package name/version facts and dependency facts without storing raw package JSON.
3. WHEN `package.json` scripts are scanned THEN script command values SHALL be hashed, not stored raw.
4. WHEN JSON, env-like, or framework config files are scanned THEN config key facts SHALL store key paths, value kind, hashes for sensitive values, and line spans.
5. WHEN config parsing fails THEN the scanner SHALL continue and emit an `AnalysisGap` fact with the parser error category.

### Requirement 4: Syntax Fallback

**User Story:** As a reviewer, I want useful TypeScript facts even when dependencies or configs are missing so that reduced scans still route me toward likely review points.

#### Acceptance Criteria

1. WHEN semantic analysis cannot load a project THEN the scanner SHALL parse supported source files with TypeScript syntax fallback.
2. WHEN syntax parse diagnostics occur THEN the scanner SHALL emit `AnalysisGap` facts and still emit recoverable syntax facts.
3. WHEN a recoverable declaration node is parsed THEN syntax fallback SHALL emit a fact with declaration kind, safe name, source span, and rule ID.
4. WHEN import, export, or decorator syntax is parsed THEN syntax fallback SHALL emit facts only for safe names and source spans.
5. WHEN invocation expressions are found THEN syntax fallback SHALL emit `InvocationName` and syntax `CallEdge` facts with containing function/class when available.
6. WHEN `new` expressions are found THEN syntax fallback SHALL emit `ObjectCreated` facts with created type syntax, constructor argument count, and assigned variable when obvious.
7. WHEN member access expressions are found THEN syntax fallback SHALL emit member-name facts without storing raw receiver expressions.
8. WHEN syntax facts need expression identity THEN they SHALL store expression kind and expression hash, not raw source text.

### Requirement 5: Semantic Symbol Facts

**User Story:** As a reviewer, I want TypeScript compiler-resolved facts so that contract deltas can be matched with stronger evidence than text search.

#### Acceptance Criteria

1. WHEN a TypeScript program loads and the checker resolves a symbol THEN semantic facts SHALL use `Tier1Semantic`.
2. WHEN type/interface/class/enum/function/method/property/parameter declarations are resolved THEN the scanner SHALL emit declaration facts with stable symbol IDs and fully qualified display names.
3. WHEN property reads or writes are resolved THEN the scanner SHALL emit property access/mutation facts with source symbol, target symbol, assembly/package identity equivalent, file span, and rule ID.
4. WHEN method/function calls are resolved THEN the scanner SHALL emit method invocation and semantic call-edge facts with caller, callee, package identity, argument count, and overload/signature identity when available.
5. WHEN constructor calls are resolved THEN the scanner SHALL emit object creation facts with created type, constructor symbol, caller symbol, package identity, and assignment target when obvious.
6. WHEN call-site arguments are resolved THEN the scanner SHALL emit `ArgumentPassed` facts with parameter name/type, argument symbol when available, expression kind, and expression hash; it SHOULD emit source declaration span when available.
7. WHEN imports or exports resolve to external packages THEN symbol identity SHALL include package name/version or `HEAD` fallback.
8. WHEN symbols are local-only THEN IDs SHALL be deterministic for the same file path and source span.

### Requirement 6: Symbol Relationships

**User Story:** As a reviewer, I want type and member relationships in TypeScript so that interface/class impact can be traversed across implementations and overrides.

#### Acceptance Criteria

1. WHEN a class extends another class THEN the scanner SHALL emit a `SymbolRelationship` fact with relationship kind `ExtendsClass`.
2. WHEN a class implements an interface THEN the scanner SHALL emit relationship kind `ImplementsInterface`.
3. WHEN an interface extends another interface THEN the scanner SHALL emit relationship kind `ExtendsInterface`.
4. WHEN a class member implements an interface member and TypeScript can resolve it THEN the scanner SHALL emit relationship kind `ImplementsInterfaceMember`.
5. WHEN a method is declared with the TypeScript `override` keyword and TypeScript resolves the base member THEN the scanner SHALL emit relationship kind `Overrides`.
6. WHEN `override` is absent but a method name matches a base member THEN the scanner SHALL NOT emit an override relationship.
7. WHEN relationships cannot be resolved because TypeScript uses structural typing or missing dependencies THEN the scanner SHALL not guess and SHOULD emit an analysis gap or limitation note where useful.

### Requirement 7: Integration and Contract-Mapping Evidence

**User Story:** As a platform engineer, I want common TypeScript service boundaries detected so that contract changes can route to API, persistence, serializer, and config code.

#### Acceptance Criteria

1. WHEN `fetch` or `axios` calls are found by identifier or call-expression shape only THEN the scanner SHALL emit Tier3 HTTP facts with method/URL literal hashes where present.
2. WHEN `fetch` or `axios` calls resolve to known package or lib symbols THEN the scanner SHALL emit Tier1 or Tier2 HTTP facts according to the available compiler/package evidence.
3. WHEN Express route declarations are found by structural call shape THEN the scanner SHALL emit Tier2 or Tier3 route mapping facts with route literal hashes and handler symbols when available.
4. WHEN Zod schema declarations are found THEN the scanner SHALL emit structural contract-mapping facts using existing reducer-compatible fact types and property keys where possible.
5. WHEN Prisma client calls are found THEN the scanner SHALL emit database boundary facts with compiler/package evidence tier when available.
6. WHEN `process.env` reads are found THEN the scanner SHALL emit config-use facts with `keyPath` or `memberName` populated when statically visible.
7. WHEN Nest, Fastify, Koa, Next.js, Remix, GraphQL, Yup, io-ts, class-validator, TypeBox, OpenAPI, TypeORM, Sequelize, Knex, Drizzle, or SQL template tags are encountered THEN MVP scanner MAY emit generic syntax facts but SHALL NOT claim specialized integration evidence until follow-up rules exist.
8. WHEN a boundary fact uses a string/template value THEN raw text SHALL not be stored; store literal kind, hash, length, and line span.

### Requirement 8: Flow and Logic Shape

**User Story:** As a reviewer, I want evidence about data flow and business-logic hotspots so that review can distinguish meaningful behavior from glue code.

#### Acceptance Criteria

1. WHEN a parameter is passed directly to another function or method THEN the MVP scanner SHALL emit `ArgumentPassed` facts.
2. WHEN simple local aliases are assigned from parameters or other symbols THEN the MVP scanner SHOULD emit local alias facts.
3. WHEN object fields/properties are assigned from parameters and later passed onward in the same function/class constructor pattern THEN the scanner SHALL treat this as follow-up work.
4. WHEN arithmetic, comparison-heavy, branch-heavy, retry/backoff, validation, or transformation syntax is found THEN the scanner SHALL emit Tier3 logic-shape facts with operators/kinds and expression hashes.
5. WHEN files match boilerplate patterns such as generated code, build output, framework entrypoints, routes-only glue, or dependency-injection registration THEN the scanner SHALL emit infrastructure/boilerplate facts.
6. WHEN flow cannot be proven across async callbacks, higher-order functions, decorators, dependency injection, dynamic import, reflection-like patterns, or structural typing ambiguity THEN the scanner SHALL not infer flow beyond recorded evidence.

### Requirement 9: SQLite and Reducer Compatibility

**User Story:** As a user of TraceMap, I want TypeScript indexes to work with existing reduction/reporting concepts so that contract deltas can be reduced across languages.

#### Acceptance Criteria

1. WHEN TypeScript scan completes THEN `facts.ndjson` SHALL use the same top-level fact schema and JSON casing as .NET facts.
2. WHEN a TypeScript fact is intended to participate in existing reducer classification THEN it SHALL reuse existing `FactTypes` strings verbatim.
3. WHEN a TypeScript fact is intended to be `DefiniteImpact` eligible THEN it SHALL use one of the reducer-recognized fact types: `PropertyAccessed`, `MethodInvoked`, or `TypeDeclared`.
4. WHEN a TypeScript fact is intended to be `ProbableImpact` eligible THEN it SHALL use existing structural or probable semantic fact types such as `HttpRouteBinding`, `ConfigKeyDeclared`, `ConnectionStringDeclared`, `DbContextDeclared`, `DbSetDeclared`, `SqlTextUsed`, `HttpCallDetected`, `DapperCallDetected`, `DbChangeSaved`, `SerializationLogic`, or other fact types already recognized by `ContractDeltaReducer`.
5. WHEN emitting reducer-compatible facts THEN the scanner SHALL populate existing camelCase matching keys used by the reducer, including `propertyName`, `memberName`, `fieldName`, `methodName`, `keyPath`, `name`, `containingType`, `className`, `typeName`, `namespace`, and `targetSymbol` where applicable.
6. WHEN `index.sqlite` is written for MVP THEN it SHALL include `scan_manifest` and `facts` tables readable by the existing .NET reducer without reducer changes.
7. WHEN symbols are emitted in MVP THEN SQLite SHALL include `symbols` and `symbol_occurrences`; derived flow tables MAY be deferred until the corresponding facts exist.
8. WHEN TypeScript-specific data is needed THEN schema additions SHALL be additive and documented.
9. WHEN the existing .NET reducer runs against a TypeScript index THEN it SHALL classify TypeScript semantic property/type/method matches using the same classifications as .NET facts.
10. WHEN facts contain language-specific symbol IDs THEN the symbol table SHALL include `language = 'typescript'`.

### Requirement 10: Determinism, Safety, and Performance

**User Story:** As a maintainer, I want TypeScript scanning to be deterministic, bounded, and safe for large repos.

#### Acceptance Criteria

1. WHEN the same repo commit and scan options are scanned twice THEN stable fact IDs SHALL match except for explicitly time-stamped manifest fields.
2. WHEN dependencies are missing THEN the scanner SHALL continue with reduced semantic coverage and syntax fallback.
3. WHEN a file exceeds the max byte-size threshold THEN the scanner SHALL skip semantic/syntax extraction for that file and emit an `AnalysisGap`.
4. WHEN worker/project indexing fails unexpectedly THEN the scanner SHALL record the failure and continue to remaining projects when possible.
5. WHEN paths are emitted THEN they SHALL be normalized repo-relative paths.
6. WHEN evidence spans are emitted THEN they SHALL use one-based line numbers and deterministic end-line calculation.
7. WHEN scanner logs include diagnostics THEN logs SHALL avoid raw source snippets unless an explicit future raw-snippet option is added.

## Required Property-Key Contract

The TypeScript scanner SHALL use these existing property keys when emitting reducer-compatible facts:

| Fact type | Required matching keys |
| --- | --- |
| `TypeDeclared` | `name`, `typeName`, `namespace`, `targetSymbol` |
| `PropertyAccessed` | `propertyName`, `containingType`, `targetSymbol` |
| `MethodInvoked` | `methodName`, `containingType`, `targetSymbol` |
| `FieldDeclared` | `fieldName`, `fieldType`, `containingType`, `targetSymbol` |
| `ParameterDeclared` | `parameterName`, `parameterType`, `sourceSymbol` |
| `ConfigKeyDeclared` | `keyPath`, `name`, `targetSymbol` |
| `HttpRouteBinding` | `routePatternHash`, `methodName`, `targetSymbol`, `contractElement` |
| `HttpCallDetected` | `methodName`, `targetSymbol`, `contractElement` |
| `SqlTextUsed` | `textHash`, `textLength`, `targetSymbol` |
| `SerializationLogic` | `operationName`, `targetSymbol`, `contractElement` |
| `SymbolRelationship` | `relationshipKind`, `sourceSymbol`, `targetSymbol`, `sourceSymbolId`, `targetSymbolId` |

## TypeScript-Specific Limitations

- TypeScript scanner `buildStatus = "Succeeded"` means semantic analysis succeeded; it does not mean target repository tests, build scripts, or bundlers succeeded.
- Missing `node_modules` or unresolved path mappings commonly reduce semantic coverage.
- JavaScript files are not part of MVP semantic coverage and SHALL NOT produce Tier1 semantic facts in MVP.
- `.d.ts` files may emit declaration facts but SHALL be marked as declaration-source evidence; they do not prove runtime implementation.
- Decorator execution, dependency injection, framework runtime routing, structural-type equivalence, dynamic imports, and runtime monkey-patching are not proven.
- Declaration merging and overloads can produce duplicate compiler surfaces; symbol de-duplication must be deterministic and conservative.
- Symbol IDs are stable for the same scan inputs but are not guaranteed stable across file renames, package renames, package version changes, or compiler binding changes.
