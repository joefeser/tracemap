# Assess SQL migration report improvement locally

Supply the application and authorized input slice separately when invoking this
prompt. It contains no application identity. This is a private, bounded assessment
of report usefulness; agent judgments do not become TraceMap scanner evidence.

```text
Assess whether TraceMap's SQL/database extraction and reporting improves backend
API migration planning for the application the owner explicitly selects.

This authorizes a bounded static comparison on that input slice. It does not
authorize application edits, running the application, executing SQL, invoking
database methods, connecting to databases or uploading private evidence. Follow
the local workplace's authorization and tool policies. Feedback requires workplace
approval; sanitization alone does not authorize disclosure.

Compare these exact public TraceMap executable commits:
- Baseline: 6cb7532528d687a2fc20ca1135db130b244549da
- SQL implementation with projectless correction: e1932ae006a29e9aceb9eb86e6a8156cf7717df4
- Optional intermediate SQL implementation for isolating the correction: 6448790cba9c5d0b0681f90c69befab141a923a0
- Public implementation branch: codex/sql-database-matrix-864

Verify each tool checkout/commit locally. Keep the authorized application inputs
unchanged and record their exact snapshot identity locally only. Use the same input
slice, scan options and applicable modes for both versions, with separate fresh
output directories. Read committed CLI help and documentation for actual commands;
do not guess flags or create diagnostic code, parsers or one-off pipelines. Use
only approved tools already present or committed in the relevant TraceMap head.
If a needed committed capability or tool is unavailable, record
`diagnostic-capability=missing` and its name; do not replace it with ad hoc tooling.

Assessment only: do not implement fixes, edit TraceMap or its fixtures, start an
implementation subagent, create patches, commit, or push from this machine. If a
defect is observed, stop at an abstract reproduction requirement for independent
public development. Existing local tool modifications must not be discarded or
silently included: use an approved clean pinned tool checkout, or report blocked.
Any public Windows validation result must be checked for its exact commit; the
presence of this prompt does not establish Windows acceptance.

Read docs/validation/SQL_DATABASE_MATRIX.md from the SQL implementation. Record
actual application TFM declarations and public provider/reference versions
separately. Do not infer application TFM from assembly versions. Preserve missing
inputs, failed project loads, truncation and coverage limits. A failed build is not
clean analysis. Do not manufacture missing projects, inputs or references.

Before scanning, independently identify a small set of known operations/routes by
authorized local source inspection. Keep the private locations and declarations
local. Name the controls with neutral labels and state the expected evidence before
reading scanner results. Include these controls where available:
1. A projectless VB App_Code entry -> wrapper -> database route.
2. A project-backed operation.
3. Command reuse/reassignment or parameter/transaction handling.
4. An operation without a known entry connection.
5. A negative control such as Open/Close or a content-only match.
6. An explicitly typed relative-qualified VB receiver such as a framework
   namespace-qualified command, if present. Keep actual private names local.
Mark absent or unavailable controls explicitly; do not replace them silently.

Inspect facts, SQLite, paths, grouped HTML/JSON and the backend migration inventory
using committed tooling and report readers. Compare each named control individually:
- Which independently known operations were missing before and are retained now?
- Does the known VB wrapper route survive into the migration inventory?
- Are compiler-resolved calls, syntax candidates and compiled-only IL distinct?
- Are original signatures, rule IDs, tiers, locations, provenance and coverage
  limits retained? Inspect generator/bounded-input identities locally; do not return
  private input hashes. The legacy raw paths envelope has separate limitations.
- Is command text correctly hash-only, dynamic or unavailable? Are CommandType,
  parameter direction/type, transaction and connection witnesses correctly
  associated and useful? Check that reassignment/reset boundaries clear stale state.
- Are unresolved boundaries visible without treating disconnected operations as
  unused? A selected query root is not proof of application entry-point wiring.
- Are findings duplicated, misleading, dropped or stated with excessive confidence?
- Does report.md show Database Operation Candidates separately, including each
  rule/tier, unavailable SQL content, empty-coverage warnings and display totals?
  More than 50 candidates must disclose the omitted count, not silently disappear.
- For application-only source versus source with an already authorized wrapper
  body, distinguish missing wrapper evidence from operation candidates inside the
  body. Do not assume procedure/SQL arguments propagate across the call boundary.
  Do not acquire new binaries or broaden input scope just to make a route pass.

Use prompts/plan-backend-api-extraction.md to assess the same candidate migration
slice with each version. Explain which additional planning questions the new
evidence answers and which still require private contracts, source inspection or
human decisions. API shapes are proposals. More findings alone is not success.
This comparison establishes neither runtime use nor complete application/provider
coverage. Do not infer SQL from hashes or stored procedure bodies from names.

Keep application source, SQL, credentials, business identifiers, reports, binaries,
private paths, logs, input hashes, private commits and run identities on this
machine. Do not contribute code, patches, binaries or artifacts from this machine
to the public implementation. Do not commit or push assessment outputs.

If workplace-approved feedback is allowed, return a manually reviewed, sanitized
Markdown summary capped at 700 words, containing only:
- Exact public TraceMap commits and scan modes.
- Public framework/provider versions and categorical compatibility gaps.
- Aggregate counts by rule/tier where committed reports expose them; otherwise
  state unavailable. Do not invent counts or build a new diagnostic projection.
- Before/after results for each neutral named control, including unavailable cases.
- Concrete report usefulness improvements, misleading behavior and remaining gaps.
- Abstract suspected-defect descriptions sufficient for independently authored
  public reproductions, without private code or artifacts.

Before returning, review the summary for all prohibited information. Remove it or
withhold the feedback. Missing provenance in the sanitized handoff stays explicitly
unavailable; do not invent replacement identifiers. Separate scanner evidence,
local source observations and Claude's advisory judgment.

This feedback informs TraceMap issue #864 before its PR. Other-provider parity,
full-index/whole-site discovery, verified event binding and private contract capture
remain separate work. Do not open a PR, merge or change lane policy from this task.
```
