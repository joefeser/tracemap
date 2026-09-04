# Restricted Web Forms run: diagnostic follow-up

## Current handoff: verify projection ownership in the retained index

The work-machine run already used `963392f4` / `legacy-webforms/0.7.1` and
reported zero workspace diagnostics. **Do not rerun PowerShell for this step.**
Give Claude
[`claude-projection-ownership-verification.prompt.md`](claude-projection-ownership-verification.prompt.md).
It checks the same selected event in the existing index, joining the projection
through the handler identity and exact supporting fact IDs. The last report's
missing-projection claim does not yet verify removal of the unrelated edges.

## Previous handoff: handler ownership correction (0.7.1)

The one-page review exposed unrelated same-name handler edges in projected
support. Version `legacy-webforms/0.7.1` requires the resolved handler file/span
and, for semantic facts, its canonical source symbol ID. Logic signals use the
same admission. Linked syntax evidence remains lower-tier, not assembly proof.

After pulling, rerun the existing focused PowerShell scan command once; old
indexes cannot acquire this correction by rerunning summaries. Confirm the new
index reports `legacy-webforms/0.7.1`, then give Claude
`claude-single-page-trace.prompt.md` for the **same selected event**. Compare
supporting edges and reject unrelated owners. An HTTP terminal is not proof of
no database effects in untraversed branches. No debugger or BRD is needed.

## Previous handoff: one-page static trace

After the `legacy-webforms/0.7.0` run, give Claude
[`claude-single-page-trace.prompt.md`](claude-single-page-trace.prompt.md).
It uses the existing index and matching local source to trace one explicit page
event through its handler and bounded call edges toward a database boundary.
It checks exact identity/support, preserves lower evidence tiers, and reports
the first missing hop rather than inventing a chain. No scan, rebuild, debugger,
database access or BRD is needed. This is the current task; the coverage prompts
below describe earlier or separate investigations.

## Latest step: retained coverage triage (2026-09-03)

### Follow-up from bounded samples

The local review confirmed the COM fallback was active (one COM-skip gap, no
workspace callbacks or fallback-unavailable gaps). Five samples per gap family
identified DLL-only references, case-mismatched markup type names, client/event
value limitations, and positive/compound postback conditions. These samples do
not establish the distribution of all retained gaps.

`legacy-webforms/0.7.0` adds case-insensitive matching **only for the markup type
name** within an exact namespace and scoped assembly. Case-only collisions stay
ambiguous and DLL-only controls remain unresolved. Positive `IsPostBack` and
`this.IsPostBack` conditions now have distinct Tier3 branch candidates; compound
conditions and boolean comparisons remain gaps. OnClient attributes now produce
`ClientWebFormsEventAttribute`; non-identifier event values produce
`NonIdentifierWebFormsEventValue`, with no inferred execution language or server
handler. No runtime-binding or branch-execution claim is added.

After pulling this change, use the same existing focused scan command when
ready to validate on the work machine. No debugger, dependency reinstall, or
scope expansion is needed. Verify the retained extractor is
`legacy-webforms/0.7.0`, then compare rule-specific gaps, not just total gaps:
event gaps were split into clearer categories rather than removed. The existing
triage prompt below can inspect the new retained index; no private source needs
to leave that machine.

The post-COM-fallback field run at `ad8fdd98` reported zero workspace diagnostics
and 932,070 Tier1 facts. It still correctly reports reduced coverage; these
counts do not establish full compilation or successful COM binding.

For this run, give Claude **only**
[`claude-retained-coverage-triage.prompt.md`](claude-retained-coverage-triage.prompt.md).
It executes bounded read-only queries and local source inspection using existing
artifacts, returning sanitized categories for control registrations, event
attributes and `IsPostBack` conditions. No debugger, scan, rebuild, or BRD is
needed. This supersedes the older prompt selection instructions below for runs
where the COM fallback fix is present and workspace failures are zero.

