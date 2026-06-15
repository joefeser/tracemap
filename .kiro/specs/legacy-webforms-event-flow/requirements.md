# Requirements

## Introduction

Legacy ASP.NET WebForms applications often hide backend behavior behind markup
event bindings, designer-generated controls, code-behind partial classes, WCF or
ASMX service clients, and old ORM or SQL helper layers. TraceMap already extracts
many service, WCF, call, SQL, and dependency facts, but the UI event entry point
is still hard to explain from static evidence.

This phase adds deterministic evidence for WebForms event-to-backend flow:
markup/control event declarations, code-behind handlers, generated designer
control fields, handler call edges, service-client calls, and downstream SQL or
dependency surfaces when already indexed. The goal is to show a conservative
static path such as "button click handler calls service client method that maps
to WCF operation evidence and then reaches SQL evidence" without claiming runtime
execution.

Public claim level: hidden until validated through redacted legacy summaries or
checked-in public fixtures.

## Requirements

### Requirement 1: WebForms File Inventory

**User Story:** As a maintainer, I want TraceMap to recognize WebForms markup
and code-behind files even when the legacy solution cannot build.

Acceptance Criteria:

1. WHEN `.aspx`, `.ascx`, `.master`, `.aspx.cs`, `.ascx.cs`, `.master.cs`,
   `.designer.cs`, and related WebForms files are present THEN TraceMap SHALL
   inventory them with safe repo-relative paths and line spans.
2. WHEN markup references `CodeBehind`, `CodeFile`, `Inherits`, `MasterPageFile`,
   or similar linkage attributes THEN TraceMap SHALL store only safe identifiers,
   safe relative filenames, and hashes where needed.
3. WHEN markup or code-behind linkage cannot be parsed THEN TraceMap SHALL emit
   an `AnalysisGap` rather than claiming the page has no handler evidence.
4. WHEN local absolute paths, private repository names, generated temp paths, raw
   source snippets, or config values are encountered THEN TraceMap SHALL omit or
   hash them before writing facts or reports.

### Requirement 2: Markup Event Binding Extraction

**User Story:** As a reviewer, I want to see which UI controls declare event
handlers in WebForms markup.

Acceptance Criteria:

1. WHEN WebForms markup declares server controls with event attributes such as
   `OnClick`, `OnCommand`, `OnSelectedIndexChanged`, `OnTextChanged`,
   `OnCheckedChanged`, `OnRowCommand`, `OnItemCommand`, `OnLoad`, or `OnInit`
   THEN TraceMap SHALL emit event-binding facts with control type, control ID,
   event name, handler name, markup file, and line span.
2. WHEN an event attribute references a handler by name THEN TraceMap SHALL keep
   the handler identifier as static evidence and SHALL NOT store the full markup
   snippet.
3. WHEN markup includes unsupported dynamic event wiring, data-bound command
   names, or generated controls without a static handler name THEN TraceMap SHALL
   emit a gap or needs-review fact instead of guessing.
4. WHEN duplicate controls or duplicate handler bindings exist THEN TraceMap SHALL
   preserve all evidence with deterministic ordering and stable fact IDs.

### Requirement 3: Code-Behind Handler Resolution

**User Story:** As a maintainer, I want TraceMap to connect markup event
bindings to code-behind methods when static evidence is credible.

Acceptance Criteria:

1. WHEN a markup handler name matches a method in the linked code-behind partial
   class THEN TraceMap SHALL emit a handler-resolution fact.
2. WHEN semantic analysis resolves the method symbol THEN the fact SHALL include
   the fully qualified method identity, assembly/package metadata when available,
   evidence tier, file span, and supporting fact IDs.
3. WHEN semantic analysis is unavailable but syntax matching links the markup
   handler to a method with compatible name and common event signature THEN the
   fact SHALL be emitted at syntax/textual tier.
4. WHEN multiple methods or partial classes could satisfy the same handler THEN
   TraceMap SHALL emit an ambiguity gap and SHALL NOT choose an arbitrary winner.
5. WHEN handler resolution depends on runtime auto-event-wireup conventions such
   as `Page_Load` THEN TraceMap MAY emit review-tier evidence only when page/type
   identity is clear; otherwise it SHALL emit a gap.

### Requirement 4: Designer Control Field Linkage

**User Story:** As a reviewer, I want designer-generated control fields to help
explain handler and control identity without treating generated code as business
logic.

