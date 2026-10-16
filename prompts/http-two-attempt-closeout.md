# HTTP call-tracing closeout: at most two attempts

Goal for tonight: establish what outbound HTTP call chains are already traced
for this application and what, if anything, needs work tomorrow. This is NOT
runtime endpoint validation, HTTP contract verification, or a whole-site coverage
claim. Do not execute HTTP requests or application methods.

Two attempts maximum, five minutes each, ten minutes TOTAL. Stop early if the
available evidence answers the question. No third diagnostic loop. NO build,
scan, preflight, DLL discovery, context changes, source edits, database/network
calls, SDK changes, commits or new generated machine-readable artifacts.
Existing source and private artifacts remain local. Preserve all accepted DB
reports and their truncation/provenance labels.

Important workflow boundaries:
- The accepted Save query is database-operations filtered. Its lack of HTTP
  records cannot establish absent HTTP usage.
- webforms-review requery-handler currently targets database operations (or a
  method graph); do not invent --surface http-client flags for that command.
- The repository's accept-integrated-migration-report.md historically records
  two HTTP chains preserved as regressions and four accepted candidate groups
  at vb-syntax/0.4.14. Treat that as a lead to a LOCAL acceptance receipt, not as
  proof for the present source commit or scan.

ATTEMPT 1 — Recover and audit existing HTTP evidence.
Recover the authorized source root and the existing integrated migration report,
manifest/index and any prior HTTP acceptance receipt from local session records.
Use only the already-authorized repository and report locations, not broad disk
search. Inspect the Integrated migration inventory / incoming boundary coverage
and HTTP operation groups independently of the database-filtered Save query.
If needed, use bounded READ-ONLY queries over the existing full scan index to
count facts whose surfaceKind is http-client, keeping source semantic, source
syntax candidates, compiled observations and gaps separate. Inspect the actual
schema before querying; do not assume absent fields are zero evidence.

Record the report's code version, application source commit, scan identity and
scope locally. Compare any historical acceptance to this denominator. Stale or
missing evidence is unavailable, not current success and not proven absence.
For each retained HTTP group, record locally its ordered entry-to-call chain,
operation rule/tier and source locations, known wrapper hops and coverage limits.
Counts are evidence groups, not unique business operations or remote services.

If both historical HTTP chains are present for the accepted source scope with
inspectable ordered candidate hops, stop: report bounded call-tracing acceptance.
Do not run attempt 2 merely to fill the budget or verify endpoint behavior.

ATTEMPT 2 — One targeted gap check, only if attempt 1 leaves an actual question.
Choose ONE locally known HTTP operation whose entry path is missing, ambiguous
or stale. Check that concrete chain against the already-authorized local source
and retained facts, at most six call hops / twelve source files. Stop at the first
demonstrated missing or mismatched boundary. Distinguish:
  scope/filter exclusion; stale scan; missing operation fact; missing caller edge;
  syntax-only receiver ambiguity; unsupported compiled HTTP classification;
  traversal limit; or no established caller.
Use exact identities and source spans, not name-only guesses. Source review is
separate evidence and does not turn an absent scanner path into a retained path.
If no existing HTTP evidence or exact operation can be identified, stop and say
what artifact or source location is needed tomorrow. Do not rescan to compensate.

Return ONE concise sanitized table:
  HTTP evidence group | current retained path / source-only / unresolved |
  strongest and weakest relevant tiers | first gap, if any.
Use anonymous group labels. Add attempts used, elapsed time, source-scope match
yes/no/unknown, truncation, and exactly what must be tackled tomorrow (or none
for the observed scope). Never say whole-app HTTP is complete from two examples.
Unknown destination, authentication or payload contracts do not block tonight's
call-tracing acceptance unless they also prevent establishing the call boundary.
No private URLs, symbols, paths, payloads, secrets, source or hashes in the returned
cross-machine summary. Do not create another prompt or start additional work.
