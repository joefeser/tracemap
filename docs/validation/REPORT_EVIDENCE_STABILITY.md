# Report evidence stability and HTTP follow-up

This slice preserves display evidence across scan-derived ID changes and pins
remaining HTTP entry limitations with public fixtures. It does not implement or
certify full ECMA-335 compliance, runtime HTTP behavior or database schema validity.

## Current branch

`codex/http-evidence-regressions` starts at freshly fetched `origin/dev`
`7337af3e2`, including merged PR 900. The earlier uncommitted reporting slice
was carried forward without overwriting the original worktree. No changed file
overlapped the intervening dev changes. The historical comparison below remains
pinned to its original inputs; it is not a new extraction census for this base.

On this base the 164-test focused suite passes, followed by 20 passing HTTP tests
after tightening inherited-property identity and explicit-gap assertions. The
new C# check pins a semantic property access to the base declaration's symbol;
it separately records the missing semantic declaration and entry relationship.
Ordinary and bounded readers retain the same entry inventory and empty host path.
Bind, indexer, render, Container-chain and nested-template mutations must emit
`UnsupportedExpressionOrScope` at Tier4Unknown in the affected markup file.

The pinned clean public checkouts reproduce Kaliko's 107 files, 619 syntax facts
and 7 containment entries, and CsrFence's 21 files, 189 syntax facts and zero entry
relationships. Only the trusted static diagnostic ran; neither application was
built or executed. The fresh-base full Debug suite passed with warnings treated
as errors: 4,450 passed, zero failed and two skipped in 30 minutes 35 seconds.
The skips are the opt-in full generated golden sweep and the Windows ASP.NET
publish case. This validates the final code in this slice, not PR merge readiness.

The fresh CLI smoke scan of `samples/backend-extraction` with its explicitly
supplied compiled DLL completed with build status Succeeded and 248 facts:
21 ManagedIlCallObserved, 4 HttpCallDetected and 2 DatabaseOperationCandidate.
Unbound-input, unresolved-reference and IL modeling gaps remain visible. These
are retained static observations, not runtime execution or complete coverage.

## Comparison baseline

The final PR 872 head `8323dbcfd07df6d1fa8d829782d3a9ce1575651e` and merge
`25c4f6475db9b92b3f08dafafd5117a554e3020a` have identical trees. The comparison
target was dev `50c2c29c6598144ce8592e1fb905f7e8589290ea`. At that snapshot,
main `2b1d042a35444141833d4e912df743af1a398504` had identical production code;
dev was six commits ahead with test and validation differences.

The unchanged backend-extraction sample was compiled once. Both tools consumed
the same source directory and DLL, with IL body evidence enabled. Source and
DLL SHA-256 values were respectively
`52dc3828e57edd0a8925f45fb41281178111e6e5f3a0327f942ec6d398df9262` and
`fcaf151e1824f32d22c9af3985f757d95bf55fadb8ac2ab53a86206950829a5d`.
These identify the local comparison inputs, not a reproducible-build claim.

Facts increased from 247 to 248 with no missing baseline fact anchors. The added
fact was ManagedLateBindingCensus. Existing evidence gained assembly public-key
token fields. Extractor and generator identities changed as expected.

Ordinary and bounded reports retained the same counts: SaveClick 6, JobTick 6,
UnconnectedQuery 8, RefreshClick 2, DynamicDestination 2, and NameOnlyDecoy 0.
The HTTP reports retained their cycle/truncation warnings. Counts describe path
variants, not business operations or exhaustive runtime paths.

The same current HTTP entry fixture scanned by both tools yielded 163 versus 184
facts and 0 versus 13 entry relationships: 6 containment, 4 property references,
3 lifecycle candidates. Composition gaps and diagnostics also changed; these
counts alone are not a claim that every fact was simply additive. Existing
positive and negative call-path outcomes stayed unchanged.

## Confirmed reporting defect

The graph selected a symbol's first fact in generated fact-ID order. Changing
scan identities could therefore change its displayed rule, line or tier without
changing the retained source evidence. Reconciliation-edge locations could
inherit that instability. The single-index bounded reader additionally discarded
redundant symbol witnesses in fact-ID order before graph construction.

The regression changes IDs, scan IDs and input order while preserving evidence.
It failed on both ordinary and bounded readers before this change. Algorithm
1.10 orders representatives by evidence content, preferring declarations, then
path facts, then metadata-only observations; within each class it orders by fact
type, rule, recorded tier, path, span and symbols. IDs are
only a tie-breaker after the displayed evidence is equivalent. This does not
select the highest tier, synthesize a declaration or merge provenance across
source indexes. A weaker declaration stays weak.

