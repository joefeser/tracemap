# Restricted Web Forms run observation — 2026-09-02

## Evidence boundary

This note records sanitized operator-observed output from a restricted Windows
run. The operator confirmed that no external reviewer or Claude process ran
before or during the scan. The observation is therefore TraceMap output, not an
AI-generated analysis.

The screenshots, private repository identity, source commit SHA, source
snapshot digest, source-folder names, raw facts, SQLite index, analyzer logs,
and source content are intentionally not committed. Counts and durations below
are operational observations from one private run. They are not a public
benchmark or a claim about other repositories.

## Run receipt

| Field | Observed value |
| --- | ---: |
| Local review process exit | `0` |
| Outcome | `partial` |
| Coverage | `reduced` |
| Facts | `1,653,627` |
| Gaps | `13,460` |
| Total elapsed | `1,104,151 ms` (about 18m 24s) |
| Checkpoint history | `32` |
| Heartbeats | `71` |
| Timing coverage | `complete` |
| Timings truncated | `false` |
| Extractor timing count | `17` |
| Terminal state | `completed` |
| Terminal stage | `local-review-publication` |
| Last successful stage | `webforms-modernization` |

The completed process and terminal state prove that the local review and its
publication stage finished. They do not upgrade the explicitly partial outcome
or reduced evidence coverage.

## Timing observations

| Rank | Stage | Elapsed | Approximate share of total |
| ---: | --- | ---: | ---: |
| 1 | `artifact-write` | `648,388 ms` | 58.7% |
| 2 | `specialized-extraction` | `223,281 ms` | 20.2% |
| 3 | `solution-load` | `104,305 ms` | 9.4% |
| 4 | `webforms-modernization` | `46,046 ms` | 4.2% |
| 5 | `syntax-fallback` | `11,728 ms` | 1.1% |

Artifact publication, rather than Web Forms extraction, was the largest
observed stage. The run did not show an out-of-memory termination or process
failure. These timings alone do not identify retained heap, peak RSS, disk
throughput, or a causal performance regression.

The slowest retained specialized-extractor observation was:

- extractor: `legacy-webforms`
- extractor version: `legacy-webforms/0.6.0`
- elapsed: `137,101 ms`
- emitted facts: `24,566`
- emitted gaps: `1,010`

## Sanitized outputs created

The runner reported successful creation of these summary classes under the
configured external summary directory:

- `focused-webforms-workspace-20260902-155045-623.txt`
- `focused-webforms-gap-extractor-20260902-155455-233.txt`
- `focused-webforms-accuracy-20260902-160224-852.txt`

It also retained the focused output directory and progress receipt. Those raw
artifacts remain local and must not be committed.

## Questions queued before local review

The console receipt does not expose the fields needed to decide whether the
solution unlocked compiler-resolved evidence. Before changing product code,
inspect the sanitized workspace and accuracy summaries for:

1. `semanticCompilation` state;
2. admission of the intended solution and three selected projects;
3. Tier1 semantic fact count;
4. typed workspace diagnostic categories and uncategorized count;
5. largest Web Forms accuracy gaps;
6. evidence available for event-handler to backend/database correlation.

Do not infer complete call chains, runtime reachability, successful binding,
rendered behavior, branch execution, SQL execution, or production usage from
the process exit code, fact count, or completed publication stage.

## Operator-supplied local-review readback

Later screenshots show that an on-device reviewer analyzed the three sanitized
summary files after the TraceMap run completed. This does not change the earlier
observation that no reviewer ran before or during the scan. The findings below
are transcribed from that post-run review and have not been independently
recomputed from the private artifacts.

### Compilation and scope admission

- `semanticCompilation=reduced`
- `analysisLevel=Level1SemanticAnalysisReduced`
- `buildStatus=FailedOrPartial`
- all three intended scopes (`webforms`, `backend`, and `controls`) were present
  with non-zero facts, but none reached full semantic compilation;
- the reviewer concluded that the Roslyn workspace loaded but did not reach
  full compilation across the selected scopes;
- legacy MSBuild success remains insufficient to prove Roslyn workspace
  admission.

### Semantic evidence retained

The reviewer reported **932,070 Tier1 semantic facts**:

| Scope | Tier1 facts |
| --- | ---: |
| `webforms` | 556,487 |
| `backend` | 357,255 |
| `controls` | 18,328 |

`CSharpSemanticExtractor` reportedly emitted 927,941 facts and 230 gaps. No
comparable baseline was supplied, so the review correctly made no improvement
or regression claim.

### Workspace diagnostics

The reviewer reported 10,589 workspace diagnostics and one uncategorized
failure:

| Scope | Diagnostic | Reason | Count |
| --- | --- | --- | ---: |
| `webforms` | `LegacyWorkspacePrerequisitesUnresolved` | `UseCompatibleMSBuildToolset` | 6,632 |
| `backend` | `LegacyWorkspacePrerequisitesUnresolved` | `UseCompatibleMSBuildToolset` | 3,833 |
| `controls` | `LegacyWorkspacePrerequisitesUnresolved` | `UseCompatibleMSBuildToolset` | 123 |
| `other` | `UncategorizedWorkspaceFailure` | `ReviewEnvironmentGap` | 1 |

Additional typed diagnostics included:

- `GeneratedFileMissing`: 278 total (`backend` 235, `webforms` 41,
  `controls` 2);
- `WebApplicationProjectTargets`: 3 in `webforms`;
- `NonSdkStyleProject`: one in each selected scope;
- `UnknownImportedTargets`: 2 in `webforms`;
- `UnknownLegacyProjectFormat`: 1 in `webforms`.

