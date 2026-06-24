# Route Flow Service/Data Composition Final Design

## Overview

This spec is the completion packet for route-centered static service/data
composition. The existing `tracemap route-flow` implementation can already
render route entries, selected static flow rows, touched files/symbols, context
groups, argument/fact-symbol projections, dependency surfaces, parameter-forward
bridges, endpoint bridge state, and initial implementation-candidate evidence.
The final slice should make the route-centered trace reliable when a reviewer
asks:

```text
selected endpoint/root method
  -> service/helper method calls
  -> implementation candidate boundaries
  -> repository/data/query/dependency/value-origin evidence rows
  -> coverage labels, gaps, classification downgrades, and limitations
```

Every row remains static evidence. The report does not prove runtime request
execution, runtime dependency injection, branch feasibility, database execution,
production use, or business impact.

## Relationship To Existing Specs

| Existing spec | Relationship |
| --- | --- |
| `route-centered-static-flow-report` | Owns the `tracemap route-flow` command, report type, JSON version, section shape, and broad classification contract. This spec preserves that contract. |
| `route-flow-service-data-composition` | Implemented the initial argument/fact-symbol projection, service/data traversal, interface candidates, data-surface gaps, redaction, and route-flow tests. This spec consumes its remaining polish and gap-hardening direction. |
| `route-flow-service-data-composition-next` | Added context groups and grouped service/data presentation over existing route-flow rows. This spec finishes the deferred unjoinable-context gaps, downgrade hardening, and remaining candidate/data tests rather than adding another presentation-only layer. |
| `route-centered-endpoint-trace-completeness` | Owns broad endpoint trace completeness and touched-file/touched-symbol summaries. This spec owns only service/data/query/dependency/value-origin composition and downgrades for selected route-flow rows. |
| `route-flow-endpoint-stitching` | Added endpoint bridge state and began endpoint stitching for issue #201. This spec consumes the remaining endpoint method-to-call-edge and service/data attachment precision needed for the route-centered service/data trace. |
| `static-dispatch-candidate-bridges` | If present on the implementation base, defines shared dispatch-candidate contracts that route-flow may consume. This spec consumes candidate evidence through route-flow interface bridge rows and does not redefine shared candidate derivation rules. |
| GitHub issues #159, #179, #201 | Provide the public-safe problem statements: route-flow should start from a route/client/root, stitch into method/service/implementation evidence, and render business/data/dependency rows or specific gaps. |

If live code already satisfies a row in this design, implementation should
record the evidence and move to the next smallest unchecked task.

## Already Shipped Versus Remaining

Current `dev` already contains the route-flow command, combined route-flow rule
family, the five route-flow classifications, bridge-state output, context
groups, argument/fact-symbol projection readers, parameter-forward traversal,
implementation-candidate rows, and several bridge/data-surface gap families.

This final spec is a closure packet rather than a new architecture. The
concrete remaining delta is to audit live state and complete whichever of these
still lack implementation or regression coverage:

- endpoint/root method to direct downstream call-edge stitching when route-flow
  still stops at the root;
- duplicate/ambiguous root and missing-call gaps that explain zero-row traces;
- candidate continuation edge cases such as multiple/no/high-fan-out/syntax/
  name-only/cross-source/cross-language candidates;
- adjacent-but-unjoinable data/query/dependency/value-origin facts that should
  emit scoped projection or attachment gaps;
- downgrade, redaction, deterministic-output, and public-safe smoke coverage
  for the final service/data route trace.

## Goals

- Complete route-root to downstream service/data composition over existing
  combined evidence.
- Make missing bridges actionable: root ambiguity, method bridge missing,
  missing call edge, implementation candidate unavailable/ambiguous, projection
  unavailable, data attachment missing, schema/extractor gaps, identity gaps,
  and truncation.
- Preserve all evidence provenance: rule IDs, evidence tiers, file spans,
  supporting fact IDs, supporting edge IDs, source labels, source index IDs,
  commit SHAs, extractor identities, coverage labels, classifications, and
  limitations.
