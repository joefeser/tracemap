# Claude task: inspect remaining Web Forms coverage using retained evidence

Execute this diagnostic task on this work computer. Do not just return a plan
or debugger instructions. This file is self-contained; no chat history or shared
filesystem is needed. It supersedes the older workspace/debugger prompts for a
post-COM-fallback run with zero workspace failures.

## Boundaries

- Do not write a BRD, modify source/configuration, install packages, rebuild,
  restore dependencies, rerun a scan, attach a debugger, push, or open a PR.
- Read the existing local TraceMap checkout, selected retained scan, and matching
  application source only. Do not change either repository or the retained index.
- Keep paths, repository/project/assembly/type names, custom event/handler names,
  source text, SQL, configuration values, credentials and native diagnostics on
  this machine. Do not upload artifacts or paste raw query/source output in the
  final response. Do not create diagnostic files in either repository.
- Return only the closed categories, numeric counts, public TraceMap rule IDs,
  tool versions and TraceMap commit SHA described below. Use only invented names
  if an illustrative syntax pattern is necessary. Never copy an application
  expression and merely rename its identifiers.
- Do not interpret reduced coverage as proof of failed application compilation,
  failed runtime binding, or an application defect. The fallback intentionally
  omits COM resolution; zero workspace callbacks is not full semantic coverage.

## 1. Select and verify the retained run

Locate the generated `focused-webforms-workspace-*.txt` and matching
`focused-webforms-accuracy-*.txt` under the operator's existing summary directory
(normally `C:\work\tracemap-summary`). Locate their corresponding retained review
under the existing output directory (normally `C:\work\tracemap-output`).
Use the newest matching completed run, not simply the newest unrelated summary.
Confirm correspondence using retained run identifiers/progress/manifest metadata.
If selection is ambiguous, ask the operator to select one directory once.

Read `tracemapHead` from the workspace summary. In the local TraceMap checkout:

```text
git merge-base --is-ancestor ad8fdd986d7cdc98cad606ff1db38cee8fed8f4d <tracemapHead>
```

Exit 0 confirms ancestry. This is the **scanner's** SHA, not the application
commit displayed by the scan command. Report `wrong-or-unverified-build` if the
check fails or the referenced commit is unavailable; do not fetch or rebuild.

Confirm `workspaceDiagnosticCount=0` and
`uncategorizedWorkspaceFailureCount=0`. If either is nonzero, return
`workspace-failure-remains` with its safe count and stop this coverage task.

Open `scan/index.sqlite` read-only using an installed SQLite client, for example
`sqlite3 -readonly <local-index-path>`, and set `PRAGMA query_only=ON`.
Do not load all `facts.ndjson` or the full graph into memory. If no read-only
SQLite tool is available, report `read-only-query-tool-unavailable` instead of
asking the operator to run a full scan or installing anything.

Check retained extractor provenance:

```sql
SELECT extractor_id, extractor_version, COUNT(*) AS row_count
FROM facts
WHERE extractor_id IN ('CSharpSemanticExtractor', 'BuildEnvironmentDiagnosticExtractor')
GROUP BY extractor_id, extractor_version
ORDER BY extractor_id, extractor_version;
```

Expected baseline: `csharp-semantic/0.20.0`, `build-environment/0.6.0`.
Later versions require verifying the local implementation history; do not guess.
An absent extractor is not version confirmation. Also count the originating
workspace gaps separately:

```sql
SELECT json_extract(properties_json, '$.gapKind') AS gap_kind, COUNT(*) AS row_count
FROM facts
WHERE fact_type = 'AnalysisGap' AND rule_id = 'csharp.semantic.workspace.v1'
  AND json_extract(properties_json, '$.gapKind') IN
      ('ComReferenceResolutionSkipped', 'ComReferenceResolutionFallbackUnavailable',
       'WorkspaceDiagnostic', 'ScanScopeExcludedSources')
GROUP BY gap_kind ORDER BY gap_kind;
```

These are row counts, not occurrence totals. Do not treat a scan-scope gap as
proof that a particular missing control project was excluded.

## 2. Count and sample four remaining gap kinds

Use this count-only query first:

