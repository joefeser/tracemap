# MSBuild Binlog Evidence Requirements

## Goal

TraceMap shall ingest an explicitly supplied MSBuild binary log as bounded,
offline observed-build evidence without persisting the sensitive values commonly
embedded in binlogs.

## Requirements

### R1 — Explicit artifact and provenance

- Accept `.binlog` files only through an explicit `tracemap scan --binlog` option.
- Require the declared binlog commit SHA and require it to match the repository
  commit detected for the scan.
- Preserve repository, commit SHA, artifact SHA-256, rule ID, evidence tier,
  coverage label, and extractor version on every binlog fact or gap.
- Do not silently discover `.binlog` files.

### R2 — Offline bounded parsing

- Parse locally with the pinned MIT-licensed official `Microsoft.Build`
  `BinaryLogReplayEventSource` API.
- Do not invoke `Microsoft.AITools.BinlogMcp`, an MCP server, Copilot, an LLM,
  telemetry, or a network service.
- Enforce explicit-artifact count, compressed/expanded size, record, project,
  edge, diagnostic, and safe-string limits.
- Stop projecting records at caps and emit rule-backed partial-analysis gaps.
- Convert malformed, corrupt, unreadable, and unsupported inputs into explicit
  gaps without rendering parser messages.

### R3 — Allowlisted observed evidence

- Emit the artifact hash, recorded build result, safe repository-relative
  project identities, repository-relative project graph edges, and diagnostic
  severity/code with a safe repository-relative location.
- Never persist raw diagnostic messages, arbitrary properties/items, environment
  values, task parameters, command lines, embedded project/import/source files,
  package sources, URLs, credentials, tokens, connection strings, private host
  names, usernames, or absolute/outside-root paths.
- When a project or diagnostic path cannot be normalized inside the repository,
  emit a classified gap or omission count rather than the path.

### R4 — Determinism and partial analysis

- Sort all projected facts deterministically and exclude timestamps, durations,
  process IDs, record IDs, and other unstable fields.
- Make successful bounded evidence useful even when some records are omitted.
- Label cap, malformed, unsafe-path, and unsupported outcomes as partial with
  stable gap kinds.

### R5 — Non-claims

The adapter shall not claim:

- that the binlog is authentic or untampered;
- that the build ran at the declared commit;
- that build success proves tests passed, a clean repository, release approval,
  deployment safety, or runtime correctness;
- that diagnostics are complete when parsing is partial;
- that graph edges prove runtime reachability;
- that absence of a diagnostic proves absence of a defect;
- that a package was deployed or used at runtime.

### R6 — Validation

- Tests shall generate synthetic successful and failed binlogs, graph and safe
  diagnostic evidence, outside-root paths, secret-bearing messages, malformed
  input, symlink input, and cap behavior.
- Leak tests shall prove planted credentials, connection strings, private hosts,
  local paths, command bodies, and source content do not enter any standard scan
  artifact.
- Focused tests, full solution build/test, the documented pinned smoke, private
  path guard, and `git diff --check` shall pass.
