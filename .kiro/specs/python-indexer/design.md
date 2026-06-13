# Python Indexer Design

## Overview

Add a Python TraceMap scanner under `src/python`. The package will produce TraceMap-compatible artifacts for Python repositories:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

The scanner will use Python `ast` for deterministic source analysis, local package/config metadata for structural evidence, and conservative textual fallback for files that cannot parse. It will not import target modules, execute decorators, start framework apps, run tests, install packages, or resolve package dependencies through network commands.

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
- Emit enough facts for contract reduction, dependency traversal, endpoint evidence, persistence review, serializer/schema review, and config review.
- Treat SQL as shared cross-language data dependency evidence rather than a Python-only concern.
- Keep limitations explicit in rule catalog entries, scan manifests, and reports.

## Non-Goals

- No LLM calls or embeddings.
- No runtime imports or execution of target repository code.
- No framework startup for FastAPI, Flask, Django, Celery, Airflow, or any app.
- No package installation, dependency restore, virtualenv creation, or network calls during `scan`.
- No claim of runtime reachability, middleware behavior, auth behavior, feature-flag state, dependency-injection binding, monkey-patch target, dynamic import target, decorator side effects, ORM runtime schema, serializer runtime mapping, collection contents, mutation semantics, or branch feasibility.
- No raw source snippets by default.
- No full SQL parser in the Python adapter MVP.
- No cross-module type inference in MVP.
- No `.pyi`, `__all__`, wildcard-import, descriptor, metaclass, `@property`, cached-property, validator, computed-field, context-manager, async-generator, or cache-decorator semantics in MVP.
- No Python reimplementation of `.NET` `tracemap reduce`, `tracemap export`, `tracemap combine`, or endpoint alignment.

## Locked Decisions

- Initial CLI is standalone `tracemap-py`; root `tracemap` dispatch is follow-up.
- Existing `.NET` `tracemap reduce`, `tracemap export`, `tracemap combine`, and endpoint alignment are downstream commands for Python indexes.
- Implementation lives under `src/python`, not folded into the TypeScript/JVM packages.
- MVP semantic truth is AST/package/config evidence; type-checker-backed Tier1 is post-MVP.
- FastAPI, Flask, Pydantic, dataclasses, SQLAlchemy declared columns, direct SQL, env/config reads, and `requests`/`httpx` are MVP integration families.
- Django, aiohttp, urllib, attrs, TypedDict, Poetry/uv lock parsing, and richer logic-shape detection are post-MVP.
- SQL normalization beyond hashes/operation/source spans is a shared cross-language follow-up.
- Real Python MVP scans should produce `ProbableImpact`, `NeedsReview`, or reduced no-evidence outcomes, not `DefiniteImpact`.
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
      package_model.py
    extract/
      ast_extractor.py
      route_extractor.py
      http_client_extractor.py
      pydantic_extractor.py
      dataclass_extractor.py
      sqlalchemy_extractor.py
      sql_extractor.py
      config_extractor.py
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

Implementation language should be Python 3.11+ to reuse Python `ast`, `tomllib`, and pytest fixture ergonomics. The scanner itself should avoid dependency-heavy framework imports; framework support should operate over source syntax and local metadata.

## CLI Shape

Initial Python CLI:

