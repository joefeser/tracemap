# Python Indexer Tasks

## Implementation Plan

### Phase 0: Scope Lock

- [ ] 0.1 Confirm Python lives under `src/python` as a sibling adapter.
- [ ] 0.2 Confirm MVP does not import target modules, run framework startup, run tests, install packages, or execute decorators.
- [ ] 0.3 Confirm Python MVP uses AST/package/config evidence first; type-checker-backed Tier1 is follow-up unless explicitly designed.
- [ ] 0.4 Confirm existing `.NET` `tracemap reduce`, `tracemap export`, `tracemap combine`, and `tracemap endpoints` are MVP downstream commands.
- [ ] 0.5 Confirm FastAPI, Flask, Django, Pydantic, SQLAlchemy, direct SQL, config/env reads, and common HTTP clients are MVP integration families.
- [ ] 0.6 Confirm SQL is shared cross-language evidence and not a standalone app-language adapter in this slice.
- [ ] 0.7 Confirm dynamic imports, monkey patching, decorator side effects, runtime DI, and route inclusion order are gaps/boundaries, not inferred facts.
- [ ] 0.8 Confirm reducer-compatible facts reuse existing fact types and camelCase property keys.

### Phase 1: Scaffold `src/python`

- [ ] 1.1 Create `src/python` package, `pyproject.toml`, test config, and README.
- [ ] 1.2 Choose packaging/test tooling; recommended default: Python package with pytest and standard-library-first runtime.
- [ ] 1.3 Add `tracemap-py scan --help`, `tracemap-py --help`, and `tracemap-py --version`.
- [ ] 1.4 Add deterministic local build/test commands.
- [ ] 1.5 Add package docs for standalone `tracemap-py` usage and first-scan minimum input.
- [ ] 1.6 Ensure private-path guard covers Python docs and smoke outputs.
- [ ] 1.7 Add rule-catalog validation hook so emitted Python rule IDs must exist before tests pass.

### Phase 2: Fact Model and SQLite Contract

- [ ] 2.1 Define Python models matching the existing TraceMap fact envelope and JSON casing.
- [ ] 2.2 Implement deterministic hashing and sorted-property fact creation.
- [ ] 2.3 Document and test that extractor version is excluded from fact ID hash input.
- [ ] 2.4 Implement `JsonlFactWriter`.
- [ ] 2.5 Implement SQLite writer compatible with shared `scan_manifest`, `facts`, symbols, relationship, call, object, argument, and alias tables.
- [ ] 2.6 Create a tiny Python index fixture with hand-written `TypeDeclared`, `PropertyAccessed`, `MethodInvoked`, `HttpRouteBinding`, and `SqlTextUsed` facts.
- [ ] 2.7 Run existing `.NET` `tracemap reduce` against the fixture and prove `DefiniteImpact`.
- [ ] 2.8 Verify existing `.NET` `tracemap export` reads Python fixture indexes.
- [ ] 2.9 Verify existing `.NET` `tracemap combine` can merge a Python fixture index with .NET/TypeScript/JVM fixture indexes.
- [ ] 2.10 Add tests for reducer-compatible fact types and required camelCase property keys.

### Phase 3: Repo Metadata, Manifest, and Inventory

- [ ] 3.1 Implement bounded Git metadata detection with timeout and stderr/stdout handling.
- [ ] 3.2 Implement manifest writing with conservative Python coverage semantics.
- [ ] 3.3 Implement file inventory for `.py`, Python metadata files, config files, SQL files, and migration files.
- [ ] 3.4 Implement default excludes: `.git`, output path, virtualenvs, `site-packages`, `__pycache__`, caches, build/dist outputs, generated outputs, and dependency caches.
- [ ] 3.5 Implement include/exclude/project scope filtering.
- [ ] 3.6 Implement max file byte-size parsing and skip gaps.
- [ ] 3.7 Add tests for path normalization, excluded directories, stable scan IDs, full/reduced manifest gates, and missing Git SHA failure.

### Phase 4: Package, Config, and Gap Infrastructure