`Export-FocusedWebFormsAccuracySummary.ps1` now bases workspace-repair priority
on non-informational diagnostics under the workspace-diagnostic rule, not static
legacy framework/project markers. Uncategorized workspace failures request
classification; COM task-host failures receive specific task-host guidance.
Existing summary files are immutable observations of the old report logic and
are not rewritten by pulling this branch. The new prompt can inspect them as-is.

## Earlier investigation history

This directory records the bounded-report implementation and the sanitized
2026-09-02 restricted Web Forms field observation. The private repository,
source, raw scan artifacts, screenshots, local absolute paths, and native
MSBuild/Roslyn messages must remain on the operator's machine.

The field metrics and the operator-supplied post-run readback are recorded in
[`restricted-run-2026-09-02.md`](restricted-run-2026-09-02.md). This README
defines the next diagnostic step. It does not claim a product root cause, full
semantic compilation, complete event-to-database chains, or runtime behavior.

For a computer that does not share this machine's filesystem or chat history,
pull this branch and give the on-device reviewer
[`claude-diagnostic-review.prompt.md`](claude-diagnostic-review.prompt.md). The
prompt is self-contained and uses only repository-relative instructions.

After rerunning with projection-boundary commit `08ec7348`, or a descendant, use
[`claude-post-fix-lineage-review.prompt.md`](claude-post-fix-lineage-review.prompt.md)
instead. It verifies the TraceMap head and extractor versions before reading the
new closed origin fields. Do not use the older diagnostic-review prompt for a
post-fix index.

To let an on-device coding agent reproduce and repair the TraceMap lineage
defect without repeated instructions, use
[`claude-workspace-self-help.prompt.md`](claude-workspace-self-help.prompt.md).
It authorizes a synthetic implementation and validation loop but not a push,
pull request, merge, BRD, or private application change.

## Current conclusion

`LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset` is not a
native MSBuild error or a reproduction fingerprint. It is sanitized TraceMap
guidance.

The current implementation can produce that result through this sequence:

1. Roslyn emits an ordinary `CompilationDiagnostic`.
2. Unless the diagnostic matches the bounded missing-reference classifier,
   `SanitizeWorkspaceGap` assigns `UncategorizedWorkspaceFailure`.
3. `CorroborateLegacyWorkspaceFailures` sees static legacy-project markers and
   relabels the diagnostic `LegacyWorkspacePrerequisitesUnresolved`.
4. The relabeled diagnostic receives `UseCompatibleMSBuildToolset` guidance.

Consequently, the reported 10,588 occurrences may include ordinary compiler
diagnostics. The count must not be described as 10,588 proven workspace or
toolset failures. The successful retention of substantial Tier1 evidence is
also consistent with at least some compilations being created despite errors.

A subsequent on-device execution of the count-only queries found only two
retained `csharp.semantic.workspace.v1` `AnalysisGap` rows: one
`WorkspaceDiagnostic` and one `ScanScopeExcludedSources`. It found zero
retained `CompilationDiagnostic` rows. The separate environment projection
still contained 10,588 `LegacyWorkspacePrerequisitesUnresolved` rows and one
`UncategorizedWorkspaceFailure` row. Therefore neither the compiler-error
hypothesis nor 10,588 genuine load failures is proven. The decisive defect is
that the 10,588 projected rows have no retained origin lineage in this index.

Relevant implementation points:

- `src/dotnet/TraceMap.Core/CSharpSemanticExtractor.cs`:
  `AddCompilationDiagnostics` emits `CompilationDiagnostic` gaps and retains
  the safe compiler diagnostic ID.
- `src/dotnet/TraceMap.Core/BuildEnvironmentDiagnosticExtractor.cs`:
  `SanitizeWorkspaceGap` applies the generic workspace category and
  `CorroborateLegacyWorkspaceFailures` performs the legacy relabeling.
- `scripts/Export-FocusedWebFormsWorkspaceSummary.ps1` groups the resulting
  `BuildEnvironmentDiagnostic` rows; it cannot recover the original gap kind
  after that projection.

