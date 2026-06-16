# Design

## Overview

Add a deterministic legacy flow composition/reporting layer over existing
TraceMap indexes. The feature reads already-emitted facts and edges, assembles
bounded static paths, and writes conservative Markdown/JSON reports for old .NET
applications.

Intended evidence chain:

```text
WebForms event binding / HTTP or service surface
  -> resolved handler or service/API symbol
  -> call, creation, parameter-forward, or symbol relationship evidence
  -> WCF/service-reference mapping, HTTP client, SQL/query, legacy data metadata,
     or dependency surface evidence
  -> flow report/query with provenance, coverage, and limitations
```

Every output is a static evidence view. It must not claim runtime page lifecycle
execution, event firing, endpoint reachability, service deployment, SQL
execution, database existence, production usage, branch feasibility, permission
checks, or user behavior.

## Goals

- Compose existing static evidence into user-action-to-backend/data views.
- Reuse WebForms event flow, WCF metadata normalization, HTTP/API, SQL/query,
  dependency-surface, and future legacy data metadata facts.
- Preserve supporting fact IDs, edge IDs, rule IDs, evidence tiers, file spans,
  commit SHA, extractor versions, and source coverage.
- Distinguish strong static paths, probable static paths, needs-review paths, no
  backend evidence, reduced coverage, and analysis gaps.
- Keep output deterministic, bounded, reviewable, and safe for redacted demos.
- Work against older indexes by emitting schema/extractor availability gaps.

## Non-Goals

- No runtime tracing, browser automation, IIS hosting, service execution, DB
  connections, or API calls.
- No WebForms lifecycle, ViewState, postback, event bubbling, session, auth, or
  permission simulation.
- No proof that an event always reaches a backend or that SQL/data evidence
  executes.
- No new extractor work in this slice beyond minimal reader compatibility
  required for the report.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  probabilistic ranking in TraceMap core.
- No raw source snippets, raw SQL, config values, connection strings, raw URLs,
  raw remotes, local absolute paths, private sample labels, or secrets in
  generated artifacts.
- No committed local smoke outputs.

## Command Shape

Extend the existing `tracemap paths` command instead of adding a separate
near-duplicate command. The legacy flow view is a paths mode with additional
root types, legacy-focused grouping, and stricter wording.

```bash
tracemap paths --index <index.sqlite|combined.sqlite> --out <path> --include-legacy-roots [options]
```

Options:

```text
--format <markdown|json>
--include-legacy-roots
--view <paths|legacy-flows>
--from-webforms-event <page-or-event-selector>
--from-endpoint "<METHOD> <PATH_KEY>"
--from-symbol <symbol-selector>
--from-source <label>
--to-surface <sql-query|http-route|http-client|package-config|wcf-operation|legacy-data|dependency-surface>
--surface-name <text>
--source-pair <a>:<b>
--classification <StrongStaticPath|ProbableStaticPath|NeedsReviewStaticPath|NoBackendEvidence|ReducedCoverage|AnalysisGap>
--max-depth <n>
--max-paths <n>
--max-frontier <n>
```

Defaults:

- Keep existing `tracemap paths` output semantics: file outputs honor
  `--format <markdown|json>`, and directory outputs write both Markdown and JSON
  artifacts regardless of `--format`.
- `maxDepth = 8`, `maxPaths = 100`, `maxFrontier = 10000`.
- If no selectors are provided and `--include-legacy-roots` is set, summarize
  all credible WebForms event roots and API/service roots, then show bounded
  representative paths grouped by classification and terminal surface kind.
- If a selector does not match, return a valid empty report with a
  `SelectorNoMatch` gap.
- Open SQLite indexes read-only.
- Reject unsupported files with a clear diagnostic.

`--view legacy-flows` controls report wording and grouping only. It must not
create a second graph engine or divergent selector grammar. Existing `paths`
selectors keep their meanings; new legacy selectors are additive.
Existing selectors remain supported in legacy-flow view: `--from-source`
narrows start evidence to a source label, `--surface-name` filters terminal
surface identity using the current exact/wildcard behavior, and `--source-pair`
constrains explicit endpoint or dependency crossings where such combined
evidence exists.
Safe generated display identities, including redacted hash display values such
as `<kind>-<hash-prefix>`, are surface identities for this selector model; they
do not introduce language-specific implicit prefix matching outside the existing
exact/wildcard behavior.

