# Continue bounded SQL Server result-column / reader evidence

Read AGENTS.md, docs/VALIDATION.md, docs/validation/SQLSERVER_RESULT_COLUMN_EVIDENCE.md
and the complete `.kiro/specs/sqlserver-result-column-evidence/` (especially
implementation-state.md). Fetch origin, verify the branch
`codex/sqlserver-result-column-evidence`, implementation head
`1660a5a8cb0107abb3792788f3eed637987ac30b` and its separate docs-only results commit,
and live #908/#870 (context only; both stay open). Preserve other agents' work.
Do not assume recorded gates still hold at a newer head.

## What can now be compared

Within an explicit static subset, offline and without SQL execution: a fresh
compiler-resolved C# or VB SqlCommand local with constant StoredProcedure intent and
text, a SqlDataReader local directly assigned from that command's ExecuteReader(), and
its constant name indexers, constant ordinal getters and GetOrdinal(constant) accesses
(direct or via a fresh unreassigned ordinal local), with a using lifetime and one
Read() if/while guard. Each access, bound by exact fact ID and reader-evidence digest
to one four-part identity, is compared with one admitted first-result shape from
independent JSON (`sqlserver-result-schema`) or a narrow DDL projection
(`sqlserver-ddl-result`: one unconditional aliased constant/CAST-constant SELECT, no
FROM, at most one BEGIN/END). Exact unique names and in-range ordinals are bounded
matches; missing/renamed names and out-of-range ordinals are mismatches against
complete shapes. Reports are partial, never an aggregate clean verdict.

## What remains unknown or out of scope

Case-only/collation-dependent and duplicate names, dynamic names/ordinals, unknown or
incomplete shapes and ambiguous/absent bindings; aliases, reassignment, escapes,
helpers, virtual dispatch, NextResult, other reader members and control flow; types,
nullability, cardinality, values, execution and runtime exceptions; SELECT *,
FROM/table/view resolution, joins, UNION, conditional/dynamic/multiple result sets,
SELECT assignments, EXEC/nested procedures; RETURN/OUTPUT/affected-row/scalar
channels; DataTable/DataSet, adapters, ORMs, DNN hydration (`CBO.FillCollection`,
`IHydratable`) and provider wrappers; catalog/live evidence and deployed
correspondence. Pinned OSS repos have no database calls, so reader extraction is
validated only on the synthetic corpus.

## Candidate next slices (choose one, get owner agreement first)

1. Table/view-backed projections: admit versioned synthetic table DDL and resolve
   `SELECT <explicit columns> FROM <one admitted table>` without joins, keeping
   `SELECT *` unknown unless the table version is admitted.
2. Source consumer breadth: DataTable/`SqlDataAdapter.Fill` column reads, or a
   single-level helper that returns the fresh reader, each with independent
   expectations and the same fail-closed alias/escape rules.
3. DNN route: a bounded `CBO.FillCollection<T>`/`IHydratable` hydration model tied
   to the #908 public slice, with independently inspected expectations.
4. Housekeeping found here: `database.sqlserver.ddl-parameters.v1` (#911) has no
   rule-catalog entry.

Any slice must keep the existing parameter and result contracts, both 52-case
parameter smokes (8/12/32) and the result smoke (per language JSON 18/5/18, DDL
18/5/29, 20 refusals) green, add independent C#/VB positive and negative cases,
bind new artifacts to exact generator and privacy-projected input SHA-256, and follow
the commit-then-frozen full Debug -warnaserror protocol before sequential smokes.

No private/FFP/work-machine inputs, credentials, containers/image pulls, remote
hosts, SQL execution, database provisioning, deployment, paid review requests,
policy changes or merge are authorized. If a PR is requested, use the current
pr-review-loop skill, canonical ACK preflight and owner-approved lane; never tag
Qodo or bypass human merge authority.
