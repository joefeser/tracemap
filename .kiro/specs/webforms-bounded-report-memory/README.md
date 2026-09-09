# Restricted Web Forms run: diagnostic follow-up

## Current next command: actionable unresolved buckets

After pulling, run `./scripts/Summarize-FocusedWebFormsActionableGaps.ps1` from
PowerShell 7. With no arguments it reads the newest completed page-list packet
under `C:\work\tracemap-output`. It does not scan, build, traverse, or modify an
artifact. Output is limited to page aliases and aggregate public rule IDs,
evidence tiers, coverage labels, and linked gap classifications for three
actionable buckets; bounded traversal truncation is reported separately and
deferred. Source paths, symbols, SQL, raw diagnostics, and fact IDs are never
printed. Input is capped at 128 MiB and unexpected metadata values are withheld.

## Next work-machine command: retained page triage

For the next comparison, run `git pull`, then
`.\scripts\Compare-CompletedWebFormsPageTriage.ps1`.
It reads the newest completed depth-8/10 pair sequentially, checks identical
retained sources and selection, and withholds comparison on mismatch. No scan,
build, or traversal runs. Optional `-ComparisonDirectory` selects an older pair.
Per-page reason associations require exact `legacy-flow` gap scope ID matches
to retained `path-node` evidence IDs. They are not proven chain stopping causes:
shared nodes can associate with multiple pages, and unpublished visited nodes
cannot be linked. `not-established` means no such retained link, not no limit.
The additional node-evidence inspection budget is 200,000 entries per report.
Each depth reports its own terminal-free pages plus the two focus aliases.
The page triage also prints closed traversal `stopState` and `callEvidenceState`
counts already retained on each resolved-handler chain, plus the bounded total
of retained handler-owned call evidence. These fields can separate incomplete
traversal from an unjoined call edge or no retained call evidence without
exposing symbols or claiming a runtime cause.
Packets produced after the per-root reason update additionally carry
`truncationReasons` on each traversal observation. The triage prints these as
`directTruncationReasons`; older retained packets report `not-retained` and stay
readable. A reason identifies the bounded static-search limit observed for that
chain root. It does not identify a runtime condition or prove that a single
reason exclusively prevented terminal discovery.
`Run-AndTriage-FocusedWebFormsPageList.ps1` invokes the existing locally
configured page-list runner, then triages exactly one freshly written JSON under
the bounded output root. This keeps the tracked runner unchanged so a local
43-form list does not collide with pulls. It fails closed if it cannot identify
exactly one new artifact; it never falls back to an older run.

After `git pull`, run ` .\scripts\Triage-CompletedWebFormsPages.ps1` from the repository root.
This reads the newest completed depth-8 report under `C:\work\tracemap-output`;
it does not build, scan, launch a process, or traverse the graph. Set `-ReportPath`
explicitly if the newest comparison folder is not the intended baseline.
It prints small alias-only chain buckets for pages without retained terminals,
plus exact binding-support gap links for page-004 and page-026. Missing links
do not establish a cause. Truncated packets remain explicitly partial, and
no retained events does not establish that the application has no events.
Inputs are bounded to 128 MiB, 1,000 selections, 10,000 chains/gaps each, and
200,000 inspected gap-support entries. Unknown gap/rule strings are withheld.
Do not rerun the depth comparison for this step.

## Current fix: selected-page graph admission

The 43-page field report matched every requested page, but its 191 event chains
were globally downgraded after the repository-wide graph reader reached the
deterministic input ceiling. The roughly 200 `terminal-unavailable` results and
zero downstream boundaries therefore describe incomplete report input, not
evidence that the application lacks downstream calls. The 41
`handler-unavailable` and one `no-static-event-binding` result remain review
candidates, but should not drive application changes until the selected graph
loads completely.

This branch now resolves the selected handler facts first and reads only their
deterministic, depth-bounded outgoing symbol neighborhood from the retained
index. Unrelated repository facts and edges no longer consume the selected-page
graph budget. The same fact, edge, text, depth, and frontier ceilings remain;
if the selected neighborhood itself crosses one, the packet still emits the
rule-backed input-limit gap and makes no path or boundary classification from
partial graph input.