## Proposed Package Layout

```text
src/dotnet/
  TraceMap.Reporting/
    CombinedDependencyPaths.cs
    LegacyFlowCompositionModels.cs
    LegacyFlowIndexReader.cs
    LegacyFlowClassifier.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    LegacyFlowCompositionTests.cs
```

`TraceMap.Reporting` remains the home because this is a query/reporting layer
over existing facts and edges. Reuse existing combined path/report graph helpers
where they already preserve provenance and deterministic ordering. If those
helpers are not reusable, extract shared read-only graph primitives first in a
behavior-preserving step.

Concrete reuse targets:

- `CombinedDependencyPathReporter`, `CombinedDependencyPathOptions`,
  `CombinedDependencyPathReport`, `CombinedPath`, `CombinedPathNode`,
  `CombinedPathEdge`, and `CombinedPathGap` for the existing paths command,
  output contract, graph traversal behavior, and path-context integration.
- `CombinedDependencyReporter`, `CombinedReportSource`, endpoint matching
  helpers, and surface projection helpers for source inventory, endpoint/API
  evidence, and dependency-surface rows.
- `CombinedReportHelpers` for stable hashing, sorted metadata, safe path
  rendering, output writing, and Markdown/JSON escaping conventions.
- `TraceMap.Storage.FlowPathReporter` and `SqliteIndexWriter` table conventions
  for single-index call/argument/parameter-forward evidence when single-index
  `paths` support is added.
- `IndexExporter` table/export models as a reference for combined and
  single-index schema compatibility, not as a report data source.

Parameter-forward edges are optional input evidence. When they are unavailable
because the parameter-origin feature is absent or the index predates it, the
legacy paths mode emits an availability gap and continues with call/object and
fact-symbol evidence.

## Data Sources

Read from single-index and combined-index tables where available. The reader
should tolerate missing tables/columns and emit availability gaps.

Primary fact families:

| Evidence | Example fact/edge inputs | Role |
| --- | --- | --- |
| WebForms roots | `WebFormsEventBindingDeclared`, `WebFormsHandlerResolved`, `WebFormsEventFlowProjected` | UI-action roots and existing direct projections |
| C# graph | call edges, object creations, symbol relationships, optional parameter-forward edges | Static traversal |
| WCF/service reference | generated client, operation contract, service endpoint, metadata operation, service-reference mapping | Service/backend terminal or intermediate evidence |
| HTTP/API | route/endpoint facts, HTTP client/dependency surfaces | API roots and downstream HTTP terminals |
| SQL/query | `SqlTextUsed`, `QueryPatternDetected`, SQL dependency surfaces | Data terminal evidence |
| Legacy data metadata | optional `LegacyData*` facts and generated-code links | Terminal evidence when a root reaches a generated data type, mapped storage object, table adapter, or query/data descriptor; intermediate context only when it corroborates an already connected SQL/query or generated-code path |
| Coverage/gaps | scan manifest, extractor versions, coverage labels, `AnalysisGap` facts | Coverage and classification caps |

The implementation should not require legacy data metadata facts to exist. If
they are missing and no other backend terminal evidence is found for a root, the
result is `AnalysisGap` with `ExtractorUnavailable: legacy-data-metadata`, not
`NoBackendEvidence`.

## Relationship To `legacy.webforms.event-flow.v1`

Existing `WebFormsEventFlowProjected` facts from `legacy.webforms.event-flow.v1`
are inputs, not a competing classifier. The legacy paths mode treats each
projection as an opaque single-hop evidence edge from the resolved handler to
its projected terminal or intermediate surface when primitive graph evidence is
missing. When primitive call/object/WCF/HTTP/SQL edges are also present, the
graph builder de-duplicates the projection and primitive edge by supporting fact
ID, supporting edge ID, root identity, terminal identity, and source label.

