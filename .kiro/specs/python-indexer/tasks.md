# Python Indexer Tasks

## Implementation Plan

### Phase 0: Scope Lock

- [x] 0.1 Confirm Python lives under `src/python` as a sibling adapter.
- [x] 0.2 Confirm MVP does not import target modules, run framework startup, run tests, install packages, or execute decorators.
- [x] 0.3 Confirm Python MVP uses AST/package/config evidence first; type-checker-backed Tier1 is post-MVP.
- [x] 0.4 Confirm existing `.NET` `tracemap reduce`, `tracemap export`, `tracemap combine`, and `tracemap endpoints` are downstream commands.
- [x] 0.5 Confirm real Python MVP scans do not emit Tier1 `PropertyAccessed` or `MethodInvoked`.
- [x] 0.6 Confirm real Python reducer outcomes are expected to be `ProbableImpact`, `NeedsReview`, or `NoEvidenceReducedCoverage`.
- [x] 0.7 Confirm FastAPI, Flask, Pydantic, dataclasses, SQLAlchemy declared columns, direct SQL, config/env reads, and `requests`/`httpx` are MVP integration families.
- [x] 0.8 Confirm Django, aiohttp, urllib, attrs, TypedDict, lockfile parsing, and richer logic-shape detection are post-MVP.
- [x] 0.9 Confirm `buildStatus = FailedOrPartial` means reduced semantic coverage for Python MVP, not a failed Python build.
- [x] 0.10 Confirm SQL is shared cross-language evidence and not a standalone app-language adapter in this slice.
- [x] 0.11 Confirm dynamic imports, monkey patching, decorator side effects, runtime DI, route inclusion order, collection contents, mutation semantics, and branch feasibility are gaps/boundaries, not inferred facts.
- [x] 0.12 Confirm reducer-compatible facts reuse existing fact types and camelCase property keys.
- [x] 0.13 Confirm Python emits compatible artifacts and does not reimplement `.NET` reduce/export/combine/endpoint alignment.
- [x] 0.14 Confirm shared adapter contract decisions: deterministic `scanId`, supported role-symbol property keys, route normalization, additive SQL source kinds, and SQLite DDL source of truth.

### Phase 1: Scaffold `src/python`

- [x] 1.1 Create `src/python` package, `pyproject.toml`, test config, and README.
- [x] 1.2 Choose packaging/test tooling; recommended default: Python 3.11+ package with pytest and standard-library-first runtime.
- [x] 1.3 Add `tracemap-py scan --help`, `tracemap-py --help`, and `tracemap-py --version`.
- [x] 1.4 Add deterministic local build/test commands.
- [x] 1.5 Add package docs for standalone `tracemap-py` usage and first-scan minimum input.
- [x] 1.6 Ensure private-path guard covers Python docs and smoke outputs.
- [x] 1.7 Add rule-catalog validation hook so emitted Python rule IDs must exist before tests pass.
- [x] 1.8 Document local source install flow: `python -m pip install -e .` from `src/python`.

### Phase 2: Fact Model and Cross-Language Contract