Slash direction is not implicated by this evidence. Windows and .NET generally
normalize `/` and `\` for filesystem paths, and the focused summary scripts
normalize scope prefixes to `/`. Existence and solution-relative resolution are
the relevant path checks.

## Determine what the retained run actually contains

Do not rerun the large scan first. Query the retained `scan/index.sqlite`
locally. The query below returns only scanner-owned categories, compiler IDs,
and counts. It does not select paths, source, symbols, messages, or business
data.

```sql
SELECT
  json_extract(properties_json, '$.gapKind') AS gap_kind,
  COALESCE(json_extract(properties_json, '$.diagnosticId'), '-') AS diagnostic_id,
  json_extract(properties_json, '$.diagnosticCode') AS diagnostic_code,
  COUNT(*) AS count
FROM facts
WHERE fact_type = 'AnalysisGap'
  AND rule_id = 'csharp.semantic.workspace.v1'
GROUP BY gap_kind, diagnostic_id, diagnostic_code
ORDER BY count DESC, gap_kind, diagnostic_id;
```

Interpret the rows by origin:

- `CompilationDiagnostic` means Roslyn created a compilation and reported a
  compiler error. It is not itself proof of workspace admission failure.
- `WorkspaceDiagnostic`, `ProjectLoadFailed`, and `SolutionLoadFailed` are the
  relevant native workspace/load categories.
- `CompilationCreateFailed` and `CompilationMissing` mean compilation creation
  did not complete for the affected project.
- `MSBuildRegistrationFailed` means TraceMap could not register an MSBuild
  instance.

Compare those counts with this bounded projection of the generated environment
diagnostics:

```sql
SELECT
  json_extract(properties_json, '$.diagnosticCode') AS diagnostic_code,
  json_extract(properties_json, '$.guidanceCode') AS guidance_code,
  COUNT(*) AS count
FROM facts
WHERE fact_type = 'BuildEnvironmentDiagnostic'
  AND rule_id = 'build.environment.workspace-diagnostic.v1'
GROUP BY diagnostic_code, guidance_code
ORDER BY count DESC, diagnostic_code;
```

For the retained field index, these queries were indeterminate: the first query
could account for only one genuine workspace callback, while the second
contained 10,588 additional projected rows. The next implementation must
preserve origin lineage before another retained index can answer the question.
The single workspace callback requires the local-only inspection described
below if its exact native category is needed before that rerun.

## Exact prompt for the on-device reviewer

```text
Do not write a BRD, modify application code, or rerun the TraceMap scan.

Analyze only the latest retained TraceMap scan/index.sqlite on this machine.
Do not output paths, source, symbols, project names, native diagnostic messages,
configuration values, connection information, or business data.

Run count-only queries over csharp.semantic.workspace.v1 AnalysisGap facts,
grouped by:
- properties.gapKind
- properties.diagnosticId
- properties.diagnosticCode

Separately group build.environment.workspace-diagnostic.v1 facts by:
- properties.diagnosticCode
- properties.guidanceCode

Report only these tables and answer:
1. How many rows originated as CompilationDiagnostic?
2. How many originated as WorkspaceDiagnostic, ProjectLoadFailed,
   SolutionLoadFailed, CompilationCreateFailed, CompilationMissing, or
   MSBuildRegistrationFailed?
3. What are the highest-count compiler diagnostic IDs?
4. Does the 10,588 LegacyWorkspacePrerequisitesUnresolved count primarily
   represent compiler diagnostics or genuine workspace/load failures?

