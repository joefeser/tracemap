# Restricted Web Forms run: diagnostic follow-up

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

If the first query shows approximately 10,588 `CompilationDiagnostic` rows,
the field result primarily demonstrates classifier conflation. If it instead
shows genuine workspace/load failures, those failures require the local-only
inspection described below.

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
