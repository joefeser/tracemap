# Python Indexer Requirements

## Introduction

TraceMap needs a deterministic Python repository scanner that produces the same evidence-backed artifacts as the .NET, TypeScript, and JVM adapters. The scanner should use Python-native static analysis and local package/config metadata first. Type-checker metadata can be added later, but the MVP must not claim compiler-grade semantics from AST-only evidence.

This spec intentionally does not add LLMs, embeddings, vector stores, runtime imports, app startup, traffic capture, or prompt-based classification to the scanner or reducer. The Python scanner must preserve TraceMap's core contract: no conclusion without evidence, no evidence without a rule ID, and no raw source snippets by default.

The implementation target is a new sibling package under `src/python`, with compatible scan artifacts and SQLite schema so existing `.NET` `tracemap reduce`, `tracemap export`, `tracemap combine`, and endpoint-alignment commands can read Python indexes.

MVP local installation is from source:

```bash
cd src/python
python -m pip install -e .
tracemap-py scan --repo ./my-fastapi-app --out ./tracemap-output
tracemap reduce --index ./tracemap-output/index.sqlite --contract-delta delta.json --out report.md
```

Publishing to PyPI and root `tracemap` dispatch are follow-up distribution work.

## MVP Scope Decisions

- MVP language scope is `.py` plus selected local project metadata files: `pyproject.toml`, `setup.cfg`, `setup.py`, `requirements*.txt`, and common config/SQL/migration files. `.ipynb`, generated stubs, `.pyi`, Poetry lock parsing, and uv lock parsing are follow-up.
- MVP scanner runtime is Python 3.11 or newer so `tomllib` is available. Target repositories may contain older or newer Python syntax; unsupported syntax produces reduced coverage.
- MVP semantic scope is Python `ast`, deterministic project/package metadata, and conservative import/path resolution. Pyright, MyPy, language-server, and cross-module type inference are follow-up.
- MVP framework detectors target FastAPI routes, Flask routes, Pydantic `BaseModel`, dataclasses, SQLAlchemy declarative column declarations, direct SQL literals, environment/config reads, and HTTP client calls through `requests` and `httpx`.
- MVP flow scope includes direct `ArgumentPassed`, `ObjectCreated`, `CallEdge`, and simple local alias facts from AST. Interprocedural flow beyond direct call arguments is follow-up.
- MVP SQL scope emits shared SQL evidence facts from Python sources and SQL/migration files, but does not implement a full dialect parser in the Python adapter. Deeper SQL normalization is a shared cross-language layer.
- MVP reducer integration proves real Python AST/structural facts classify as `ProbableImpact` or `NeedsReview`. `DefiniteImpact` is tested only with a synthetic fixture containing hand-authored Tier1 facts, not from real AST-only Python extraction.
- MVP reduction is performed by existing `.NET` command: `tracemap reduce --index <python index.sqlite> --contract-delta <delta.json> --out <report.md>`.

## Python-Specific Constraints

Python is highly dynamic. The scanner must not claim runtime behavior from:

- dynamic imports, import hooks, monkey patching, module-level side effects, metaclasses, descriptors, `__getattr__`, `__getattribute__`, `__init_subclass__`, dependency injection containers, plugin discovery, environment-dependent settings, or route registration behind branches.
- decorator side effects, including `@property`, `cached_property`, validators, computed fields, `lru_cache`, and arbitrary decorator call logic.
- framework startup, ASGI/WSGI router inclusion order, middleware behavior, auth policies, reverse proxies, deployment base paths, context managers, async context managers, callbacks, async generators, or branch feasibility.
- SQLAlchemy runtime engine/session binding, lazy relationships, hybrid properties, runtime relationship targets, or query construction hidden in functions the scanner cannot see.
- `__all__`, wildcard imports, `TYPE_CHECKING` imports, `__init__.py` re-export chains, namespace-package ambiguity, or import-time aliases as proof of runtime availability.
- Pydantic validators, `model_validator`, `field_validator`, `computed_field`, class-body transformations produced by decorator execution, `Annotated[...]` metadata beyond a direct `Annotated[T, Field(...)]` pattern, and `__slots__` as runtime field definitions.

