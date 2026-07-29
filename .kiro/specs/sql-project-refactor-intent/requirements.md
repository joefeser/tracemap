# SQL Project Refactor Intent Requirements

## Goal

TraceMap shall extract bounded, deterministic evidence from checked-in SQL project
(`.sqlproj`) refactor logs (`.refactorlog`) and compose that evidence into database
design review and release review.

## Requirements

### R1 — Repository inventory

- Inventory `.sqlproj` and `.refactorlog` files as static repository inputs.
- Preserve repository and commit SHA provenance for every emitted fact.
- Do not invoke MSBuild, DacFx, SqlPackage, SQL Server, or any database connection.

### R2 — Safe project linkage

- Parse `.sqlproj` XML with DTD and external entity resolution disabled.
- Recognize literal `RefactorLog Include="..."` items only.
- Resolve references relative to the project directory and require the resolved
  file to remain inside the scan root.
- Emit reduced-coverage gaps for properties, globs, rooted paths, traversal,
  missing files, ambiguous case-insensitive matches, malformed XML, and oversized
  inputs.

### R3 — Bounded refactor intent

- Parse `.refactorlog` XML with DTD and external entity resolution disabled.
- Emit Tier 2 structural facts for supported rename and schema-move operations
  when their object identities can be parsed safely.
- Preserve operation kind, bounded object identity, file span, rule ID, evidence
  tier, coverage label, commit SHA, extractor version, and supporting fact ID.
- Do not persist raw XML, raw SQL, operation bodies, connection material, local
  paths, or arbitrary XML fields.
- Hash, rather than render, an operation key when one is present.
- Cap input size and operation count; label truncation or unsupported structures
  as partial analysis.

### R4 — Review composition

- Database design review shall render SQL-project refactor intent as global
  review-recommended evidence, without treating it as PostgreSQL schema state.
- Release review SQL evidence shall render each supported refactor operation as
  review-recommended evidence and preserve upstream provenance and limitations.
- Extractor gaps shall become review gaps, not status values.
- Existing `ReleaseReviewStatuses` remain the only release-review status values.

### R5 — Non-claims

Every composed result shall preserve these limitations:

- no SQL project build or `.dacpac` inspection;
- no generated deployment-script inspection;
- no SQL execution or database connection;
- no proof that `[dbo].[__RefactorLog]` exists or contains an operation key;
- no proof that a rename or schema transfer was deployed or applied;
- no runtime reachability, compatibility, safe rollout, production state,
  release approval, or “safe to run” conclusion.

### R6 — Determinism and safety validation

- Equivalent inputs produce stable facts and review output.
- Tests cover supported operations, malformed/unsafe/missing inputs, caps,
  provenance, report composition, and non-rendering of sentinel content.
- Rule catalog entries document evidence and limitations for every new rule ID.