```sql
SELECT rule_id, json_extract(properties_json, '$.gapKind') AS gap_kind,
       COUNT(*) AS row_count
FROM facts
WHERE fact_type = 'AnalysisGap'
  AND rule_id IN ('legacy.webforms.composition.v1',
                 'legacy.webforms.event-binding.v1',
                 'legacy.webforms.lifecycle-context.v1')
  AND json_extract(properties_json, '$.gapKind') IN
      ('WebFormsAssemblyProjectUnavailable', 'WebFormsAssemblyTypeUnavailable',
       'UnsupportedWebFormsEventAttribute', 'UnsupportedWebFormsIsPostBackCondition')
GROUP BY rule_id, gap_kind ORDER BY rule_id, gap_kind;
```

Inspect at most **five originating facts per gap kind**, at most twenty total.
Use deterministic ordering by `file_path`, `start_line`, `fact_id`; verify the
schema with `PRAGMA table_info(facts)` first. Filter by the exact rule and gap kind,
then use `LIMIT 5`. Read only the relevant fact properties and a small source
window around each evidence span (start with +/- 10 lines). This local inspection
may contain private identifiers; never include them in the shareable answer.
Do not infer population-wide percentages or findings from this bounded sample.

Verify that current source matches the retained application snapshot before
using it to explain a fact. If unavailable or changed, use `source-unverified`
and do not treat current code as the scanned expression. Do not reset the repo.

### Assembly/project and type gaps

Read the relevant `WebFormsAssemblyRegistrationResolver` implementation in
`src/dotnet/TraceMap.Core/LegacyWebFormsExtractor.cs`. It uses source-project
assembly names and source type candidates; a DLL-only control can be usable by
the application while unavailable to this resolver.

For each sampled registration, inspect its local directive/configuration and
cross-check its assembly against the retained project inventory, actual scan
scope, project AssemblyName declarations, references, and source type candidates.
Inspect project metadata/source only; do not load or execute third-party DLLs.
Select one category, or leave unresolved if the evidence is insufficient:

- `source-project-outside-scan-scope`: a matching source project exists locally
  and exclusion from this scan is verified.
- `dll-reference-only`: a matching DLL/package reference exists but no matching
  source project is present in the inspected inventory; this does not prove
  that the DLL contains the requested control type.
- `assembly-identity-mismatch`: a concrete mismatch between registration and
  inventoried source-project assembly identity is verified.
- `source-type-not-in-candidates`: the matching source project is inventoried,
  but this resolver lacks the requested type candidate.
- `registration-conflict`: competing registrations are locally verified.
- `source-unverified` or `unresolved`.

Never recommend broadening the scan to every solution as an automatic repair.

### Event attributes

Compare each source attribute with the extractor's supported attribute rules.
Classify as `framework-server-event`, `custom-server-event`,
`client-side-attribute`, `dynamic-or-expression-value`, `source-unverified`, or
`unresolved`. A name beginning with `On` alone is not proof of a server event.
Report only category counts, not custom names or handler identifiers.

### IsPostBack conditions

Compare the exact local syntax with the lifecycle extractor's accepted shapes.
Classify as `negation`, `boolean-comparison`, `qualified-member`,
`compound-boolean`, `nested-or-wrapper-expression`, `source-unverified`, or
`unresolved`. If several apply, choose the outermost unsupported syntax shape
according to the implementation. Do not assert branch execution or reachability.

## 3. Finish with a shareable result

Return:

1. `result=coverage-triage-completed`, or one of the blocking results above.
2. Scanner SHA, extractor versions, workspace counts and COM-gap row counts.
3. A table: public gap kind, total retained row count, sampled count, category,
   sampled category count. Category counts must sum to the sampled count per kind.
4. At most three proposed next actions chosen from `review-scan-scope`,
   `investigate-dll-only-control-support`, `investigate-assembly-identity`,
   `investigate-source-type-candidates`, `extend-bounded-event-rule`,
   `extend-bounded-lifecycle-rule`, or `needs-more-local-evidence`.

Do not implement those proposals. If you cannot inspect a sample, explicitly
report it unresolved rather than sending the operator another long manual task.
Ignore an old accuracy-summary `recreate-compatible-legacy-msbuild-workspace`
priority based solely on static legacy markers. No BRD and no new scan.
