# Implementation state

Branch: `codex/backend-extraction-inventory`, based on `origin/dev` 0d3107cc.
Issue: #861. Phase one is a retained-query projection, not full-index discovery.

New grouped handoffs carry optional `backendExtraction`; legacy handoffs without
it remain readable. The builder derives it from indexed full node records and
ordered variants. Restore verifies a supplied projection against those records.
Rows are evidence records, not unique business operations; callers are query
roots, not asserted UI events. Candidate tiers remain explicit. Inventory-only
records remain found-not-connected. Human decisions remain undecided.

Constructed-generic edges now carry limitations from a deterministic rule-owned
mapping. Newly generated grouped/method-graph JSON carries those edge fields;
HTML also explains them. Existing artifacts are not rewritten.

No changes to ACK, lane policy, raw SQL defaults or source-sharing authority.
Base44 stash and primary dev checkout remain untouched.

PR #862 review corrections: preserve candidate path/edge classifications and
mixed-variant state; expose querySurface and explicit HTTP-coverage exclusion
for database-filtered producers; materialize legacy rule limitations in both
method-graph JSON and HTML while hashing the original bounded input. HTTP
extractor expansion remains in its separate lane.

Validation so far:
- 222 focused generic/framework/two-hop/grouped/lazy-constructor tests passed
  with warnings as errors.
- 30 grouped-projection and compiler-built DB/HTTP sample tests passed.
- Graph-share, wizard-requery and SQL-route PowerShell suites passed.
- Initial synthetic semantic+IL CLI smoke: 243 facts, Level1SemanticAnalysis;
  revealed absent GetStringAsync HTTP surface. Positive control now uses GetAsync,
  with native scan/report regression coverage. Convenience HTTP coverage logged.
- 229 broader path/native execution regression tests passed (4m15s).
- Full suite and authentic Windows/private-site validation not run for this branch.

Commands (from the worktree root):
```
dotnet test src/dotnet/TraceMap.sln --filter 'FullyQualifiedName~GroupedCompiledPathHandoffTests|FullyQualifiedName~ConstructedGeneric|FullyQualifiedName~FrameworkSignatureCandidateTests|FullyQualifiedName~LazyConstructorLoggingTests|FullyQualifiedName~CompiledTwoHopCutoffTests' --verbosity quiet -warnaserror
dotnet test src/dotnet/TraceMap.sln --filter 'FullyQualifiedName~BackendExtractionSampleTests|FullyQualifiedName~GroupedCompiledPathHandoffTests' --verbosity quiet -warnaserror
dotnet test src/dotnet/TraceMap.sln --no-build --no-restore --filter 'FullyQualifiedName~WebFormsReviewExecutionTests|FullyQualifiedName~CombinedDependencyPathTests' --verbosity quiet
pwsh -NoProfile -File scripts/tests/Test-WebFormsGraphShare.ps1
pwsh -NoProfile -File scripts/tests/Test-WebFormsWizardRequery.ps1
pwsh -NoProfile -File scripts/tests/Test-WebFormsSqlRoute.ps1
dotnet build samples/backend-extraction/BackendExtraction.Sample.csproj --verbosity quiet
```

## Isolated outbound HTTP follow-up

Branch `codex/outbound-http-coverage-861`, in a separate isolated worktree.
Dependency/base: inventory `d55eb44b3d4781d2adeffa11a6d42a8ca90e7ea5` atop dev
`0d3107cc4e7edb9fca20928f43e6604a4ff2b992`. The backend-extraction and debugging
lanes were not modified. Source API classification is shared across C#/VB and
requires exact signed framework identities and resolved parameter shapes. JSON
content reads are excluded; inline trusted HttpRequestMessage constructors can
retain standard HttpMethod properties, while request variables stay UNKNOWN.
WebClient constant standard explicit verbs are retained; default/dynamic/custom
verbs remain UNKNOWN. WebRequest response verbs and mutable request state remain
unresolved. WebClient protocol is never inferred from API recognition.