The ordinary reader, SQLite-backed graph and bounded single-index compaction
use the same ordering key. The disk-backed graph indexes the key rather than
sorting all facts in managed memory. Existing input/storage limits still apply.
Algorithm version changed from 1.9 to 1.10; extractor versions did not change.

The backend sample test compares complete path objects and backend inventories
between ordinary and bounded combined readers, including supporting IDs,
locations, tiers and classifications. The permutation test covers ordinary,
bounded-single and bounded-combined readers. A dispatch-gap location assertion
now names the stable call-site witness rather than a hash-selected implementation.

Re-querying both saved extraction indexes with the corrected reporter preserved
all six selectors' path counts. Comparison retained representative rules, tiers,
spans, route topology, terminal metadata and normalized supporting-fact identities.
The remaining differences were scan/fact/source IDs, unordered support-ID ordering,
opaque unknown-SQL/persistence labels, and generator/bounded-input provenance
hashes. These were normalized for comparison only; the reports retain the original
values. No evidence-tier or location difference remained in these sample routes.

## HTTP coverage and next decisions

The new C# inheritance fixture records a direct framework override witness but
no semantic method declaration for its lifecycle join. Ordinary property
declarations are also syntax-tier. Thus C# entry parity is not complete even
though source calls and override relationships are retained. This is distinct
from ECMA IL extraction and should not be hidden by reporting changes.
Running the PR 872 baseline tool against the same new fixture also produced zero
semantic method/property declarations. This limitation predates the recent ECMA
work; the fixture does not demonstrate an extraction regression since that baseline.

A separate extraction-side selection issue was observed in this fixture:
`WebFormsAssemblyRegistrationResolver.Create` groups equivalent type candidates
and chooses the first TypeFactId. The direct control selected a syntax type fact
even though its semantic type fact was retained. Entry receiver checking then
reported ConflictingControlReceiver. The inherited control reached the separate
MissingOrAmbiguousProperty boundary. This hash-based registration selection is
not changed by the reporting fix; it needs its own exact-owner/assembly regression
and extractor-version decision during triage.

The existing VB mutation matrix now also tests Bind, indexed access, render
expressions, Container chains and nested templates. These remain explicit gaps.

Next implementation candidates, after triage:

1. Stabilize registration type-witness selection within a proven declaration
   identity, then add semantic C# method/property declaration parity, with exact
   owner and assembly identities and negative same-name/ambiguous cases.
2. Investigate inherited getter ownership and source-intermediary lifecycle
   overrides. Require bounded, unambiguous base chains; keep runtime activation
   unproven and do not fabricate calls.
3. Model naming-container/template instance scope before accepting expressions
   in those scopes. Keep Eval/Bind/indexers/chains unavailable until a bounded
   expression model is explicitly specified.
4. Any mixed entry explanation belongs in a separate candidate report inventory,
   not source-call traversal or proof of runtime dispatch.

Reuse the pinned Kaliko and CsrFence repositories from
`WEBFORMS_HTTP_ENTRY_PUBLIC_CORPUS.md` for syntax-only realism checks. Build only
the trusted diagnostic, never downloaded project files or scripts.

## Validation

The pre-fix representative test failed twice as expected. The final focused run
passed 164 tests with warnings treated as errors, including the report-memory
suite. Fresh clean clones of the pinned
public corpus reproduced Kaliko's 107 files, 619 syntax facts and 7 containment
entries, and CsrFence's 21 files, 189 syntax facts and 0 entry relationships.
Neither public application was built or executed.

A full Debug run before the final correction completed with 4,398 passed,
2 skipped and 1 failed in 27 minutes 39 seconds. The failing repetitive-input
test exposed argument-only metadata replacing a call witness, not a memory
budget failure. Representative priority now places path facts ahead of those
observations. The final 164-test run includes that test and passes without
relaxing its assertions. The subsequent fresh-dev full run described above passes
with this correction and the strengthened HTTP assertions. The earlier failed
run is retained here as defect history, not the current validation outcome.

Focused command:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --filter 'FullyQualifiedName~WebFormsReportMemoryTests|FullyQualifiedName~CombinedDependencyPathTests|FullyQualifiedName~BackendExtractionSampleTests|FullyQualifiedName~HttpEntryBoundaryRegressionTests'
```

Shared reporting changes require the full Debug suite with `-warnaserror` before
merge. No private work-machine artifacts or source are needed for these tests.
