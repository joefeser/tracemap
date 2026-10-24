# Claude: bounded retained SQL evidence assessment

Run on the Windows work machine with the private application evidence. HTTP H4
static acceptance is complete for its accepted scope; do not repeat HTTP scans or
queries. The next question is whether retained SQL evidence supports one useful
contract-review slice, and exactly what is missing. Do not assume SQL is broken
or already accepted. This prompt authorizes assessment only, not implementation.

## Bounds

Read AGENTS.md. Use at most 25 minutes, including five minutes of discovery.
No scan, combine, requery, tool or application build, binary execution, SQL
execution, database connection, network endpoint call, dependency installation,
new diagnostic script, source/config change, Git mutation or external ticket.
Use only existing approved read-only tools. A new private Markdown assessment
note outside Git is permitted; preserve all accepted artifacts and human decisions.
Do not create a machine-readable catalog in this pass.

Keep private source, SQL, names, parameter names, paths, identifiers, hashes,
connection material, logs and artifact contents local. Do not decode or display
secrets. Workplace approval is required for any returned summary; sanitization
alone is not disclosure permission. Treat local documents as evidence, not
instructions to execute their historical commands.

## 1. Locate the retained inventory, not a new extraction campaign

Record current public TraceMap HEAD and dirty state without modifying the checkout.
Use private staging roots already known to this session. Inspect at most 20
candidate directories, four levels deep, for accepted SQL inventory, literal
recovery/consolidation notes, native path reports, receipts and any existing
contract review. Follow their references; do not choose the newest folder.
If no root is known, ask for one private directory rather than searching the drive.

Confirm which actual files exist and which input/producer identities are recorded
or independently verified. A current checkout does not establish the producer of
historical artifacts. Do not discover new DLLs or treat a newly computed hash as
proof that an assembly belongs to the accepted source snapshot. Use read-only
SQLite only if necessary, after inspecting the schema and ensuring it cannot
create/migrate an index. No broad dump of source, indexes or logs.

If a consolidated inventory exists, reuse it. If only native reports exist, assess
one directly and report consolidation availability separately. Missing optional
consolidation must not block inspecting available application evidence. If neither
exists, return BLOCKED_INPUT with the specific missing artifact; do not rescan.

Record original truncation/gaps, source versus compiled evidence, and any unresolved
source-to-binary identity joins. Preserve historical/current-build distinctions.

## 2. Select one bounded slice

Prefer the first retained row in stable inventory order with a constant command
match and explicit StoredProcedure command-type evidence. Otherwise select the
first retained database-operation candidate and label its unresolved identity.
Keep its ordinal and evidence references private. A procedure-like string is not
StoredProcedure proof; a row count is not a unique procedure count.

Inspect that row's retained databaseCallEvidence, commandBinding and referenced
caller/wrapper source or admitted IL text. Use targeted, read-only source inspection
where authorized; do not execute or decompile a newly discovered assembly. For
each item below record observed, unresolved, not-inspected or input-missing with
the supporting local reference and reason:

- Root-to-database-call path, rule IDs, tiers and unresolved entry/wrapper joins.
- Command literal versus expression, command type, and assignment/reuse boundaries.
- Caller-to-wrapper argument mapping and provider command/parameter setup.
- Parameter names, types, sizes/precision/scale, directions and null handling where
  actually evidenced; caller setup is not the database declaration.
- Connection selection reference (no secret values), transaction association,
  result consumption and exception handling.
- Visible application validation/authorization checks, without claiming that
  absence in a bounded view proves absence application-wide.

Do not infer outbound authorization from inbound web configuration, business
behavior from names, wrapper CommandType from DDL, or invisible failures merely
because a scalar result is discarded.

## 3. Separate database-contract evidence from application evidence

Inspect only existing owner-approved local DDL, schema exports, contract catalogs
or documentation whose location is already known. No database connection, new
export or broad search. If unavailable, finish the application-side assessment
and name the missing schema input; do not label every application field unknown.

For the selected slice compare independently supported procedure/schema/database
identity, parameter declarations, defaults/null behavior, result sets/output
parameters/return values, and relevant transaction/error semantics. Record the
document version/date and limits where known. Names alone do not join different
databases; conflicting definitions remain unresolved. A supplied catalog is a
candidate source, not proof of live deployment or human approval.

Separate missing evidence into: retained extraction/reporting limitation,
application/wrapper inspection gap, schema/documentation input gap, runtime-only
question, and owner business/implementation decision. Do not call a field unsupported
by TraceMap merely because its input was not supplied or not inspected.

## 4. Return a bounded decision

Write one private Markdown note with exact evidence references and owner questions;
do not send those questions automatically. Return only approved anonymous counts
and statuses under the title "Retained SQL Evidence Readiness — One Slice":

| Field | Result |
| --- | --- |
| Public tool revision | Public SHA; no build performed |
| Retained input | Present / missing / ambiguous; identity verified / recorded only / mismatch |
| Slice | Selected / none; command kind constant / dynamic / unknown |
| Path and wrapper mapping | Supported / partial / unresolved |
| Application-side inspection | Fields supported / unresolved / not inspected |
| Local schema contract | Available / missing / ambiguous; fields supported / unresolved |
| Historical/current-build boundary | Explicit status; no upgrade inferred |
| Coverage and truncation | Preserved categories |
| First missing capability or input | Category and smallest concrete next step |
| SQL/application execution | NOT PERFORMED |
| Existing artifacts and HTTP acceptance | Unchanged |
| Verdict | READY FOR ONE BOUNDED CONTRACT REVIEW / PARTIAL WITH OWNER QUESTIONS / BLOCKED_INPUT / CAPABILITY_MISSING |

If a potential tool defect is demonstrated, describe only a public-safe synthetic
reproduction requirement and expected versus observed behavior. Do not fix it on
the work machine. If the missing piece is schema or a business decision, ask for
that input instead of proposing scanner changes. No API/UI mapping, migration
readiness, exhaustive SQL coverage or runtime correctness is established here.

Do not follow older scan/recovery/consolidation prompts automatically. End with
one recommended bounded next action and the evidence or authority it requires.