Acceptance Criteria:

1. WHEN a `.designer.cs` partial class declares fields for server controls THEN
   TraceMap SHALL emit control-field facts with field name, control type, partial
   class identity, file span, and evidence tier.
2. WHEN a markup control ID matches a designer field on the same page/control
   partial class THEN TraceMap SHALL link the event binding to the control-field
   evidence.
3. WHEN designer files are missing, stale, or mismatched THEN TraceMap SHALL keep
   markup event facts and emit reduced-confidence gaps rather than dropping the
   event.
4. Designer evidence SHALL NOT by itself classify a method as business logic.

### Requirement 5: Event-To-Backend Flow Projection

**User Story:** As a maintainer, I want to see what backend evidence a WebForms
event handler can statically reach.

Acceptance Criteria:

1. WHEN a resolved event handler has call edges, object creations, service-client
   calls, WCF mappings, HTTP calls, SQL/query facts, or dependency-surface facts
   THEN TraceMap SHALL emit a static event-flow projection that preserves
   supporting fact IDs, edge IDs, rule IDs, evidence tiers, and coverage labels.
2. WHEN a handler calls a generated WCF or ASMX client and existing WCF evidence
   maps that client method to service operation metadata THEN the flow SHALL
   include the service-reference mapping as static evidence.
3. WHEN a handler reaches SQL/query evidence through known call edges or query
   facts THEN the flow SHALL include the SQL/query surface identity already safe
   for reports, not raw SQL.
4. WHEN path evidence is incomplete because semantic analysis, call edges,
   generated code, dynamic dispatch, reflection, event bubbling, or unsupported
   framework behavior is missing THEN TraceMap SHALL label the flow partial.
5. WHEN no backend evidence is found under full coverage THEN TraceMap MAY report
   no static backend evidence; under reduced coverage it SHALL report an
   analysis gap instead.

### Requirement 6: Business-Logic Signal Without Overclaiming

**User Story:** As a reviewer, I want TraceMap to help separate likely business
logic from page boilerplate without making subjective claims.

Acceptance Criteria:

1. WHEN an event handler performs calculations, branching, validation,
   transformations, service calls, database calls, or writes to domain/service
   types THEN TraceMap MAY emit deterministic structural signals for likely
   handler logic.
2. WHEN a handler only delegates to another method, sets labels, toggles
   visibility, binds UI controls, or calls framework lifecycle methods THEN
   TraceMap MAY emit a lower-strength UI-boilerplate signal.
3. These signals SHALL be rule-backed static heuristics, not semantic judgments,
   code quality claims, or AI classifications.
4. Reports SHALL phrase this as "static logic signals" or "UI boilerplate
   signals", not "business logic proven".

### Requirement 7: Reports, Queries, And Combination

**User Story:** As a TraceMap user, I want WebForms event evidence to flow into
existing reports without creating a one-off analyzer island.

Acceptance Criteria:

1. WHEN `tracemap scan` emits WebForms event facts THEN `index.sqlite`,
   `facts.ndjson`, and `report.md` SHALL include deterministic counts and gaps.
2. WHEN a combined index imports these facts and supporting edges THEN combined
   report/path/reverse/impact/release-review commands SHALL either consume them
   where supported or emit explicit availability gaps.
3. WHEN optional precise WebForms flow tables are absent from older indexes THEN
   reports SHALL continue to work and label the missing precision as unavailable
   rather than failing.
4. Generated Markdown/JSON SHALL be deterministic and safe for public or
   redacted summaries.

### Requirement 8: Validation Baseline

**User Story:** As a maintainer, I want to measure improvement against ugly old
codebases without leaking private local paths.

Acceptance Criteria:

1. WHEN this spec is implemented THEN validation SHALL include checked-in unit
   fixtures for markup, designer, code-behind, WCF mapping, SQL reachability, and
   ambiguous/malformed cases.
2. WHEN local legacy samples are used for smoke testing THEN their manifests and
   outputs SHALL stay ignored/local-only.
3. WHEN a smoke summary is committed THEN it SHALL be redacted and SHALL include
   labels, counts, rule IDs, evidence tiers, coverage, limitations, and hashes,
   not local absolute paths, private repo names, raw remotes, raw SQL, config
   values, secrets, or source snippets.
4. Validation SHALL run `dotnet build`, `dotnet test`, private-path guard, and
   relevant legacy validation scripts before PR.
