# Guided Local Review Workflow Design

## Overview

Issue #666 is split into a decision gate and an implementation seam:

1. validate distribution candidates with synthetic, reproducible smoke tests;
2. select the smallest candidate that works without a source checkout;
3. add one thin guided orchestration layer over existing command services;
4. emit a deterministic workflow result and a human readback from the same
   structured model.

The design does not make packaging responsible for evidence semantics. Scan,
Web Forms packet, and explorer contracts remain owned by their current
producers.

## Current Repository Evidence

- `src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj` is a `net10.0` executable named
  `tracemap`, but it is not currently configured as a .NET tool or release
  archive.
- `src/dotnet/TraceMap.Cli/Program.cs` owns command parsing and currently prints
  concise completion lines for scan, Web Forms packet, and explorer commands.
- `src/dotnet/TraceMap.Core/ScanExecutionReceipt.cs` and
  `docs/contracts/scan-execution-receipt.v1.schema.json` define the shipped,
  commit-bound operational receipt.
- `docs/WEBFORMS_MODERNIZATION_PACKET.md` defines the local packet workflow.
- `docs/STATIC_HTML_EVIDENCE_EXPLORER.md` defines the static explorer and its
  compatible input readers.
- The explorer does not yet accept `webforms-modernization-packet.v1`; issue
  #667 owns that reader. #666 must expose the dependency as unavailable until
  #667 ships rather than copying its parser.

## Distribution Decision Record

The implementation begins with a committed matrix and executable smoke harness.

| Candidate | Strength | Known cost | Required proof |
|---|---|---|---|
| .NET tool | Natural `tracemap` command, small package, upgrade/uninstall built in | Requires compatible .NET SDK/runtime; native dependency behavior must be proven | pack, install from local feed, run outside checkout, upgrade, uninstall on supported hosts |
| Framework-dependent archive | Simple `dotnet tracemap.dll`, predictable contents | Less friendly command/PATH story; runtime required | publish, archive, hash, extract, run outside checkout, remove |
| Self-contained archive | No preinstalled .NET runtime | Large per-RID outputs; signing and native dependencies multiply | deterministic per-RID publish/content audit and host smoke |
| Offline/container bundle | Strong isolation and repeatability | Docker may be unavailable; repository mounts and ownership need care | offline image/load/run/remove, read-only source mount, output ownership and no-network proof |

Selection gates:

- no source checkout at runtime;
- stable version readback;
- supported SQLite/native dependency behavior;
- install/run/remove smoke on every claimed host;
- no implicit network after installation;
- no security-control bypass instructions;
- bounded, inspectable contents and SHA-256 receipt;
- documented runtime and architecture requirements.

The first implementation SHOULD prefer a .NET tool only if those gates pass.
That is a hypothesis, not approval in the spec-only slice. Self-contained or
container outputs remain separate optional distributions rather than automatic
requirements if a smaller mechanism satisfies the owner outcome.

## Proposed Command Surface

Subject to CLI-name collision validation, the proposed surface is:

```text
tracemap version [--json]

tracemap local-review run \
  --repo <repository> \
  --out <new-output-root> \
  [scan options] \
  [--webforms-modernization] \
  [--explorer] \
  [--force-generated]
```

`local-review` is distinct from `access-review`, `release-review`, and ACK's
local reviewer terminology. If usability testing shows that collision is
confusing, the implementation task may select `guided-review`; the chosen name
must be documented and locked by CLI tests before product code expands.

The command handler should be thin. Extract reusable services from standalone
handlers only where necessary so both paths invoke the same implementation.
Do not shell out recursively to the installed executable.

## Workflow State Machine

```text
validate arguments
  -> prepare staged output
  -> run scan
  -> verify scan artifacts and receipt
  -> optionally compose Web Forms packet
  -> verify upstream hashes unchanged
  -> optionally generate compatible explorer
  -> verify upstream hashes unchanged
  -> write workflow result and human summary
  -> atomically publish output root
```

Terminal stage outcomes:

- `succeeded`
- `partial`
- `failed`
- `cancelled`
- `timed-out`
- `skipped`
- `unavailable`

The workflow stops after failure, cancellation, timeout, input mutation, or
identity conflict. A partial scan may continue only into consumers that already
accept and visibly retain reduced coverage.

## Output Layout

Proposed portable layout:

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
  webforms/
    webforms-modernization.json
    webforms-modernization.md
  explorer/
    index.html
    README.md
    assets/
    data/