When these patterns are statically visible and relevant, the scanner should emit explicit `AnalysisGap` or boundary facts instead of guessing. Facts from `TYPE_CHECKING` blocks may be emitted only as Tier3 evidence with `typeCheckingOnly = true`.

## Requirements

### Requirement 1: Python Scan CLI

**User Story:** As a reviewer, I want to scan a Python repository with TraceMap so that contract-change reduction and dependency reports can use deterministic Python evidence.

#### Acceptance Criteria

1. WHEN the user runs `tracemap-py scan --repo <path> --out <path>` THEN the scanner SHALL write `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
2. WHEN the repo path does not exist THEN the scanner SHALL exit non-zero and not write a partial success manifest.
3. WHEN the repo is inside a Git checkout THEN the manifest SHALL include repo name, remote URL when available, branch when available, and commit SHA.
4. WHEN Git commit metadata is unavailable THEN the scanner SHALL fail before writing scan artifacts.
5. WHEN output already exists THEN the scanner SHALL drop and rebuild only the requested output path contents and SHALL not delete files outside that path.
6. WHEN static analysis has parser, import-resolution, project-load, type-context, skipped-file, unsupported-feature, or framework gaps THEN the manifest SHALL set reduced coverage and emit at least one `AnalysisGap` fact.
7. WHEN computing coverage THEN the scanner SHALL follow the Python coverage mapping table below and SHALL NOT claim `Level1SemanticAnalysis` without type-checker-backed semantic coverage.
8. WHEN type-checker semantic extraction is absent or disabled THEN the manifest SHALL not claim compiler-grade type semantics; AST facts SHALL use `Tier2Structural` or `Tier3SyntaxOrTextual` as appropriate.
9. WHEN the user runs `tracemap-py --help`, `tracemap-py scan --help`, or `tracemap-py --version` THEN the CLI SHALL print deterministic usage/version output and exit successfully.
10. WHEN users need contract reduction, export, combine, or endpoint reports THEN they SHALL run the existing `.NET` `tracemap` commands against the Python `index.sqlite`; the Python adapter SHALL NOT reimplement those downstream commands.

Python coverage mapping SHALL be conservative:

| Condition | `analysisLevel` | `buildStatus` |
| --- | --- | --- |
| MVP AST/package/config evidence, selected files parse, concrete commit SHA, and useful structural facts emitted | `Level1SemanticAnalysisReduced` | `FailedOrPartial` |
| Future type-checker-backed selected scope with no known gaps and concrete commit SHA | `Level1SemanticAnalysis` | `Succeeded` |
| Any parse/import/project/config/skipped-file/framework gap exists but useful facts are emitted | `Level1SemanticAnalysisReduced` | `FailedOrPartial` |
| AST parse is disabled, no supported files exist, or only textual inventory/config facts are emitted | `Level3SyntaxAnalysis` | `NotRun` |

`FailedOrPartial` means "not full semantic coverage" for Python MVP. It does not mean a target project build was attempted and failed.

### Requirement 2: Project and Package Discovery

**User Story:** As a maintainer, I want the Python scanner to understand common Python layouts so that packages, apps, and monorepos can be scanned predictably.

#### Acceptance Criteria

1. WHEN `pyproject.toml` is present THEN the scanner SHALL parse project name/version, dependencies, optional dependencies, build backend, tool sections relevant to static analysis, and source package hints when statically visible.
2. WHEN `setup.cfg` or `requirements*.txt` are present THEN the scanner SHALL emit package/dependency facts from literal metadata only.
3. WHEN `setup.py` is present THEN the scanner SHALL treat it as dynamic unless literal metadata can be extracted without executing code.
4. WHEN package roots are discoverable through `src/`, package directories with `__init__.py`, or configured package-dir metadata THEN the scanner SHALL use those roots before computing module and symbol identity.
5. WHEN dependency entries are VCS URLs, editable installs, or path installs THEN the scanner SHALL emit package boundary facts with reference kind and hashed reference value, and SHALL NOT claim a semver package version.
6. WHEN namespace packages, flat-layout/src-layout collisions, relative imports, or editable installs are required to resolve imports THEN the scanner SHALL emit reduced coverage unless the path mapping is statically visible.
7. WHEN `--project <path>` is provided once or multiple times THEN the scanner SHALL write one index for the selected scan scope, SHALL include facts for each selected project scope, and SHALL include `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash` in the manifest.
8. WHEN `--include` or `--exclude` globs are provided THEN file inventory, syntax fallback, and extraction SHALL obey those filters.
9. WHEN files are under `.git`, virtualenvs, `.venv`, `venv`, `env`, `site-packages`, `__pycache__`, `.mypy_cache`, `.pytest_cache`, build/dist outputs, generated outputs, or TraceMap output directories THEN they SHALL be excluded by default unless explicitly included.

### Requirement 3: File Inventory, Package, and Config Facts

**User Story:** As a reviewer, I want Python repository structure and config represented as evidence so that structural matches can support reducer findings.

#### Acceptance Criteria

1. WHEN supported files are discovered THEN the scanner SHALL emit deterministic `FileInventoried` facts with relative path, kind, size, rule ID, evidence tier, and extractor version.
2. WHEN package metadata files are discovered THEN the scanner SHALL emit package/module identity and dependency facts without storing raw metadata snippets.
3. WHEN dependency versions are literal in supported metadata THEN the scanner SHALL store package name/version/source file evidence.
4. WHEN dependency versions are dynamic, inherited from environment, or require executing setup code THEN the scanner SHALL store safe package names when possible and record a gap.
5. WHEN `.env`, `.ini`, `.cfg`, `.toml`, `.yaml`, `.json`, and Flask config files are scanned THEN config key facts SHALL store key paths, value kind, hashes for sensitive values, and line spans.
6. WHEN config parsing fails THEN the scanner SHALL continue and emit an `AnalysisGap` fact with parser category.

### Requirement 4: Syntax and AST Fallback

**User Story:** As a reviewer, I want useful Python facts even when dependency or type information is missing.

#### Acceptance Criteria

1. WHEN a `.py` file parses with Python `ast` THEN the scanner SHALL emit declaration, import, invocation-name, call-edge, object-creation, route, model, SQL/config, and direct flow facts where patterns are statically visible.
2. WHEN syntax parse diagnostics occur THEN the scanner SHALL emit `AnalysisGap` facts and still emit recoverable textual/inventory facts.
3. WHEN modules, classes, functions, async functions, methods, dataclasses, enums, and assignments are parsed THEN the scanner SHALL emit declaration facts with safe names, module path, line spans, and rule IDs.
4. WHEN decorators are parsed THEN the scanner SHALL support bare name, attribute, and call decorators, emit decorator facts with safe names and hashed literal arguments, and SHALL NOT execute decorators.
5. WHEN invocation expressions are found THEN syntax fallback SHALL emit `InvocationName` and `CallEdge` facts with containing module/function/class when available.
6. WHEN constructor-like calls are found THEN syntax fallback SHALL emit `ObjectCreated` facts with created type syntax, constructor argument count, and assigned variable when obvious.
7. WHEN attribute access expressions are found THEN syntax fallback SHALL emit member-name facts without storing raw receiver expressions.
8. WHEN `match`, walrus, async functions, or other supported Python 3 AST nodes are found THEN the scanner SHALL parse them when the scanner runtime supports them and degrade to gaps otherwise.
9. WHEN syntax facts need expression identity THEN they SHALL store expression kind and expression hash, not raw source text.
10. WHEN simple aliases are emitted THEN MVP SHALL limit them to `Name = Name` and `Name = Attribute.Name` assignment shapes; tuple unpacking, augmented assignment, walrus aliases, comprehensions, and mutation aliases SHALL be gaps or omitted.

### Requirement 5: Python Symbol Identity

**User Story:** As a reviewer, I want stable Python symbol IDs so facts can be combined across indexes and compared across scans.

#### Acceptance Criteria

1. WHEN a module path is resolved relative to a package root THEN symbol IDs SHALL include language, package/project identity, module path, qualified symbol path, and local span where needed.
2. WHEN package name/version is available from metadata THEN symbol properties SHALL include package name/version for source and dependency identity.
3. WHEN function, method, class, attribute, and parameter declarations are parsed THEN declaration facts SHALL include stable display names and symbol IDs.
4. WHEN imports resolve to in-repo modules by static path rules THEN facts SHALL include target module symbol identity.
5. WHEN imports are external, dynamic, optional, hidden behind `importlib`, `__import__`, wildcard imports, or plugin loading THEN the scanner SHALL not guess exact targets and SHOULD emit boundary or gap evidence.
6. WHEN type annotations are present THEN the scanner SHALL record annotation names/hash-safe shape as structural evidence; it SHALL not claim runtime type enforcement.
7. WHEN a reducer-compatible fact uses `targetSymbol` THEN `targetSymbol` SHALL be a human dotted display symbol such as `orders.models.OrderResponse.status`; stable internal IDs SHALL be stored separately as `sourceSymbolId` and `targetSymbolId`.
8. WHEN `.pyi` stubs or type-checker metadata are used in a future slice THEN the source of type evidence SHALL be recorded and coverage SHALL remain reduced if stubs disagree with source or are incomplete.

### Requirement 6: Framework and API Boundary Evidence

**User Story:** As a platform engineer, I want Python service boundaries detected so contract and endpoint changes can route to API, persistence, serializer, and config code.

#### Acceptance Criteria

1. WHEN FastAPI decorators such as `@app.get`, `@router.post`, or `APIRouter` route methods are found with literal paths and either same-module FastAPI import evidence or package dependency evidence THEN the scanner SHALL emit Tier2 `HttpRouteBinding` facts with HTTP method, normalized path key, handler symbol, router/app variable when available, and source span.
2. WHEN FastAPI route-shaped decorators are found without FastAPI import/dependency evidence THEN the scanner SHALL emit Tier3 route-shape evidence rather than Tier2 framework evidence.
3. WHEN `include_router` is found and the router variable/prefix cannot be linked in the same module through literal references THEN the scanner SHALL emit a dynamic-boundary gap and SHALL NOT emit merged path evidence.
4. WHEN Flask decorators such as `@app.route`, `@blueprint.route`, `@app.get`, or `@app.post` are found with same-module Flask app/blueprint evidence and either same-module Flask import evidence or package dependency evidence THEN the scanner SHALL emit `HttpRouteBinding` facts with route shape and handler symbol when literal paths are visible.
5. WHEN Flask blueprint registration or route wiring occurs outside the same module or through runtime logic THEN the scanner SHALL emit a gap and SHALL NOT claim merged blueprint path evidence.
6. WHEN route decorators are dynamic, imported through wildcard, or registered through runtime startup logic THEN the scanner SHALL emit reduced evidence or gaps rather than claiming endpoint reachability.
7. WHEN `requests` or `httpx` client calls are found with literal method/path/URL components and import/dependency evidence THEN the scanner SHALL emit `HttpCallDetected` facts with normalized path key when possible and hashed raw URL details; without that evidence it SHALL emit Tier3 call-shape evidence.
8. WHEN Pydantic `BaseModel` classes or dataclasses are found THEN the scanner SHALL emit `TypeDeclared`, `FieldDeclared`, and `SerializerContractMember`/schema facts for statically written field annotations and direct `Field(...)` calls.
9. WHEN SQLAlchemy declarative models have literal `Column` or `mapped_column` assignments with statically visible attribute names, SQLAlchemy import evidence, and a recognized declarative base THEN the scanner SHALL emit structural `DatabaseColumnMapping` facts for declared column-to-attribute mappings.
10. WHEN SQLAlchemy query calls, ORM filters, `relationship()` strings, or runtime builder expressions are found THEN the scanner SHALL emit query boundary/gap evidence and SHALL NOT infer runtime table/column access.
11. WHEN direct SQL literals are passed to DB-API, SQLAlchemy `text`, or migration helpers THEN the scanner SHALL emit SQL facts with text hash, length, leading operation name when allowed, source kind, target symbol, and source span.
12. WHEN environment/config reads through `os.environ`, `os.getenv`, or Flask config are found THEN the scanner SHALL emit config-use facts with `keyPath` or hashed dynamic expression evidence. Literal `os.environ`/`os.getenv` keys MAY be Tier2 from stdlib evidence; Flask config requires Flask app evidence for Tier2.

### Requirement 7: Relationships and Flow

**User Story:** As a reviewer, I want Python relationship and flow facts so review can distinguish boundary forwarding from code that deserves deeper inspection.

#### Acceptance Criteria

1. WHEN a class inherits from statically named base classes THEN the scanner SHALL emit `SymbolRelationship` with relationship kind `ExtendsClass`.
2. WHEN a method overrides a base method by name only THEN the scanner SHALL NOT emit `Overrides` unless a future type-aware extractor proves the base member.
3. WHEN a name-only override pattern is detected THEN the scanner MAY emit Tier3 relationship evidence such as `OverridesByName` with an explicit limitation note.
4. WHEN a parameter is passed directly to another function, method, constructor, or framework boundary THEN the scanner SHALL emit `ArgumentPassed` with parameter/argument names when available.
5. WHEN simple local aliases are assigned from parameters or other variables THEN the scanner SHOULD emit `LocalAlias` facts only for MVP-supported alias shapes.
6. WHEN instance attributes are assigned from constructor parameters and later passed onward in a low-ambiguity pattern THEN field aliasing MAY be a follow-up, but MVP SHALL not infer broad object state flow.
7. WHEN files match boilerplate patterns such as generated code, migrations, settings modules, FastAPI/Flask app wiring, test fixtures, or DTO-only modules THEN the scanner SHALL emit infrastructure/boilerplate facts.
8. WHEN flow crosses async tasks, callbacks, dependency injection, decorators, monkey patches, runtime imports, ORM lazy loading, serializer internals, `*args`/`**kwargs`, nested/decorated call chains, async task scheduling, or collection contents THEN the scanner SHALL not infer flow beyond recorded evidence.

### Requirement 8: SQL as Shared Evidence

**User Story:** As a data/platform engineer, I want Python SQL usage to line up with SQL evidence from other languages and database artifacts.

#### Acceptance Criteria

1. WHEN Python emits SQL facts THEN it SHALL use shared fact types and properties compatible with .NET, TypeScript, and JVM SQL evidence.
2. WHEN SQL text is found THEN raw SQL SHALL not be stored by default; store SHA-256 over the exact raw SQL string bytes encoded as UTF-8 and truncated to 32 lowercase hex chars as `textHash`, plus `textLength`, `sqlSourceKind`, source span, and target symbol.
3. WHEN a direct SQL literal begins with one of `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `CREATE`, `ALTER`, `DROP`, `TRUNCATE`, `CALL`, `EXEC`, or `EXECUTE` after trimming leading whitespace THEN the scanner SHALL emit `operationName` as the uppercase first token; otherwise it SHALL leave `operationName` empty or omit it.
4. WHEN `.sql`, Alembic migration files, or SQLAlchemy migration files are discovered THEN they SHALL be inventoried and scanned for SQL text/resource facts.
5. WHEN a future shared SQL parser extracts tables, columns, joins, procedure calls, views, or write operations THEN Python shall attach those derived facts to the original source fact IDs and rule IDs.
6. WHEN SQL is dynamically assembled from f-strings, string concatenation, templates, or ORM expression builders THEN the scanner SHALL emit dynamic SQL boundary evidence and avoid guessing concrete runtime query text, table names, column names, or operation kind.
7. WHEN emitting dynamic SQL boundary evidence THEN the scanner SHALL use `AnalysisGap` with `gapKind = "dynamic-sql"` unless a shared `DynamicSqlBoundary` fact type is added later.
8. WHEN emitting ORM query boundary evidence THEN the scanner SHALL use `AnalysisGap` with `gapKind = "orm-query-boundary"` unless a shared ORM boundary fact type is added later.
9. WHEN no shared parser exists THEN Python SHALL NOT emit `tableName`, `columnName`, or `operationKind` from SQL text guesses. `operationName` is the literal first-token verb, while `operationKind` is reserved for future normalized parser output.

