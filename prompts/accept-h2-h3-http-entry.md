# Verify H2 and H3 from their retained event entry

This corrects the root scope of the previous H2/H3 investigation. Use the existing
private inventory, combined index and retained event-binding/call facts. Do not
rescan or change extraction. H1 is accepted; preserve its artifacts and correction
note, all previous H2/H3/H4 reports, and database baselines byte-for-byte.

## Bounds

15 minutes; at most one CLI build and two HTTP requeries. Prefer one shared-entry
query if both operations have the same verified entry. No scan, combine, preflight,
source edits, new diagnostic helpers, application execution, network requests,
database-server access, dependency installation or Git mutation. Existing approved
read-only SQLite inspection is allowed. Keep recorded traversal limits unchanged.
Stop at the budget; never retry merely to obtain an untruncated result.

All private identifiers, selectors, hashes, reports and notes stay local, outside
Git. Outputs must be new and outside the retained run and input directories.

## Verify identity and choose the correct root

1. Read current branch, full HEAD and dirty state live from the operator-prepared
   checkout of codex/http-evidence-regressions. Never reuse a session-start SHA.
   Preserve unpushed commits and unrelated files; stop on build-affecting changes.
   Verify the retained combined-index hash against its accepted receipt.
2. Reconcile the prior investigation's entry claim against retained facts: identify
   the exact event binding, its handler declaration and the outgoing CallEdge to
   the previously selected H2 control-method root. Verify source/scan/commit/symbol
   identities, receiver/event identity, rule, tier, locations and supporting facts.
   Do not choose the helper or constructor merely because it already yielded paths.
   Do not fabricate a caller from naming, markup containment or lifecycle presence.
3. Independently identify the expected H2 and H3 HTTP operation facts and their
   caller chains. Do not assume a path to H2 establishes a path to H3. Record their
   exact expected terminal identities locally before querying.
4. Copy the verified event handler's sourceIndexId, scanId, commitSha and symbolId
   into a private JSON selector. Require exactly one retained MethodDeclared for
   that full identity. If evidence is missing or ambiguous, stop with that finding;
   do not substitute a downstream root and call it entry-to-HTTP acceptance.

## Run and inspect

Build once if needed:

```text
dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj --nologo -c Debug
```

Record HEAD before/after build and query. Hash the actual invoked tracemap.dll
before/after query; require unchanged hashes and equality to GeneratorSha256 in
the new receipt. Git revision and binary SHA-256 are distinct identities. Stop
on changes or inability to establish which binary was run.

```text
dotnet <built-tracemap.dll> webforms-review requery-handler --run <retained-run> --bundle <verified-bundle> --source-root <private-entry-selector.json> --out <new-private-output> --surface http-client --max-depth <recorded-depth>
```

Use one query for a shared verified entry. A second is allowed only for a distinct
verified entry necessary for the other group, not as a repeated search with larger
bounds. Validate receipt root, HTTP scope, limits, index/generator hashes and native
bounded-input hash. Retain all native artifacts unchanged.

For each group, inspect actual ordered paths from that exact handler to its expected
HTTP terminal, with supporting facts, rules, tiers and locations. Check the event
binding separately as the entry anchor; it need not be a traversed call edge.
A positive aggregate chain count or unrelated HTTP terminal is not acceptance.

## Truncation is not an extraction verdict

Preserve truncated, traversal work, counts and any native cutoff/limit diagnostics.
If no reason is recorded, say reason unavailable; do not infer which limit fired.
`truncated=true` means enumeration is incomplete. It does not invalidate an emitted
supported path, prove a missing terminal absent, or establish an extraction defect.
Distinguish native chain/variant counts from counts of variants actually matching
H2 or H3. If the expected path is absent under truncation, report PATH NOT ESTABLISHED
WITHIN BOUNDS, not extraction failure. Missing facts require separate index evidence.

H4 remains out of scope: preserve its method-to-HTTP result and framework-entry
modeling limitation. Do not invent a source CallEdge or upgrade tiers to close it.

## Return

Write one new private note. Return a manually sanitized table with one row each
for H2 and H3: verified event entry yes/no; expected terminal present in returned
path yes/no; ordered identity joins verified yes/no; strongest/weakest tier;
matching path/variant counts; native truncation; recorded limit reason if available;
and ENTRY-TO-HTTP RETAINED / PATH NOT ESTABLISHED WITHIN BOUNDS / ROOT NOT FOUND /
AMBIGUOUS ROOT / CONTEXT REJECTED / TIME LIMIT.

Include public tool revision, build status, binary verification yes/no, elapsed
time and whether H1/database artifacts stayed unchanged. Do not return private
paths, symbols, hashes, source, payloads or URLs. Do not declare exhaustive HTTP
coverage, runtime execution, or H4 completion.