Public net10.0 fixtures enumerate 166 overloads per language with literal String/
Uri and HttpRequestMessage forms, cancellation/completion/options/context/typeinfo,
event/user-token/task forms and controls. Every overload is checked through
semantic facts, SQLite, retained query handoff and HTML. Projectless and compiled
paths have independent assertions; compiled-only retains original IL calls, without
HTTP operation classification. New optional HttpCallEvidence carries only bounded
privacy-safe API metadata through surface/path/grouped reports. Existing generator/
bounded-input hashes bind those reports. No new standalone machine-readable matrix.

Implementation validation completed at `4d52ca6a1c862622d69f95e975ffd650f9f6bae4`. net48/other-framework overload parity, pinned OSS
smokes, non-.NET adapters, private Windows/site and runtime behavior are deferred.
Full-index discovery, private readable contract capture and owner decision receipts
remain open. The convenience gap recorded above describes the first delivery,
not this follow-up implementation.

Compatibility: existing backendExtraction limitation text is preserved because it
participates in deterministic reconstruction. New handoffs add optional validated
HttpCoverageLimitations; older handoffs may omit it. Nullable HttpCallEvidence is
omitted when absent, preserving old node hashes. Altered coverage metadata is
rejected. Report projection admits only bounded metadata values, never arbitrary
property strings or new private destination hashes.


Validation receipt for the implementation commit:
- Warning-as-error solution build succeeded with zero warnings/errors.
- 82 focused API, integration, VB external-boundary, retained-handoff, projection,
  sample and Razor version regressions passed.
- Full solution suite passed: 3,900 passed, one gated Windows ASP.NET corpus skip,
  zero failures (21m02s). Binaries remained unchanged during this accepted run;
  earlier interrupted/invalidated exploratory runs are not acceptance evidence.
- Twelve native CLI outputs (both languages x three modes x two repeats) validated
  with scripts/validate-adapter-artifacts.py. Facts and report.md were byte-identical
  within each pair. Semantic outputs retain 171 Tier1 HTTP callsites per language
  (166 overloads + five controls); C# projectless retains 176 unresolved Tier3
  candidates; VB projectless and both compiled-only outputs classify no HTTP calls.
  All 166 compiled Case methods retain original IL call observations per language.
- Seven artifact-validator tests passed; whitespace checks passed.
- GitHub Private Path Guard passed on the implementation commit.
- No fixture method was executed; no application HTTP/DB calls or private artifacts.

The implementation branch is pushed for testing. The later inventory/debugging
commit `022e1c722195033e1975226daee5e76f894115af` advanced independently after
isolation and was not integrated. The HTTP branch depends on recorded inventory
`d55eb44b`; integration with the newer reporting changes needs its own validation.
No merge, ACK, lane-policy change or whole-site completeness claim.

The follow-up documentation-only commit records this receipt. Rebuild and rerun
the 82 focused tests plus twelve CLI outputs on that final head; no repeat of the
full suite is required for documentation-only changes. The full-suite claim is
explicitly bound to the implementation commit above, not to a later commit.

## Legacy reference compatibility extension

Current origin/dev `278bb821fe41aca8221d2193d15abaa7b92c6e64` (inventory #862)
merged cleanly into this isolated branch in merge commit
`922fc0b182e9d39d9e41f4971ecef625af3434a6`. The historical no-integration
receipt above remains bound to its original head; this extension supersedes that
integration gap. No dev checkout/debugging lane was modified.

Public reference-built C#/VB net20/net35/net40 fixtures each enumerate 57 overloads;
net45/net48 enumerate 116 each, alongside 166 net10.0 overloads. All ten legacy
projects compile on macOS with zero warnings/errors, and ten legacy tests passed
(39s): every overload through semantic facts/SQLite/handoff/HTML, independently
decoded emitted IL signatures, signed assembly identities, TFM metadata and separate
projectless/compiled-only limits. System 2.0.0.0 is shared by net20 and net35 and
does not establish application TFM; missing binary TFM remains unknown. C# legacy
SendAsync may emit a HttpMessageInvoker base-slot reference, retained as emitted.
Trusted framework checks have not changed.

Windows public compatibility CI is configured; observed run/head and broader
local regression/native CLI receipt are pending. Admitted application framework
evidence has not been identified; a path/admission reference was requested. This
application-specific prerequisite remains open, with no inferred TFM/private
application compatibility claim. See the legacy compatibility matrix for details.
