# Design

## Overview

This phase adds a WebForms entry-point layer to the existing static evidence
graph. The implementation should reuse the C# scanner, file inventory,
call-edge/object-creation extraction, WCF evidence, SQL/query facts, combined
reporting, and legacy validation harness wherever possible.

The intended evidence chain is:

```text
WebForms markup event binding
  -> code-behind handler method
  -> call/object creation evidence
  -> WCF/ASMX/service-client evidence or SQL/query/dependency surface
  -> existing reports, paths, reverse queries, impact, and release-review
```

Every edge remains static evidence. TraceMap must not claim runtime page
lifecycle execution, postback behavior, event bubbling feasibility, dependency
injection resolution, service deployment, endpoint reachability, SQL execution,
or production usage.

## Non-Goals

- No browser automation or runtime UI execution.
- No IIS, ASP.NET pipeline, session, auth, ViewState, or postback simulation.
- No guarantee that an event fires in production.
- No raw source snippets, raw SQL, config values, local absolute paths, raw
  remotes, or private sample identifiers in committed artifacts.
- No LLM, embedding, vector database, or prompt-based classification.
- No full taint analysis, mutation tracking, or branch feasibility proof.

## Proposed Fact Types

- `WebFormsPageDeclared`
- `WebFormsControlDeclared`
- `WebFormsEventBindingDeclared`
- `WebFormsDesignerControlDeclared`
- `WebFormsHandlerResolved`
- `WebFormsEventFlowProjected`
- `WebFormsLogicSignalDetected`

These should be additive and backward compatible. If an existing fact type can
carry equivalent evidence without weakening meaning, prefer reuse, but do not
overload unrelated facts.

Existing coarse legacy validation rule `legacy.validation.ui-events.v1`
summarizes WinForms/WebForms-style event wiring today. The new precise
`WebForms*` facts should feed or supersede that coarse summary where available;
they should not create a second divergent UI-event count with different
semantics.

## Proposed Rules

- `legacy.webforms.inventory.v1`
- `legacy.webforms.event-binding.v1`
- `legacy.webforms.handler-resolution.v1`
- `legacy.webforms.designer-control.v1`
- `legacy.webforms.event-flow.v1`
- `legacy.webforms.logic-signal.v1`

Rule catalog entries must include limitations. In particular:

- markup handlers are static declarations, not proof that the event fires,
- designer fields can be stale or generated from unavailable markup,
- code-behind matching may be syntax-only when builds fail,
- call edges are static and may miss reflection, dynamic dispatch, events,
  delegates, partial methods, generated code, or runtime wiring,
- logic signals are deterministic heuristics, not business judgment.

## File Inventory

Extend inventory to identify:

- `.aspx`
- `.ascx`
- `.master`
- `.aspx.cs`
- `.ascx.cs`
- `.master.cs`
- `.designer.cs`

Inventory should use repo-relative paths and safe file kinds. Do not store local
absolute paths. Treat malformed markup or unreadable files as `AnalysisGap`
facts with rule IDs and evidence tiers.

## Markup Parser

Use a tolerant parser strategy because real WebForms markup is often not strict
XML:

1. Detect page/control/master directives.
2. Extract safe directive attributes such as `Language`, `CodeBehind`,
   `CodeFile`, `Inherits`, and `MasterPageFile`.
3. Detect server controls and event attributes with line spans.
4. Preserve control ID, control type, event name, and handler name.
5. Hash or omit unsupported attribute values that may contain URLs, expressions,
   data source names, config values, or secrets.

Known event attributes for MVP:

- `OnClick`
- `OnCommand`
- `OnSelectedIndexChanged`
- `OnTextChanged`
- `OnCheckedChanged`
- `OnRowCommand`
- `OnItemCommand`
- `OnLoad`
- `OnInit`

Unsupported event-like attributes should be counted as needs-review evidence only
when they look like static handler names. Avoid broad regexes that capture
arbitrary markup values as handlers.

## Code-Behind Resolution

Build candidate page/type identities from:

- directive `Inherits`,
- `CodeBehind` or `CodeFile`,
- repo-relative code-behind filename,
- partial class declarations,
- designer partial class declarations.

Resolution order:

1. Tier1 semantic method symbol in linked partial class.
2. Tier2 structural match by directive/page identity plus method name and common
   event signature.
3. Tier3 syntax/textual match by linked code-behind filename and method name.
4. Tier4 gap for ambiguity, missing linked files, parse failures, or unsupported
   auto-wireup.

Do not match handlers globally across the repository by name alone. If multiple
candidate methods remain after page/type scoping, emit an ambiguity gap.

Auto-event-wireup MVP scope is intentionally narrow: only `Page_Load` and
`Page_Init` may be emitted as `Tier3SyntaxOrTextual` evidence, and only when
page/type identity is clear plus either `AutoEventWireup` is explicitly enabled
by page directive or safe config evidence, or explicit static event subscription
evidence exists. If `AutoEventWireup` is false, unknown, contradictory, or
unavailable, emit a `Tier4Unknown` gap instead of a handler-resolution fact or
event-flow root. Other lifecycle conventions such as `Page_PreRender` and
control-name patterns such as `Button1_Click` are out of MVP unless the markup
declares the handler explicitly.

## Designer Control Linkage

Parse `.designer.cs` partial classes for field declarations that look like
server controls. Link markup controls to designer fields only when page/type
identity and control ID both align.

Designer facts are supporting evidence. They should improve confidence in
control identity but should not create backend flow or logic signals by
themselves.

## Event Flow Projection

