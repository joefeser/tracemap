# Access Screen-to-Data Static Flow Requirements

## Goal

Compose already-persisted Microsoft Access design facts into a bounded,
human-reviewable static trail from candidate startup/UI roots toward controls,
VBA procedures, saved queries, reports, tables, and external boundaries.

## Requirements

### R1 — Existing evidence only

1. Composition SHALL read a completed TraceMap single index.
2. It SHALL add no Access COM, DAO, VBE, database, row, query, form, report,
   macro, or VBA read/execution path.
3. Item-level flow is available only when #550 source-neutral facts exist.
   Count-only input SHALL produce an explicit missing-input gap.

### R2 — Roots and nodes

1. An inventory macro with `startupRole=autoexec` MAY be a startup candidate.
2. Declared forms SHALL be UI root candidates, not proven startup surfaces.
3. If no autoexec fact exists, the report SHALL emit
   `AccessStartupIdentityUnavailable`.
4. Nodes SHALL be limited to macro, form, report, control, procedure,
   saved-query, table/field, and external-boundary projections backed by exact
   Access facts.
5. Private identities SHALL remain opaque stable keys; raw names SHALL not be
   rendered.

### R3 — Edges

1. `AccessControlDeclared` SHALL compose surface-to-control ownership.
2. `AccessEventBindingCandidate` SHALL compose owner-to-procedure static event
   candidates.
3. `AccessNavigationCandidate` SHALL compose procedure-to-procedure,
   procedure-to-form/report, and procedure-to-query candidates only when an
   exact target stable key exists.
4. `AccessBindingDeclared` SHALL compose declared surface/control-to-data
   bindings.
5. `AccessQueryDependencyCandidate` SHALL compose saved-query dependencies.
6. `AccessExternalLinkDeclared` SHALL compose an external-boundary terminal
   from the linked source object.
7. Every edge SHALL retain exact supporting fact IDs, rule ID, tier, commit
   SHA, file span, extractor ID/version, coverage, and limitations.

### R4 — Traversal

1. Traversal SHALL be deterministic breadth-first traversal with stable root
   and edge ordering.
2. Defaults SHALL be maximum depth 12, maximum paths 100, and maximum gaps
   1,000; all SHALL be positive and bounded.
3. A repeated node SHALL end that branch and emit `AccessFlowCycleDetected`.
4. Depth/path/gap ceilings SHALL label output partial and emit rule-backed
   truncation gaps.
5. Branches SHALL remain separate paths and preserve all supporting evidence.

### R5 — Gaps

The report SHALL represent at least:

- source-neutral item evidence unavailable;
- startup identity unavailable;
- dynamic/unresolved navigation or event target;
- missing referenced procedure/object;
- propagated Access analysis gaps;
- cycles and bounds.

An empty flow SHALL never mean no Access behavior exists.

### R6 — Output

1. The command SHALL write deterministic Markdown and JSON.
2. Rows SHALL be stably ordered and carry opaque IDs derived only from safe
   evidence identity.
3. Output SHALL state the evidence profile, coverage, limitations, and
   non-claims.
4. Standard output SHALL not contain raw names, SQL, VBA, expressions, macro
   bodies, connection material, paths outside repository-relative evidence,
   or customer identity.

### R7 — Non-claims

The report SHALL NOT claim user navigation, startup selection, event firing,
branch feasibility, runtime reachability, query/macro/VBA/UI execution, row
access, external connectivity, production use, correctness, completeness,
release approval, or safety to run.

### R8 — Validation

Mac-only synthetic tests SHALL cover a complete small flow, branching, cycles,
missing procedures, dynamic targets, unavailable startup identity, count-only
input, determinism, bounds, provenance, and planted protected markers.
