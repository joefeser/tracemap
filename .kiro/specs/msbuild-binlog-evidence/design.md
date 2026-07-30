# MSBuild Binlog Evidence Design

## Boundary

`tracemap scan` accepts one or more explicit `--binlog <path>` values together
with required `--binlog-commit-sha <sha>`. The declared SHA must be a full
lowercase or uppercase hexadecimal commit and must equal the repository commit
detected by TraceMap. The artifact path is never emitted.

The adapter replays records from an in-memory bounded artifact snapshot through
the pinned official `Microsoft.Build` `BinaryLogReplayEventSource` API. It does
not build a tree, inspect embedded files, launch tools, enable telemetry, or make
network calls. The dependency is MIT licensed, shares TraceMap's existing
MSBuild Locator registration, excludes runtime assets to avoid copying a partial
toolset, and is used as a parser library only; TraceMap owns the output schema.

## Safe schema

Rules:

- `build.msbuild-binlog.observation.v1`
- `build.msbuild-binlog.gap.v1`

Fact types:

- `MsBuildBinlogObserved`
- `MsBuildProjectObserved`
- `MsBuildProjectReferenceObserved`
- `MsBuildDiagnosticObserved`
- `AnalysisGap`

Every fact uses a synthetic artifact span
`@artifact/msbuild-binlog/<sha256-prefix>` so the local input path cannot leak.
Safe properties are allowlisted and bounded. Project and diagnostic paths are
stored only after canonical resolution inside the declared repository and
normalization to repository-relative `/` paths.

The recorded build outcome is `succeeded`, `failed`, or `unknown`. Diagnostics
contain only `error|warning`, a conservative recognized MSBuild/compiler/analyzer
diagnostic-code shape, safe relative file/line/column, and artifact provenance.
Unknown/custom code strings are omitted with a gap rather than rendered. Raw
messages are never read into output properties.

## Bounds

- maximum artifact size: 64 MiB;
- maximum expanded binary-log stream: 256 MiB;
- maximum explicit artifacts per scan: 8;
- maximum processed records: 250,000;
- maximum projects: 5,000;
- maximum graph edges: 10,000;
- maximum diagnostics: 5,000;
- maximum emitted safe string: 512 characters.

When a bound is reached, the reader stops or omits the affected projection and
emits one deterministic gap with aggregate counts. Malformed or parser-failed
artifacts emit a sanitized gap kind without exception text.

## Safety

The adapter rejects symlink/reparse-point artifact inputs because a declared path
could otherwise be swapped across a trust boundary. It hashes the artifact before
parsing and records only the digest and byte count. Outside-root identities,
unsafe diagnostic codes, and ambiguous/missing parent graph identities are not
rendered.

The parser necessarily materializes third-party event objects in process, but
TraceMap accesses only the allowlisted event properties described above. No
arbitrary `Message`, property, item, task, command, or embedded-content field is
projected.

## Deferred

- target framework/configuration projection pending a separately reviewed
  evaluated-property safety contract;
- package identity/version evidence;
- downstream database design-review, release-review, runbook, site, and vault
  composition;
- artifact signature/attestation and commit corroboration;
- out-of-process hard wall-clock/memory sandboxing.
