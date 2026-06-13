# Python Indexer Requirements

## Introduction

TraceMap needs a deterministic Python repository scanner that produces the same evidence-backed artifacts as the .NET, TypeScript, and JVM adapters. The scanner should use Python-native static analysis, type-checker metadata where available, and syntax fallback when project or dependency context is incomplete.

This spec intentionally does not add LLMs, embeddings, vector stores, runtime imports, app startup, traffic capture, or prompt-based classification to the scanner or reducer. The Python scanner must preserve TraceMap's core contract: no conclusion without evidence, no evidence without a rule ID, and no raw source snippets by default.

The implementation target is a new sibling package under `src/python`, with compatible scan artifacts and SQLite schema so existing `.NET` `tracemap reduce`, `tracemap export`, `tracemap combine`, and endpoint-alignment commands can read Python indexes.

## MVP Scope Decisions

- MVP language scope is `.py` plus selected local project metadata files: `pyproject.toml`, `setup.cfg`, `setup.py`, `requirements*.txt`, `Pipfile`, `poetry.lock`, `uv.lock`, and common config files. `.ipynb` and generated stubs are follow-up.
- MVP semantic scope starts with Python `ast` and deterministic project/package metadata. Optional type-checker integration using Pyright/Microsoft language-server metadata or MyPy cache is follow-up unless implemented without executing target code.
- MVP framework detectors target FastAPI routes, Flask routes, Django URL patterns/views, Pydantic models, dataclasses, SQLAlchemy ORM/core, direct SQL literals, environment/config reads, and HTTP client calls through `requests`, `httpx`, `aiohttp`, and `urllib`.
- MVP flow scope includes direct `ArgumentPassed`, `ObjectCreated`, `CallEdge`, and simple local alias facts from AST. Interprocedural flow beyond direct call arguments is follow-up.
- MVP SQL scope emits shared SQL evidence facts from Python sources and SQL/migration files, but does not implement a full dialect parser in the Python adapter. Deeper SQL normalization is a shared cross-language layer.
- MVP reduction is performed by existing `.NET` command: `tracemap reduce --index <python index.sqlite> --contract-delta <delta.json> --out <report.md>`.

## Python-Specific Constraints

Python is highly dynamic. The scanner must not claim runtime behavior from:

- dynamic imports, import hooks, monkey patching, module-level side effects, metaclasses, decorators with runtime logic, dependency injection containers, plugin discovery, environment-dependent settings, or route registration behind branches.
- framework startup, ASGI/WSGI router inclusion order, middleware behavior, auth policies, reverse proxies, or deployment base paths.
- SQLAlchemy runtime engine/session binding, lazy relationships, hybrid properties, or query construction hidden in functions the scanner cannot see.

When these patterns are statically visible and relevant, the scanner should emit explicit `AnalysisGap` or boundary facts instead of guessing.

## Requirements

### Requirement 1: Python Scan CLI

**User Story:** As a reviewer, I want to scan a Python repository with TraceMap so that contract-change reduction and dependency reports can use deterministic Python evidence.

#### Acceptance Criteria

