# Claude prompt: classify the retained Web Forms diagnostics

Read and follow
`.kiro/specs/webforms-bounded-report-memory/README.md` before doing any work.
The README is authoritative if this prompt and the README ever differ.

Do not write a BRD, migration requirements, application documentation, or
source changes. Do not rerun TraceMap. Do not modify the scanned repository or
the TraceMap repository.

Use only the latest retained TraceMap `scan/index.sqlite` selected by the
operator. Do not search unrelated directories. Keep all repository names,
project names, paths, source, symbols, configuration values, connection
information, native diagnostic messages, and business data on this computer.

## Task

Determine whether the reported 10,588
`LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset` rows
originated primarily from ordinary Roslyn `CompilationDiagnostic` gaps or from
genuine MSBuild workspace/project/solution-load failures.

Run this count-only query against the selected index:

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

Then run this count-only projection query:

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

If the `sqlite3` command is unavailable, use a local SQLite API to run exactly
these read-only queries. Do not copy the database, install an extension, attach
another database, or enable network access.

## Allowed output

Return only:

1. A table with `gapKind`, `diagnosticId`, `diagnosticCode`, and `count`.
2. A table with `diagnosticCode`, `guidanceCode`, and `count`.
3. The count of rows originating as `CompilationDiagnostic`.
4. The combined count originating as `WorkspaceDiagnostic`,
   `ProjectLoadFailed`, `SolutionLoadFailed`, `CompilationCreateFailed`,
   `CompilationMissing`, or `MSBuildRegistrationFailed`.
5. The highest-count compiler diagnostic IDs.
6. One conclusion stating whether the 10,588 projected rows primarily represent
   compiler diagnostics, genuine workspace/load failures, or remain
   indeterminate from the retained index.

Do not report file paths, line numbers, project identities, source identities,
symbols, diagnostic messages, diagnostic tokens derived from source names, or
sample evidence rows.

Do not infer that `UseCompatibleMSBuildToolset` is the root cause. It is
currently conservative TraceMap guidance and can be applied after an ordinary
compiler diagnostic is generically categorized and correlated with a legacy
project.

## Stop condition

If genuine workspace/load failures remain, stop after reporting their aggregate
counts. State that exact native-message classification requires the local-only
debugger procedure in the README. Do not expose, save, screenshot, summarize,
or transmit the native message unless the operator separately performs that
procedure on this computer.