After pulling this branch on the work machine, rerun
`scripts/Run-FocusedWebFormsPageList.ps1` with the same 43-line form list and
the same retained `index.sqlite`. This is report-only: it does not rescan,
compile, or modify the private application. A useful comparison requires
`truncated: false` and no `WebFormsModernizationInputLimitReached` gap. Only
then compare resolved paths, downstream boundaries, and the remaining explicit
handler/binding gaps. Static paths still do not prove runtime event firing,
branch feasibility, successful binding, SQL execution, or whole-application
coverage.

The first selected-neighborhood rerun confirmed that
`WebFormsModernizationInputLimitReached`, `TruncatedByLimit`, event-chain limits,
and boundary limits were absent. Its sole truncation classification was
`WebFormsModernizationGapLimitReached`, proving that the remaining `true` value
came from the wrapper's default 1,000-row gap publication cap rather than graph
admission. The wrapper now passes a bounded 5,000-row gap cap and prints the
remaining limit classifications after each run. Pull and run the same
zero-argument script again; no scan or source change is required.

After a non-truncated report completes, run
`scripts/Summarize-FocusedWebFormsPageList.ps1`. It automatically reads the
newest `webforms-page-list-*` JSON under `C:\work\tracemap-output` and prints
mutually exclusive counts for handler-unavailable chains, handler-resolved
chains without a terminal, and terminal-resolved chains. It also prints page
category counts and safe `page-NNN` aliases, plus terminal-kind counts. It does
not print repository paths, source symbols, SQL, procedure names, or source
content. Set `$ReportPath` at the top only when an older report must be selected.

For the next unresolved-chain breakdown, run
`scripts/Triage-FocusedWebFormsUnresolvedChains.ps1`. It uses the same newest
completed JSON without rescanning the application. Packet generation now retains
bounded per-handler traversal observations, excluding the synthetic handler-root
selection edge from the downstream count. For resolved handlers it distinguishes
`no-observed-downstream-edge`,
`observed-downstream-without-supported-terminal`,
`supported-terminal-reached`, and `bounded-traversal-truncated`. The script groups
those closed states with reached-node, traversed-edge, downstream-edge and
truncated-chain totals, public rule/evidence metadata, and safe page aliases.
For a resolved handler with no joined downstream edge, it also distinguishes
`handler-owned-call-evidence-unjoined` from
`no-handler-owned-call-evidence-retained`. The former counts only call-edge fact
IDs already attributed to that exact handler by the Web Forms flow projection;
the latter is a bounded retained-evidence result, not proof that the source has
no calls. This separates an identity/composition follow-up from an extraction
coverage follow-up without exposing handler names or private paths.
The bounded graph now seeds both canonical identities from the exact handler
fact and re-anchors syntax support using only the exact handler and call fact
IDs already carried by the projection. Compiler-resolved targets can
continue through the ordinary bounded symbol closure; syntax-only targets stop
at an isolated candidate instead of joining globally by a simple method name.
Same-named calls outside the projection support set remain excluded, and every
bridge remains a static projection candidate under
`legacy.flow.static-traversal.v1`.
Handler-unavailable chains receive their own alias and linked-gap summary.
These are bounded static observations, not runtime presence, absence, execution,
branch feasibility, successful binding, or broken-code claims.

Reports created before this change do not contain `traversalObservation`. After
pulling the branch, first rerun `scripts/Run-FocusedWebFormsPageList.ps1` against
the existing retained index, then rerun
`scripts/Triage-FocusedWebFormsUnresolvedChains.ps1`. Neither command rescans,
compiles, or changes the private application.

## Tuesday handoff: report every page in a saved list

Pull this branch, then open
[`scripts/Run-FocusedWebFormsPageList.ps1`](../../../scripts/Run-FocusedWebFormsPageList.ps1).
At the top of that file, confirm `$IndexPath` and put one repository-relative
`.aspx` path per line inside the `$Forms` block. No quotes or commas are needed.
Then save the file and run this one command from the TraceMap repo:

