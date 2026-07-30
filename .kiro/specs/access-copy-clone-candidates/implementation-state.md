# Access Copy/Clone Static Candidate Implementation State

Status: implementation and validation complete; PR handoff in progress

PR: #559

PR URL: https://github.com/joefeser/tracemap/pull/559

Issue: #552

Parent: #549

Depends on: #550 / PR #557 and #551 / PR #558

Branch: `codex/access-copy-clone-candidates`

Stacked base SHA and merge-base:
`bd29ed9cff899327d47098ccf15209dcc29d4583`

PR base: `codex/access-screen-to-data-flow`

## Scope

Mac-only read-side composition over an existing enriched Access index. No
Windows, Access, COM, DAO, VBE, database file, exporter, row read, source
acquisition, query/VBA/macro execution, or protected input-contract expansion.

## Decision

Use persisted action-query kinds as conservative candidate shapes and compose
their undirected dependency evidence with exact #551 flow paths. Current v1
facts do not retain role-specific SQL structure, so direction, field mapping,
generated keys, and parent/child sequencing remain Tier4 gaps. This is an
honest usable result without reopening the extraction boundary.

## Implemented

- `tracemap-access copy-clone --index <index.sqlite> --out <new-directory>`;
- deterministic `access-copy-clone.md` and `access-copy-clone.json`;
- append/make-table `Candidate` shapes;
- update/bulk/compound `NeedsReview` shapes;
- exact opaque dependency participants with unknown roles;
- references to screen-to-data candidate paths containing each query;
- safe evidence aggregation and explicit direction, mapping, path, upstream,
  fan-out, external-source, cycle, parent/child, and bound gaps;
- false-positive protection for ordinary select and name-only evidence;
- rule-catalog limitations and Mac-only synthetic tests.

Exact-head review corrections add the inherited hashed repository/real-commit
contract, first-class rule/tier/span/commit/extractor candidate metadata,
host-independent Windows/POSIX/UNC path rejection, one precomputed
stable-key-to-flow-path lookup, and incremental hashing for gap-support
identity. The branch also merges the repaired #551 parent API so the stacked
result is validated as one coherent head.

## Validation

- focused `AccessCopyCloneCandidateTests`: 4/4 passed.
- all Access-focused tests: 130/130 passed;
- full solution tests: 1004/1004 passed;
- full solution build: passed with 0 errors;
- focused `dotnet format --verify-no-changes`: passed;
- `./scripts/check-private-paths.sh`: passed;
- `git diff --check`: passed.

Locked restore and build repeated only the separately tracked
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 NU1903 advisory.

## Deferred

- transient role-specific `INSERT ... SELECT` parsing;
- source/target and bounded field-correspondence facts;
- DAO/recordset/query-def mutation calls;
- generated-key and parent/child sequence analysis;
- macro action/body analysis;
- external-link runtime behavior;
- hidden-local identity display;
- any Windows exporter or richer extraction.

## Runway audit

- #550 / PR #557 is the source-neutral ingestion base at
  `33602ef962dce99e2306bf5c544a763bfe9d6ee6`; its code/tests/checks are clean,
  while its ACK handoff still requires owner disposition of outdated prior-head
  Qodo evidence.
- #551 / PR #558 is stacked exactly on #557 at
  `bd29ed9cff899327d47098ccf15209dcc29d4583`; checks are green and it awaits
  its required review/ACK process after #557.
- #552 / PR #559 is stacked exactly on #558 and awaits CI/review/ACK after this
  handoff.
- Merge order is #557, #558, then #559. Retarget each surviving PR to `dev`
  only after its parent is merged, without changing its approved head.
- Issues #550–#552 and parent #549 remain open until their linked work is
  merged. No Windows or representative database validation is required for
  these Mac-only read-side slices.