1. WHEN the user runs `tracemap-py scan --repo <path> --out <path>` THEN the scanner SHALL write `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
2. WHEN the repo path does not exist THEN the scanner SHALL exit non-zero and not write a partial success manifest.
3. WHEN the repo is inside a Git checkout THEN the manifest SHALL include repo name, remote URL when available, branch when available, and commit SHA.
4. WHEN Git commit metadata is unavailable THEN the scanner SHALL fail before writing scan artifacts.
5. WHEN output already exists THEN the scanner SHALL overwrite only the requested output path and SHALL not delete files outside that path.
6. WHEN static analysis has parser, import-resolution, project-load, type-context, skipped-file, or unsupported-feature gaps THEN the manifest SHALL set reduced coverage and emit at least one `AnalysisGap` fact.
7. WHEN the MVP scanner uses AST/package/config evidence without a type-checker-backed semantic pass THEN it SHALL NOT set `analysisLevel = "Level1SemanticAnalysis"`; it SHALL use `Level1SemanticAnalysisReduced` for useful AST/structural evidence or `Level3SyntaxAnalysis` for syntax/textual-only coverage.
8. WHEN type-checker semantic extraction is absent or disabled THEN the manifest SHALL not claim compiler-grade type semantics; AST facts SHALL use `Tier2Structural` or `Tier3SyntaxOrTextual` as appropriate.
9. WHEN the user runs `tracemap-py --help`, `tracemap-py scan --help`, or `tracemap-py --version` THEN the CLI SHALL print deterministic usage/version output and exit successfully.

Python coverage mapping SHALL be conservative:

| Condition | `analysisLevel` | `buildStatus` |
| --- | --- | --- |
| MVP AST/package/config evidence, selected files parse, concrete commit SHA, and useful structural facts emitted | `Level1SemanticAnalysisReduced` | `FailedOrPartial` |
| Future type-checker-backed selected scope with no known gaps and concrete commit SHA | `Level1SemanticAnalysis` | `Succeeded` |
| Any parse/import/project/config/skipped-file/framework gap exists but useful facts are emitted | `Level1SemanticAnalysisReduced` | `FailedOrPartial` |
| AST parse is disabled, no supported files exist, or only textual inventory/config facts are emitted | `Level3SyntaxAnalysis` | `NotRun` |

### Requirement 2: Project and Package Discovery

**User Story:** As a maintainer, I want the Python scanner to understand common Python layouts so that packages, apps, and monorepos can be scanned predictably.

#### Acceptance Criteria

1. WHEN `pyproject.toml` is present THEN the scanner SHALL parse project name/version, dependencies, optional dependencies, build backend, tool sections relevant to static analysis, and source package hints when statically visible.
2. WHEN `setup.cfg`, `setup.py`, `requirements*.txt`, `Pipfile`, `poetry.lock`, or `uv.lock` are present THEN the scanner SHALL emit package/dependency facts from literal metadata only and mark dynamic setup execution as a gap.
3. WHEN package roots are discoverable through `src/`, package directories with `__init__.py`, or configured package-dir metadata THEN the scanner SHALL use those roots for module identity.
4. WHEN namespace packages or editable installs are required to resolve imports THEN the scanner SHALL emit reduced coverage unless the path mapping is statically visible.
5. WHEN `--project <path>` is provided THEN inventory and extraction SHALL be limited to the selected project/config/package scope.
6. WHEN `--include` or `--exclude` globs are provided THEN file inventory, syntax fallback, and extraction SHALL obey those filters.
7. WHEN files are under `.git`, virtualenvs, `.venv`, `venv`, `env`, `site-packages`, `__pycache__`, `.mypy_cache`, `.pytest_cache`, build/dist outputs, generated outputs, or TraceMap output directories THEN they SHALL be excluded by default unless explicitly included.

### Requirement 3: File Inventory, Package, and Config Facts

**User Story:** As a reviewer, I want Python repository structure and config represented as evidence so that structural matches can support reducer findings.

#### Acceptance Criteria

1. WHEN supported files are discovered THEN the scanner SHALL emit deterministic `FileInventoried` facts with relative path, kind, size, rule ID, evidence tier, and extractor version.
2. WHEN package metadata files are discovered THEN the scanner SHALL emit package/module identity and dependency facts without storing raw metadata snippets.
3. WHEN dependency versions are literal or lockfile-proven THEN the scanner SHALL store package name/version/source file evidence.
4. WHEN dependency versions are dynamic, inherited from environment, or require executing setup code THEN the scanner SHALL store safe package names when possible and record a gap.
5. WHEN `.env`, `.ini`, `.cfg`, `.toml`, `.yaml`, `.json`, and framework config files are scanned THEN config key facts SHALL store key paths, value kind, hashes for sensitive values, and line spans.
6. WHEN config parsing fails THEN the scanner SHALL continue and emit an `AnalysisGap` fact with parser category.

### Requirement 4: Syntax and AST Fallback

**User Story:** As a reviewer, I want useful Python facts even when dependency or type information is missing.

#### Acceptance Criteria

1. WHEN a `.py` file parses with Python `ast` THEN the scanner SHALL emit declaration, call, object creation, route, model, SQL/config, and logic-shape facts where patterns are statically visible.
2. WHEN syntax parse diagnostics occur THEN the scanner SHALL emit `AnalysisGap` facts and still emit recoverable textual/inventory facts.
3. WHEN modules, classes, functions, async functions, methods, dataclasses, enums, and assignments are parsed THEN the scanner SHALL emit declaration facts with safe names, module path, line spans, and rule IDs.
4. WHEN decorators are parsed THEN the scanner SHALL emit decorator facts with safe names and hashed literal arguments; it SHALL NOT execute decorators.
5. WHEN invocation expressions are found THEN syntax fallback SHALL emit `InvocationName` and `CallEdge` facts with containing module/function/class when available.
6. WHEN constructor-like calls are found THEN syntax fallback SHALL emit `ObjectCreated` facts with created type syntax, constructor argument count, and assigned variable when obvious.
7. WHEN attribute access expressions are found THEN syntax fallback SHALL emit member-name facts without storing raw receiver expressions.
8. WHEN syntax facts need expression identity THEN they SHALL store expression kind and expression hash, not raw source text.

### Requirement 5: Python Symbol Identity

**User Story:** As a reviewer, I want stable Python symbol IDs so facts can be combined across indexes and compared across scans.

#### Acceptance Criteria

1. WHEN a module path is resolved relative to a package root THEN symbol IDs SHALL include language, package/project identity, module path, qualified symbol path, and local span where needed.
2. WHEN package name/version is available from metadata or lockfiles THEN symbol properties SHALL include package name/version for source and dependency identity.
3. WHEN function, method, class, attribute, and parameter declarations are parsed THEN declaration facts SHALL include stable display names and symbol IDs.
4. WHEN imports resolve to in-repo modules by static path rules THEN facts SHALL include target module symbol identity.
5. WHEN imports are external, dynamic, optional, or hidden behind `importlib`, `__import__`, or plugin loading THEN the scanner SHALL not guess exact targets and SHOULD emit boundary or gap evidence.
6. WHEN type annotations are present THEN the scanner SHALL record annotation names/hash-safe shape as structural evidence; it SHALL not claim runtime type enforcement.
7. WHEN `.pyi` stubs or type-checker metadata are used in a future slice THEN the source of type evidence SHALL be recorded and coverage SHALL remain reduced if stubs disagree with source or are incomplete.

### Requirement 6: Framework and API Boundary Evidence

**User Story:** As a platform engineer, I want Python service boundaries detected so contract and endpoint changes can route to API, persistence, serializer, and config code.

#### Acceptance Criteria

1. WHEN FastAPI decorators such as `@app.get`, `@router.post`, or `APIRouter` route methods are found with literal paths THEN the scanner SHALL emit `HttpRouteBinding` facts with HTTP method, normalized path key, handler symbol, router/app variable when available, and evidence tier based on package/import evidence.
2. WHEN Flask decorators such as `@app.route`, `@blueprint.route`, `@app.get`, or `@app.post` are found THEN the scanner SHALL emit `HttpRouteBinding` facts with route shape and handler symbol when literal paths are visible.
3. WHEN Django `path`, `re_path`, `include`, class-based views, or view function mappings are found in URL config files THEN the scanner SHALL emit route facts when pattern and view target are statically visible; `include` expansion is a follow-up unless the included module path resolves statically.
4. WHEN `requests`, `httpx`, `aiohttp`, or `urllib` client calls are found with literal method/path/URL components THEN the scanner SHALL emit `HttpCallDetected` facts with normalized path key when possible and hashed raw URL details.
5. WHEN Pydantic `BaseModel`, dataclasses, TypedDicts, attrs classes, or serializer/schema declarations are found THEN the scanner SHALL emit `TypeDeclared`, `FieldDeclared`, and `SerializerContractMember`/schema facts where field names are statically visible.
6. WHEN SQLAlchemy declarative models, mapped columns, relationships, tables, or repository/query calls are found THEN the scanner SHALL emit `DatabaseColumnMapping`, `SqlTextUsed`, and query boundary facts where statically visible.
7. WHEN direct SQL literals are passed to DB-API, SQLAlchemy `text`, pandas SQL helpers, or migration helpers THEN the scanner SHALL emit SQL facts with operation name, text hash, length, and source span.
8. WHEN environment/config reads through `os.environ`, `os.getenv`, `decouple.config`, Dynaconf, Pydantic settings, Django settings, or Flask config are found THEN the scanner SHALL emit config-use facts with `keyPath` or hashed dynamic expression evidence.
9. WHEN framework registration is dynamic, computed, imported through wildcard, or hidden in runtime plugin discovery THEN the scanner SHALL emit reduced evidence or gaps rather than claiming endpoint or dependency reachability.

### Requirement 7: Relationships, Flow, and Logic Shape

**User Story:** As a reviewer, I want Python flow and logic facts so review can distinguish business logic from framework glue.

#### Acceptance Criteria

1. WHEN a class inherits from statically named base classes THEN the scanner SHALL emit `SymbolRelationship` with relationship kind `ExtendsClass`.
2. WHEN a method overrides a base method by name only THEN the scanner SHALL NOT emit `Overrides` unless a future type-aware extractor proves the base member.
3. WHEN a parameter is passed directly to another function, method, constructor, or framework boundary THEN the scanner SHALL emit `ArgumentPassed` with parameter/argument names when available.
4. WHEN simple local aliases are assigned from parameters or other variables THEN the scanner SHOULD emit `LocalAlias` facts.
5. WHEN instance attributes are assigned from constructor parameters and later passed onward in a low-ambiguity pattern THEN field aliasing MAY be a follow-up, but MVP SHALL not infer broad object state flow.
6. WHEN arithmetic, comparison-heavy, branch-heavy, validation, transformation, retry/backoff, collection-processing, date/time, or data-shaping logic is found THEN the scanner SHALL emit Tier3 logic-shape facts with operators/kinds and expression hashes.
7. WHEN files match boilerplate patterns such as generated code, migrations, settings modules, FastAPI/Flask/Django app wiring, test fixtures, or DTO-only modules THEN the scanner SHALL emit infrastructure/boilerplate facts.
8. WHEN flow crosses async tasks, callbacks, dependency injection, decorators, monkey patches, runtime imports, ORM lazy loading, serializer internals, or collection contents THEN the scanner SHALL not infer flow beyond recorded evidence.

### Requirement 8: SQL as Shared Evidence

**User Story:** As a data/platform engineer, I want Python SQL usage to line up with SQL evidence from other languages and database artifacts.

#### Acceptance Criteria

1. WHEN Python emits SQL facts THEN it SHALL use shared fact types and properties compatible with .NET, TypeScript, and JVM SQL evidence.
2. WHEN SQL text is found THEN raw SQL SHALL not be stored by default; store hash, length, operation kind, line span, and optional normalized table/column names when a deterministic shared parser exists.
3. WHEN `.sql`, Alembic, Django migration, or SQLAlchemy migration files are discovered THEN they SHALL be inventoried and scanned for SQL text/resource facts.
4. WHEN a future shared SQL parser extracts tables, columns, joins, procedure calls, views, or write operations THEN Python shall attach those derived facts to the original source fact IDs and rule IDs.
5. WHEN SQL is dynamically assembled from f-strings, string concatenation, templates, or ORM expression builders THEN the scanner SHALL emit dynamic SQL boundary evidence and avoid guessing concrete runtime query text.

### Requirement 9: SQLite and Reducer Compatibility

**User Story:** As a user of TraceMap, I want Python indexes to work with existing reduction/reporting concepts so contract deltas can be reduced across languages.

#### Acceptance Criteria

1. WHEN Python scan completes THEN `facts.ndjson` SHALL use the same top-level fact schema and JSON casing as existing adapters.
2. WHEN a Python fact is intended to participate in existing reducer classification THEN it SHALL reuse existing `FactTypes` strings verbatim.
3. WHEN a Python fact is intended to be `DefiniteImpact` eligible THEN it SHALL use one of the reducer-recognized fact types: `PropertyAccessed`, `MethodInvoked`, or `TypeDeclared`.
4. WHEN a Python fact is intended to be `ProbableImpact` eligible THEN it SHALL use existing structural/probable fact types such as `HttpRouteBinding`, `HttpCallDetected`, `ConfigKeyDeclared`, `SqlTextUsed`, `DatabaseColumnMapping`, `SerializerContractMember`, `SymbolRelationship`, or `DependencyRegistered` where evidence supports those meanings.
5. WHEN emitting reducer-compatible facts THEN the scanner SHALL populate existing camelCase matching keys, including `propertyName`, `memberName`, `fieldName`, `methodName`, `keyPath`, `name`, `containingType`, `className`, `typeName`, `namespace`, and `targetSymbol` where applicable.
6. WHEN `index.sqlite` is written THEN it SHALL include `scan_manifest` and `facts` tables readable by the existing .NET reducer without reducer changes.
7. WHEN symbols, relationships, calls, object creation, and flow evidence are emitted THEN SQLite SHALL include `symbols`, `symbol_occurrences`, `fact_symbols`, `symbol_relationships`, `call_edges`, `object_creations`, `argument_flows`, and `local_aliases` where supported.
8. WHEN Python-specific data is needed THEN schema additions SHALL be additive and documented.
9. WHEN facts contain Python symbol IDs THEN the symbol table SHALL include `language = 'python'`.
10. WHEN endpoint, SQL, schema, or config facts are emitted THEN the scanner SHALL populate `contractElement`, `name`, `methodName`, `containingType`, `targetSymbol`, or other reducer-matched plain keys when a `Type.member` contract element is known.

### Requirement 10: Determinism, Safety, and Performance

**User Story:** As a maintainer, I want Python scanning to be deterministic, bounded, and safe for large repos.

#### Acceptance Criteria

1. WHEN the same repo commit and scan options are scanned twice THEN stable fact IDs SHALL match except for explicitly time-stamped manifest fields.
2. WHEN dependencies are missing or imports cannot resolve THEN the scanner SHALL continue with reduced coverage and syntax/AST facts.
3. WHEN a file exceeds the max byte-size threshold THEN the scanner SHALL skip extraction for that file and emit an `AnalysisGap`.
4. WHEN parser or worker failures occur THEN the scanner SHALL record bounded diagnostics and continue to remaining files when possible.
5. WHEN paths are emitted THEN they SHALL be normalized repo-relative paths.
6. WHEN evidence spans are emitted THEN they SHALL use one-based line numbers and deterministic end-line calculation.
7. WHEN scanner logs include diagnostics THEN logs SHALL avoid raw source snippets unless an explicit future raw-snippet option is added.
8. WHEN package metadata or lockfiles are huge THEN parsing SHALL be bounded and failure SHALL produce reduced evidence rather than process failure.

## Required Property-Key Contract

The Python scanner SHALL use these existing property keys when emitting reducer-compatible facts:

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
| `SqlTextUsed` | `textHash`, `textLength`, `operationName`, `targetSymbol` |
| `SerializerContractMember` | `contractName`, `memberName`, `containingType`, `targetSymbol` |

## Out of Scope for MVP

- executing target Python modules, importing app code, running framework startup, or evaluating decorators.
- full type-checker semantics, cross-module type inference, or proving dynamic dispatch.
- Jupyter notebook parsing.
- full SQL dialect parsing; shared SQL normalization is a separate cross-language layer.
- route reachability through runtime router inclusion, middleware, settings, feature flags, or deployment config.
- GraphQL, Celery deep task graph traversal, Airflow DAG semantics, Pandas schema inference, Spark lineage, or LLM/agent framework tracing.