```powershell
.\scripts\Run-FocusedWebFormsPageList.ps1
```

The script reads the existing `index.sqlite`; it does not scan or modify the
application repository. It creates a timestamped output directory under
`C:\work\tracemap-output`. Prefer paths such as `Area/Orders.aspx`. A filename
alone is accepted only when unique. The parameterized
`Invoke-FocusedWebFormsPageListReport.ps1` remains available for automation.

Open `webforms-modernization.md` in the new output directory. Its **Requested
page coverage** table retains input order as `page-001`, `page-002`, and so on,
and reports match status, static event-chain count, downstream-boundary count,
and the first unresolved state. Raw page-list values are not copied into the
packet. Unmatched and duplicate filename-only entries become explicit gaps.
The output is static evidence only: it does not prove runtime page rendering,
event firing, handler reachability, successful binding, SQL execution, or
whole-application coverage.

## Current handoff: one database-backed event in the existing index

Give Claude
[`claude-database-backed-event-trace.prompt.md`](claude-database-backed-event-trace.prompt.md).
**No PowerShell rerun is needed.** It uses the existing 0.7.1 index and asks
which page/event to use if no known database-backed target is already identified.
The expectation of database access is not itself evidence.

The field ownership check for the previous event passed: one projection,
six resolved support facts, two resolved edges, no mismatches or missing IDs.
Both unrelated edges remain indexed but are excluded from that projection;
the legitimate direct edge remains. This verifies that tested event, not every
projection or runtime behavior.

Work-machine handoffs belong in committed prompt files linked here, not in
chat-only copy/paste instructions; the two computers are separate.

## Previous handoff: verify projection ownership in the retained index

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
# Partial retained-report triage

## Resource safeguards and next read-only action

Use `./scripts/Summarize-CompletedWebFormsDepths.ps1 -Details` for the next work
handoff. It prints baseline depth-8 aliases missing terminal evidence or containing
unavailable-handler chains. It only reads completed reports. No rerun is needed.

Automatic depth comparisons and wrapper depths above 8 are disabled. Searches now
count state removals plus inspected edges (including cycle-rejected edges) against
a deterministic 100,000-work-unit default per Search invocation. Exhaustion emits
TruncatedByLimit with reason work, retains collected evidence and marks pending
roots incomplete. This also applies when no terminal exists. It is not a whole
pipeline memory bound; graph loading and report construction are separate stages.

Legacy Web Forms roots now share that same ceiling through deterministic slices:
each handler explores depth-first for at most 64 work units (or its smaller equal
share) and then yields to the next waiting handler. Expansion resumes at the exact
edge cursor, so a high-fan-out handler cannot bypass the slice. The public command
accepts `--max-traversal-work`; the focused wrapper keeps the safe 100,000 default.
This improves coverage fairness but does not prove complete traversal or justify
raising the limit.

The report wrapper requires PowerShell 7, builds TraceMap without restore, then
launches the DLL directly under an owned-process watchdog. Each build/report
process gets 300 seconds; the direct report process is sampled every 250 ms for
working/private memory over 4 GiB. A 2 GiB managed-heap limit is also set. These
are not an OS-enforced total-memory cap: native memory can exceed a threshold
between samples, and build-child memory is not aggregated. Timeout, memory stop,
nonzero exit or cancellation aborts the wrapper and cleans up its owned process
tree. Killed outputs are incomplete, not a successful partial packet. No raw
diagnostic logs or dumps are created. Do not resume deeper runs based on these
local tests; private-workload validation is still pending.

## Current safe handoff: completed files only

Run `./scripts/Summarize-CompletedWebFormsDepths.ps1` in PowerShell 7 after pulling.
It automatically finds the newest comparison folder containing both depth-8 and
depth-10 JSON reports. It NEVER launches TraceMap, dotnet, another script, or a
traversal. Do not rerun Compare-FocusedWebFormsDepth.ps1 while resource safeguards
are pending. The new summary reads one JSON document at a time with JsonDocument,
refuses files above 128 MiB, and retains only compact identities/counts between
reads. The reported 66/84 MB completed files fit that input limit. Console output
is count/alias-only, roughly a dozen lines, suitable for a photo. Source/selection
mismatch fails closed. Optional ComparisonDirectory at the top can override folder
discovery. No output files are modified. Distinct terminal evidence is an exact
kind/target/evidence tuple, not distinct runtime database operations.

