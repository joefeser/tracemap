# Web Forms VB receiver bridge validation — PR 753

Date: 2026-09-18

## Exact-head tests

```text
dotnet test src/dotnet/TraceMap.sln --no-restore
Passed: 1956; Failed: 0; Skipped: 0
```

The review-finding patch pass additionally ran the focused VB extraction,
legacy Web Forms, modernization packet, static explorer, and raw evidence audit
tests, plus the focused PowerShell workbench, selected-source, graph-layout,
and pipeline contract tests.

## Pinned VB.NET adapter smoke

Command:

```bash
TRACEMAP_SKIP_BUILD=1 \
TRACEMAP_OSS_SMOKE_REPOS=community-visual-basic \
  scripts/smoke-open-source-repos.sh \
  /tmp/tracemap-oss-cache \
  /tmp/tracemap-oss-smoke-pr753
```

Pinned source:

```text
CommunityVB/Community.VisualBasic
20d2a51dfc9f342848ad134952ceaa8d79302559
```

Observed result:

```text
analysisLevel=Level1SemanticAnalysisReduced
buildStatus=FailedOrPartial
facts=110726
call_edges=13905
object_creations=517
argument_flows=175
analysis_gaps=75455
```

The required scan artifacts were present. Reduced coverage and the fact counts
match the pinned expectations in `docs/VALIDATION.md`. This smoke proves bounded
artifact generation and static VB.NET extraction at the pin; it does not prove
compilation success, runtime execution, or complete coverage.