- [x] 2.1 Update/confirm `docs/LANGUAGE_ADAPTER_CONTRACT.md` covers Python-needed scan IDs, supported role-symbol properties, route normalization, additive SQL source kinds, and SQLite DDL conventions.
- [x] 2.2 Define Python models matching the existing TraceMap fact envelope and JSON casing.
- [x] 2.3 Implement deterministic hashing and sorted-property fact creation.
- [x] 2.4 Document and test that extractor version is excluded from fact ID hash input.
- [x] 2.5 Implement deterministic `scanId` helper from repository identity, commit SHA, and sorted inventory signature.
- [x] 2.6 Implement SQL/text hash helper: SHA-256 over exact UTF-8 string bytes, truncated to 32 lowercase hex chars.
- [x] 2.7 Create hand-crafted synthetic Python `.ndjson`/`.sqlite` fixture files with Tier1 `TypeDeclared`, `PropertyAccessed`, and `MethodInvoked` facts labeled with `isSyntheticFixture = "true"` and a fixture rule ID.
- [x] 2.8 Run existing `.NET` `tracemap reduce` against the synthetic fixture and prove `DefiniteImpact`.
- [x] 2.9 Create hand-crafted real-MVP-style Python `.ndjson`/`.sqlite` fixture files with Tier2 `HttpRouteBinding`, `SerializerContractMember`, `SqlTextUsed`, and `ConfigKeyDeclared` facts.
- [x] 2.10 Run existing `.NET` `tracemap reduce` against the real-MVP-style fixture and prove `ProbableImpact`/`NeedsReview`, not `DefiniteImpact`.
- [x] 2.11 Verify no-match real-MVP-style fixture deltas return `NoEvidenceReducedCoverage`.
- [x] 2.12 Verify existing `.NET` `tracemap export` reads Python fixture indexes and includes Python `call_edges`, route, object, and SQL rows in output.
- [x] 2.13 Verify existing `.NET` `tracemap combine` can merge a Python fixture index with .NET/TypeScript/JVM fixture indexes.
- [x] 2.14 Implement `JsonlFactWriter`.
- [x] 2.15 Implement SQLite writer compatible with shared `scan_manifest`, `facts`, symbols, relationship, call, object, argument, and alias tables.
- [x] 2.16 Add tests for reducer-compatible fact types, required camelCase property keys, role-symbol property keys, and `contractElement` plus `Type.member` matching.

### Phase 3: Repo Metadata, Manifest, Inventory, and Package Roots

- [x] 3.1 Implement bounded Git metadata detection with timeout and stderr/stdout handling.
- [x] 3.2 Implement manifest writing with conservative Python coverage semantics.
- [x] 3.3 Implement file inventory for `.py`, Python metadata files, config files, SQL files, and migration files.
- [x] 3.4 Implement default excludes: `.git`, output path, virtualenvs, `site-packages`, `__pycache__`, caches, build/dist outputs, generated outputs, and dependency caches.
- [x] 3.5 Implement include/exclude/project scope filtering and manifest scan-root metadata.
- [x] 3.6 Implement package-root discovery before symbol identity is computed.
- [x] 3.7 Implement max file byte-size parsing and skip gaps.
- [x] 3.8 Add tests for path normalization, excluded directories, package-root ordering, stable scan IDs, full/reduced manifest gates, missing Git SHA failure, and repeated `--project` one-index manifest shape.

### Phase 4: Rule Catalog, Package, Config, and Gap Infrastructure

- [x] 4.1 Add Python rule IDs and limitations to `rules/rule-catalog.yml` before extractors emit those rule IDs.
- [x] 4.2 Parse `pyproject.toml` with `tomllib` for project name/version/dependencies/build backend/tool sections.
- [x] 4.3 Parse `setup.cfg` for literal metadata/dependencies.
- [x] 4.4 Parse `requirements*.txt` for package references and literal pins/ranges, plus VCS/editable/path entries as boundary facts with hashed references.
- [x] 4.5 Treat `setup.py` as dynamic unless literal metadata can be extracted without executing code.
- [x] 4.6 Emit package/dependency facts.
- [x] 4.7 Parse `.env`, `.ini`, `.cfg`, `.toml`, `.yaml`, `.json`, and Flask config key paths with hashes.
- [x] 4.8 Add `AnalysisGapCollector` with bounded diagnostic output.
- [x] 4.9 Add generated-file and migration-file detectors.
- [x] 4.10 Add tests for package metadata, dynamic setup gaps, config parsing, repeated keys, parse gaps, and diagnostic caps.

### Phase 5: AST Baseline

- [x] 5.1 Parse `.py` files with Python `ast` and record parse diagnostics as gaps.
- [x] 5.2 Emit module, class, function, async function, method, field/assignment, and parameter declaration facts.
- [x] 5.3 Emit decorator facts for bare name, attribute, and call decorators with safe names and hashed literal arguments.
- [x] 5.4 Emit import facts and in-repo module references when statically resolvable.
- [x] 5.5 Emit `TYPE_CHECKING` block facts only as Tier3 with `typeCheckingOnly = true`.
- [x] 5.6 Emit member-name facts with receiver hashes, not raw expressions, and do not emit `PropertyAccessed`.
- [x] 5.7 Emit invocation-name and syntax call-edge facts, and do not emit `MethodInvoked`.
- [x] 5.8 Emit object creation facts for class-like calls and obvious assignments.
- [x] 5.9 Emit direct argument-passed facts.
- [x] 5.10 Emit local alias facts only for `Name = Name` and `Name = Attribute.Name` assignments.
- [x] 5.11 Emit symbol table, occurrence, fact-symbol, call-edge, object-creation, argument-flow, and alias rows.
- [x] 5.12 Add tests for decorators, async functions, nested functions, class methods, imports, calls, object creation, arguments, aliases, `TYPE_CHECKING`, match/walrus syntax, parse errors, and deterministic spans.