Projection facts cannot upgrade path confidence. A path that uses
`WebFormsEventFlowProjected` keeps the projection fact's rule ID and evidence
tier, then applies the same weakest-required-evidence cap as any other path. If
the primitive path and projection disagree on terminal identity or
classification, the result is capped at `NeedsReviewStaticPath` and includes an
ambiguity note rather than choosing the stronger conclusion.

## Proposed Rule IDs

Add rule catalog entries before implementation emits new results:

- `legacy.flow.input-availability.v1`
- `legacy.flow.root-selection.v1`
- `legacy.flow.static-traversal.v1`
- `legacy.flow.parameter-forward-unavailable.v1`
- `legacy.flow.classification.v1`
- `legacy.flow.gap-propagation.v1`
- `legacy.flow.redaction.v1`
- `legacy.flow.report.v1`

Rule limitations must document:

- composed paths are static evidence, not runtime execution proof;
- WebForms events may not fire in a real session;
- call edges may miss reflection, dynamic dispatch, delegates, partial methods,
  generated code, runtime DI, and event bubbling;
- WCF/service-reference metadata does not prove deployed services, binding
  compatibility, network reachability, or operation execution;
- SQL/query/data metadata does not prove query execution, database existence, or
  schema compatibility;
- reduced coverage and missing extractors cap confidence;
- unsafe names/values are hashed or omitted.

`legacy.flow.gap-propagation.v1` covers propagated cross-source stitching
unavailable, extractor unavailable, schema missing, cycle-only traversal, and
other rule-backed gaps. `legacy.flow.parameter-forward-unavailable.v1` is
reserved for optional parameter-forward edge availability gaps so missing
parameter-origin evidence does not get mistaken for clean absence.

## Report Model

Suggested JSON root:

```csharp
public sealed record LegacyFlowReport(
    string SchemaVersion,
    LegacyFlowQuery Query,
    LegacyFlowCoverage Coverage,
    IReadOnlyList<LegacyFlowSource> Sources,
    LegacyFlowSummary Summary,
    IReadOnlyList<LegacyFlowResult> Results,
    IReadOnlyList<LegacyFlowGap> Gaps,
    IReadOnlyList<string> Limitations);
```

The `SchemaVersion` value SHALL be `"legacy-flow.v1"` for this implementation.
Future breaking schema changes require a new version identifier.

A breaking change is any modification that would cause a v1 consumer to
misinterpret output, including removing or renaming required fields, changing
field types or semantics, changing stable ID formats, or changing classification
enum meanings. Non-breaking changes, such as optional new fields, additional gap
kinds, or additional limitations, may use the same schema version. Consumers
should fail gracefully on unknown schema versions with a clear diagnostic.
Multi-version readers are not required in v1.

Suggested result:

```csharp
public sealed record LegacyFlowResult(
    string FlowId,
    string Classification,
    string Coverage,
    LegacyFlowNode Root,
    LegacyFlowNode? Terminal,
    IReadOnlyList<LegacyFlowNode> Nodes,
    IReadOnlyList<LegacyFlowEdge> Edges,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<LegacyFlowNote> Notes,
    IReadOnlyList<string> Limitations);
```

Node display properties should be safe:

| Property | Purpose |
| --- | --- |
| `nodeId` | Stable report-local node ID. |
| `kind` | `webforms-event`, `webforms-lifecycle`, `handler`, `wcf-client`, `api-route`, `wcf-operation`, `http-client`, `sql-query`, `legacy-data`, `dependency-surface`, `gap`. |
| `displayLabel` | Safe identifier or neutral label. |
| `displayHash` | Hash for unsafe names or values. |
| `sourceLabel` | User-provided source label or neutral label in combined indexes. |
| `path` | Repo-relative path only when safe. |
| `lineSpan` | Start/end lines from source facts. |
| `factIds` | Supporting fact IDs in deterministic order. |
| `ruleIds` | Supporting rule IDs in deterministic order. |
| `evidenceTier` | Strongest directly supporting tier for this node, with caps applied at result level. |

No generated timestamp should be emitted by default. Identical inputs and options
should produce byte-stable JSON. Identical inputs means identical index contents,
selectors, source labels, format/view options, and traversal limits. Different
combined-index labels are different inputs and may produce different JSON, but
ordering must remain stable.