```bash
cd src/python
python -m pip install -e .
tracemap-py scan --repo <path> --out <path>
tracemap reduce --index <path>/index.sqlite --contract-delta delta.json --out report.md
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
4. Discover package roots before computing module or symbol IDs.
5. Parse package metadata and config files.
6. Parse Python files with `ast.parse`.
7. Emit baseline declaration/import/invocation-name/call/object/argument/member facts.
8. Run framework/integration extractors over AST and metadata context.
9. Run SQL/config/resource extractors.
10. Derive symbol, call, object, argument-flow, and relationship rows for SQLite.
11. Compute coverage level from gaps.
12. Rebuild the output directory contents and write manifest, facts, SQLite, report, and analyzer log.

## Evidence Tiers

Python evidence needs careful tiering because AST is deterministic but not compiler-resolved in the same sense as Roslyn, TypeScript, or Javac.

- `Tier1Semantic`: reserved for future type-checker/import-resolved evidence that proves the symbol with explicit project/type context. Real MVP Python extraction should not emit Tier1.
- `Tier2Structural`: framework/package/config evidence where imports, package metadata, decorators, base classes, or known file roles prove a common structure. Examples: FastAPI route decorator with FastAPI import evidence; Pydantic `BaseModel` class with import/base evidence; SQLAlchemy `Column` under declarative model evidence.
- `Tier3SyntaxOrTextual`: AST-only declarations, calls, member names, direct strings, or syntax shape without package/framework confirmation.
- `Tier4Unknown`: analysis gaps and unable-to-prove states.

Do not upgrade a fact to Tier2 solely because a name looks likely. Tier2 requires recognized local structure: import, dependency metadata, base class/decorator shape, or framework file role.

## Reducer Classification Policy

Existing reducer behavior is tier-driven:

- `DefiniteImpact` requires a reducer-recognized definite fact type and Tier1 semantic evidence.
- `ProbableImpact` is appropriate for strong structural DTO, HTTP, DB, config, dependency, and serializer facts.
- `NeedsReview` is appropriate for syntax/textual name matches and dynamic boundaries.
- `NoEvidenceReducedCoverage` is the expected no-match result for MVP Python scans.

Because AST-only Python cannot prove receiver types, real MVP extraction should not emit Tier1 `PropertyAccessed` or `MethodInvoked`. A small synthetic fixture may hand-author Tier1 facts only to prove .NET reducer wiring, and should be labeled as a fixture rather than real scanner output.

`contractElement` is the fact-envelope field. It does not replace reducer-matched property keys such as `memberName`, `fieldName`, `methodName`, `containingType`, or `targetSymbol`.

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

`buildStatus = FailedOrPartial` is the compatibility value for Python MVP reduced coverage. It means "full semantic analysis was not achieved"; it does not mean the scanner attempted and failed a Python build.

`scanId` should be deterministic and mirror the existing .NET approach: derive it from stable repository identity, commit SHA, scan options, and a sorted file-inventory signature. It must not use timestamps, UUIDs, process IDs, or output paths because fact IDs include `scanId`.

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

Reducer-compatible MVP fact types:

- `TypeDeclared`
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

`PropertyAccessed` and `MethodInvoked` are post-MVP unless a type-checker-backed slice can prove receiver types.

No property should store raw source text. Literal URL, SQL, config, and serializer values should become normalized route keys where safe, hashes, lengths, kinds, and spans.

Synthetic Tier1 fixtures should use an explicit fixture rule ID and properties such as `isSyntheticFixture = "true"` so they cannot be confused with real Python extractor output.

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

Reducer-facing `targetSymbol` values should be human dotted display symbols, such as `orders.models.OrderResponse.status`, so `Type.member` contract deltas can match. Stable internal IDs should be stored in `sourceSymbolId` and `targetSymbolId` properties and symbol tables.

Facts that populate symbol/relationship/flow tables should use the role-property convention in `docs/LANGUAGE_ADAPTER_CONTRACT.md`: `{role}SymbolId`, `{role}SymbolLanguage`, `{role}SymbolKind`, and `{role}SymbolDisplayName`. Python should not invent different role names for the shared SQLite tables.

## Framework Extractors

### FastAPI

Recognize:

- `FastAPI()`
- `APIRouter()`
- decorator calls: `.get`, `.post`, `.put`, `.patch`, `.delete`, `.head`, `.options`, `.api_route`
- route prefix from `APIRouter(prefix=...)` when literal
- `include_router` only as boundary evidence in MVP unless literal router variable and prefix are statically linked in the same module

Emit `HttpRouteBinding` with method, normalized path, handler symbol, route hash, router/app variable, and evidence tier.

Tier2 requires FastAPI import/dependency evidence plus a route-shaped decorator or app/router construction. Route-shaped decorators without evidence are Tier3. Same-module literal `APIRouter(prefix=...)` can be combined into `normalizedPathKey`; unresolved `include_router` should emit a gap rather than a merged path.

### Flask

Recognize:

- `Flask(...)`
- `Blueprint(...)`
- `.route`, `.get`, `.post`, `.put`, `.patch`, `.delete`
- literal `methods=[...]`
- blueprint prefix only when literal in same module

Emit `HttpRouteBinding` and avoid claiming runtime blueprint registration unless `register_blueprint` is statically visible in a same-module literal pattern.

Tier2 requires Flask import/dependency evidence plus same-module app or blueprint evidence. Runtime blueprint registration outside the module is a gap.

### Pydantic and Dataclasses

Recognize:

- classes inheriting `BaseModel`
- `Field(...)`
- `model_config`, `Config`, aliases where literal
- standard-library `@dataclass`

Emit `TypeDeclared`, `FieldDeclared`, and `SerializerContractMember` where field names are statically visible. Do not claim runtime validators, computed fields, descriptors, or post-init mutation.

Pydantic v2 `Annotated[T, Field(...)]` is MVP-supported only for the direct `Annotated[T, Field(...)]` pattern. Arbitrary `Annotated` metadata is not expanded. Dataclass Tier2 requires a visible standard-library `dataclasses` import or fully qualified `dataclasses.dataclass` decorator.

### SQLAlchemy and SQL

Recognize:

- declarative model bases when visible from imports/base class syntax
- `Column`, `mapped_column`, `ForeignKey`, `Table`
- query boundary calls: `.execute`, `.scalar`, `.scalars`, `.query`, `.filter`, `.where`, `select`, `insert`, `update`, `delete`
- `text("...")`
- DB-API cursor/connection `.execute*`
- SQL files and migration files

Emit `DatabaseColumnMapping` only for literal declarative column-to-attribute declarations with SQLAlchemy import evidence and a recognized declarative base. SQLAlchemy 1.x `Column(...)` and 2.0 `Mapped[...]`/`mapped_column(...)` are in scope. Emit `tableName` only when `__tablename__` is a literal string. ForeignKey string targets should be hashed reference evidence, not resolved table names. Emit `SqlTextUsed` for direct SQL literals. Emit `AnalysisGap` boundary facts for ORM expression builders, filters, relationship strings, and dynamic SQL; do not infer runtime table/column usage from those patterns.

### HTTP Clients

Recognize:

- `requests.get/post/...`
- `requests.request(method, url, ...)`
- `httpx.get/post/...`, `httpx.Client`, `httpx.AsyncClient`

Emit `HttpCallDetected` with method/path when literal or static-enough and import/dependency evidence exists. Dynamic URLs become boundary evidence, not guessed endpoints.

## SQL Shared Layer

The Python adapter should emit SQL facts into the shared schema and leave deep SQL normalization to a future shared module. That module should be language-neutral and able to consume SQL text/resource facts from .NET, TypeScript, JVM, and Python.

Initial shared SQL properties:

- `textHash`: SHA-256 over the exact raw SQL string bytes encoded as UTF-8, truncated to 32 lowercase hex chars.
- `textLength`: length of the raw SQL string.
- `operationName`: only the visible leading SQL verb from a direct literal after trimming leading whitespace; empty when dynamic, unclear, or not one of `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `CREATE`, `ALTER`, `DROP`, `TRUNCATE`, `CALL`, `EXEC`, or `EXECUTE`.
- `sqlSourceKind`: shared values from `docs/LANGUAGE_ADAPTER_CONTRACT.md`, such as `literal-string`, `sql-file`, `migration-file`, `orm-text`, `dbapi-execute`, and `dynamic-boundary`.
- `containingModule`, `containingType`, `containingFunction`, or equivalent symbol display fields.
- `targetSymbol`: reducer-friendly display symbol.

