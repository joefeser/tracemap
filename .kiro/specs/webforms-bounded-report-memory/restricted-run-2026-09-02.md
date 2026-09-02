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

## What remains unknown

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

No scanner fix is justified from the screenshots alone. First classify the
sanitized summary evidence. Open a focused implementation slice only when a
typed diagnostic, missing rule-backed correlation, deterministic truncation,
or reproducible publication bottleneck identifies a bounded product defect.
