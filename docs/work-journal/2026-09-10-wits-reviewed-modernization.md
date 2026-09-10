# WTJ-2026-09-10-001 — WITS reviewed modernization loop

## Journal metadata

- Date: 2026-09-10
- Start: 09:30 CDT
- End: 09:33 CDT
- Elapsed wall clock: 3 minutes
- Confidence: verified
- Maximum concurrency: 1 active Codex agent; 0 child agents
- Billing posture: TBD
- Session treatment: interactive product-direction capture
- External waiting: waiting for Jack and Jennifer is excluded from elapsed work
- Historical treatment: earlier implementation is summarized from Git and retained
  field-result evidence; no historical elapsed time is claimed here

## Product goal captured

TraceMap should extract as much deterministic, evidence-backed legacy application
behavior as practical and preserve the path from UI to controller or event handler,
backend logic, and database operation. WITS should present that evidence to a human,
allow the human to describe or correct the intended behavior, and retain the review
as a separate versioned overlay. Reviewed output should support migration planning
and bounded generation for:

- Angular pages, components, forms, validation, state, and services;
- additions or replacements in the .NET backend, including endpoints, DTOs,
  validators, and application services;
- SQL Server schema and stored-procedure understanding; and
- PostgreSQL schema migration definitions plus explicitly reviewed procedure or
  function ports.

The intended flow is:

```text
legacy source and database evidence
  -> TraceMap facts, rule IDs, tiers, paths, gaps, and provenance
  -> bounded code-path review package
  -> WITS human description/correction/disposition
  -> versioned reviewed modernization model
  -> Angular, .NET, and PostgreSQL migration artifacts
  -> human and test validation
```

## System boundary

TraceMap remains deterministic and evidence-first. It must not turn human opinion
into scanner evidence or silently widen a static conclusion. WITS review should be
stored separately and joined by stable provenance such as repository identity,
commit SHA, scan ID, page or surface ID, handler ID, fact IDs, and rule IDs.

A review may add intent and disposition, for example:

- expected UI/control-only behavior;
- backend operation present;
- backend operation expected but evidence missing;
- corrected binding or callee;
- obsolete behavior that should not be migrated;
- behavior assigned to Angular, .NET, PostgreSQL, or manual redesign; and
- unresolved or source-mismatch status.

Every generated artifact should retain links to both the original evidence and the
human review that authorized the interpretation. Corrections may motivate a new
deterministic extractor rule and regression fixture, but do not rewrite old facts.

## Candidate reviewed modernization model

The technology-neutral model should be able to represent:

- page or screen identity and source provenance;
- controls, layout relationships, defaults, visibility, validation, and bindings;
- events, handlers, branches, state changes, and bounded call paths;
- application operations, request/response shapes, services, and backend calls;
- database commands, command type, parameters, result shapes, tables, views,
  stored procedures, functions, and dependencies;
- evidence tiers, rule IDs, line spans, extractor versions, coverage, truncation,
  and limitations; and
- human verdict, correction, rationale, reviewer, timestamp, and migration
  disposition.

## Current evidence supporting the direction

- The focused run matched all 43 requested pages and categorized all 643 retained
  event chains.
- 539 chains reached supported backend terminals: 453 SQL-query, 70
  SQL-persistence, and 16 HTTP-client terminals.
- Exact framework `DataAdapter.Fill` recognition converted previously invisible
  database boundaries into supported SQL-query terminals without guessing command
  values or runtime execution.
- The seven highest-priority terminal-free chains were reduced to six repeated
  UI/control-reset patterns with unresolved helper leaves and one case containing
  only recognized UI/control endpoints. None reached a traversal limit.
- The local code-path prototype can now render one case as bounded, line-numbered
  working-tree excerpts with retained caller/callee witnesses, optional uniquely
  named definition candidates, and a human verdict section. Git is not required;
  working-tree/commit equality remains explicitly unproven.

These counts are evidence categories, not a claim that 99.8 percent of runtime
behavior or migration work is complete. Any percentage must name its denominator.

## Existing foundations to reuse

- `docs/ADAPTER_RUNWAY.md` already frames TraceMap artifacts as input for future
  WITS and ACK classification.
- `docs/contracts/angular-dotnet-interaction-feedback.v1.schema.json` establishes
  provenance-preserving categorical feedback patterns.
- UI-field/property lineage specs establish Angular UI roots and backend-property
  composition.
- PostgreSQL schema/migration extraction already covers bounded DDL families and
  explicitly records deferred dialect and runtime work.
- Commit `9101299e00aba2f8ca6310f3775225de92f7e0d3` provides the first local
  one-case code-path review prototype.
- Commit `f8affde0c931e4c003e70a3b646aa43aa54c9282` separates observed UI/control
  endpoints from other unresolved leaves.
- Commit `1fc9e0c50ff915cb2ce84ec4fd28228cd177679b` recognizes supported framework
  data-adapter Fill boundaries.

## Proposed next slices

1. Dogfood the one-case report and record what the reviewer needs to see, hide,
   correct, or reorder.
2. Define a small `wits-modernization-review.v1` contract as an immutable review
   overlay; reuse existing feedback provenance patterns rather than inventing an
   unrelated format.
3. Add local export/import validation for human verdicts without modifying the
   source index or original report.
4. Define the minimum technology-neutral reviewed model for one Web Forms page.
5. Generate a review-only Angular/.NET/PostgreSQL migration plan before generating
   code.
6. Add code generation only for evidence-backed and human-approved shapes, with
   deterministic fixtures and traceability back to evidence and review.

## Explicit non-claims and deferred work

- Static evidence does not prove runtime execution, successful binding, branch
  feasibility, returned rows, or whole-application coverage.
- A control `DataBind` call alone does not prove UI-only behavior or database work.
- Stored-procedure translation is not mechanical equivalence. Dynamic SQL,
  temporary tables, transactions, exception behavior, SQL Server-specific
  functions, identity semantics, permissions, and result contracts require
  explicit review.
- Human approval does not replace compilation, tests, database validation,
  security review, or release approval.
- Private source excerpts, SQL, configuration values, and database definitions
  remain local unless an authorized workflow explicitly permits otherwise.

## Evidence anchors

- Git commits: `9101299e`, `f8affde0`, `1fc9e0c5`
- Relevant state: `.kiro/specs/webforms-bounded-report-memory/implementation-state.md`
- Existing WITS framing: `docs/ADAPTER_RUNWAY.md`
- Existing feedback contract:
  `docs/contracts/angular-dotnet-interaction-feedback.v1.schema.json`
- Existing PostgreSQL state:
  `.kiro/specs/postgres-schema-migration-v0/implementation-state.md`

## Exclusions and known missing time

This entry does not attempt to reconstruct weekend work, work-machine scan runtime,
human source-inspection time, prior conversation time, external waiting, or work by
Jack and Jennifer. Those intervals require separate evidence-backed or reported
entries if needed.