### Phase 6: Framework Boundary Extractors

- [x] 6.1 Emit FastAPI route facts for app/router decorators with literal paths and HTTP methods.
- [x] 6.2 Emit FastAPI APIRouter prefix facts when prefix is literal and same-module.
- [x] 6.3 Emit FastAPI route gaps/boundaries for dynamic decorators and unresolved `include_router`.
- [x] 6.4 Emit Flask route facts for `route`, HTTP-method shortcuts, literal `methods`, and same-module literal blueprint prefixes.
- [x] 6.5 Emit Flask route gaps/boundaries for dynamic route registration and runtime blueprint wiring.
- [x] 6.6 Emit shared path-only, lowercased `normalizedPathKey` values with `{}` placeholders according to `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
- [x] 6.7 Emit HTTP client facts for `requests` and `httpx` calls.
- [x] 6.8 Emit dynamic client URL boundary facts when URL cannot be normalized.
- [x] 6.9 Add tests for each supported route/client shape, async route handlers, method mismatch possibilities, dynamic URLs, route prefix limitations, and endpoint-reader visibility.

### Phase 7A: Schema, Serializer, and Config Extractors

- [x] 7.1 Emit Pydantic `BaseModel` type/field/schema facts.
- [x] 7.2 Emit direct Pydantic v2 `Annotated[T, Field(...)]` field facts; do not expand arbitrary `Annotated` metadata.
- [x] 7.3 Emit dataclass type/field/schema facts when standard-library dataclass evidence is visible.
- [x] 7.4 Emit config/env facts for `os.environ`, `os.getenv`, and Flask config where statically visible.
- [x] 7.5 Add tests for Pydantic fields/aliases, direct `Annotated[T, Field(...)]`, dataclass fields, config reads, validators/computed-field limitations, and `__slots__` limitations.

### Phase 7B: SQL and Persistence Extractors

- [x] 7.6 Emit SQLAlchemy declarative model table/column mapping facts only for literal declarations with SQLAlchemy import evidence and recognized declarative base evidence.
- [x] 7.7 Emit SQLAlchemy query boundary facts as `AnalysisGap` with `gapKind = "orm-query-boundary"` for ORM builders, filters, relationships, and dynamic query patterns.
- [x] 7.8 Emit DB-API and SQLAlchemy `text(...)` SQL call facts with `textHash`, `textLength`, target symbol, and additive allowlisted `operationName`/`sqlSourceKind` when available.
- [x] 7.9 Emit dynamic SQL boundary facts as `AnalysisGap` with `gapKind = "dynamic-sql"`.
- [x] 7.10 Emit SQL resource facts for `.sql` files and migration files.
- [x] 7.11 Add tests for SQLAlchemy declared columns, recognized declarative base gates, query boundaries, direct SQL, dynamic SQL, migrations, operation-name allowlist, and config reads.

### Phase 8: Relationships and Boilerplate

- [x] 8.1 Emit class inheritance `SymbolRelationship` facts for statically named bases.
- [x] 8.2 Defer `Overrides` unless type-aware evidence is added.
- [x] 8.3 Optionally emit Tier3 `OverridesByName` relationship evidence for name-only override patterns with limitation notes.
- [x] 8.4 Emit infrastructure/boilerplate facts for generated files, migrations, settings modules, app wiring, test fixtures, and DTO-only modules.
- [x] 8.5 Add tests for inheritance, name-only override boundaries, boilerplate classification, and false-positive limitations.

### Phase 9: Samples and Smoke

- [x] 9.1 Add `samples/python-fastapi-sample` with FastAPI route, Pydantic DTO, SQLAlchemy declared column/direct SQL, config/env read, HTTP client call, call edges, object creation, and argument flow.
- [x] 9.2 Add `samples/python-flask-sample` with small Flask route and dynamic reduced-coverage examples.
- [x] 9.3 Add `samples/python-broken-sample` for parse errors and dynamic reduced coverage.
- [x] 9.4 Add Python sample contract delta.
- [x] 9.5 Add synthetic Tier1 reducer contract delta fixture and label it as synthetic compatibility evidence.
- 9.6 Pin Python OSS smoke targets in `docs/VALIDATION.md` before scripting smoke execution.
- 9.7 Add Python section to open-source smoke script after public repos are selected/pinned.
- 9.8 Smoke-test at least one FastAPI repo, one Flask repo, and one SQLAlchemy-heavy repo from public pinned SHAs.
- [x] 9.9 Verify existing `.NET` endpoint alignment can read Python `HttpRouteBinding` and `HttpCallDetected` rows without error.
- [x] 9.10 Record smoke results without committing absolute local paths or sensitive repository names.

### Phase 10: Report, Docs, and Validation

- [x] 10.1 Generate `report.md` with metadata, coverage, gaps, fact counts, package counts, route/client/config/SQL/model counts, and Python limitations.
- [x] 10.2 Update root README with Python scan/reduce/combine examples.
- [x] 10.3 Add `src/python/README.md`.
- [x] 10.4 Update `docs/ACCEPTANCE.md` and `docs/VALIDATION.md` with Python scenarios and smoke commands.
- [x] 10.5 Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` if implementation discovers contract gaps.
- [x] 10.6 Document that endpoint alignment can consume Python route/client facts, but full cross-repo endpoint matching is post-MVP validation unless implemented naturally by existing commands.
- [x] 10.7 Add public OSS smoke candidates to `docs/VALIDATION.md` with pinned SHAs after selection.