### Requirement 9: SQLite and Reducer Compatibility

**User Story:** As a user of TraceMap, I want Python indexes to work with existing reduction/reporting concepts so contract deltas can be reduced across languages.

#### Acceptance Criteria

1. WHEN Python scan completes THEN `facts.ndjson` SHALL use the same top-level fact schema and JSON casing as existing adapters.
2. WHEN a Python fact is intended to participate in existing reducer classification THEN it SHALL reuse existing `FactTypes` strings verbatim.
3. WHEN real MVP Python extraction emits AST/package/config facts THEN it SHALL NOT emit Tier1 `PropertyAccessed` or `MethodInvoked`; receiver types are not proven without a type-checker-backed slice.
4. WHEN a synthetic compatibility fixture needs to prove reducer wiring THEN it MAY include hand-authored Tier1 `PropertyAccessed`, `MethodInvoked`, or `TypeDeclared` facts and SHALL label them as fixture evidence only.
5. WHEN a real Python fact is intended to be `ProbableImpact` eligible THEN it SHALL use existing structural/probable fact types such as `HttpRouteBinding`, `HttpCallDetected`, `ConfigKeyDeclared`, `SqlTextUsed`, `DatabaseColumnMapping`, `SerializerContractMember`, `SymbolRelationship`, or `DependencyRegistered` where evidence supports those meanings.
6. WHEN a real Python fact is intended to be `NeedsReview` eligible THEN it SHALL use Tier3 syntax/textual facts such as `InvocationName`, member-name facts, dynamic boundary facts, or textual config/SQL/name matches.
7. WHEN emitting reducer-compatible facts THEN the scanner SHALL populate existing camelCase matching keys, including `fieldName`, `memberName`, `methodName`, `keyPath`, `name`, `containingType`, `className`, `typeName`, `namespace`, and `targetSymbol` where applicable.
8. WHEN `index.sqlite` is written THEN it SHALL include `scan_manifest` and `facts` tables readable by the existing .NET reducer without reducer changes.
9. WHEN symbols, relationships, calls, object creation, and flow evidence are emitted THEN SQLite SHALL include `symbols`, `symbol_occurrences`, `fact_symbols`, `symbol_relationships`, `call_edges`, `object_creations`, `argument_flows`, and `local_aliases` using the shared SQLite DDL and role-property derivation conventions documented in `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
10. WHEN Python-specific data is needed THEN schema additions SHALL be additive and documented.
11. WHEN facts contain Python symbol IDs THEN the symbol table SHALL include `language = 'python'`.
12. WHEN endpoint, SQL, schema, or config facts are emitted THEN the scanner SHALL populate `contractElement`, `name`, `methodName`, `containingType`, `targetSymbol`, or other reducer-matched plain keys when a `Type.member` contract element is known. `contractElement` is the fact-envelope field, not a replacement for matched property keys.
13. WHEN a real Python MVP scan has no matches for a contract delta THEN the reducer outcome SHALL be `NoEvidenceReducedCoverage`. `NoEvidenceFullCoverage` requires `analysisLevel = Level1SemanticAnalysis`, `buildStatus = Succeeded`, concrete commit SHA, and no known gaps, which Python MVP does not achieve.

### Requirement 10: Determinism, Safety, and Performance

**User Story:** As a maintainer, I want Python scanning to be deterministic, bounded, and safe for large repos.

#### Acceptance Criteria

1. WHEN the same repo commit and scan options are scanned twice THEN stable scan IDs and fact IDs SHALL match except for explicitly time-stamped manifest fields.
2. WHEN computing `scanId` THEN the scanner SHALL derive it deterministically from repository identity, commit SHA, scan options, and a sorted file-inventory signature, mirroring the existing .NET approach and avoiding timestamps, UUIDs, process IDs, or output paths.
3. WHEN dependencies are missing or imports cannot resolve THEN the scanner SHALL continue with reduced coverage and syntax/AST facts.
4. WHEN a file exceeds the max byte-size threshold THEN the scanner SHALL skip extraction for that file and emit an `AnalysisGap`.
5. WHEN parser or worker failures occur THEN the scanner SHALL record bounded diagnostics and continue to remaining files when possible.
6. WHEN paths are emitted THEN they SHALL be normalized repo-relative paths.
7. WHEN evidence spans are emitted THEN they SHALL use one-based line numbers and deterministic end-line calculation.
8. WHEN scanner logs include diagnostics THEN logs SHALL avoid raw source snippets unless an explicit future raw-snippet option is added.
9. WHEN package metadata or config files are huge THEN parsing SHALL be bounded and failure SHALL produce reduced evidence rather than process failure.

## Required Property-Key Contract

The Python scanner SHALL use these existing property keys when emitting reducer-compatible facts:

| Fact type | Required matching keys | MVP tier guidance |
| --- | --- | --- |
| `TypeDeclared` | `name`, `typeName`, `namespace`, `targetSymbol` | Tier2 only for recognized structural model/schema evidence; otherwise Tier3 |
| `FieldDeclared` | `fieldName`, `fieldType`, `containingType`, `targetSymbol` | Tier2 for Pydantic/dataclass declarations with structural evidence; SQLAlchemy columns use `DatabaseColumnMapping` |
| `ParameterDeclared` | `parameterName`, `parameterType`, `sourceSymbol` | Tier3 unless type-checker-backed |
| `ConfigKeyDeclared` | `keyPath`, `name`, `targetSymbol` | Tier2 for literal config/env keys with known framework/config source |
| `HttpRouteBinding` | `httpMethod`, `normalizedPathKey`, `methodName`, `targetSymbol`, `contractElement` | Tier2 for FastAPI/Flask evidence with literal route |
| `HttpCallDetected` | `httpMethod`, `normalizedPathKey`, `methodName`, `targetSymbol`, `contractElement` | Tier2 for recognized `requests`/`httpx` call with literal/static-enough URL |
| `SqlTextUsed` | `textHash`, `textLength`, `operationName`, `sqlSourceKind`, `targetSymbol` | Tier3 by default for `.execute(<literal>)`; Tier2 only with structural DB/ORM/migration/file evidence; no table/column guesses |
| `DatabaseColumnMapping` | `columnName`, `attributeName`, `containingType`, `targetSymbol` | Tier2 only with SQLAlchemy import evidence and a recognized declarative base |
| `SerializerContractMember` | `contractName`, `memberName`, `containingType`, `targetSymbol` | Tier2 for Pydantic/dataclass fields with structural evidence |
| `ArgumentPassed` | `parameterName`, `argumentKind`, `containingSymbol`, `targetSymbol` | Tier3 for AST-only direct argument evidence |

`PropertyAccessed` and `MethodInvoked` are intentionally excluded from real MVP Python extraction because receiver types are not proven by AST alone. They remain type-checker-backed follow-up fact types.

`FieldDeclared` and `SerializerContractMember` facts MUST populate `containingType` when a containing class/model is known, so `Type.member` contract deltas do not fan out on member name alone.

## Out of Scope for MVP

- executing target Python modules, importing app code, running framework startup, or evaluating decorators.
- reimplementing `.NET` `tracemap reduce`, `tracemap export`, `tracemap combine`, or endpoint alignment in the Python adapter.
- full type-checker semantics, cross-module type inference, or proving dynamic dispatch.
- Django route extraction, aiohttp/urllib clients, attrs, TypedDict, Poetry lock parsing, uv lock parsing, and `.pyi` stubs.
- Jupyter notebook parsing.
- logic-shape/business-logic scoring beyond boilerplate and explicit boundary facts.
- full SQL dialect parsing; shared SQL normalization is a separate cross-language layer.
- route reachability through runtime router inclusion, middleware, settings, feature flags, or deployment config.
- GraphQL, Celery deep task graph traversal, Airflow DAG semantics, Pandas schema inference, Spark lineage, or LLM/agent framework tracing.