- [ ] 4.1 Parse `pyproject.toml` with `tomllib` for project name/version/dependencies/build backend/tool sections.
- [ ] 4.2 Parse `setup.cfg` for literal metadata/dependencies.
- [ ] 4.3 Parse `requirements*.txt` for package references and literal pins/ranges.
- [ ] 4.4 Parse lockfiles where practical: Poetry and uv lockfiles first.
- [ ] 4.5 Treat `setup.py` as dynamic unless literal metadata can be extracted without executing code.
- [ ] 4.6 Emit package/dependency facts.
- [ ] 4.7 Parse `.env`, `.ini`, `.cfg`, `.toml`, `.yaml`, `.json`, and framework config key paths with hashes.
- [ ] 4.8 Add `AnalysisGapCollector` with bounded diagnostic output.
- [ ] 4.9 Add generated-file and migration-file detectors.
- [ ] 4.10 Add tests for package metadata, dynamic setup gaps, config parsing, repeated keys, parse gaps, and diagnostic caps.

### Phase 5: AST Baseline

- [ ] 5.1 Parse `.py` files with Python `ast` and record parse diagnostics as gaps.
- [ ] 5.2 Emit module, class, function, async function, method, field/assignment, and parameter declaration facts.
- [ ] 5.3 Emit decorator facts with safe names and hashed literal arguments.
- [ ] 5.4 Emit import facts and in-repo module references when statically resolvable.
- [ ] 5.5 Emit member access facts with receiver hashes, not raw expressions.
- [ ] 5.6 Emit invocation and syntax call-edge facts.
- [ ] 5.7 Emit object creation facts for class-like calls and obvious assignments.
- [ ] 5.8 Emit direct argument-passed facts.
- [ ] 5.9 Emit local alias facts for simple assignments.
- [ ] 5.10 Emit symbol table, occurrence, fact-symbol, call-edge, object-creation, argument-flow, and alias rows.
- [ ] 5.11 Add tests for decorators, async functions, nested functions, class methods, imports, calls, object creation, arguments, aliases, parse errors, and deterministic spans.

### Phase 6: Framework Boundary Extractors

- [ ] 6.1 Emit FastAPI route facts for app/router decorators with literal paths and HTTP methods.
- [ ] 6.2 Emit FastAPI APIRouter prefix facts when prefix is literal and same-module.
- [ ] 6.3 Emit Flask route facts for `route`, HTTP-method shortcuts, and literal `methods`.
- [ ] 6.4 Emit Django route facts for `path`, `re_path`, and view target references in URL config files.
- [ ] 6.5 Emit HTTP client facts for `requests`, `httpx`, `aiohttp`, and `urllib` calls.
- [ ] 6.6 Emit dynamic client URL boundary facts when URL cannot be normalized.
- [ ] 6.7 Add tests for each supported route/client shape, method mismatch possibilities, dynamic URLs, and route prefix limitations.

### Phase 7: Schema, Serializer, Config, and Persistence Extractors

- [ ] 7.1 Emit Pydantic `BaseModel` type/field/schema facts.
- [ ] 7.2 Emit dataclass, TypedDict, attrs, and simple serializer/schema facts.
- [ ] 7.3 Emit config/env facts for `os.environ`, `os.getenv`, Pydantic settings, Django settings, Flask config, Dynaconf, and decouple where statically visible.
- [ ] 7.4 Emit SQLAlchemy declarative model table/column mapping facts.
- [ ] 7.5 Emit SQLAlchemy and DB-API SQL call facts with hashes and operation names.
- [ ] 7.6 Emit SQL resource facts for `.sql` files and migration files.
- [ ] 7.7 Add tests for Pydantic fields/aliases, SQLAlchemy models/queries, direct SQL, migrations, and config reads.

### Phase 8: Relationships and Logic Shape