Do not infer that UseCompatibleMSBuildToolset is the root cause. It is currently
conservative TraceMap guidance. Do not write migration requirements or BRDs.
```

## Local-only inspection when a genuine load failure remains

The shareable TraceMap artifacts intentionally omit native MSBuild/Roslyn text.
They cannot reveal the exact remaining workspace failure. The implementation
does not currently provide an explicit unsafe local-diagnostic option.

For a genuine `WorkspaceDiagnostic` or load failure, run TraceMap under a local
debugger and break in the `MSBuildWorkspace` failure callback in
`CSharpSemanticExtractor`. Inspect `args.Diagnostic.Kind` and
`args.Diagnostic.Message` on screen. For a thrown solution/project load error,
break where the corresponding exception is caught and inspect `ex.Message`.

Do not save the raw value into scan output, a repository file, a screenshot, a
chat, or a shareable log. Reduce it locally to only:

```text
diagnostic kind
MSB or CS diagnostic ID, when present
missing-component category
affected project ordinal
normalized-message count
```

An eventual product-facing diagnostic capture must be explicit, local-only,
outside the repository and scan output, clearly marked unsafe to share, and
excluded from all normal artifacts. Until that option exists, debugger
inspection is the narrowest path.

## Synthetic reproduction and expected fix boundary

The suspected classifier defect can be reproduced without private source:

1. Create an old-style, non-SDK .NET Framework 4.5 project with a legacy
   `ToolsVersion` or import so the static legacy-project diagnostics are present.
2. Make the project loadable by `MSBuildWorkspace`.
3. Add one ordinary compiler error, such as an unresolved identifier producing
   `CS0103`.
4. Run `CSharpSemanticExtractor` and materialize build-environment diagnostics.
5. Demonstrate that the `CompilationDiagnostic` is currently projected as
   `LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset`.

The focused correction should preserve the original gap kind and diagnostic ID
through classification, keep compiler errors distinct from workspace/load
failures, and bound repeated categories. It must retain syntax, markup,
configuration, SQL, Web Forms, and other independently proven evidence when
compilation is reduced.

No raw message is required to prove this classifier defect. Native diagnostic
inspection is necessary only to classify genuine workspace/load failures that
remain after compiler diagnostics are separated.

## Diagnostic-lineage correction on this branch

The same evidence branch now contains the prospective product correction so it
can be debugged against synthetic and restricted local reruns before the code is
cherry-picked onto a fresh implementation branch. This is not a merge-ready
branch and no private scan artifact is committed.

The corrected projection:

- classifies ordinary `CompilationDiagnostic` gaps as compiler diagnostics,
  retains their safe IDs on the originating `AnalysisGap`, and does not emit a
  duplicate `BuildEnvironmentDiagnostic` for them;
- projects a recognized reference-assembly compiler diagnostic only with
  `originCategory=compilation`;
- preserves `originCategory`, `originGapKind`, a strictly safe diagnostic ID,
  `occurrenceCount`, and `aggregationState` on projected environment facts;
- permits `LegacyWorkspacePrerequisitesUnresolved` corroboration only for
  `workspace`, `project-load`, or `solution-load` origins that share identity
  with static legacy-project evidence;
- aggregates exactly equivalent projected diagnostics deterministically; and
- reports missing lineage from older indexes as `unknown` with next action
  `rerun-with-diagnostic-lineage` rather than reconstructing a cause.

The closed origin categories are `compilation`, `workspace`, `project-load`,
`solution-load`, `compilation-creation`, `compilation-input`,
`msbuild-registration`, `restore`, `static-project-inspection`,
`generated-file-inspection`, and `unknown`. These categories describe scanner
provenance. They do not prove a build repair, runtime behavior, branch
reachability, or a compatible toolset.

## Restricted validation of the lineage correction

The 2026-09-02 post-fix restricted run verified TraceMap head `90309df6`,
`build-environment/0.4.0`, and `csharp-semantic/0.19.0`. It established:

- `LegacyWorkspacePrerequisitesUnresolved` fell from 10,588 occurrences to 0;
- two genuine `WorkspaceDiagnostic` callback occurrences remained;
- no project-load, solution-load, compilation-creation, compilation-input, or
  MSBuild-registration occurrence was retained; and
- 10,609 `PropertyMappingShapeUnsupported` plus 24
  `PropertyMappingTruncated` gaps were incorrectly projected as 10,633 unknown
  build-environment workspace failures.

The final item is a second TraceMap classifier defect, not a private-application
failure. `ReadWorkspaceDiagnostics` admitted arbitrary semantic gaps that lacked
a diagnostic kind. Projection-boundary commit `08ec7348` restricts admission to
the closed workspace/load/compilation/registration/restore gap kinds and bumps
the build-environment extractor to `0.5.0`. The original property-mapping gaps
remain as rule-backed analysis limitations; only their bogus environment
diagnostic duplicates are removed.

After rerunning this version, the expected environment summary is approximately
two genuine workspace callback occurrences, zero unknown-origin property-mapping
projections, and zero legacy-prerequisite occurrences. Exact classification of
the two native callbacks still requires the local-only debugger inspection
described above.

### Projection-boundary restricted rerun result

The subsequent restricted review verified TraceMap head
`a3de925b23a75ca78a779b93bfe6f215f7020116` as a descendant of projection fix
`08ec7348`. The retained index contained `build-environment/0.5.0` and
`csharp-semantic/0.19.0`. The safe aggregate result was:

- `workspaceDiagnosticCount=2` and
  `uncategorizedWorkspaceFailureCount=2`;
- `unknownDiagnosticOriginCount=0`;
- `legacyWorkspacePrerequisitesUnresolvedCount=0`;
- no compiler, project-load, solution-load, compilation-creation,
  compilation-input, or MSBuild-registration diagnostic occurrences; and
- one aggregated build-environment row with `originCategory=workspace`,
  `originGapKind=WorkspaceDiagnostic`, no safe diagnostic ID, guidance
  `ReviewEnvironmentGap`, and `occurrenceCount=2`.

No property-mapping gap was projected into build-environment diagnostics. The
independent `csharp.semantic.propertymapping-gap.v1` evidence remained with
10,604 `PropertyMappingShapeUnsupported` occurrences and 1,074
`PropertyMappingTruncated` occurrences. Those are bounded property-mapping
analysis limitations, not workspace failures.

This validates both classifier corrections against the restricted run. The
remaining outcome is
`result=lineage-fix-verified-genuine-workspace-failure-remains`. The sanitized
artifacts cannot classify the two native callbacks further; use the local-only
inspection boundary above before proposing another product correction.

For that local-only step, give the on-device reviewer
[`claude-local-workspace-callback-classification.prompt.md`](claude-local-workspace-callback-classification.prompt.md).
It limits the debug run to observing the two callbacks, forbids retaining their
native messages, and permits only a closed categorical result.

### Local callback result and COM-reference admission fallback

The on-device debugger inspection completed without retaining native callback
text. Both callbacks had kind `Failure`, no safe `CS####` or `MSB####`
identifier, the same `sdk-resolution` category, and aggregate occurrence count
2. A normal Visual Studio solution build succeeded independently. That
combination bounds the defect to TraceMap's workspace task-host path rather than
proving that the application solution cannot compile.

The observed category was narrowed further to COM-reference task-host
incompatibility. TraceMap now classifies that bounded native shape as
`MSBuildTaskHostIncompatible|UseCompatibleMSBuildTaskHost`. Before opening a
selected project that statically declares `COMReference` or `COMFileReference`,
TraceMap installs a temporary MSBuild targets override that omits only
`ResolveComReferences` and `ResolveComReferencesDesignTime`. This prevents a COM
tooling limitation from rejecting the rest of the project while preserving a
project-scoped `ComReferenceResolutionSkipped|ReviewComReferenceCoverage` gap.
COM-defined symbols can remain unresolved and must not be reported as semantic
facts without independent evidence.

The override contains no repository data and is deleted after workspace use. It
is not installed when any selected project defines
`CustomAfterMicrosoftCommonTargets`; TraceMap preserves that project extension
point and emits `ComReferenceResolutionFallbackUnavailable` instead. The
application repository and its normal Visual Studio build are never modified.
