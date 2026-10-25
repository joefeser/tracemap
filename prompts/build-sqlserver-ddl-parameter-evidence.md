# Build bounded offline SQL Server DDL parameter evidence

Implement the next local, public-synthetic SQL slice after merged PR #910.
This prompt branch starts at origin/dev merge commit
560fa24216516b17f75882d7d10011361b3f9c11. Verify current Git state before work;
do not overwrite other work or assume later changes are already validated.

## Objective and authority

Add a separately identified, bounded SQL Server DDL origin for stored-procedure
parameter declarations and connect it to the existing native comparison/report
pipeline. Prove the route from synthetic SQL declaration plus C#/VB command
evidence to parameter comparison without a running SQL Server.

Implement, test, document, commit and push the bounded slice on this branch.
Do not merge, launch another agent/thread, request paid reviews, activate Docker,
pull images, run SQL, access remote hosts, deploy applications or use private
schemas/data. This is not permission to activate the optional harness. No new
work-machine acceptance round trip is required for this phase.

## Read first

- AGENTS.md and docs/VALIDATION.md.
- docs/validation/SQLSERVER_PARAMETER_EVIDENCE.md and DNN_SQL_VALIDATION.md.
- .kiro/specs/sqlserver-parameter-evidence/ including implementation-state.md.
- Current issues #908 and #870, the existing native produce/compare code, and
  the public synthetic SQL/C#/VB fixtures. Issue scope is not live-execution authority.

Create a small spec with requirements, design, tasks and implementation-state
under .kiro/specs/sqlserver-ddl-parameter-evidence/. Keep checkboxes current.
Reuse the existing comparison machinery; do not replace independently authored
JSON declarations or mislabel DDL as that origin.

## Bounded implementation

1. Inspect existing SQL parsing support and dependency policy. Prefer a real
   bounded T-SQL AST parser over regex pretending to implement SQL grammar.
   If adding a dependency, verify its official license, compatible target and
   pinned version, and document the decision. No broad SQL interpreter.
2. Admit an explicit list of public synthetic SQL files with logical repository,
   revision and server/database identity supplied separately. Parse explicitly
   schema-qualified CREATE PROCEDURE, ALTER PROCEDURE and CREATE OR ALTER
   PROCEDURE declarations within documented bounds. Do not apply migration order:
   duplicate or conflicting declarations are ambiguous, not last-file-wins.
3. Preserve SQL file/line spans, parser/extractor version, rule IDs, evidence
   tier and origin. DDL-supported structural declarations are not Roslyn semantic
   evidence, deployed catalog state or proof that a procedure executes.
4. Extract only the supported parameter subset needed by current comparisons:
   exact names, built-in type, declared size/MAX, and OUTPUT capability.
   Preserve defaults/precision/scale/collation as explicit unsupported evidence
   where comparison cannot prove their semantics. Do not turn SQL OUTPUT into
   proof of a runtime output assignment or conflate RETURN with result columns.
5. Fail closed or emit typed unknown/gap records for malformed SQL, unresolved
   SQLCMD variables/includes, implicit schema, duplicate identities, alias/CLR/
   table-valued types, encrypted/unavailable definitions, or exceeded bounds.
   Procedure bodies are not interpreted; dynamic SQL and result shapes stay
   outside scope. Document whether each unsupported input is rejected or partial.
6. Bind declarations to source calls only through the existing explicit exact
   logical server/database/schema/procedure and source-evidence bindings. No
   procedure-short-name, connection-string, DNN qualifier or deployment inference.
7. Add a distinct native CLI admission route and preserve the old JSON route.
   Version schemas when necessary instead of silently changing meaning. Every
   new derived machine-readable artifact requires exact generator SHA-256 and
   bounded-input SHA-256; shareable outputs hash only privacy-projected inputs.
   Validate origin/version/digests/identity at admission, not just serialization.
8. Preserve partial coverage, unknowns, gaps, truncation and no aggregate clean
   verdict in JSON/Markdown/other existing relevant renderers. Use the hardened
   no-overwrite publication and error-preservation contracts from #910.

## Independent regression matrix

Author expected outcomes independently of parser output. Include:

- C# and VB positive matches, size/type/direction mismatches and unbound calls.
- All admitted procedure statement forms, quoted identifiers, comments,
  multiline declarations, batch separators, nvarchar(MAX), and OUTPUT parameters.
- Malformed syntax, fake declarations in strings/comments, duplicate/conflicting
  objects, case/schema/database identity differences, stale revision/digests,
  unsupported types/facets/defaults and bounded-input limits.
- Separate RETURN, OUTPUT and result-set controls; no result-column or runtime
  interpretation. Unsupported/default-dependent conclusions must remain unknown.
- Byte-identical repeat reports, exact provenance, no-overwrite/concurrent output
  protection, failed-write cleanup and preservation of the primary exception.

Retain the original 52-case JSON-origin matrix (8 bounded matches, 12 mismatches,
32 unknowns), its per-report unbound-call gaps, and #910 initializer/VB With,
execution-argument mutation and publication regressions. If a deliberate change
alters this denominator, explain and independently update the expectation rather
than silently replacing it. Keep real DNN selected-reader/wrapper gaps explicit;
synthetic DDL success does not establish DNN end-to-end binding.

## Validation and delivery

Run focused tests and Python harness guards first. Then commit implementation
and keep source, Git revision and build outputs stable while running each gate.
Do not commit, rebuild shared outputs or run a competing smoke during a full
suite: repeat scans bind source and revision. Run the full Debug solution with
-warnaserror, followed by committed-head CLI smoke in new directories. Follow
docs/VALIDATION.md for relevant pinned adapter checks or explicitly record any
deferral. Run private-path and whitespace guards before publication.

Deliver one consolidated implementation report and follow-up prompt: exact head,
supported grammar/denominator, origin and provenance guarantees, independent
expected/observed matrix, test results, limitations and genuine remaining blockers.
Keep result-column/consumer comparison, catalog/live evidence, private SQL-A,
application build and migration readiness as separate future work. Do not close
#908 or #870 merely because this bounded slice passes.

If PR work is requested later, read the current pr-review-loop skill, verify the
canonical ACK executable/version/provenance and repository doctor, and preserve
lane policy, reviewer ceilings and human merge authority. Never tag Qodo or
silently weaken review requirements. This prompt does not request a PR or merge.
