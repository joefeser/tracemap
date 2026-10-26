# Build bounded SQL Server result-column / reader evidence

Implement this next offline slice, validate it, and push the code with one
consolidated follow-up prompt. Do not return another planning-only handoff.
No work-machine input or schema is required. Do not open or merge a PR unless
separately requested.

## Starting point and authority

PR #911 merged at `730610582660fe32354479b35cf80eb2aa954a62`. Fetch `origin/dev`,
verify that merge is an ancestor, inspect status and preserve other work. Use a
clean `codex/sqlserver-result-column-evidence` implementation branch from fresh
`origin/dev`; do not reset an existing branch or overwrite another agent's work.
This prompt's branch is a handoff, not an implementation or test result.

Read `AGENTS.md`, `docs/VALIDATION.md`,
`docs/validation/SQLSERVER_PARAMETER_EVIDENCE.md`,
`docs/validation/SQLSERVER_DDL_PARAMETER_EVIDENCE.md`,
`docs/validation/DNN_SQL_VALIDATION.md`, and the complete existing
`.kiro/specs/sqlserver-ddl-parameter-evidence/`. Inspect live #908/#870 for context,
not additional authority. Create a small spec under
`.kiro/specs/sqlserver-result-column-evidence/` with requirements, design, tasks,
and implementation state; then implement it in this run.

Reuse the existing exact source-fact/digest and four-part logical identity
admission patterns, pinned ScriptDom parser, provenance/reporting conventions,
and immutable publication helpers. Keep the parameter-only contracts and
independently authored JSON versus DDL origins backward compatible. Do not
silently reinterpret older receipts as result-shape evidence.

## Outcome

Demonstrate a native, offline end-to-end comparison from a compiler-resolved
C# or VB command/reader consumer to one admitted first-result-set column shape.
The useful new answer is: within this explicit static subset, does the selected
reader's column-name or ordinal access agree with the declared projection?
Missing/renamed columns and out-of-range ordinals must be visible; uncertain
associations must remain unknown. Never produce an aggregate clean verdict.

Implement both independent, explicitly public synthetic result declarations
and a deliberately narrow DDL projection origin. Use distinct versioned
contracts or additive routes; do not bolt result claims onto a parameter match.
Choose CLI spelling consistent with the existing commands and document it.

## Bounded implementation scope

1. Admit a versioned public synthetic first-result schema: exact logical
   repo/revision/server/database/schema/procedure identity, ordered column names,
   explicit completeness/unknown state, source locations, rule/tier/version and
   exact generator/bounded-input SHA-256. A hand-authored declaration is not
   compiler, catalog, runtime, or deployed evidence.
2. Extend DDL admission through a separate opt-in result-shape path for one
   procedure with one unconditional SELECT projection of constants or explicit
   CAST constants, each with an explicit unique alias, with no FROM dependency.
   Accept at most a simple BEGIN/END wrapper. Inspect the entire body before
   claiming a complete shape. Everything else is explicit unsupported/unknown,
   not a partial column list mislabeled complete. Preserve whole-input failures
   for malformed SQL, identity ambiguity, SQLCMD, hash failures and bounds.
   Do not change existing parameter admission of procedures with other bodies.
3. Extract bounded consumer evidence in both C# and VB for a fresh resolved
   SqlCommand local with constant StoredProcedure intent and a directly assigned
   fresh SqlDataReader from that command's ExecuteReader. Cover direct constant
   name indexers, direct constant ordinal getters, and GetOrdinal of a constant
   name used directly or through a fresh unreassigned ordinal local. Support a
   simple using lifetime and a single Read() guard/loop without relaxing alias,
   mutation or escape checks. Bind every consumer to its exact command execution
   and reader instance, not merely a method, procedure name, or nearby call.
4. Compare column identity/existence and ordinal bounds only. Exact-case unique
   name matches may be bounded matches; case-only/collation-dependent names,
   duplicates, dynamic names, unknown/incomplete shapes or ambiguous bindings
   remain unknown. A missing name or out-of-range ordinal is a mismatch only
   against an admitted complete shape. Preserve source/DDL spans and distinct
   evidence tiers in JSON and Markdown.

This phase does NOT prove CLR/SQL type conversion safety, nullability safety,
row cardinality, values, execution, or runtime exceptions. Do not infer those
from a matching column name or a typed getter. Keep unsupported types/facets
visible without upgrading the column comparison to a type-contract verdict.

Explicit unknowns/non-goals: SELECT *, FROM/table/view resolution, joins, UNION,
conditional/dynamic/multiple result sets, SELECT assignments, EXEC/nested
procedures, NextResult, reader aliases/reassignment/escape, helper or virtual
dispatch, DNN hydration/provider wrappers, DataTable/DataSet inference, adapters,
ORMs, RETURN/OUTPUT/affected-row/scalar channel conflation, catalog/live evidence,
and deployed correspondence. Do not build a general SQL interpreter.

## Regression and smoke matrix

Author expectations independently before looking at extractor output. Include
both C# and VB, positive and negative cases, and unrelated commands/readers in
the same method. At minimum cover:

- Matching name; missing/renamed name; valid and out-of-range ordinal; reordered
  projection with name lookup; GetOrdinal association; case-only and duplicate
  names; dynamic name/ordinal; absent exact binding; stale revision/digest.
- Reader/command/ordinal alias, reassignment and helper escape; two reader
  instances; a Read guard/loop; NextResult and channel confusion controls.
- One supported SELECT versus IF/dynamic SQL/multiple SELECTs/SELECT */FROM/
  UNION/SELECT assignment; a supported-looking projection inside an unsupported
  body must never establish completeness.
- Mutated declaration/source/artifact bytes, provenance, deterministic repeated
  reports, malformed/oversized input, SQLCMD in comments/strings, symlink and
  output-publication safety. Preserve #911's scan-reuse byte bindings.
- Existing parameter and DDL tests remain green, and both retained 52-case
  parameter smokes retain their independently expected 8/12/32 denominator.

Use a small public synthetic corpus with a documented denominator and an
independent expected/observed matrix. A missing case is an explicit gap, never
a renamed pass. Unknown cases must survive into native machine and human output.
New derived artifacts need exact generator and bounded-input SHA-256; shareable
artifacts hash only privacy-projected public inputs. Update rule limitations
and extractor versions when source evidence changes.

## Validation and delivery

Run focused regressions and Python guards first. Commit implementation, then
freeze source, Git revision and shared build outputs for the full Debug suite
with `-warnaserror`. No concurrent build, smoke or commit during that gate.
Run the retained JSON/DDL smokes and the new native result-column smoke
sequentially afterward at the committed implementation head. Follow
`docs/VALIDATION.md` for affected C#/VB adapter pinned smokes; report actual
partial coverage and any justified deferrals, not implied passes. Run privacy
and whitespace guards. Record exact commands, counts, skips, revisions,
provenance and repeated-output checks in the spec state and validation doc.
A later docs-only results commit must be identified separately.

Resolve failures locally within this scope. Finish by pushing the implementation
branch and one consolidated follow-up prompt, reporting exactly what can now
be compared and what remains unknown. No repeated work-machine round trips.
Do not close #908/#870 or claim real DNN/SQL-A acceptance from synthetic results.

No private application or work-machine inputs, credentials, containers/image pulls, remote
hosts, SQL execution, database provisioning, full DNN installer, deployment,
paid review requests, policy changes or merge are authorized. If later asked
for a PR, use the current pr-review-loop skill, canonical ACK preflight and
owner-approved lane; never tag Qodo or bypass human merge authority.
