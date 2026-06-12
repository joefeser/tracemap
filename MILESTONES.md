# Build Milestones for Codex

## Milestone 0 — Bootstrap

Goal: Create the solution, project layout, test project, and basic CLI skeleton.

Deliverables:

- `TraceMap.sln`
- `src/TraceMap.Cli`
- `src/TraceMap.Core`
- `src/TraceMap.Storage`
- `src/TraceMap.Reporting`
- `src/tests/TraceMap.Tests`
- `tracemap scan`, `tracemap report`, `tracemap reduce` commands with help output
- Unit test proving command parsing or command dispatch works

Acceptance:

- `dotnet build` succeeds.
- `dotnet test` succeeds.
- `tracemap scan --help` works.

## Milestone 1 — Manifest, file inventory, JSONL facts

Goal: Make `tracemap scan` useful without Roslyn.

Deliverables:

- Git repo metadata: repo name, branch, commit SHA, remote URL if available
- Solution/project/config/file inventory
- `scan-manifest.json`
- `facts.ndjson`
- Basic `report.md`
- `AnalysisGap` and `BuildStatus` fact support

Acceptance:

- Running against any folder produces outputs.
- Running outside a git repo still works with `commitSha: unknown` and warning.
- Tests cover manifest and JSONL writing.

## Milestone 2 — C# syntax fallback extractor

Goal: Parse `.cs` files without build/project load.

Deliverables:

- Type/method/property/enum declarations
- Attribute names
- Member access names
- Invocation names
- File/line evidence spans
- Syntax parse errors recorded as `AnalysisGap`

Acceptance:

- Broken sample repo still emits syntax facts.
- A line containing `profile.PrimaryEmail` emits a `MemberAccessName` fact.
- No semantic claims are made from syntax-only evidence.

## Milestone 3 — SQLite index

Goal: Make facts queryable.

Deliverables:

- `index.sqlite`
- `scan_manifest` table
- `facts` table
- indexes for fact type, rule, target symbol, contract element, file
- tests that query inserted facts

Acceptance:

- Same facts are present in JSONL and SQLite.
- Report can read from SQLite.

## Milestone 4 — Roslyn semantic extractor

Goal: Add compiler-backed facts where possible.

Deliverables:

- MSBuild workspace loading
- Best-effort solution/project loading
- Semantic model extraction
- Type declarations with fully qualified symbols
- Property access facts with resolved `IPropertySymbol`
- Method invocation facts with resolved `IMethodSymbol`
- Partial compilation handling

Acceptance:

- Modern sample project emits Tier1 semantic facts.
- Broken/missing dependency sample still falls back to syntax facts.
- Build/project load failures are logged and emitted as `AnalysisGap`.

## Milestone 5 — HTTP, DB, config extractors

Goal: Capture integration surface.

Deliverables:

- HttpClient method calls
- common JSON HTTP extension calls when present
- `IHttpClientFactory.CreateClient` name extraction when obvious
- EF/EF Core-like DbContext and DbSet detection
- SaveChanges/SaveChangesAsync detection
- Dapper Query/Execute detection by invocation name/namespace where possible
- ADO.NET SqlCommand detection
- SQL-like string/file detection
- appsettings/Web.config/App.config/package/config key extraction

Acceptance:

- Sample repo report includes HTTP calls, DB calls, config keys.
- Evidence tiers are appropriate: semantic where resolved, structural/textual otherwise.

## Milestone 6 — Markdown report

Goal: Produce a useful human-readable repo map.

Deliverables:

- Repo summary
- Analysis coverage
- Known gaps
- Project/package/target framework summary
- Top contract-like DTOs
- HTTP calls
- Database calls
- Config keys
- Facts by evidence tier

Acceptance:

- Report explains partial coverage.
- Report does not claim no impact unless reducer is used.

## Milestone 7 — Basic contract reducer

Goal: Compare a contract delta to the index.

Deliverables:

- Parse `contract-delta.json`
- Match changed type/property/field names against facts
- Classify as DefiniteImpact, ProbableImpact, NeedsReview, NoEvidenceFullCoverage, NoEvidenceReducedCoverage, UnknownAnalysisGap
- Emit `impact-report.md`

Acceptance:

- Removed property with semantic property access -> DefiniteImpact.
- Removed property with syntax-only match -> NeedsReview.
- No match with full semantic coverage -> NoEvidenceFullCoverage.
- No match with reduced coverage -> NoEvidenceReducedCoverage.

## Milestone 8 — Call flow and logic shape

Goal: Make code flow and review-routing signals queryable without claiming runtime execution.

Deliverables:

- Syntax-level `CallEdge` facts for invocation expressions.
- Syntax-level `ObjectCreated` facts for object creation expressions.
- Semantic `CallEdge` facts when Roslyn resolves method and constructor symbols.
- Semantic `ObjectCreated` facts with assembly identity when Roslyn resolves created type symbols.
- Semantic `ArgumentPassed` facts that map call-site arguments to callee parameters.
- Semantic `LocalAlias` facts for simple local aliases and assignments.
- Semantic `FieldAlias` facts for simple field assignments, including constructor field initialization.
- Semantic flow-boundary facts for DI resolution, deserialization, reflection, dynamic invocation, collection mutation, object mutation, and branch conditions.
- Semantic runtime-evidence facts for statically visible DI registrations, serializer contract members, reflection targets, dynamic dispatch candidates, collection element inputs, mutation semantics, and simple branch feasibility.
- SQLite `call_edges` table for caller/callee queries.
- SQLite `object_creations` table for created-type queries.
- SQLite `argument_flows` table for parameter/argument provenance queries.
- SQLite `local_aliases` table for local alias provenance queries.
- SQLite `field_aliases` table for field alias provenance queries.
- SQLite `parameter_forward_edges` table for direct parameter-to-parameter forwarding queries.
- `tracemap flow --index <path> --symbol <symbol-or-fragment> --out <path>` for bounded parameter-forwarding path reports.
- Deterministic logic shape facts:
  - `CalculationExpression`
  - `BranchingLogic`
  - `RetryPolicyLogic`
  - `SerializationLogic`
  - `InfrastructureBoilerplate`
- Markdown report sections for call flow, logic hotspots, and boilerplate signals.

Acceptance:

- A syntax-only repo emits `CallEdge` facts with containing member and invocation name.
- A syntax-only repo emits `ObjectCreated` facts with created type and assigned variable when obvious.
- A semantic repo emits Tier1 `CallEdge` facts with fully resolved caller and callee symbols.
- A semantic repo emits Tier1 `ObjectCreated` facts with caller/created assembly name and version when available.
- A semantic repo emits Tier1 `ArgumentPassed` facts with parameter name/type, argument symbol, and source declaration span when available.
- A semantic repo emits Tier1 `LocalAlias` facts for `var alias = parameter` and simple assignment aliases.
- A semantic repo emits Tier1 `FieldAlias` facts for field assignments such as `_cached = parameter`.
- A semantic repo emits Tier1 flow-boundary facts for runtime-sensitive constructs without claiming execution or value provenance across them.
- A semantic repo emits Tier1 runtime-evidence facts for statically resolvable registrations, contract member names, reflection targets, dynamic candidates, collection inputs, mutation semantics, and simple branch feasibility while documenting runtime limitations.
- `index.sqlite` contains queryable `call_edges`.
- `index.sqlite` contains queryable `object_creations`.
- `index.sqlite` contains queryable `argument_flows`.
- `index.sqlite` contains queryable `local_aliases`.
- `index.sqlite` contains queryable `field_aliases`.
- `index.sqlite` contains queryable `parameter_forward_edges`.
- `tracemap flow` emits a Markdown report that chains direct forwarding, same-method aliases, and unique constructor field initialization with rule IDs and evidence spans.
- Scan reports include flow-boundary and runtime-evidence sections for review routing.
- Calculation/retry logic is findable without storing raw source snippets.
- Boilerplate/generated/DI glue files are labeled as review-routing signals, not omitted from inventory.