### Phase 11: Post-MVP Backlog

- 11.1 Type-checker-backed Tier1 facts using Pyright/MyPy metadata or another deterministic static source.
- 11.2 Real Python `PropertyAccessed` and `MethodInvoked` facts after receiver types can be proven.
- 11.3 Cross-module route prefix/include expansion for FastAPI/Django/Flask.
- 11.4 Django route/view facts.
- 11.5 aiohttp and urllib HTTP client facts.
- 11.6 attrs and TypedDict schema facts.
- 11.7 Poetry and uv lockfile parsing.
- 11.8 Logic-shape/business-logic facts.
- 11.9 Celery task graph and background job boundary facts.
- 11.10 Airflow DAG/task boundary facts.
- 11.11 GraphQL schema/resolver facts.
- 11.12 Pandas/Spark/dataframe schema and SQL facts.
- 11.13 Deep SQL dialect parser shared across all adapters.
- 11.14 Field/attribute aliasing and derived `parameter_forward_edges`.
- 11.15 Framework-specific dependency injection containers.
- 11.16 Jupyter notebook source extraction.

## Definition of Done

- [x] Python package builds/installs locally.
- [x] Python tests pass.
- [x] Existing `.NET` `dotnet build src/dotnet/TraceMap.sln` passes after repo changes.
- [x] Existing `.NET` `dotnet test src/dotnet/TraceMap.sln` passes after repo changes.
- [x] Existing TypeScript and JVM checks pass if shared docs/schema changed.
- [x] `tracemap-py scan` writes required artifacts for modern and broken Python samples.
- [x] Existing `.NET` `tracemap reduce` classifies a synthetic Python fixture as `DefiniteImpact`.
- [x] Existing `.NET` `tracemap reduce` classifies a real Python structural fixture as `ProbableImpact` or `NeedsReview`.
- [x] Existing `.NET` `tracemap reduce` returns `NoEvidenceReducedCoverage` for no-match real Python MVP fixtures.
- [x] Existing `.NET` `tracemap export` reads Python route/call/object/SQL rows.
- [x] Existing `.NET` `tracemap combine` imports a Python index.
- [x] Existing `.NET` endpoint alignment reads Python route/client rows without error.
- [x] Facts contain rule IDs, evidence tiers, repo/commit SHA, line spans, extractor versions, and no raw snippets.
- [x] Reduced scans are labeled reduced and never reported as clean.
- [x] Rule catalog limitations are updated for every new rule ID.
- [x] `./scripts/check-private-paths.sh` passes.