Before the shared SQL parser exists, Python must not emit `tableName`, `columnName`, or `operationKind` from guessed SQL text. Dynamic SQL from f-strings, concatenation, `.format`, templates, or ORM builders should be represented as `AnalysisGap` boundary facts with `gapKind` values such as `dynamic-sql` or `orm-query-boundary`, plus hashes of expressions, not raw snippets.

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

The table shapes should match the current shared schema and snake_case DDL used by the .NET storage layer. JSON fact properties remain camelCase. Schema additions must be additive and documented in `docs/LANGUAGE_ADAPTER_CONTRACT.md`.

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

For MVP Python scans, the report should explain that `buildStatus = FailedOrPartial` means AST/package/config evidence was collected but full type-checker semantic analysis was not performed. This is expected for MVP scans and does not by itself indicate a scanner error.

## Samples

Committed samples should include:

- `samples/python-fastapi-sample`
  - FastAPI route
  - Pydantic model
  - SQLAlchemy declared column and direct SQL
  - config/env read
  - `requests` or `httpx` client call
  - argument flow from handler to service/repository
- `samples/python-flask-sample`
  - small Flask route
  - literal config/env read
  - dynamic route/client examples that produce reduced evidence
- `samples/python-broken-sample`
  - parse error and recoverable files
  - dynamic imports/config/route gaps
