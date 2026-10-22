# Claude: verify work-machine HTTP readiness after PR #901

Determine whether this machine has the tool and retained evidence needed for the
accepted static HTTP review scope. Verify; do not restart the debugging campaign.
Historical acceptance is a lead, not a substitute for checking local artifacts.

## Safety and bounds

20 minutes, one TraceMap CLI build, at most three HTTP requeries (H1, the shared
H2/H3 event entry, and H4). No scan, combine, preflight, application build/execution,
database access, outbound HTTP requests, dependency installation, source changes,
new diagnostic scripts, Git mutation, or full test suite. Stop at the budget.
Use only committed tooling and existing approved read-only inspection tools.
Do not increase traversal limits or retry to remove truncation.

Keep private paths, symbols, source, selector IDs, hashes, payloads, URLs, logs,
indexes, reports, and business identifiers local and outside Git. Write new outputs
outside all retained inputs/runs. Never overwrite accepted artifacts or notes.

## 1. Establish the tool and inventory

Use the operator-prepared checkout containing merged PR #901, not a deleted feature
branch. Read live full HEAD and dirty state. Verify commit
`d8fef8660997e7a9271d6d24dc68bb1e24057170` is an ancestor of HEAD; otherwise stop with
TOOL UPDATE REQUIRED and ask the operator to update. Do not pull or switch branches.
Stop on build-affecting changes. Record public tool revision separately from binary
SHA-256; neither a session-start revision nor a later pull identifies an old binary.

Locate the accepted baseline inventory and acceptance/correction notes locally.
If ambiguous, use `prompts/locate-accepted-work-baseline.md` for bounded read-only
discovery, within this same total budget. Do not choose the newest folder by name.
Identify the verified completed run, bundle and native receipt for each group.
H1 and H2/H3 may use the old accepted index; H4 must use the accepted fresh index
that contains its lifecycle candidate. Do not expect that fact in the old index.
Verify artifact hashes against their receipts and record baseline hashes locally.
Missing or inconsistent inputs mean INPUT MISSING / INPUT DRIFT, not permission
to regenerate them. Preserve the accepted database baseline without rerunning it.

Build once:

```text
dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj --nologo -c Debug -warnaserror
```

Record exit status and warnings. Hash the actual invoked DLL before and after the
queries, verify HEAD stayed unchanged, and match each receipt's GeneratorSha256
to that DLL. Verify other native generator hashes against their own assemblies,
not the CLI by assumption. If no committed verifier exists for a hash contract,
report NOT VERIFIED; do not invent a verification claim or a new helper.

HARD STOP: a nonzero build exit, warnings promoted to errors, or unverifiable
invoked-binary identity means BUILD BLOCKED / BINARY PROVENANCE UNVERIFIED.
Do not run any requery using an existing DLL after a failed build, even if the
compiler emitted a DLL or the diagnostic appears unrelated to HTTP. Retained
historical evidence may still be useful, but it cannot qualify the current tool.
Preserve the prior note and add a correction; never silently rewrite its verdict.

If a local governance wrapper rejects `exact-source-method`, identify whether it
is committed tooling or a private runner. Keep source-method, compiled-method and
named-handler selection kinds distinct. Do not relabel a source receipt as compiled
to appease a wrapper. Report RUNNER CONTRACT MISMATCH separately; do not modify the
runner. Direct native CLI verification below is permitted only after the build and
binary identity gates pass, and does not certify wrapper compatibility.

## 2. Verify roots and generate bounded current-tool reports

Resolve each root from retained facts with sourceIndexId, scanId, commitSha and
symbolId. Require exactly one MethodDeclared for the tuple. Keep selectors private.
For H1 and H2/H3, verify the event-binding anchor separately and follow exact
identity joins; do not substitute a helper or constructor for an event entry.
For H4, select the lifecycle override method, not an invented framework caller.
Identify each expected HTTP terminal before querying. Do not equate aggregate
chain counts with a path to the expected terminal.

Use the corresponding run/bundle and unchanged recorded depth and other budgets:

```text
dotnet <built-tracemap.dll> webforms-review requery-handler --run <verified-run> --bundle <verified-bundle> --source-root <private-selector.json> --out <new-private-output> --surface http-client --max-depth <recorded-depth>
```

