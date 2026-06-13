# Python Indexer Design

## Overview

Add a Python TraceMap scanner under `src/python`. The package will produce TraceMap-compatible artifacts for Python repositories:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

The scanner will use Python `ast` for deterministic source analysis, local package/config metadata for structural evidence, and conservative textual fallback for files that cannot parse. It will not import target modules, execute decorators, start framework apps, run tests, or resolve package dependencies through network commands.

This is a sibling implementation:

```text
src/
  dotnet/
  typescript/
  jvm/
  python/
```

## Goals

- Preserve TraceMap's deterministic, evidence-backed behavior for Python repositories.
- Support Python service/API projects, scripts, packages, and monorepos.
- Emit enough facts for contract reduction, dependency traversal, endpoint alignment, persistence review, serializer/schema review, config review, and business-logic routing.
- Treat SQL as shared cross-language data dependency evidence rather than a Python-only concern.
- Keep limitations explicit in rule catalog entries, scan manifests, and reports.

## Non-Goals

- No LLM calls or embeddings.
- No runtime imports or execution of target repository code.
- No framework startup for FastAPI, Flask, Django, Celery, Airflow, or any app.
- No package installation, dependency restore, virtualenv creation, or network calls during `scan`.
- No claim of runtime reachability, middleware behavior, auth behavior, feature-flag state, dependency-injection binding, monkey-patch target, dynamic import target, decorator side effects, ORM runtime schema, serializer runtime mapping, or branch feasibility.
- No raw source snippets by default.
- No full SQL parser in the Python adapter MVP.
- No cross-module type inference in MVP unless a later type-checker-backed extractor is designed.

## Locked Decisions To Review

- Initial CLI is standalone `tracemap-py`; root `tracemap` dispatch is follow-up.
- Existing `.NET` `tracemap reduce`, `tracemap export`, `tracemap combine`, and endpoint alignment are MVP downstream commands for Python indexes.
- Implementation lives under `src/python`, not folded into the TypeScript/JVM packages.
- MVP semantic truth is AST/package/config evidence; type-checker-backed Tier1 can be added later.
- FastAPI, Flask, Django, Pydantic, SQLAlchemy, direct SQL, env/config reads, and common HTTP clients are MVP integration families.
- SQL normalization beyond hashes/operation/source spans is a shared cross-language follow-up.
- Facts that should influence reducer classification reuse existing `FactTypes` strings and camelCase property keys.

## Package Structure

Proposed package shape:

```text
src/python/
  README.md
  pyproject.toml
  tracemap_py/
    __init__.py
    cli.py
    scan/
      engine.py
      git_metadata.py
      inventory.py
      gaps.py
      manifest.py
      report.py
    facts/
      factory.py
      models.py
      fact_types.py
      rule_ids.py
    metadata/
      pyproject_reader.py
      setup_cfg_reader.py
      requirements_reader.py
      lockfile_reader.py
      package_model.py
    extract/
      ast_extractor.py
      route_extractor.py
      http_client_extractor.py
      pydantic_extractor.py
      sqlalchemy_extractor.py
      sql_extractor.py
      config_extractor.py
      logic_extractor.py
    storage/
      jsonl_writer.py
      sqlite_writer.py
    symbols/
      identity.py
      imports.py
    util/
      hashes.py
      paths.py
      line_map.py
      globs.py
  tests/
```

Implementation language should be Python to reuse Python `ast`, `tomllib`, packaging metadata helpers, and pytest fixture ergonomics. The package should avoid dependency-heavy framework imports in the scanner itself; framework support should operate over source syntax and local metadata.

## CLI Shape

Initial Python CLI:

```bash
tracemap-py scan --repo <path> --out <path>
```

Options:

- `--project <path>` repeatable: explicit `pyproject.toml`, package directory, source directory, or app root.
- `--include <glob>` repeatable.
- `--exclude <glob>` repeatable.
- `--max-file-byte-size <value>` default `1mb`.
- `--no-metadata`: skip package/config metadata parsing for debugging.
- `--language python` reserved for future multi-language dispatch symmetry.

Follow-up options:

- `--type-cache <path>` for type-checker metadata.
- `--dependencies-file <path>` for user-supplied package/version mapping.
- root `tracemap` language dispatch.

## Pipeline