The dominant workspace blocker was therefore reported as
`LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset` with
10,588 occurrences. Subsequent TraceMap code inspection showed that ordinary
`CompilationDiagnostic` rows can be projected into that legacy-workspace
category. The count is therefore not proof of 10,588 workspace/toolset
failures. Use the count-only origin queries in [`README.md`](README.md) before
interpreting it. The one uncategorized failure remains a separate bounded
classification target.

### Highest-count Web Forms gaps

| Artifact | Rule | Reason | Count |
| --- | --- | --- | ---: |
| `aspx-codebehind` | `database.operation.call-pattern.v1` | `SyntaxFallbackOperationCandidate` | 405 |
| `aspx` | `legacy.webforms.composition.v1` | `WebFormsAssemblyProjectUnavailable` | 323 |
| `aspx-codebehind` | `legacy.aspnet.navigation.v1` | `DynamicCodeNavigationTarget` | 313 |
| `aspx` | `legacy.webforms.event-binding.v1` | `UnsupportedWebFormsEventAttribute` | 166 |
| `aspx-codebehind` | `legacy.webforms.lifecycle-context.v1` | `UnsupportedWebFormsIsPostBackCondition` | 129 |
| `aspx-codebehind` | `legacy.webforms.event-binding.v1` | `DynamicWebFormsEventSubscription` | 98 |
| `cs` in Web Forms scope | `legacy.aspnet.navigation.v1` | `DynamicCodeNavigationTarget` | 94 |
| `aspx` | `legacy.webforms.composition.v1` | `WebFormsAssemblyTypeUnavailable` | 80 |
| `ascx` | `legacy.webforms.composition.v1` | `WebFormsAssemblyProjectUnavailable` | 68 |
| `ascx` | `legacy.webforms.composition.v1` | `UnresolvedWebFormsControlRegistration` | 47 |
| `aspx-codebehind` | `legacy.webforms.client-script.v1` | `DynamicWebFormsClientScriptRegistration` | 39 |
| `ascx-codebehind` | `database.operation.call-pattern.v1` | `SyntaxFallbackOperationCandidate` | 35 |
| `aspx` | `legacy.webforms.handler-resolution.v1` | `AutoEventWireupUnavailable` | 31 |
| `ascx` | `legacy.webforms.event-binding.v1` | `UnsupportedWebFormsEventAttribute` | 23 |
| `aspx-codebehind` | `database.sql.text.v1` | `dynamic-sql-boundary` | 18 |
| `aspx-codebehind` | `legacy.webforms.client-script.v1` | `AmbiguousWebFormsClientScriptRegistrationReceiver` | 18 |
| `aspx-codebehind` | `database.operation.call-pattern.v1` | `DatabaseOperationTargetUnavailable` | 9 |

For backend context, the reviewer also reported 303 `dynamic-sql-boundary`,
217 `DatabaseOperationTargetUnavailable`, 208
`SyntaxFallbackOperationCandidate`, and 156
`AmbiguousAsmxMetadataOperationMapping` gaps.

The largest gap across the entire run was
`csharp.semantic.propertymapping-gap.v1` at 10,628, including 10,604
`PropertyMappingShapeUnsupported` results. The corresponding extractor emitted
13,836 facts. This is a separate extractor-shape limitation and should not be
conflated with the workspace toolset blocker.

### Static event-to-database posture

The reviewer found useful static evidence toward handler-to-backend and
database operations, while retaining the required non-claim about complete
runtime chains:

- 476 `aspx-codebehind` files with 384,933 Tier1 and 303,578 Tier3 facts;
- 27 `ascx-codebehind` files with 22,777 Tier1 facts;
- database-operation candidate extraction was active, with unresolved and
  syntax-fallback gaps retained rather than promoted;
- `LegacyAsmxExtractor` emitted 5,739 facts;
- `CSharpIntegrationSyntaxExtractor` emitted 3,409 facts and 986 gaps.

The review summarized static event-to-database evidence as present but
unproven as complete chains. That is consistent with TraceMap's evidence
posture.

## Bounded local-review prompt

The following prompt may be used by an on-device reviewer after the operator
provides only the sanitized summary files:

```text
Read only these sanitized TraceMap summary files. Do not inspect the private
source repository, facts.ndjson, index.sqlite, or raw analyzer logs.

Determine:

1. Whether semanticCompilation is available, reduced, or unavailable.
2. Whether the solution and all three intended projects were admitted.
3. The Tier1 semantic fact count and whether it materially improved relative
   to an explicitly supplied comparable baseline.
4. Every typed workspace diagnostic and remaining uncategorized workspace
   failure.
5. The largest Web Forms accuracy gaps by count.
6. Which gaps appear actionable in TraceMap versus inherent runtime or dynamic
   limitations.
7. Whether the new run provides static evidence connecting page or control
   event handlers toward backend and database operations.

Do not claim runtime reachability, successful binding, rendered behavior, or
complete call chains. Cite the exact summary fields supporting every
conclusion. If no comparable baseline is supplied, do not claim improvement or
regression.
```

## Follow-up decision rule

The next investigation is to separate original `CompilationDiagnostic` gaps
from genuine workspace/load failures using the retained index and the bounded
queries in [`README.md`](README.md). Code inspection already supplies a
synthetic classifier hypothesis: an ordinary compiler error in a legacy
project can be relabeled as a toolset-prerequisite failure.

Before implementing another scanner fix, reproduce that projection with a
synthetic, non-compiling legacy solution. A focused implementation slice is
justified when the reproducer demonstrates the deterministic classification
defect. Inspect native diagnostic text locally only if genuine workspace/load
failures remain after separation. Treat the single `ReviewEnvironmentGap`,
generated designer inputs, dynamic navigation/events/SQL, unsupported
property-mapping shapes, and workspace compatibility as distinct problem
classes rather than one failure.