- [ ] 8.1 Emit class inheritance `SymbolRelationship` facts for statically named bases.
- [ ] 8.2 Defer `Overrides` unless type-aware evidence is added.
- [ ] 8.3 Emit logic-shape facts for arithmetic, comparison, branching, validation, transformations, collection processing, retry/backoff, and date/time logic.
- [ ] 8.4 Emit infrastructure/boilerplate facts for generated files, migrations, settings modules, app wiring, test fixtures, and DTO-only modules.
- [ ] 8.5 Add tests for inheritance, logic-shape detection, boilerplate classification, and false-positive limitations.

### Phase 9: Report, Docs, Rule Catalog, and Validation

- [ ] 9.1 Generate `report.md` with metadata, coverage, gaps, fact counts, package counts, route/client/config/SQL/model counts, and Python limitations.
- [ ] 9.2 Update root README with Python scan/reduce/combine examples.
- [ ] 9.3 Add `src/python/README.md`.
- [ ] 9.4 Update `docs/ACCEPTANCE.md` and `docs/VALIDATION.md` with Python scenarios and smoke commands.
- [ ] 9.5 Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` only if implementation discovers contract gaps.
- [ ] 9.6 Add Python rule IDs and limitations to `rules/rule-catalog.yml` before any extractor emits those rule IDs.
- [ ] 9.7 Add public OSS smoke candidates to `docs/VALIDATION.md` with pinned SHAs after selection.

### Phase 10: Samples and Smoke

- [ ] 10.1 Add `samples/python-fastapi-sample` with FastAPI route, Pydantic DTO, SQLAlchemy/direct SQL, config/env read, HTTP client call, call edges, object creation, and argument flow.
- [ ] 10.2 Add `samples/python-flask-django-sample` with small Flask and Django route fixtures.
- [ ] 10.3 Add `samples/python-broken-sample` for parse errors and dynamic reduced coverage.
- [ ] 10.4 Add Python sample contract delta.
- [ ] 10.5 Add endpoint-alignment smoke between Python server and TypeScript or .NET client sample if useful.
- [ ] 10.6 Add Python section to open-source smoke script after public repos are selected/pinned.
- [ ] 10.7 Smoke-test at least one FastAPI repo, one Flask/Django repo, and one SQLAlchemy-heavy repo from public pinned SHAs.
- [ ] 10.8 Record smoke results without committing absolute local paths or private repo names.

### Phase 11: Post-MVP Backlog

- [ ] 11.1 Type-checker-backed Tier1 facts using Pyright/MyPy metadata or another deterministic static source.
- [ ] 11.2 Cross-module route prefix/include expansion for FastAPI/Django/Flask.
- [ ] 11.3 Celery task graph and background job boundary facts.
- [ ] 11.4 Airflow DAG/task boundary facts.
- [ ] 11.5 GraphQL schema/resolver facts.
- [ ] 11.6 Pandas/Spark/dataframe schema and SQL facts.
- [ ] 11.7 Deep SQL dialect parser shared across all adapters.
- [ ] 11.8 Field/attribute aliasing and derived `parameter_forward_edges`.
- [ ] 11.9 Framework-specific dependency injection containers.
- [ ] 11.10 Jupyter notebook source extraction.

## Definition of Done

- [ ] Python package builds/installs locally.
- [ ] Python tests pass.
- [ ] Existing `.NET` `dotnet build src/dotnet/TraceMap.sln` passes after repo changes.
- [ ] Existing `.NET` `dotnet test src/dotnet/TraceMap.sln` passes after repo changes.
- [ ] Existing TypeScript and JVM checks pass if shared docs/schema changed.
- [ ] `tracemap-py scan` writes required artifacts for modern and broken Python samples.
- [ ] Existing `.NET` `tracemap reduce` classifies a Python contract match.
- [ ] Existing `.NET` `tracemap export` reads Python route/call/object/SQL rows.
- [ ] Existing `.NET` `tracemap combine` imports a Python index.
- [ ] Facts contain rule IDs, evidence tiers, repo/commit SHA, line spans, extractor versions, and no raw snippets.
- [ ] Reduced scans are labeled reduced and never reported as clean.
- [ ] Rule catalog limitations are updated for every new rule ID.
- [ ] `./scripts/check-private-paths.sh` passes.