1. Validate repo path and output path.
2. Read Git metadata; fail before writing success artifacts if commit SHA is unavailable.
3. Build file inventory with scope filters and default excludes.
4. Parse package metadata and lock/config files.
5. Parse Python files with `ast.parse`.
6. Emit baseline declaration/import/call/object/argument/member facts.
7. Run framework/integration extractors over AST and metadata context.
8. Run SQL/config/resource extractors.
9. Derive symbol, call, object, argument-flow, and relationship rows for SQLite.
10. Compute coverage level from gaps.
11. Write manifest, facts, SQLite, report, and analyzer log.

## Evidence Tiers

Python evidence needs careful tiering because AST is deterministic but not compiler-resolved in the same sense as Roslyn/TypeScript/Javac.

- `Tier1Semantic`: reserved for future type-checker/import-resolved evidence that proves the symbol with explicit project/type context. MVP should use Tier1 sparingly, if at all.
- `Tier2Structural`: framework/package/config evidence where imports, package metadata, decorators, or known file roles prove a common structure. Examples: FastAPI route decorator with FastAPI import evidence; Pydantic `BaseModel` class with import/base evidence; SQLAlchemy `Column` under declarative model evidence.
- `Tier3SyntaxOrTextual`: AST-only declarations, calls, member names, direct strings, or syntax shape without package/framework confirmation.
- `Tier4Unknown`: analysis gaps and unable-to-prove states.

Do not upgrade a fact to Tier2 solely because a name looks likely. Tier2 requires recognized local structure: import, dependency metadata, base class/decorator shape, or framework file role.

## Manifest Compatibility

Use the same manifest fields as existing adapters:

- `scanId`
- `repo`
- `remoteUrl`
- `branch`
- `commitSha`
- `scannerVersion`
- `scannedAt`
- `analysisLevel`
- `buildStatus`
- projects/packages
- source roots
- target language summaries
- `knownGaps`
- scan root metadata: `scanRootRelativePath`, `scanRootPathHash`, and `gitRootHash`

Analysis-level policy:

- `Level1SemanticAnalysis`: reserved for a future type-checker-backed selected scope with no known gaps. AST-only MVP must not use full semantic coverage.
- `Level1SemanticAnalysisReduced`: normal MVP case for Python projects with useful AST/package/config/framework evidence.
- `Level3SyntaxAnalysis`: no AST extraction, metadata-only scan, or syntax-only fallback without credible structural evidence.

## Fact Compatibility

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
- `SqlTextUsed`
- `DatabaseColumnMapping`
- `SerializerContractMember`
- `SymbolRelationship`
- `CallEdge`
- `ObjectCreated`
- `ArgumentPassed`
- `LocalAlias`
- `AnalysisGap`

No property should store raw source text. Literal URL, SQL, config, and serializer values should become normalized route keys where safe, hashes, lengths, kinds, and spans.

## Symbol Identity

Python symbol IDs should include:

- language: `python`
- package/project name and version when known
- module path
- qualified symbol name
- symbol kind
- file path and span for local disambiguation where needed

Examples:

```text
py:pkg:orders-api@1.2.3:module:orders.routes:function:get_order
py:pkg:orders-api@HEAD:module:orders.models:class:OrderResponse
py:local:src/app.py:42:7:function:handler
```

External imports should use package metadata when resolvable, otherwise record the imported module name as unresolved external evidence without claiming target implementation details.

## Framework Extractors

### FastAPI

Recognize:

- `FastAPI()`
- `APIRouter()`
- decorator calls: `.get`, `.post`, `.put`, `.patch`, `.delete`, `.head`, `.options`, `.api_route`
- route prefix from `APIRouter(prefix=...)` when literal
- `include_router` only as boundary evidence in MVP unless literal router variable and prefix are statically linked in the same module

Emit `HttpRouteBinding` with method, normalized path, handler symbol, route hash, router/app variable, and evidence tier.

### Flask

Recognize:

- `Flask(...)`
- `Blueprint(...)`
- `.route`, `.get`, `.post`, `.put`, `.patch`, `.delete`
- literal `methods=[...]`
- blueprint prefix only when literal in same module

Emit `HttpRouteBinding` and avoid claiming runtime blueprint registration unless `register_blueprint` is statically visible.

### Django

Recognize:

- `path(...)`
- `re_path(...)`
- `include(...)`
- `View.as_view()`
- function views and class-based views where symbol path is literal/import-resolved

Emit route facts for URL config evidence. `include` expansion is follow-up unless the included module can be statically resolved without executing imports.

### Pydantic and Schemas

Recognize:

- classes inheriting `BaseModel`
- `Field(...)`
- `model_config`, `Config`, aliases where literal
- dataclasses, TypedDict, attrs, and simple serializer/schema classes

