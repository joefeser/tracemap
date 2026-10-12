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

Final validation is in progress. net48/other-framework overload parity, pinned OSS
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