One query may verify both H2 and H3 only if they share the verified event entry.
If they require distinct entries, report the additional query needed rather than
exceeding three queries. Validate HTTP scope, exact source root, index identity,
`RootSelectionKind=exact-source-method`, source-method ClaimLevel, and recorded
bounds. Check native hash contracts where supported by committed tooling.

## 3. Acceptance matrix

- H1: supported event-entry-to-expected-HTTP path, ordered identity joins, rules,
  tiers, locations and supporting facts. Event binding is an anchor, not a call edge.
- H2 and H3: independently verify each expected terminal from the shared event
  entry. One does not prove the other. Preserve UNKNOWN HTTP verb when not proven.
- H4: supported method-to-HTTP path plus the matching Tier2Structural
  framework-lifecycle-candidate in the fresh retained index, handoff
  `header.webFormsEntries`, and separate HTML entry-relationships section. Verify
  scoped supporting declaration/override evidence. Candidate presence must not
  manufacture a CallEdge, upgrade the path tier, or prove framework dispatch,
  control construction or lifecycle execution.
- All groups: HTTP-specific HTML/receipt limitations, unchanged accepted artifacts,
  and native truncation, work units and cutoff reasons preserved. A supported path
  remains useful under truncation; an unlisted path is not proven absent. Missing
  paths under bounds are not extraction failures without separate index evidence.

Do not require byte-identical new reports across tool versions. Compare the expected
root/terminal identities, rule/tier/location/supporting evidence and limitations.
Separate extraction absence, root-selection error, bounded traversal, projection
loss, rendering loss and missing local inputs. Do not reopen database schema or
stored-procedure validation: those are deferred and outside this readiness scope.

## Return one bounded answer

Write one new private local readiness note. Return only a manually sanitized table:
group; required inputs present; expected path verified; strongest/weakest tier;
candidate visible in index/JSON/HTML (H4); truncation and categorized reason;
first missing capability/input, if any. Include public tool revision, build result,
binary verification yes/no, baseline unchanged yes/no and elapsed time.

Overall verdict: READY FOR ACCEPTED STATIC HTTP REVIEW / READY WITH RECORDED
LIMITATIONS / BLOCKED BY MISSING INPUT / TOOL UPDATE REQUIRED / REGRESSION SUSPECTED /
TIME LIMIT / BUILD BLOCKED / BINARY PROVENANCE UNVERIFIED. A failed build or unverified
binary forbids either READY verdict. Truncation and unproven H4 dispatch must remain explicit even when ready.
If blocked, name the smallest operator action; do not execute it. No exhaustive
HTTP coverage, runtime behavior, SQL validity or migration-readiness claim.

## Committed reporting-component verification

Verification-only follow-up: when the operator already has a successful readiness
run and its original invoked binaries, skip the build and all requeries. Run only
the verifier below for those three receipts against those preserved binaries.
Do not compare old receipts to newly rebuilt DLLs. Retain the prior tool revision
and readiness verdict, and append a separate component-verification note; this
does not qualify a newer checkout. Stop if the original binaries are unavailable.

For each of the three new native HTTP receipts, run the read-only verifier using
an already installed Python 3.11+ (do not install dependencies):

```text
python scripts/verify-http-report-provenance.py --receipt <new-private-output>/handler-requery.local.json --cli-dll <actual-invoked-cli-dll>
```

Require exit 0 and CLI_AND_REPORTING_BYTES_MATCH; HANDOFF_LINK_MATCH. The verifier
compares CLI and reporting hashes separately, uses TraceMap.Reporting.dll beside
the selected CLI, checks the grouped handoff hash/size against its receipt entry,
and checks the handoff reporting-generator and index identity. It does not execute
assemblies or read source/index contents, and writes no files. Its output deliberately
contains no private paths or hashes. Run within the existing 20-minute budget.

A nonzero exit is PROVENANCE_NOT_VERIFIED and forbids READY. Missing Python is
VERIFIER UNAVAILABLE: stop, do not install or invent a helper. Keep all existing
artifacts unchanged. Record this result separately from the successful build.
This is byte identity against operator-selected binaries, not authenticated build
provenance, a recomputation of boundedInputSha256, complete report validation,
verification of nested inventory generators, or runtime proof.