- contract delta fixture targeting a Pydantic field such as `OrderResponse.status`
- synthetic reducer fixture with hand-authored Tier1 facts for `DefiniteImpact` compatibility only

Public OSS smoke candidates should be pinned in `docs/VALIDATION.md` after selection. Good candidates are small FastAPI/Flask or SQLAlchemy projects with permissive licenses and stable testable layouts.

## Rule Catalog

New rule IDs should use a Python namespace:

```text
python.repo.manifest.v1
python.file.inventory.v1
python.package.metadata.v1
python.ast.declarations.v1
python.ast.imports.v1
python.ast.invocation.v1
python.ast.callgraph.v1
python.ast.objectcreation.v1
python.ast.argumentflow.v1
python.ast.relationships.v1
python.ast.dynamic-boundary.v1
python.integration.fastapi.v1
python.integration.flask.v1
python.integration.pydantic.v1
python.integration.dataclass.v1
python.integration.sqlalchemy.v1
python.integration.sql.v1
python.integration.config.v1
python.integration.httpclient.v1
python.boilerplate.v1
```

Every emitted rule must be added to `rules/rule-catalog.yml` before tests pass.

## Validation

Minimum implementation validation:

- Python package tests pass.
- Existing `.NET` build/test pass after schema/docs changes.
- Existing TypeScript/JVM checks pass if shared schema/docs changed.
- `tracemap-py scan` writes required artifacts for modern and broken Python samples.
- `.NET` `tracemap reduce` classifies the synthetic Python fixture as `DefiniteImpact`.
- `.NET` `tracemap reduce` classifies a real Python structural contract match as `ProbableImpact` or `NeedsReview`.
- `.NET` `tracemap export` reads Python call/object/route/SQL rows.
- `.NET` `tracemap combine` imports Python indexes with .NET/TypeScript/JVM indexes.
- `.NET` endpoint alignment reads Python route/client rows without error; exact cross-repo matching remains a post-MVP quality goal.
- No-match MVP Python deltas report `NoEvidenceReducedCoverage`.
- `./scripts/check-private-paths.sh` passes.

Endpoint alignment should be smoke-tested after route and HTTP client facts exist, but full cross-repo endpoint matching is not a blocker for the MVP scanner.

## Risks

- Python AST does not prove runtime types, decorator effects, dependency injection, or import side effects.
- Framework registration often spans modules and runtime startup; route evidence must be coverage-relative.
- Package metadata can be dynamic or split across tools; dependency facts must label uncertainty.
- SQLAlchemy expression trees can be highly dynamic; initial facts should be conservative.
- Exact endpoint alignment may need base-path/prefix propagation across router inclusion; MVP should handle only same-module literal cases.
