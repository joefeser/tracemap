# Claude takeover prompt: unresolved handler-to-database report

Take over this debugging task on `codex/webforms-directory-publication-maps`. Read `AGENTS.md` first. Inspect current HEAD and preserve unrelated changes. Implement and verify the actual fix locally before asking the owner to test again. The owner has spent weeks and many repeated export/rerun cycles on this; batch the investigation and regression coverage instead of shipping another diagnostic-only microchange.

## The unresolved problem

The owner's normal-looking `compiled-paths.local.html` reports **0 exact chains and 0 retained variants**, despite earlier unfiltered method-graph evidence retaining a database command witness. **The actual zero-chain result is not confirmed fixed.** Local tests passing do not establish acceptance against the owner's input.

The latest screenshot showed thousands of retained gaps but did not establish the exact query mode, root identity, input index, or receipt for that zero-result file. Do not infer those from the filename alone.

There are two separate problems to distinguish:

1. Graph-only queries deliberately leave normal `Paths` empty. Older code nevertheless published a normal-looking zero-chain companion report. This misleading-output bug is fixed in `ad322adf`, but we did **not** establish that it caused the owner's photographed result.
2. A genuine normal query might still produce no retained database routes. Its cause remains unproven. Investigate root admission, terminal selection, traversal restrictions/budgets, classification, and projection rather than declaring the first problem explains the second.

## Recent changes and evidence boundaries

### `8c3c5db3644f7b2f1d9a68b47f393f4530e206f9`

Retains admitted but unknown method returns as `symbolic-method-return` SQL inputs. `CompiledSymbolicMethodInput` binds method, producer call, body, and return fact identities. Connection gaps remain separate from `ValueGaps` such as `IlCommandReturnOriginUnknown` and `IlCommandReturnValuesDisagree`. Admission still requires the existing exact nonvirtual call, unique method/body, return-summary, and work checks. Parent hashes include the new input, and the share exporter aliases identities.

The owner's subsequent aliased export confirmed, for **one retained shortest witness**:

- SQL concatenation and both argument alternatives were retained.
- Both alternatives referenced a symbolic method-return input.
- Four producer calls were present; `missingProducerRecords=0`.
- Command-trace connection gaps and operand-check failures were empty recursively.
- Runtime value uncertainty remained explicit.
- `focusRecordsComplete=true`, `sliceLimited=true`, and `sourceHadCutoffs=false` did not prove all-route completeness.
- The exact-IL-target-without-source-commit-binding caveat remained. Export aliases are local to each export, not global identities.

### `9819ab26069a10a2f784802c4fb83367310018a4`

Adds command expressions, command type evidence, named symbolic inputs, and separate connection/value uncertainty to normal grouped HTML reports. It preserves variant associations, escapes HTML, bounds recursive display, and does not reconstruct SQL literals. The JSON handoff remains lossless. This changes presentation; it cannot create missing paths.

### `ad322adff53745880ef4e50acd9237c2791bb0da`

Graph-only native queries now publish only method-graph artifacts, not empty grouped path artifacts. Normal queries publish grouped path artifacts. Additive receipt fields `PrimaryReport` and `PathEnumerationPerformed` are hash-bound. The operator script identifies the appropriate report. A real empty normal report warns that no retained route is not evidence of no database dependencies.

Old output directories were preserved; they can still contain misleading companion reports. The owner's run against this commit has not been confirmed.

## Code to inspect

- `src/dotnet/TraceMap.Cli/WebFormsReviewExecutionCommand.HandlerRequery.cs`: exact root admission, query options, native receipts, and output-mode selection. Normal mode sets `ToSurface` to `database-api`; method-graph mode does not. Requery does not scan or combine.
- `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.cs`: `BuildReportWithTraversalObservations`, `ResolveStartNodes`, `ResolveTerminalNodes`, and normal `Search`. The method-graph observer branch intentionally does not enumerate normal paths.
- `src/dotnet/TraceMap.Reporting/RetainedMethodGraph.cs`: unfiltered retained graph, unique-node traversal, and bounded shortest command witnesses. A witness is diagnostic static evidence, not proof of feasible dispatch or all normal routes.
- `src/dotnet/TraceMap.Reporting/CombinedDependencyPaths.CommandReturns.cs` and `CombinedDependencyPaths.CompiledCommandValues.cs`: return admission and symbolic command inputs.
- `src/dotnet/TraceMap.Reporting/GroupedCompiledPathReportWriter.cs` and `GroupedCompiledPathHandoff.cs`: grouping, lossless variant references, provenance, output bounds, and HTML.
- `scripts/wrequery.ps1`, `scripts/wgraph-share.ps1`: operator mode selection and privacy projection.

Check current locations and symbols rather than relying on stale line numbers.

## Required investigation

