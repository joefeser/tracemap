# Plan a human-approved .NET 10 backend extraction

Use only the explicitly supplied TraceMap evidence and authorized private source
or contract selections. This is planning, not permission to edit, run the site,
contact databases/services, read secrets, or retain/retire functionality.

Read the receipt, generator/input identities, scope, coverage and truncation
before interpreting rows. For `backendExtraction.scope =
retained-query-report-not-full-index`, do not call the inventory site-wide.
Report evidence records separately from proposed business-operation groupings.

For each database or outbound HTTP operation:
1. Cite node references, ordered variant indexes, query-root references, rule,
   tier, source/compiled location and supporting fact IDs from the handoff.
2. Separate evidenced entry-point wiring from a method merely selected as root.
   Candidate paths remain candidates. No path does not mean unused.
3. Read edge limitations and gaps. A generic definition target has no specialized
   downstream body. A retained edge with a provenance warning is not missing.
4. State the command/procedure or HTTP contract only when supplied evidence
   establishes it. A command-text hash is not readable SQL; StoredProcedure
   command type is not a procedure name. Never infer targets from method names.
5. List missing parameters/results, transaction boundaries, auth/session context,
   error behavior, destinations and procedure dependencies. Ask for the smallest
   authorized input that resolves each blocking question. Do not reconstruct secrets.
6. Keep owner retain/retire/replace decisions undecided unless separately supplied.

Return: evidence/coverage receipt; operation-to-functionality table; established,
candidate and unproven facts; unresolved blockers; proposed .NET 10 API groupings
and contracts; human decisions required; one DB and one HTTP vertical-slice test
plan. Label API routes/DTOs as proposals, never extracted facts. Do not generate
production code or change the application until the owner approves a slice.

Keep private identifiers/source/SQL in the authorized private assessment only.
No private artifacts belong in public issues, PRs, fixtures or shared projections.

For outbound HTTP, read the mode coverage limits. Signed semantic framework calls,
unresolved name-only candidates and compiled-only IL observations are different
evidence. Request/convenience/JSON, WebClient and WebRequest coverage depends on
target-framework references. Do not turn WebClient address evidence into an HTTP
protocol claim, content-only reads into outbound requests, or UNKNOWN verbs and
destinations into proposed extracted contracts. Wrapper specialization and mutable
request state remain unresolved. Absence never means unused; static evidence never
proves runtime execution. Request authorized contracts for these gaps.

`vb.syntax.http-coverage-gap.v1` means file-level HTTP analysis is unsupported
without VB semantics. It does not identify a call or prove absence. Preserve that
limitation in migration plans even when the operation inventory is empty.
The work machine cannot contribute public code or patches. Keep private
validation local; any feedback requires workplace approval, not merely sanitization.

Legacy HTTP compatibility: use [the public reference matrix](../samples/outbound-http-coverage/LEGACY-COMPATIBILITY.md) and its per-TFM overload inventories. Record application project TFM, TargetFrameworkAttribute and exact referenced assembly/member identities separately. System 2.0.0.0 does not identify an application's TFM. Missing TFM stays unknown. Package-added/backported APIs, other profiles and private application parity remain unvalidated. Run the public static-only smoke with `python3 scripts/validation/http-coverage-smoke.py --output <public-temporary-output>`; Windows CI results must bind an actual run and commit. Never execute fixture methods.

The legacy HTTP fixture TFMs are **representative test targets, not confirmed application targets**. Application-specific compatibility is **pending work-machine validation**, a deferred check that does not block this public branch. Use [InspectHttpFramework](../scripts/diagnostics/InspectHttpFramework/README.md) for read-only XML/metadata collection and its [sanitized return template](../scripts/diagnostics/InspectHttpFramework/SANITIZED-SUMMARY.md). Keep project/config declarations, binary attributes and encoded reference signatures separate; no value wins automatically. Keep the local report, private input hashes, paths and artifacts on the work machine. Public guards: `python3 scripts/validation/test_http_framework_inspection.py`.