Implementation should prefer extending `CombinedDependencyPathReport` with
legacy-root fields over introducing an unrelated schema. If a separate
`LegacyFlowReport` wrapper is needed for `--view legacy-flows`, it must embed or
mirror the same path node/edge/gap contracts used by `tracemap paths`.

Stable IDs:

- `nodeId` is `node:<kind>:<sourceIndexId-or-single>:<hash>`, where `<hash>` is
  a stable hash of the ordered safe identity tuple: fact ID when available,
  source label, kind, safe display key or display hash, repo-relative path, and
  line span.
- `edgeId` is `edge:<kind>:<sourceIndexId-or-single>:<hash>`, where `<hash>` is
  a stable hash of source node ID, target node ID, edge kind, supporting edge ID
  when available, supporting fact IDs, and rule IDs.
- `flowId` is `flow:<classification-independent-hash>`, where the hash is built
  from the ordered root node ID, terminal node ID, node ID sequence, edge ID
  sequence, source labels, and supporting fact/edge IDs. Classification,
  evidence tiers, notes, limitations, and gap lists are excluded so refinements
  to classification logic or gap reporting do not change flow identity.
- Hash inputs are sorted with ordinal comparison before hashing except for the
  path node/edge sequence, which preserves traversal order.

## Classifier

Classification is rule-based and capped by weakest required evidence:

| Classification | Required conditions |
| --- | --- |
| `StrongStaticPath` | Root, traversal, and terminal are connected by semantic or strongly structural evidence; no unresolved ambiguity; coverage for required extractors is full or explicitly sufficient. |
| `ProbableStaticPath` | Credible structural evidence connects root to terminal, but semantic evidence is incomplete; no unresolved high-risk ambiguity. |
| `NeedsReviewStaticPath` | Syntax-only/name-only edges, generated-code uncertainty, high fan-out, ambiguous candidates, or partial terminal evidence are involved. |
| `NoBackendEvidence` | No downstream backend/data evidence found and relevant extractor coverage is full. |
| `ReducedCoverage` | Missing project load, missing graph edges, missing generated code, old schema, or parse gaps prevent clean absence or stronger path classification. |
| `AnalysisGap` | A specific rule-backed gap blocks or invalidates the conclusion. |

Rules:

- A result cannot be stronger than its weakest required edge or node tier.
- `Tier3SyntaxOrTextual` evidence caps a connected path at
  `NeedsReviewStaticPath` unless the syntax evidence is only ancillary display
  evidence and the required path has stronger support.
- `Tier4Unknown` required evidence produces `ReducedCoverage` or `AnalysisGap`.
- High fan-out terminal names such as common status-like names should downgrade
  to `NeedsReviewStaticPath` when ambiguity is present.
- Missing legacy data metadata facts produce `AnalysisGap` with
  `ExtractorUnavailable: legacy-data-metadata` when no other backend terminal is
  found for a root.
- High fan-out is defined in v1 as five or more inbound candidate paths from
  distinct roots to the same terminal identity, or terminal display keys
  matching common generic names such as `status`, `id`, `name`, `value`,
  `result`, or `response` when ambiguity is present. The threshold is not
  configurable in v1. The five-path threshold is a conservative v1 placeholder
  chosen to allow common small helper/service patterns with two to four callers
  while flagging widely shared terminals. It has not been empirically validated
  against real legacy codebases. Future calibration should measure false-positive
  rates, meaning legitimate strong paths downgraded to `NeedsReviewStaticPath`,
  and false-negative rates, meaning ambiguous paths not flagged, across redacted
  legacy validation indexes with manual review sampling and regression test
  coverage. Calibration should also measure whether thresholds should vary by
  terminal kind, such as SQL query, HTTP client, WCF operation, or legacy data,
  in a future spec.
- WCF operation nodes are terminal in v1. Traversal stops at operations; no
  outbound edges exist. Classification uses the operation node's evidence tier
  as the terminal tier. Service-side implementation evidence and downstream
  service-side evidence are not included in v1 paths.

## Selector Grammar

- `--from-webforms-event` accepts either a fact ID, or
  `<repo-relative-page>/<controlId>/<eventName>`, or `<controlId>_<eventName>`.
  The short form may match multiple roots; multiple matches are reported
  deterministically and do not merge roots.