```

Only generated stages that ran are present. The portable result refers to paths
relative to the output root. The terminal may print the absolute output root as
an ephemeral convenience.

## `local-review-result.v1`

Suggested top-level model:

```json
{
  "schemaVersion": "local-review-result.v1",
  "workflowId": "workflow-...",
  "toolVersion": "...",
  "distributionKind": "dotnet-tool",
  "repositoryIdentityHash": "...",
  "commitSha": "...",
  "scanId": "...",
  "sourceSnapshotDigest": "sha256:...",
  "outcome": "partial",
  "coverage": "reduced",
  "lastProvenSafeState": "scan-artifacts-verified",
  "cleanupResult": "not-required",
  "retryability": "retry-after-owner-review",
  "nextAction": "review-scan-gaps",
  "stages": [],
  "artifacts": [],
  "summary": {},
  "gaps": [],
  "limitations": []
}
```

Workflow ID inputs:

- schema version;
- tool version and distribution kind;
- repository identity hash;
- commit SHA and source snapshot digest;
- normalized authorized options;
- selected stage names;
- producer-owned scan ID and source-snapshot digest;
- selected stage outcomes.

Workflow ID excludes start time, end time, duration, the scan manifest's
producer-owned `scannedAt` observation, absolute paths, host identity, and raw
artifact hashes. Artifact records still carry the exact SHA-256 of the bytes
that were consumed or emitted. Re-rendering a workflow projection from the same
stage artifacts is byte-stable; independently executing a fresh scan is not
misrepresented as byte-identical because its existing manifest records when it
ran.

## Artifact Contract

Each artifact record carries:

- `artifactKind` from a closed vocabulary;
- `relativePath` under the output root;
- `sha256`;
- `schemaVersion` where applicable;
- `producerStage`;
- `status`: `available`, `partial`, `unavailable`, or `skipped`;
- `inputArtifactHashes` for derivative outputs;
- limitations by stable ID or closed text owned by the workflow contract.

The guided workflow validates existing producer schemas; it does not copy their
internal models into its own schema.

## Identity And Stage Composition

The scan manifest is authoritative for repository, commit, scan, source
snapshot, build, and analysis identity. `scan-receipt.json` is an operational
sidecar and must agree with the scan manifest when both exist.

For every derivative stage:

1. hash required input files;
2. validate the producer's schema and identity;
3. run the existing producer service;
4. hash outputs;
5. re-hash required inputs;
6. stop with `LOCAL_REVIEW_INPUT_MUTATED` if any required input changed;
7. record input/output hashes in the result.

No digest is a trust assertion.

## Output Transaction

Use a sibling staging directory owned by this invocation. Before writing:

- canonicalize the requested output path;
- resolve existing parent symlinks/reparse points to their final canonical
  target, then reject repository root, `.git`, input artifact roots, an
  existing output link, filesystem root, and any path outside that authorized
  canonical parent;
- reject existing nonempty output unless `--force-generated` verifies a
  workflow sentinel and every overwritten path is generated;
- never recursively delete a path that has not passed those checks.

Publish with directory rename when supported. If rename is not atomic on the
host filesystem, record the reduced mutation boundary and use bounded
file-by-file publication with a generated manifest.

## Failure Model

Public failure codes include:

- `LOCAL_REVIEW_ARGUMENT_INVALID`
- `LOCAL_REVIEW_IDENTITY_UNAVAILABLE`
- `LOCAL_REVIEW_OUTPUT_COLLISION`
- `LOCAL_REVIEW_OUTPUT_UNSAFE`
- `LOCAL_REVIEW_SCAN_FAILED`
- `LOCAL_REVIEW_SCAN_PARTIAL`
- `LOCAL_REVIEW_INPUT_INCOMPATIBLE`
- `LOCAL_REVIEW_INPUT_IDENTITY_CONFLICT`
- `LOCAL_REVIEW_INPUT_MUTATED`
- `LOCAL_REVIEW_WEBFORMS_UNAVAILABLE`
- `LOCAL_REVIEW_WEBFORMS_FAILED`
- `LOCAL_REVIEW_EXPLORER_UNAVAILABLE`
- `LOCAL_REVIEW_EXPLORER_FAILED`
- `LOCAL_REVIEW_CLEANUP_FAILED`
- `LOCAL_REVIEW_HOST_UNSUPPORTED`

Raw exceptions stay in local diagnostic channels only where current safety
policy permits; portable results contain categorical codes and bounded safe
messages.

## Security And Privacy

- No network client or telemetry exporter is added.
- Restore remains opt-in at the scanner command and is not enabled by the
  guided safe path in v1.
- Repository and artifact paths are accepted as explicit local inputs only.
- Portable output excludes absolute paths, raw remotes, source snippets, SQL,
  configuration values, URLs, credentials, connection strings, private server
  names, and exception text.
- Hashes, provenance, and receipts do not establish authority or authenticity.
- Partial or unavailable stages remain visible.

## Validation Strategy

The implementation should use synthetic repositories and isolated temporary
install roots. It must prove:

- package runs after the source checkout is unavailable;
- version JSON schema and safety;
- install/upgrade/uninstall behavior;
- standalone and guided scan/packet parity;
- collision and generated-only force behavior;
- symlink/reparse refusal;
- upstream mutation detection;
- failed/partial/cancelled stage readback;
- deterministic ordering and safe relative paths;
- Windows, macOS, and Linux claims only where matching CI or owner smoke exists.

## Sequencing With #667

#666 may ship package/version support and guided scan plus ordinary explorer
generation first. It may generate the Web Forms packet as a separate derivative.
It must not claim the explorer renders that packet until #667's compatible
reader is merged and its schema/identity checks pass. After #667, the guided
workflow may add that stage by invoking the existing explorer generator without
changing the v1 workflow evidence semantics.
