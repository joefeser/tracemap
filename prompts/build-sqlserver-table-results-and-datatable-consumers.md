# SQL priorities: table-backed results, then DataTable consumers

Implement the next bounded SQL evidence slice, not another planning-only handoff.
SQL coverage is the priority. Test-suite splitting and wall-clock optimization
are deferred: record observed timings, but do not reorganize test projects,
change concurrency, weaken gates or spend this slice on infrastructure cleanup.

## Start from current evidence

Read AGENTS.md, docs/VALIDATION.md, the current SQLSERVER_PARAMETER_EVIDENCE,
SQLSERVER_DDL_PARAMETER_EVIDENCE and SQLSERVER_RESULT_COLUMN_EVIDENCE validation
documents, and the complete sqlserver-result-column-evidence spec. Inspect the
current implementation, tests and smoke scripts before editing. Read #864, #865,
#870 and #908 as backlog context, not permission to implement all their scope.

Fetch origin/dev and verify merged PR #912 (merge commit
fc14857a54044e8edfaea170dab3b01141c0842a) is an ancestor. Use a clean dedicated
codex/ implementation branch from the fetched dev head; preserve other worktrees.
This prompt branch is a handoff, not the required implementation base. Read this
prompt before switching. Do not use the historical implementation head in the
older continuation prompt as the new base.

The existing pipeline compares bounded source parameters and reader accesses to
independently admitted JSON/DDL evidence. Result DDL currently admits constant
SELECT projections without FROM. Preserve exact identity, hashes, tiers, refusal
behavior and all previously fixed mutation/alias/control-flow protections.

## Delivery order: two separate implementation PRs

Build Phase 1 now. Commit, validate, push and return its ready-to-review branch
and one consolidated Phase 2 continuation prompt. Do not combine both phases
into one PR or start Phase 2 against an unmerged Phase 1. After the owner merges
Phase 1 and resumes the agent, refresh origin/dev and implement Phase 2 on a new
branch. Do not merge or request paid review. Open a PR only when separately asked.

Maintain a small phase-local requirements/design/tasks/implementation-state
record, updating completed tasks and exact validation heads. Make reasonable
in-scope design choices; ask only for a material authority or scope expansion.

## Phase 1 — explicit result columns from one admitted table

Extend offline DDL result-shape admission to a bounded, versioned synthetic table
definition and one procedure whose supported body projects explicit columns
from exactly one admitted table. Reuse the pinned parser and strict manifest
admission. Define the precise accepted grammar before implementation; do not
silently broaden the existing whole-input checks.

- Require exact manifest-bound database/schema/object identity and hashed table
  and procedure inputs. Never join by unqualified name alone or infer deployment.
- Start with an unconditional single SELECT of direct column references, with
  optional explicit output aliases, from one schema-qualified table. A simple
  table alias may be supported only with exact binding and negative tests.
- Retain projection order and output names. A complete shape requires every
  projected column to resolve uniquely against the admitted table definition.
- Missing dependencies, stale hashes, ambiguous/duplicate definitions, case-only
  or collation-dependent resolution, unsupported DDL and expressions remain
  rejected or typed unknown. Do not manufacture a complete shape from a supported
  fragment inside an unsupported procedure or table definition.
- Keep SELECT *, joins, views, CTEs, subqueries, UNION, temporary tables, dynamic
  SQL, conditional/multiple result sets, EXEC and schema migration execution out
  of this phase. Unsupported clauses stay explicit; this is not a SQL interpreter.
- Compare existing C#/VB reader name/ordinal accesses to these shapes using exact
  source/reader bindings. Preserve mismatches only against complete admitted
  shapes. Types, nullability, rows, values and runtime behavior remain non-claims.

Author expected outcomes independently of the extractor. Cover normal names,
aliases, reordered projections, missing/renamed consumer names, invalid ordinals,
missing tables/columns, wrong schema/database, duplicate/case ambiguity, stale
inputs and each newly relevant unsupported grammar family. Include real native
CLI comparisons, deterministic repeats and wrong-origin/artifact refusal tests.

## Phase 2 — SqlDataAdapter.Fill to DataTable column reads

After Phase 1 merges, add one bounded source-consumer path in both C# and VB:
a fresh trusted SqlCommand with constant stored-procedure intent, one fresh
SqlDataAdapter tied to that command, one fresh DataTable filled exactly once,
and constant name/ordinal reads through a directly associated DataRow.

Inspect provider overloads and author the accepted syntax matrix first. Specify
the admitted construction/SelectCommand assignment, Fill overload and row-read
forms explicitly; unsupported alternatives must not count as supported cells.
Allow only the small read/iteration guard needed for ordinary row consumption,
with tests binding it to the selected table, not an unrelated table or reader.

- Bind consumer evidence to the exact command, Fill invocation, adapter, table
  and row association. Parameter or reader bindings must not substitute for this
  new consumer identity. Evolve contracts compatibly and use distinct/versioned
  evidence where required; do not overload reader-only fields misleadingly.
- Compare column names/ordinals against Phase 1's admitted shapes and the existing
  independent JSON shapes, preserving explicit partial/unknown outcomes.
- Refuse or mark unknown aliases, reassignment, ref escapes, helper boundaries,
  repeated Fill, command mutation, table/column mutation, table mappings,
  unsupported MissingSchemaAction/MissingMappingAction behavior and ambiguous
  row ownership. No claim that rows exist, values convert safely or Fill executes.
- Preserve original source tier; structural schema correlation does not upgrade
  the source or prove runtime behavior. Failed semantic loading retains gaps.
- DataSet, reader-returning helpers, ORMs, DNN hydration, broader provider wrappers,
  result type/nullability comparison and multiple-result semantics are separate
  later slices, not extra tasks to absorb into this phase.

## Evidence and validation gates for each phase

Use only independently authored public synthetic C#/VB/SQL inputs. No work-machine
requests, private schemas, credentials, containers, SQL Server provisioning,
database/application execution, remote-host mutation or deployment. A live server
is not required. These results are not DNN or private-application acceptance.

Every derived artifact needs exact generator SHA-256 and bounded-input SHA-256;
shareable hashes cover only privacy-projected input. Preserve rule IDs, evidence
tiers, locations, revisions, extractor versions, explicit gaps and partial/no-
aggregate-clean-verdict labels. Update catalogs and current version assertions
when semantics change without rewriting historical validation evidence.

Run focused touched lanes while iterating. Commit the implementation, freeze the
source/head/build, then run the full Debug solution with -warnaserror for shared
core changes. Run native smokes sequentially after the full gate; do not rebuild
or change source underneath a test or smoke. Retain both existing 52-case parameter
smokes (8 match / 12 mismatch / 32 unknown) and existing result outcomes (JSON
36/10/36, DDL 36/10/58; 20 refusals) unless an independently explained intentional
expansion warrants a separate denominator. Add new phase cases separately.

Run relevant Python guards, private-path and whitespace checks. For language
changes follow pinned smoke policy and report reduced coverage honestly. Existing
public OSS scans without SQL calls do not establish SQL consumer coverage.
Do not change ACK policy; if PR work is requested, read the current pr-review-loop
skill and verify the canonical installation and lane before acting. Never tag Qodo.

Final handoff: branch/commit, exact supported grammar and consumer denominator,
expected versus observed outcomes, tests/timings, gaps/non-claims and one next-phase
prompt. Exhaust locally answerable questions rather than sending repeated partial
requests to the work machine. Keep test wall-clock refactoring explicitly deferred.
