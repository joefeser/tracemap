# Claude prompt: verify the post-fix Web Forms diagnostic lineage

Read and follow
`.kiro/specs/webforms-bounded-report-memory/README.md` before doing any work.
The README is authoritative if it conflicts with this prompt.

Do not write a BRD, migration requirements, or source changes. Do not rerun
TraceMap. Do not modify either repository. Analyze only the operator-selected
latest retained TraceMap output and its generated workspace summary.

Keep all repository names, project names, paths, source, symbols, configuration
values, connection information, native diagnostic messages, diagnostic tokens,
and business data on this computer. Return only the closed aggregate fields
listed below.

## 1. Verify that the fixed scanner produced the index

Read the latest generated `focused-webforms-workspace-*.txt` selected by the
operator. Report only these fields when present:

- `tracemapHead`
- `analysisLevel`
- `buildStatus`
- `semanticCompilation`
- `workspaceDiagnosticCount`
- `compilerDiagnosticCount`
- `staticDiagnosticCount`
- `unknownDiagnosticOriginCount`
- `uncategorizedWorkspaceFailureCount`
- `legacyWorkspacePrerequisitesUnresolvedCount`
- `nextAction`

The selected run must contain the projection-boundary fix at or after:

```text
08ec7348e01f66dcb461c476f67c3849f2d66238
```

In the local TraceMap checkout, verify this without changing repository state:

```text
git merge-base --is-ancestor 08ec7348e01f66dcb461c476f67c3849f2d66238 <tracemapHead>
```

Exit code 0 means the selected run contains the fix. A later documentation-only
head is acceptable when this ancestry check succeeds.

Run this count-only query against the selected `scan/index.sqlite`:

```sql
SELECT extractor_id, extractor_version, COUNT(*) AS count
FROM facts
WHERE (fact_type = 'BuildEnvironmentDiagnostic'
       AND rule_id = 'build.environment.workspace-diagnostic.v1')
   OR (fact_type = 'AnalysisGap'
       AND rule_id = 'csharp.semantic.workspace.v1')
GROUP BY extractor_id, extractor_version
ORDER BY extractor_id, extractor_version;
```

The fixed index must contain the applicable expected versions:

```text
BuildEnvironmentDiagnosticExtractor | build-environment/0.5.0
CSharpSemanticExtractor             | csharp-semantic/0.19.0
```

If the ancestry check fails or the applicable extractor versions do not match,
stop and report `result=wrong-tracemap-build`. Do not interpret the diagnostic
counts.

## 2. Analyze only closed lineage fields

If provenance passes, run:

```sql
SELECT
  COALESCE(json_extract(properties_json, '$.originCategory'), 'unknown') AS origin_category,
  COALESCE(json_extract(properties_json, '$.originGapKind'), 'unknown') AS origin_gap_kind,
  COALESCE(json_extract(properties_json, '$.diagnosticId'), '-') AS diagnostic_id,
  json_extract(properties_json, '$.diagnosticCode') AS diagnostic_code,
  json_extract(properties_json, '$.guidanceCode') AS guidance_code,
  SUM(CAST(COALESCE(json_extract(properties_json, '$.occurrenceCount'), '1') AS INTEGER)) AS occurrence_count
FROM facts
WHERE fact_type = 'BuildEnvironmentDiagnostic'
  AND rule_id = 'build.environment.workspace-diagnostic.v1'
GROUP BY origin_category, origin_gap_kind, diagnostic_id, diagnostic_code, guidance_code
ORDER BY occurrence_count DESC, origin_category, origin_gap_kind, diagnostic_id;
```

No `PropertyMappingShapeUnsupported`, `PropertyMappingTruncated`, or other
property-mapping gap kind may appear in this build-environment result. If one
does, report `result=wrong-tracemap-build` and stop.

Then run the compiler-origin query against the originating gaps so a projected
reference-assembly diagnostic is not double-counted:

```sql
SELECT
  COALESCE(json_extract(properties_json, '$.diagnosticId'), '-') AS diagnostic_id,
  json_extract(properties_json, '$.diagnosticCode') AS diagnostic_code,
  COUNT(*) AS count
FROM facts
WHERE fact_type = 'AnalysisGap'
  AND rule_id = 'csharp.semantic.workspace.v1'
  AND json_extract(properties_json, '$.gapKind') = 'CompilationDiagnostic'
GROUP BY diagnostic_id, diagnostic_code
ORDER BY count DESC, diagnostic_id;
```

Confirm separately that the original property-mapping gaps remain in their own
rule-backed evidence family:

```sql
SELECT
  json_extract(properties_json, '$.gapKind') AS gap_kind,
  SUM(CAST(COALESCE(json_extract(properties_json, '$.occurrenceCount'), '1') AS INTEGER)) AS occurrence_count
FROM facts
WHERE fact_type = 'AnalysisGap'
  AND rule_id = 'csharp.semantic.propertymapping-gap.v1'
GROUP BY gap_kind
ORDER BY occurrence_count DESC, gap_kind;
```

These rows are expected analysis limitations. Do not add them to workspace,
load, compilation, registration, or unknown diagnostic-origin counts.

Finally report these derived counts:

1. ordinary compiler-diagnostic gaps;
2. workspace-callback occurrences;
3. project-load plus solution-load occurrences;
4. compilation-creation plus compilation-input occurrences;
5. MSBuild-registration occurrences;
6. projected diagnostics with `originCategory=unknown`;
7. `LegacyWorkspacePrerequisitesUnresolved` occurrences, grouped by origin;
8. the highest-count safe `CS####` and `MSB####` IDs.

Use `occurrenceCount`, not row count, for projected diagnostics. Use row count
for originating compiler gaps.

## 3. Conclusion

Return exactly one of these conclusions:

- `result=lineage-fix-verified-no-genuine-workspace-failure`
- `result=lineage-fix-verified-genuine-workspace-failure-remains`
- `result=lineage-fix-verified-unknown-lineage-remains`
- `result=wrong-tracemap-build`

Do not infer that `UseCompatibleMSBuildToolset` is a proven repair. Do not claim
runtime reachability, successful binding, a complete call chain, or migration
readiness. If a genuine workspace/load failure remains, state only its safe
origin, category, safe diagnostic ID when present, and aggregate count. Exact
native-message classification remains a separate local-debugger action.