- Keep interface, override, DI-adjacent, dynamic, syntax-only, name-only,
  high-fan-out, and reduced-coverage evidence at review-tier or unknown.
- Keep JSON and Markdown deterministic and backward-compatible.
- Keep all committed fixtures, reports, logs, and review notes public-safe.

## Non-Goals

- No scanner rewrite or new language adapter scope.
- No new route-flow command, report type, JSON version, rule namespace, or
  persisted derived tables.
- No runtime proof of request execution, dependency-injection target selection,
  dynamic dispatch, branch feasibility, authorization, SQL execution, database
  existence, production traffic, release safety, outage cause, or business
  impact.
- No raw source snippets, raw SQL, config values, connection strings, raw URLs,
  query strings, raw remotes, local absolute paths, private labels, private
  sample names, private route values, hostnames, secrets, or raw diagnostics in
  default outputs.
- No LLM calls, embeddings, vector databases, semantic search, fuzzy matching,
  or prompt-based classification.

## Command And Output Contract

The public command remains:

```bash
tracemap route-flow --index combined.sqlite --route "GET /api/admin/users/roles" --out out/
tracemap route-flow --index combined.sqlite --client-call "GET /api/items/{id}" --out out/
tracemap route-flow --index combined.sqlite --from-endpoint "GET /api/items/{id}" --out out/
```

The examples are synthetic. The implementation should keep existing selector
grammar, caps, file output behavior, and `--exit-code` semantics.

Directory or extensionless output writes:

```text
route-flow-report.md
route-flow-report.json
```

JSON remains additive:

```json
{
  "reportType": "route-flow",
  "version": "1.0",
  "reportCoverage": "FullEvidenceAvailable",
  "coverageWarnings": [],
  "query": {},
  "snapshot": {},
  "summary": {},
  "entryEvidence": [],
  "flowRows": [],
  "logicRows": [],
  "dependencySurfaces": [],
  "touchedFiles": [],
  "touchedSymbols": [],
  "contextGroups": [],
  "gaps": [],
  "limitations": []
}
```

This spec may add fields on existing rows or context groups, such as:

- `bridgeState`
- `groupKind`
- `matchKind`
- `attachmentKind`
- `pathContextKind`
- `valueSafety`
- `classificationCap`
- `supportingRowIds`
- `supportingFactIds`
- `supportingEdgeIds`

Fields already emitted by prior route-flow specs must be confirmed during the
Task 4 live audit before treating this list as new additive work. Existing
fields must not be renamed or redefined in this PR boundary.

New collections should be avoided unless implementation review proves existing
row shapes cannot express the evidence. Any new collection must be additive,
deterministically sorted, and rendered as an empty array when unavailable.

Markdown keeps the existing route-flow section order. Narrow refinements are
allowed for Endpoint Root, Static Flow, Context Groups, Business/Data Logic,
Dependency Surfaces, Value Origin, Gaps, and Limitations.

## Data Sources

The final slice reads existing combined-index evidence read-only:

- combined source metadata: source labels, source index IDs, scan IDs, commit
  SHAs, source identity, coverage, build/analysis state, extractor versions;
- endpoint evidence: HTTP route bindings, HTTP client calls, endpoint
  alignment, WebForms/legacy roots where already supported;
- graph evidence: `combined_dependency_edges`, `combined_call_edges`,
  `combined_object_creations`, `combined_parameter_forward_edges`, and path
  graph helper rows already used by route-flow;
- symbol evidence: `combined_symbols`, `combined_symbol_relationships`, and
  selected source-local symbol IDs;
- projection evidence: `combined_argument_flows` and `combined_fact_symbols`;
- attached facts: object/projection/DTO, query-shape, SQL-shape,
  repository/data access, legacy-data, package/config, HTTP client, WCF,
  ASMX/SOAP, remoting, storage, queue/event, validation/guard, branch,
  serializer/contract, async/callback, and flow-boundary facts;
- analysis gaps and optional schema/extractor availability evidence.