1. Establish whether the zero report was from a graph-only or normal query using its native receipt/handoff, if available locally. Bind the investigation to exact source/scan/commit/symbol root identity, index hash, generator, query options, and depth/path/frontier/work budgets. New receipts expose `PathEnumerationPerformed`; inspect old query metadata when that field is absent.
2. If that private artifact is unavailable, say the cause is unconfirmed. Build a synthetic reproduction that includes a valid publication/source-to-compiled bridge, not merely similarly named methods.
3. Compare unfiltered graph and normal traversal on the **same exact source handler and index**. Trace the first divergence: root admission, database-terminal admission, reverse reachability pruning, dispatch restrictions, cycles, limits, path classification, or report projection.
4. Add a regression that fails for the proven defect before changing implementation. Fix the underlying issue without relaxing evidence admission. Cover adjacent supported cases in one batch.
5. Verify the public native operator workflow, not only an internal reporter call. Inspect generated receipt, handoff counts, and HTML together.

Important traversal constraints: incomplete reverse-terminal indexes must never prune; dispatch cross-hop rejection must remain; reconciliation back-edges are not application recursion; compiled-only root attachment is scoped; normal limits and partial-analysis labels remain meaningful. Do not turn every unfiltered edge sequence into an admitted normal route.

## Existing synthetic fixture and a known false lead

Relevant fixture projects live under `samples/fixture-build/lazy-constructor`, with source in `samples/messy-dotnet-workspace/vb-lazy-constructor` and `vb-lazy-logging-provider`.

They model conditional argument prefixing, cache early returns, HTTP-shaped code, LINQ, Catch/Finally, nested SQL wrappers, concatenation, getter chains, session/cache identities, impersonation, unknown values, virtual negatives, and bounds. Do not execute their database or HTTP operations.

The current positive operator tests use a **compiled method root**. A source-root experiment produced zero normal paths and zero graph command traces because that fixture lacked a publication/PDB identity receipt binding the source root to the compiled method. That is an expected negative case, not a demonstrated traversal defect. Do not add a name-only bridge to make it pass. Extend the fixture with legitimate mapping evidence to test the actual source-handler workflow.

## Tests and validation

Inspect these test classes:

- `LazyConstructorLoggingTests`: structured profile argument alternatives, symbolic returns, compiled-root normal route, and unmapped source-root negative.
- `GroupedCompiledPathHandoffTests`: lossless variants, display/privacy bounds, escaping, provenance, and empty-report warning.
- `WebFormsOperatorWorkflowTests`: public CLI attached/separate/missing-provider cases, currently rooted in compiled methods.
- `WebFormsReviewExecutionTests`: native recovered outputs, mode-specific artifacts, receipts, and tampering rejection.
- `CombinedDependencyPathTests`, `IlCommandBindingExtractorTests`, and authentic Windows publication cases in `MessyWorkspaceRegressionTests`.

The latest bounded run at `ad322adf` passed 84 tests with no skips/failures:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter 'FullyQualifiedName~Report_recovery_reuses_consistent_failed_outputs|FullyQualifiedName~LazyConstructorLoggingTests|FullyQualifiedName~GroupedCompiledPathHandoffTests|FullyQualifiedName~WebFormsOperatorWorkflowTests'
pwsh -NoProfile -File scripts/tests/Test-WebFormsWizardRequery.ps1
```

The script test and a 672-fact sample CLI scan also passed. This is **not** a claim that the full suite passed at current HEAD or that the private failure is resolved. Prior broader local gates included Windows-only skips; do not count those as authentic Windows publication validation.

Additional relevant checks:

```sh
pwsh -NoProfile -File scripts/tests/Test-WebFormsGraphShare.ps1
pwsh -NoProfile -File scripts/tests/Test-WebFormsGraphRefresh.ps1
pwsh -NoProfile -File scripts/wlocal.ps1 -OutputRoot /tmp/tracemap-claude-local-validation
```

Use a fresh output root. Never modify source or rebuild tool assemblies while a provenance-sensitive workflow/test sweep is running: doing so previously caused self-inflicted preflight hash failures. Full-suite runs have taken roughly 20–24 minutes; collect actual completion and failures, not assumptions based on elapsed time. Follow `docs/VALIDATION.md` and Homebrew discovery instructions for missing local tools.

## Acceptance criteria

- A synthetic, legitimately mapped **source handler** reaches the supported database endpoint through both graph diagnostics and normal native requery using the same index/root.
- The normal receipt/handoff/HTML agree on nonzero retained chains and variants for that supported case.
- The command expression retains named symbolic runtime inputs and separates value uncertainty from missing connection evidence.
- Missing/mismatched mapping, ambiguous targets, unproven virtual dispatch, and exhausted budgets still produce appropriate gaps rather than invented connections.
- Graph-only mode never publishes a fake zero-chain normal report.
- Regression tests and a sample CLI workflow pass, with exact commands and skips reported.
- Private end-to-end acceptance remains explicitly pending unless actually observed. A local synthetic pass alone must not be called a complete fix.

## Privacy and delivery

Private screenshots, source, graph exports, SQL literals, paths, identities, and configuration must stay out of commits and public comments. Use them locally only if available and authorized; commit synthetic fixtures. Do not request private alias maps for upload. Preserve evidence tiers, rule IDs, limitations, and artifact provenance requirements from `AGENTS.md`.

The owner wants a tested fix on this existing branch, not another sequence of tiny diagnostic patches. Do not merge or force-push. Finish with a concise account of the proven cause, change, exact commit, validation, and any genuinely unverified acceptance. If blocked on unavailable evidence, identify that precise boundary instead of guessing or claiming success.
