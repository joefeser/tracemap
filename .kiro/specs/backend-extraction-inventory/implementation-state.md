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
