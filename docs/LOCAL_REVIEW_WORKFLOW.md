# Guided Local Review Workflow

Status: implementation runway for issue #666. Distribution publication and
cross-platform installation claims remain gated by `docs/LOCAL_DISTRIBUTION.md`.

The guided workflow composes existing TraceMap producers. It does not add
extraction rules or reinterpret scanner evidence.

## Command

```text
tracemap local-review run \
  --repo <repository> \
  --out <new-or-empty-output-directory> \
  [--solution <path>] \
  [--project <path>] \
  [--include <glob>] \
  [--exclude <glob>] \
  [--target-framework <tfm>] \
  [--webforms-modernization] \
  [--explorer] \
  [--diagnostic-progress <file>] \
  [--timeout-seconds <30-86400>]
```

The v1 safe path intentionally does not accept `--restore`. It does not upload
artifacts, call a hosted service, enable telemetry, or overwrite a nonempty
output directory.

## Output

```text
review-output/
  local-review-result.json
  README.md
  scan/
    scan-manifest.json
    scan-receipt.json
    facts.ndjson
    index.sqlite
    report.md
    logs/analyzer.log
  webforms/                 # when requested and compatible
  explorer/                 # when requested and compatible
```

`local-review-result.json` follows
`docs/contracts/local-review-result.v1.schema.json`. It binds the workflow to
the repository identity hash, commit SHA, scan ID, and source-snapshot digest;
records relative artifact paths and exact SHA-256 hashes; and reports typed
stage outcomes, gaps, the last proven safe state, cleanup, retryability, and a
bounded next action.

Absolute output paths are printed only to the interactive terminal. Portable
JSON and Markdown omit them.

## Safety Behavior

- Output inside the scanned repository, filesystem roots, `.git`, files,
  nonempty directories, and an existing output symlink/reparse point is
  rejected. Existing parent links are resolved to their final canonical target
  before the target is authorized; this supports normal host aliases such as
  macOS `/tmp` without placing evidence at an unverified location.
- Work is generated in a sibling staging directory and published by directory
  rename.
- Each downstream stage hashes the complete scan directory before it runs and
  verifies those bytes afterward. Mutation stops later work and records
  `LOCAL_REVIEW_INPUT_MUTATED`.
- A failed scan publishes a categorical failure result and any bounded
  producer receipt that exists. Raw exception text is not copied into the
  portable result.
- A downstream failure preserves the already verified scan and records the
  failed stage instead of presenting the workflow as successful.
- The Web Forms packet and ordinary explorer stages call the same producers as
  their standalone commands. #667 remains responsible for rendering the Web
  Forms packet itself in the explorer.

## Hidden Staging and Long Runs

The workflow intentionally buffers scanner output and writes into a hidden
staging sibling named `.<output>.local-review-<guid>`. Until the atomic
publication rename, the requested output directory does not exist.

An empty or absent requested output during execution is therefore **not**
proof that no work occurred. A run that stays active for minutes may be
blocked in MSBuild workspace loading, Roslyn compilation, or source snapshot
verification inside the hidden staging directory. Use `--diagnostic-progress`
to observe those stages live instead of inferring from the output path.

## Diagnostic Progress

`--diagnostic-progress <file>` enables bounded, privacy-safe progress
diagnostics:

- Progress lines are written **immediately to stderr**, never through the
  buffered scan output capture, so they appear while a scan is still running.
- A sanitized checkpoint file is maintained atomically (temp file plus
  rename) at the operator-selected path and remains available after
  cancellation, timeout, ordinary failure, or manual process termination. It
  is retained on successful completion.
- The checkpoint follows
  `docs/contracts/tracemap-scan-progress.v1.schema.json` and contains the
  latest event plus at most 32 recent non-heartbeat events.