- `--from-endpoint` keeps the existing paths format: `"<METHOD> <PATH_KEY>"`.
  It is a literal selector, not a regular expression.
- `--from-symbol` accepts a fully qualified symbol identity, source-local symbol
  display name, or fact ID. Ambiguous matches stay separate and may produce
  selector gaps when narrowed by source.
- `--to-surface` uses exact surface-kind values. Existing values keep their
  current meaning; `wcf-operation`, `legacy-data`, and `dependency-surface` are
  additive.
- `--classification` is an exact enum match.

Gap kinds:

- `SelectorNoMatch` means no root or terminal matched the selector.
- `ClassificationFilterNoMatch` means paths existed before filtering but all
  were excluded by the `--classification` filter.
- `NoRootsFound` means the index contains no credible WebForms event or
  API/service root facts given the current extractor availability and schema
  version. This is distinct from `SelectorNoMatch`, which means roots exist but
  the selector did not match them.

## Traversal

Build a directed evidence graph from supported facts and edges:

1. Add roots from supported WebForms handler/event facts and API/service facts.
2. Add symbol nodes and source-local aliases only when supported by existing
   fact-symbol attachments or deterministic same-source evidence.
3. Add call, creation, symbol relationship, optional parameter-forward, WCF mapping,
   service-reference, HTTP, SQL/query, data metadata, and dependency edges.
4. Apply source/index boundaries. Cross-source traversal is allowed only through
   explicit combined endpoint or dependency evidence already materialized in the
   combined index tables. If no such persisted alignment evidence exists, v1
   reports paths where static call/object/symbol edges stay within one source
   label and emits `AnalysisGap` with rule ID
   `legacy.flow.gap-propagation.v1` plus note
   `CrossSourceStitchingUnavailable` for cross-source legacy flow stitching.
   When parameter-forward edges are unavailable because the parameter-origin
   feature is absent or the index predates it, emit a single global availability
   gap in the report Gaps section using rule ID
   `legacy.flow.parameter-forward-unavailable.v1` with note
   `ParameterForwardEvidenceUnavailable`. Paths assembled without
   parameter-forward evidence use call/object and fact-symbol evidence only and
   do not carry per-path parameter-forward gaps unless a specific path would
   have stronger classification with parameter evidence; in that case the path
   includes `legacy.flow.parameter-forward-unavailable.v1` in its notes and is
   capped accordingly.
5. Traverse breadth-first with deterministic ordering by source label, node kind,
   safe display key/hash, fact ID, and edge ID.
6. Stop at terminal backend/data surfaces or configured limits.
7. Stop at terminal backend/data surfaces or configured limits.
8. Treat WCF operations as traversal terminals in v1. When a path reaches a
   normalized WCF operation through a WCF client call, the operation is the
   terminal node. The path includes both the intermediate `wcf-client` node
   carrying generated-client or call evidence and the terminal `wcf-operation`
   node carrying normalized service-reference mapping fact IDs.

   Traversal stops at WCF operation nodes. No outbound edges from operations are
   followed in v1. This means service-side implementation evidence and
   downstream service-side evidence such as calls, SQL, or HTTP are not included.
   A future spec may define explicit service-side continuation evidence if
   needed. Classification uses the operation node's evidence tier as the
   terminal tier.
9. Detect cycles by node ID within a single path. Revisiting a node in a
   different path is allowed up to `maxPaths`. Detected cycles prune that branch
   silently; if every branch from a root is pruned by cycles, emit
   `AnalysisGap` with `CycleDetected`.
10. Apply `maxFrontier` globally per root. Roots are processed in deterministic
   order, and truncation gaps cite the first root whose frontier is capped.
11. Emit truncation gaps when `maxDepth`, `maxPaths`, or `maxFrontier` is hit.

Do not create edges through:

- runtime DI container resolution;
- reflection target guessing;
- serializer or model binder behavior;
- branch feasibility or data-dependent conditions;
- event bubbling or postback state;
- deployment or network reachability;
- database/schema existence;
- arbitrary string similarity;
- outbound traversal from WCF operation terminals.

## Markdown Shape

Suggested sections:

```text
# Legacy Static Flow Report

## Summary
## Coverage And Availability
## Flow Classifications
## Representative Static Paths
## Roots Without Backend Evidence
## Analysis Gaps
## Limitations
```

Wording examples:

- "Possible static path from WebForms event evidence to WCF operation evidence."
- "No backend evidence found under available full coverage; absence is not
  proven."
- "Reduced coverage: missing call graph evidence prevents a clean absence claim."

Forbidden wording:

- "this button calls the database";
- "proves impact";
- "always reaches";
- "runtime path";
- "executed query";
- "production dependency".

## Privacy And Redaction

All display goes through existing safe identifier/path helpers where possible.
Source labels in combined indexes must be neutral labels or reviewed identifiers
before they appear in `sourceLabel`; private repository names and unreviewed
sample identifiers are replaced by `source:<hash>`. `displayLabel` follows the
same final guard because service, WCF, and data names can reveal internal
architecture.

The report writer must apply an additional final guard for:

- local absolute paths;
- private repository names or raw remotes;
- raw SQL or query text;
- connection strings and config values;
- URLs and endpoint addresses;
- WSDL/SOAP action addresses;
- source snippets;
- secret-looking tokens.

When a value is unsafe, prefer a stable hash plus a neutral kind label. Report
`legacy.flow.redaction.v1` in notes when a displayed value was hashed or
omitted. The original source fact's rule ID remains preserved separately in
`supportingFactIds` and `ruleIds`.

Logs must not echo unsafe selector or display values, including SOAP action
URIs, service endpoint addresses, or raw WCF metadata URLs.

## Compatibility

Older indexes:

- Missing WebForms facts: report "WebForms event evidence unavailable".
- Missing WCF metadata normalization facts: still use older WCF facts where
  available, with reduced coverage for normalized operation links.
- Missing legacy data metadata facts: report "legacy data metadata evidence
  unavailable".
- Missing graph tables: report `SchemaMissing` and use available summary facts
  only.

Combined indexes:

- Keep source labels and source index IDs in all nodes.
- Do not stitch symbols across sources without explicit combined evidence.
- Same-source paths are valid and should be labeled as such.
- Cross-source paths require endpoint/service/dependency alignment evidence that
  is already persisted in the combined index. If current combined indexes do not
  persist the needed alignment, v1 emits an availability gap and leaves
  cross-source legacy stitching to a follow-up.

## Validation Strategy

Unit fixtures should build small synthetic indexes or checked-in sample scans
that cover:

- WebForms event root to direct service call;
- WebForms event root to normalized WCF operation;
- WebForms handler to WCF client to normalized operation producing exactly one
  path with the WCF operation as terminal context;
- WebForms event root to SQL/query fact;
- WebForms/API root to legacy data metadata when available;
- API route root to backend surface;
- unresolved handler under full coverage;
- missing extractor availability;
- malformed or partial analysis gaps;
- ambiguous name-only and high fan-out evidence;
- traversal cycles and truncation;
- recursive/cyclic calls that prune without infinite traversal;
- selector no-match;
- selector parsing for WebForms event, endpoint literals with special
  characters, symbol/fact IDs, surface kind, classification filter, and
  selector no-match after classification filtering;
- reused `WebFormsEventFlowProjected` evidence that does not duplicate
  primitive supporting IDs or upgrade classification;
- full-coverage no-backend evidence versus missing-extractor reduced coverage;
- input row permutation preserving stable node IDs, flow IDs, and byte-identical
  JSON;
- same index with same labels producing identical JSON and same contents with
  different labels producing different JSON with stable ordering;
- combined indexes with private source labels producing neutral `sourceLabel`
  fields;
- unaligned cross-source symbol-name matches rejected instead of stitched;
- read-only input enforcement by checking that the source index is unmodified;
- forbidden wording guard for generated Markdown and JSON;
- deterministic JSON byte stability;
- privacy suppression in Markdown, JSON, logs, and display fields.

Validation commands for implementation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Run relevant pinned smoke checks from `docs/VALIDATION.md` when implementation
touches language adapters or public validation scripts. For spec-only delivery,
Kiro review plus private-path and diff checks are sufficient unless repo
validation scripts require more.