## Depth comparison (one command)

**Historical workflow: automatic execution is now disabled.** Use the completed
file summary above instead. The following describes the earlier experiment only.

Run `./scripts/Compare-FocusedWebFormsDepth.ps1` after pulling. It reuses the
existing IndexPath, OutputRoot and Forms block in Run-FocusedWebFormsPageList.ps1;
do not retype the form list. Runs depths 8, 10 and 12 sequentially with unchanged
path/event-chain/gap caps, writing under a separate webforms-depth-comparison
directory so ordinary latest-report triage is unaffected. No source rescan occurs.
The wrapper may build TraceMap itself via dotnet run, not the private application.
Console output includes per-depth totals, alias-only per-page counts, closed gap
reasons, and added/lost retained terminal identities. Distinct means the exact
boundaryKind + boundaryTargetId + terminalEvidenceId tuple, not distinct runtime
database operations. Shared terminals reached by alternate routes count once.
Reports must agree on source provenance and page selection. Any cap keeps results
partial; deeper traversal can replace a bounded subset rather than only add to it.
Send photos of the final comparison output, especially depth/depthDelta/depthGap.

Legacy path exploration now uses deterministic depth-first branch scheduling to
reduce breadth-wide pending paths. Alternate routes are not globally deduplicated.
This is not shortest-path ordering or exhaustive runtime analysis. Existing depth,
path, and frontier limits remain enforced; cycle revisit gaps remain explicit and
keep coverage partial. After pulling, regenerate with
`./scripts/Run-FocusedWebFormsPageList.ps1`, then run the triage script. Reuse the
existing index; no source rescan is needed. A remaining cycle count is not evidence
of a runtime infinite loop, and a remaining frontier count still means incomplete
exploration.

Run `./scripts/Triage-FocusedWebFormsUnresolvedChains.ps1` again after pulling.
It reads the existing latest page-list JSON; it does not rebuild or rescan.
Truncated packets now produce partial diagnostic output instead of throwing.
All counts describe retained evidence only and cannot establish absence or completeness.
Newly generated packets retain an optional `truncationReason` for
`TruncatedByLimit`: depth, frontier, path, or cycle. Scripts print reason counts.
Older packets report unavailable; no reason is inferred from IDs or private text.
After pulling this change, run `./scripts/Run-FocusedWebFormsPageList.ps1` once
to regenerate the report from the existing index, then run the triage script.
No source rescan is required. Cycle means revisit protection, not a proven runtime loop.

Newly generated packets retain bounded, sorted traversal-shape sets for each
handler root: exhausted leaf node/surface kinds and rule IDs, bounded frontier
node/surface kinds and rule IDs, and traversed downstream edge kinds and rule IDs.
Each set is capped at 32 distinct closed values and the packet explicitly marks
shape-set truncation. No node identity, display name, source symbol, file path,
SQL, or terminal target is added by these diagnostics. Pull and rerun
`Run-FocusedWebFormsPageList.ps1` once against the existing index, then rerun
`Summarize-FocusedWebFormsActionableGaps.ps1`. The second command prints the new
aggregate fields and does not rebuild or rescan. Leaf and frontier observations
remain bounded static evidence; they do not prove runtime execution or absence.

The leaf diagnostic also publishes the leaf evidence tiers and a closed
reconciliation state. `nonsemantic-projection-isolated-by-evidence-tier` means an exact
handler-owned call was retained, but its target was syntax-only and intentionally
kept as an isolated candidate rather than joined by a simple method name.
`canonical-symbol-no-reconciliation-needed` means traversal reached a canonical
symbol, method, or type whose retained outgoing graph was exhausted. These states
explain the stopping condition without publishing target identities or weakening
the fail-closed reconciliation policy.