The first implementation should project flows from event handlers into existing
evidence instead of creating a separate graph engine.

Inputs:

- `WebFormsHandlerResolved`
- call edges and object creation facts,
- existing WCF/service-reference facts,
- ASMX/service host facts where available,
- SQL/query/dependency-surface facts,
- combined graph edges if available.

Projection rules:

- Preserve supporting fact IDs and edge IDs.
- Cap classification by the weakest required evidence tier.
- Label reduced coverage when project load, semantic analysis, generated code,
  call edges, WCF metadata, or SQL extraction is partial.
- Prefer direct handler-to-call evidence over name-only downstream matches.
- Do not infer collection contents, runtime DI targets, reflection targets,
  dynamic dispatch targets, branch feasibility, or mutation semantics.

Suggested classifications:

- `StrongStaticEventFlow`: handler, call edge, and terminal surface/service
  evidence are all semantically or strongly structurally connected.
- `ProbableStaticEventFlow`: handler and downstream evidence are connected by
  structural WCF/config/call evidence but semantic resolution is incomplete.
- `NeedsReviewEventFlow`: name-only, syntax-only, ambiguous, or partial generated
  evidence requires review.
- `NoBackendEvidence`: no downstream backend evidence under full coverage.
- `UnknownAnalysisGap`: reduced coverage or missing evidence prevents a credible
  conclusion.

Minimum safe properties for `WebFormsEventFlowProjected`:

| Property | Purpose |
| --- | --- |
| `pageTypeName` | Safe page/control type identity when known. |
| `markupFile` | Repo-relative markup path. |
| `handlerName` | Static handler identifier. |
| `sourceSymbolId` | Shared symbol role for the resolved handler method when semantic evidence exists, so existing symbol tables and graph consumers can see the event root. |
| `handlerSymbolId` | WebForms-specific alias for the same resolved handler identity, retained only as display/compatibility metadata. |
| `controlId` | Markup/designer control identifier when known. |
| `eventName` | Event attribute or auto-wireup event name. |
| `flowClassification` | One of the event-flow classifications above. |
| `terminalSurfaceKind` | Safe terminal evidence kind such as `wcf-operation`, `sql-query`, `http-client`, or `dependency-surface`. |
| `terminalSurfaceNameHash` | Hash of unsafe terminal names when cleartext is not safe. |
| `coverage` | Full/reduced coverage label. |
| `supportingFactIds` | Deterministically ordered supporting fact IDs. |
| `supportingEdgeIds` | Deterministically ordered supporting edge IDs. |
| `ruleIds` | Deterministically ordered contributing rule IDs. |
| `evidenceTiers` | Deterministically ordered contributing evidence tiers. |

Raw SQL, snippets, config values, endpoint addresses, local absolute paths, and
private repository identifiers must not appear in these properties.

## Logic Signals

Logic signals are optional row-level annotations, not reducer conclusions.

Possible deterministic signals:

- arithmetic or aggregate operations,
- conditionals/switches with domain/service variables,
- validation branches,
- DTO/domain object construction or mutation,
- service/repository/client calls,
- SQL/query calls,
- writes to non-UI fields/properties.

Possible UI-boilerplate signals:

- control visibility/text/style changes,
- data binding calls,
- page lifecycle calls,
- direct delegation to another method without local logic,
- framework-only event signatures.

Reports must keep the language bounded: "static logic signal" or
"UI-boilerplate signal." Do not say TraceMap proves business logic.

## Reporting Integration

Minimum implementation:

- `tracemap scan` emits counts in `report.md`.
- facts and indexes contain new WebForms evidence.
- legacy validation summary includes WebForms counts and gap counts.

Preferred implementation:

- combined report summarizes event entry points and event-flow availability,
- paths/reverse can include event roots when graph evidence exists,
- release-review can mention event-flow sections as available, unavailable, or
  partial without treating them as approval.

Older indexes without these facts must remain readable.

## Safety And Redaction

Safe to render:

- repo-relative paths,
- line spans,
- control IDs,
- control type names,
- handler method names,
- type names,
- rule IDs,
- evidence tiers,
- hashes,
- public sample labels.

Hash or omit:

- local absolute paths,
- raw source snippets,
- raw SQL,
- raw URLs,
- config values,
- connection strings,
- secrets,
- private sample names,
- generated temp paths,
- raw data-binding expressions that may include config or SQL-like text.

## Validation

Unit fixtures should cover:

- simple `.aspx` `OnClick` to code-behind handler,
- `.ascx` and `.master` variants,
- designer control field linkage,
- semantic and syntax-only handler resolution,
- duplicate/ambiguous handler methods,
- missing designer file,
- malformed markup,
- auto-event-wireup `Page_Load`,
- handler calling WCF generated client mapped by existing WCF facts,
- handler reaching SQL/query evidence through existing call/query facts,
- lifecycle/control-only handler classified as lower-strength UI signal,
- duplicate controls and duplicate handler bindings produce deterministic
  ordering and stable fact IDs,
- facts and generated reports contain no local absolute paths, private sample
  identifiers, raw SQL, source snippets, raw URLs, config values, or secrets,
- no raw local paths, private paths, source snippets, config values, or raw SQL
  in facts/reports.

Validation commands:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

Also follow `docs/VALIDATION.md` for pinned smoke guidance when scanner behavior
changes. If Python adapter tests are required by a later implementation slice,
use the temporary virtual environment pattern from `AGENTS.md` rather than a
global interpreter install.

Ignored local smoke validation may use private legacy samples, but only redacted
label/count summaries may be committed.