Older indexes may lack optional route-flow precision tables. Missing optional
tables or columns produce `SchemaMissing` or equivalent gaps. Missing
extractor-family evidence produces `ExtractorUnavailable` or equivalent gaps.
Neither condition is clean absence.

## Stitching Pipeline

### 1. Live Audit

Before product edits, inspect live route-flow behavior and tests. Record in the
implementation state which existing code already satisfies this spec and which
unchecked task remains in scope for the PR.

### 2. Selector And Endpoint Root

Reuse existing selector normalization and redaction. A selected endpoint root is
usable for downstream traversal only when it has one of these static bridge
states:

- `method-symbol`
- `path-node`
- `symbol-fallback`

The states `missing`, `ambiguous`, and `reduced-coverage` remain reportable but
must not seed strong downstream conclusions. Duplicate route roots or ambiguous
normalized selectors produce deterministic gaps.

### 3. Endpoint Root To Direct Calls

Traverse from the root using source-local graph identity:

- caller/callee symbol IDs;
- path graph node IDs;
- supporting fact IDs;
- supporting edge IDs;
- route-flow helper identity already used by `CombinedRouteFlowReport`.

Do not stitch by same file, directory, short name, display label, route text, or
type name alone. If call-like evidence exists but cannot be joined, emit a
scoped missing-call/projection gap with counts and safe categories.

### 4. Implementation Candidates

When a traversed target is an interface member, read source-local implementation
relationships from `combined_symbol_relationships`. Candidate bridge rows carry
relationship kind, source and target symbol IDs, supporting fact or edge IDs,
file spans, evidence tier, rule IDs, candidate count, and limitations.

Classification rules:

- direct concrete call rows may be stronger when full coverage and semantic
  evidence allow it;
- any required implementation-candidate bridge caps dependent rows at
  `NeedsReviewStaticRouteFlow`;
- multiple candidates, no candidates, high fan-out, cross-source,
  cross-language, name-only, syntax-only, generated-code uncertain, or reduced
  coverage evidence stays review-tier or unknown;
- DI registration and service locator facts are context/limitations only, not
  runtime target proof.

### 5. Service/Data/Query/Dependency Attachment

Attach context only when one of these relationships exists:

- selected route-flow path edge;
- traversed source or target method symbol;
- selected source-local fact-symbol attachment;
- selected argument-flow or parameter-forward row;
- selected route-flow dependency surface;
- supporting fact/edge IDs shared with a selected row.

Do not attach by same-file or textual proximity alone. If facts exist but are
unjoinable, emit scoped gaps rather than silently dropping them or inferring a
flow.

Context row kinds should reuse existing row collections:

- service/helper/repository method evidence: `flowRows` or `contextGroups`;
- object/projection/validation/branch/value-origin evidence: `logicRows`;
- package/config/HTTP/client/service/SQL/data/legacy/event/storage surfaces:
  `dependencySurfaces` or `logicRows` according to current route-flow style;
- unjoinable or unavailable context: `gaps`;
- grouping summaries: `contextGroups`.

## Gap Taxonomy

Reuse shipped gap names before adding new ones. The implementation should map
concepts to current route-flow vocabulary during live audit.

Expected families include:

- `SelectorNoMatch`
- `MissingMethodSymbolBridge`
- `MissingRouteRoot`
- `MissingCallEdge`
- `MissingImplementationBridge`
- `ImplementationCandidateUnavailable`
- `AmbiguousImplementationCandidates`
- `RuntimeBindingNotProven`
- `ArgumentProjectionUnavailable`
- `FactSymbolProjectionUnavailable`
- `DataSurfaceAttachmentMissing`
- `SchemaMissing`
- `ExtractorUnavailable`
- `IdentityGap`
- `ReducedCoverage`
- `TruncatedByLimit`
- `TraversalBounds`
- `UnsafeValueOmitted`

Duplicate or ambiguous endpoint-root states should map to `SelectorNoMatch`
when the selector itself is ambiguous, or to `MissingRouteRoot` when the
selected root cannot resolve to route-root evidence. A new duplicate-root code
requires amending `combined.route-flow.gap.v1`.