Emit `TypeDeclared`, `FieldDeclared`, and `SerializerContractMember` where field names are statically visible. Do not claim runtime validators or computed fields unless explicitly represented in source.

### SQLAlchemy and SQL

Recognize:

- declarative models: `declarative_base`, `DeclarativeBase`, `Base`
- `Column`, `mapped_column`, `relationship`, `ForeignKey`, `Table`
- query calls: `.execute`, `.scalar`, `.scalars`, `.query`, `.filter`, `.where`, `select`, `insert`, `update`, `delete`
- `text("...")`
- DB-API cursor/connection `.execute*`
- SQL files and migrations

Emit `DatabaseColumnMapping`, `SqlTextUsed`, query boundary facts, and model/table relationship facts where statically visible. Raw SQL is hashed by default.

### HTTP Clients

Recognize:

- `requests.get/post/...`
- `requests.request(method, url, ...)`
- `httpx.get/post/...`, `httpx.Client`, `httpx.AsyncClient`
- `aiohttp.ClientSession` methods
- `urllib.request.urlopen`

Emit `HttpCallDetected` with method/path when literal or static-enough. Dynamic URLs become `DynamicClientUrlNeedsReview`-style boundary evidence, not guessed endpoints.

## SQL Shared Layer

The Python adapter should emit SQL facts into the shared schema and leave deep SQL normalization to a future shared module. That module should be language-neutral and able to consume SQL text/resource facts from .NET, TypeScript, JVM, and Python.

Initial shared SQL properties:

- `operationName`
- `textHash`
- `textLength`
- `sqlSourceKind`
- `containingType` or module/function
- `targetSymbol`
- optional `tableName`, `columnName`, `operationKind` only when deterministic extraction exists

## SQLite Writer

Python indexes must include:

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

The table shapes should match the current shared schema. Schema additions must be additive and documented.

## Report

`report.md` should include:

- repo metadata and commit SHA
- analysis level/build status
- fact counts by type/tier/rule
- package/dependency summary
- route/client/config/SQL/model counts
- analysis gaps
- Python-specific limitations

The report should explicitly state that no target code was imported or executed.

## Samples

Committed samples should include:

- `samples/python-fastapi-sample`
  - FastAPI route
  - Pydantic model
  - SQLAlchemy model/query or direct SQL
  - config/env read
  - HTTP client call
  - argument flow from handler to service/repository
- `samples/python-flask-django-sample`
  - small Flask route
  - small Django URL/view pattern
- `samples/python-broken-sample`
  - parse error and recoverable files
  - dynamic imports/config/route gaps
- contract delta fixture targeting a Pydantic field such as `OrderResponse.status`

Public OSS smoke candidates should be pinned in `docs/VALIDATION.md` after selection. Good candidates are small FastAPI/Flask/Django or SQLAlchemy projects with permissive licenses and stable testable layouts.

## Rule Catalog

New rule IDs should use a Python namespace:

```text
python.repo.manifest.v1
python.file.inventory.v1
python.package.metadata.v1
python.ast.declarations.v1
python.ast.invocation.v1
python.ast.callgraph.v1
python.ast.objectcreation.v1
python.ast.argumentflow.v1
python.ast.relationships.v1
python.integration.fastapi.v1
python.integration.flask.v1
python.integration.django.v1
python.integration.pydantic.v1
python.integration.sqlalchemy.v1
python.integration.sql.v1
python.integration.config.v1
python.integration.httpclient.v1
python.logic.shape.v1
```

Every emitted rule must be added to `rules/rule-catalog.yml` before tests pass.

## Validation

Minimum implementation validation:

- Python package tests pass.
- Existing `.NET` build/test pass after schema/docs changes.
- Existing TypeScript/JVM checks pass if shared schema/docs changed.
- `tracemap-py scan` writes required artifacts for modern and broken Python samples.
- `.NET` `tracemap reduce` classifies the Python sample contract delta.
- `.NET` `tracemap export` reads Python call/object/route/SQL rows.
- `.NET` `tracemap combine` imports Python indexes with .NET/TypeScript/JVM indexes.
- `./scripts/check-private-paths.sh` passes.

## Risks

- Python AST does not prove runtime types, decorator effects, dependency injection, or import side effects.
- Framework registration often spans modules and runtime startup; route evidence must be coverage-relative.
- Package metadata can be dynamic or split across tools; dependency facts must label uncertainty.
- SQLAlchemy expression trees can be highly dynamic; initial facts should be conservative.
- Exact endpoint alignment may need base-path/prefix propagation across router inclusion; MVP should handle only same-module literal cases.