- The same option also maintains an adjacent
  `<checkpoint>.performance.json` receipt following
  `docs/contracts/tracemap-scan-performance.v1.schema.json`. It retains at
  most 64 terminal specialized-extractor timings, a separately aggregated
  heartbeat count, an active categorical extractor when one has started but
  not terminated, truthful timing coverage, and a bounded next action.
- While a stage is active, a heartbeat line is emitted every 15 seconds
  reporting only the categorical stage, elapsed milliseconds, the last
  completed stage, and the monotonic sequence. Heartbeats are produced by a
  background timer, so they continue while the scan thread is blocked inside
  MSBuildWorkspace or Roslyn waits. Stages nest: completing an inner stage
  (for example one project's compilation) restores the enclosing stage
  (project load, then the workflow scan), so document-level Roslyn work stays
  observable between inner stages.

The checkpoint path must be a file path outside the scanned repository and
outside the review output (including hidden staging siblings). Paths inside
either tree, existing directories, and staging-directory names are rejected
with `LOCAL_REVIEW_PROGRESS_PATH_UNSAFE` before any scanning starts.
The derived performance receipt remains beside that authorized checkpoint;
it is never written into the repository or review output.

### Privacy Boundary

Progress events are operational observations, **not** TraceMap evidence
facts. They carry no evidence tier, no rule ID, and no scan conclusion. Each
event contains only bounded categorical data:

- schema version, monotonic sequence, operation (`scan` or `local-review`),
  stage catalog value, state, elapsed milliseconds;
- optional aggregate counts (`files`, `solutions`, `projects`, `facts`,
  `gaps`);
- an optional categorical failure code;
- the last successful stage;
- a deterministic ordinal for repeated solution/project/compilation work.

The contract contains no absolute or relative source paths, no repository,
project, or solution names, no filenames or symbols, no source values, no
command lines, no exception messages, no environment values, and no
credentials, URLs, or identity-derived hashes. Unknown stage names normalize
to `other`, unknown count keys are dropped, and failure codes are checked
against a closed catalog — anything else, including a path or exception text
passed by mistake, collapses to `UNKNOWN` — so even a programming mistake
cannot leak a value through this channel.

The performance receipt applies the same boundary. Extractor categories and
versions come from a closed scanner-owned catalog; an unknown value becomes
`other` / `unavailable`. Its `inputCount` is the bounded scan-inventory count
presented to that extractor, not a claim that every row was parsed; it also
records aggregate emitted-fact and emitted-gap counts. It never contains
per-file, per-symbol, per-fact, source,
configuration, diagnostic, or exception data. Timings are local operational
observations and do not change evidence tiers, rules, coverage, or scan
conclusions.

### Stage Catalog

| Stage | Meaning |
| --- | --- |
| `arguments-validated` | Arguments parsed and timeout value validated. |
| `output-authorized` | Output path validated against the safety rules. |
| `staging-initialized` | Hidden staging sibling created. |
| `scan` | Workflow scan stage (guided local review operation). |
| `repository-identity` | Git metadata detection and binding. |
| `inventory` | File inventory collection and scope application. |
| `source-snapshot-capture` | Pre-semantic input snapshot capture. |
| `project-selection` | Scoped solution/project selection counts. |
| `msbuild-registration` | MSBuild runtime registration for Roslyn. |
| `solution-load` | One MSBuild workspace solution load (by ordinal). |
| `project-load` | One MSBuild workspace project load (by ordinal). |
| `compilation` | One Roslyn compilation retrieval (by ordinal). |
| `syntax-fallback` | C# syntax-only extraction. |
| `specialized-extraction` | Deterministic specialized extractors. |
| `source-verification` | Pre- and post-extraction snapshot verification. |
| `artifact-write` | Scan artifact writing inside the output transaction. |
| `scan-publication` | Scan output transaction completed. |
| `webforms-modernization` | Web Forms modernization packet stage. |
| `explorer` | Static HTML explorer stage. |
| `local-review-publication` | Guided review published by directory rename. |

## Timeout and Cancellation

`--timeout-seconds <30-86400>` bounds the whole guided run:

- Omitting the option keeps the previous no-timeout behavior.
- Values outside the bounds, or non-numeric values, fail before scanning with
  `LOCAL_REVIEW_TIMEOUT_INVALID`.
- The timeout creates a cancellation token linked with external cancellation
  and passes it through the scan engine, the semantic extractor, MSBuild
  workspace open operations, Roslyn compilation, syntax-tree, and text
  retrieval, and the downstream stages.
- On timeout the workflow emits a `timed-out` progress event carrying the
  exact last successful stage, preserves the sanitized checkpoint, returns
  the typed failure `LOCAL_REVIEW_TIMEOUT`, and publishes a `timed-out`
  local-review result. It never publishes an incomplete scan or modernization
  packet as successful.

Cancellation is cooperative. Unblocking relies on cancellation-aware waits at
MSBuild/Roslyn seams and cancellation checks at stage boundaries; an API that
demonstrably ignores its cancellation token cannot be interrupted in-process,
and TraceMap does not abort threads or kill itself. Two guarantees hold even
in that ignored-token case: the timeout deadline callback records the
`timed-out` observation in the checkpoint, ends every active stage, and
latches the diagnostics terminal — a scanner thread that keeps running after
the deadline cannot restart heartbeats or overwrite the terminal observation —
every stage boundary and the finalization step check the timeout token, the
final result writes observe the timeout token, and the deadline timer is
disarmed and the token rechecked immediately before the atomic publication
rename, so an expired run can never publish a successful review. If a blocked
stage
never observes the token the process may remain alive; treat the checkpoint,
not process liveness, as the authoritative observation. External cancellation
(`Ctrl-C`) keeps its existing behavior and records `LOCAL_REVIEW_CANCELLED`.

### Typed Failure Codes

Workflow-level typed failures (returned on stderr and recorded in the
portable result and progress events):

- `LOCAL_REVIEW_TIMEOUT` — the `--timeout-seconds` budget elapsed; the last
  successful categorical stage is preserved in the progress checkpoint.
- `LOCAL_REVIEW_TIMEOUT_INVALID` — `--timeout-seconds` was non-numeric or
  outside 30–86400; rejected before scanning.
- `LOCAL_REVIEW_PROGRESS_PATH_UNSAFE` — `--diagnostic-progress` pointed
  inside the scanned repository, the review output (including hidden staging
  siblings), an existing directory, or an invalid path; rejected before
  scanning.
- `LOCAL_REVIEW_CANCELLED`, `LOCAL_REVIEW_SCAN_FAILED`,
  `LOCAL_REVIEW_OUTPUT_UNSAFE`, `LOCAL_REVIEW_ARGUMENT_INVALID`, and the
  stage failure codes keep their existing meanings.

Progress-event categorical failure codes emitted by scan-internal stages:
`MSBUILD_REGISTRATION_FAILED`, `SOLUTION_LOAD_FAILED`,
`PROJECT_LOAD_FAILED`, `COMPILATION_CREATE_FAILED`, `COMPILATION_MISSING`,
`SCAN_DISCOVERY_FAILED`, `SEMANTIC_STAGE_FAILED`,
`SOURCE_VERIFICATION_FAILED`, and `ARTIFACT_WRITE_FAILED`. These mirror the
scanner's existing gap kinds; they are operational observations, not evidence
facts.

## Reporting a Stuck Run

When a run looks stuck, report only the sanitized checkpoint file (plus the
console progress lines if captured). It answers: is it alive, what stage is
active, what completed last, how long it has been there, and whether it timed
out or failed. Do not share the scanned repository, the hidden staging
directory, or raw console output that may embed local paths.

## Interpretation

The workflow provides deterministic local static evidence. It does not prove
runtime execution, application correctness, complete coverage, migration
safety, release approval, publisher identity, or production state. Reduced
scanner coverage remains reduced in the workflow result.
