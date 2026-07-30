# Access Screen-to-Data Static Flow Implementation State

Status: implementation complete; PR handoff in progress

PR: #558

PR URL: https://github.com/joefeser/tracemap/pull/558

PR base: `codex/access-design-evidence-enrichment`

Issue: #551

Parent: #549

Depends on: #550 / PR #557

Branch: `codex/access-screen-to-data-flow`

Stacked base SHA:
`33602ef962dce99e2306bf5c544a763bfe9d6ee6`

Merge-base at start:
`33602ef962dce99e2306bf5c544a763bfe9d6ee6`

## Scope

Mac-only read-side composition over a completed source-neutral Access index.
No Windows, Access, COM, DAO, VBE, database file, exporter, row read, source
acquisition, or execution.

## Decision

Use a dedicated Access report/CLI selector. Generic route-flow models encode
web endpoint semantics and would make Access startup/event limitations less
explicit. Reuse deterministic bounded traversal concepts and existing Access
facts, rule IDs, tiers, provenance, coverage, and limitations.

## Implemented

- `tracemap-access flow --index <index.sqlite> --out <new-directory>`;
- deterministic `access-flow.md` and `access-flow.json` under schema
  `tracemap.access-screen-data-flow.v1`;
- form and autoexec candidate roots;
- exact fact-backed surface/control ownership, event/procedure, VBA
  call/navigation, UI/data binding, saved-query dependency, and external
  boundary edges;
- stable breadth-first branching/cycle/depth/path traversal;
- explicit count-only, startup, missing declaration, dynamic target, cycle,
  depth/path/gap-cap, and upstream Access gaps;
- path evidence propagation for fact IDs, rules, weakest tier, commit, span,
  extractor/version, coverage, and limitations;
- opaque stable-key and categorical allowlists plus protected-marker tests.

Exact-head review corrections add a hashed repository identifier and require a
real commit SHA, give every gap file/span/commit/extractor provenance, make
report coverage partial when any path is partial, normalize unexpected tiers to
Tier4, drop non-categorical limitation text, and reuse one traversal edge
lookup. The already-completed PR checkbox was confirmed current.

## Validation

- locked restore: passed, with only the separately tracked
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 advisory;
- focused `AccessScreenDataFlowTests`: 6/6 passed;
- all Access-focused tests: 126/126 passed;
- full solution tests: 1000/1000 passed;
- full solution build: passed with 0 errors;
- focused `dotnet format --verify-no-changes`: passed;
- `./scripts/check-private-paths.sh`: passed;
- `git diff --check`: passed.

## Deferred

- #552 copy/clone candidate composition;
- any Windows exporter or richer extraction;
- hidden-local identity display;
- runtime or customer-database validation;
- optional HTML presentation.