New gap codes are allowed only after a rule-catalog update documents the code,
limitations, evidence tier, and safe rendering behavior.

Gap codes are closed-set values carried under route-flow rules such as
`combined.route-flow.gap.v1`; they are not rule IDs by themselves. Rule-catalog
resolution tests apply to emitted rule IDs. When a new gap code is needed, the
implementation should amend the existing route-flow gap rule entry and
limitations rather than creating a parallel rule namespace.

## Classification Rollup

Use only existing route-flow classifications:

- `StrongStaticRouteFlow`
- `ProbableStaticRouteFlow`
- `NeedsReviewStaticRouteFlow`
- `NoRouteFlowEvidence`
- `UnknownAnalysisGap`

Rollup rules:

- Strong requires full relevant coverage, verified identity, known commit SHA,
  unambiguous root bridge, at least one stitched downstream row backed by
  Tier1Semantic or Tier2Structural evidence, and no candidate bridge required
  on the critical path.
- Probable is the strongest allowed when structural but credible static evidence
  connects the route without semantic completeness.
- NeedsReview applies to candidate bridges, syntax/text/name-only evidence,
  ambiguity, high fan-out, dynamic selectors, generated-code uncertainty,
  unjoined context, and other review-sensitive rows.
- Unknown wins when coverage, schema, extractor, source identity, commit, or
  truncation prevents a credible conclusion.
- NoRouteFlowEvidence requires full coverage and no unresolved bridge,
  projection, identity, schema, extractor, selector, or truncation gaps.

Full relevant route-flow coverage is defined normatively in Requirement 5.8.

Grouped context inherits the weakest classification, weakest evidence tier, and
weakest coverage from its contributing rows.

## Determinism

Stable IDs must be derived only from safe deterministic inputs:

- safe source labels;
- route root keys or hashes;
- selected row kinds;
- supporting fact IDs;
- supporting edge IDs;
- symbol IDs;
- repo-relative file paths;
- line spans;
- safe closed-set metadata.

Ordering should use ordinal comparison over:

```text
source label -> route/root key -> selector kind -> row kind ->
classification rank -> path length -> safe display label -> file path ->
start line -> end line -> symbol ID -> fact ID -> edge ID -> stable row ID
```

Outputs must not include timestamps or local artifact paths. Optional values
use explicit `null`, empty arrays, or closed-set placeholders.

## Safety

All rendering must use existing safe rendering and hashing helpers where
practical. Unsafe values include:

- raw SQL;
- raw config values;
- connection strings;
- secrets and tokens;
- source snippets;
- raw endpoint URLs and query strings;
- hostnames;
- private route strings;
- private sample names;
- private repository names;
- raw local absolute paths;
- raw remotes;
- private labels;
- raw diagnostics.

Unsafe values are omitted, hashed, or replaced with closed-set descriptors.
Rows that redact unsafe values cite `combined.route-flow.redaction.v1` where
supporting rule IDs are available.

## Next Implementation PR Scope

Use the Suggested PR Boundaries in `tasks.md` as the implementation sequencing
source of truth. The first implementation PR should begin with the audit task,
then ship only the smallest bridge/gap/test slice that the audit identifies as
still missing. If a task grows large, split after the audit and keep each PR to
one evidence-backed route-flow improvement.

## Follow-Ups

Keep out of the next PR unless the audit proves they are tiny prerequisites:

- batch route-flow queries;
- HTML or graph visualization;
- persisted route-flow derived rows;
- new scanner/language-adapter extraction;
- runtime DI/container analysis;
- cross-repository runtime binding;
- site/public copy;
- private smoke artifact publication.

## Validation Plan

For implementation:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Follow `docs/VALIDATION.md` for route-flow/reporting changes. Run the relevant
pinned public-safe smoke checks or record an explicit deferral in
`implementation-state.md` with rationale. Private validation, if performed,
must remain local-only and be summarized generically.
