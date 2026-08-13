# Web Forms Modernization Evidence Packet

This runbook explains how to scan an authorized ASP.NET Web Forms repository
and generate TraceMap's local modernization packet. The packet is intended for
private discovery and migration planning. It composes deterministic static
evidence; it does not execute the application or infer business intent.

## What the workflow produces

The scan writes the standard TraceMap artifacts:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

The packet command reads that `index.sqlite` and creates a new directory with:

```text
webforms-modernization.json
webforms-modernization.md
```

The JSON schema is `webforms-modernization-packet.v1`. The Markdown file is a
human-readable projection of the same packet. Both are local-only artifacts.

The packet can contain:

- declared `.aspx`, `.ascx`, and `.master` surfaces;
- server controls and declared master/user-control composition;
- supported markup and named code subscriptions;
- statically resolved handler identities;
- existing bounded static paths to supported terminal surfaces;
- a bounded downstream-boundary inventory for supported database, service,
  messaging, configuration, and dependency surfaces;
- structural slice candidates based only on declared surface composition;
- provenance, evidence tiers, coverage labels, source spans, supporting fact
  and edge IDs, limitations, and explicit gaps.

It does not add new authentication, batch, database, service, or runtime
extraction. It only composes evidence already present in one scan.

## Requirements

- A current TraceMap checkout containing the `webforms-modernization` command.
- The .NET 10 SDK. Confirm with `dotnet --version`.
- An authorized local checkout of the Web Forms application.
- A Git repository and resolvable commit SHA for the application checkout.
- Enough local disk space for the scan and two packet directories when
  verifying byte-for-byte determinism.
- An optional solution or project path when the repository contains more than
  one build entry point.

Use a clean, committed application checkout when practical so the recorded
commit identifies the reviewed source state. TraceMap can retain useful syntax
and structural evidence when semantic project loading fails, but it labels the
result as reduced coverage.

Do not copy the application, its scan, or its generated packet into the public
TraceMap repository. Keep generated output in an owner-controlled local path
that is outside the application repository or already ignored by it.

## Build TraceMap

From the TraceMap checkout:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter 'FullyQualifiedName~WebForms|FullyQualifiedName~LegacyFlowComposition'
```

The examples below run the CLI from source. A built or packaged `tracemap`
executable may be substituted with the same command arguments.

## Windows PowerShell workflow

Choose new output directory names for every run. The packet command refuses to
overwrite an existing file or directory.

```powershell
$TraceMap = "C:\work\tracemap"
$Application = "C:\work\legacy-webforms"
$Solution = Join-Path $Application "LegacyApplication.sln"
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ScanOut = "C:\work\tracemap-output\webforms-scan-$Stamp"
$PacketOut = "C:\work\tracemap-output\webforms-packet-$Stamp"
$Cli = Join-Path $TraceMap "src\dotnet\TraceMap.Cli"
$PacketBounds = @(
    "--max-surfaces", "1000",
    "--max-event-chains", "1000",
    "--max-boundaries", "1000",
    "--max-candidates", "1000",
    "--max-gaps", "1000",
    "--max-depth", "8",
    "--max-paths", "1000"
)

if (Test-Path $ScanOut) { throw "Scan output already exists: $ScanOut" }
if (Test-Path $PacketOut) { throw "Packet output already exists: $PacketOut" }

git -C $Application rev-parse --show-toplevel
git -C $Application rev-parse HEAD
git -C $Application status --short

dotnet run --project $Cli -- scan `
  --repo $Application `
  --solution $Solution `
  --out $ScanOut
if ($LASTEXITCODE -ne 0) { throw "TraceMap scan failed" }

$IndexHashBefore = (Get-FileHash (Join-Path $ScanOut "index.sqlite") -Algorithm SHA256).Hash

dotnet run --project $Cli -- webforms-modernization `
  --index (Join-Path $ScanOut "index.sqlite") `
  --out $PacketOut `
  @PacketBounds
if ($LASTEXITCODE -ne 0) { throw "Web Forms packet generation failed" }

$IndexHashAfter = (Get-FileHash (Join-Path $ScanOut "index.sqlite") -Algorithm SHA256).Hash
if ($IndexHashBefore -ne $IndexHashAfter) { throw "Input index changed" }

Get-ChildItem $PacketOut | Select-Object Name, Length
```

Omit `--solution $Solution` when TraceMap should discover the repository's
build targets. Use `--project <path>` instead when one project is the intended
scope. Add `--restore` only when package restoration and its network/cache
effects are explicitly authorized.

## Bash workflow

```bash
set -euo pipefail

TRACEMAP=/work/tracemap
APPLICATION=/work/legacy-webforms
SOLUTION="$APPLICATION/LegacyApplication.sln"
STAMP=$(date +%Y%m%d-%H%M%S)
SCAN_OUT="/work/tracemap-output/webforms-scan-$STAMP"
PACKET_OUT="/work/tracemap-output/webforms-packet-$STAMP"
PACKET_BOUNDS=(
  --max-surfaces 1000
  --max-event-chains 1000
  --max-boundaries 1000
  --max-candidates 1000
  --max-gaps 1000
  --max-depth 8
  --max-paths 1000
)

test ! -e "$SCAN_OUT"
test ! -e "$PACKET_OUT"

git -C "$APPLICATION" rev-parse --show-toplevel
git -C "$APPLICATION" rev-parse HEAD
git -C "$APPLICATION" status --short

dotnet run --project "$TRACEMAP/src/dotnet/TraceMap.Cli" -- scan \
  --repo "$APPLICATION" \
  --solution "$SOLUTION" \
  --out "$SCAN_OUT"

INDEX_HASH_BEFORE=$(shasum -a 256 "$SCAN_OUT/index.sqlite" | awk '{print $1}')

dotnet run --project "$TRACEMAP/src/dotnet/TraceMap.Cli" -- \
  webforms-modernization \
  --index "$SCAN_OUT/index.sqlite" \
  --out "$PACKET_OUT" \
  "${PACKET_BOUNDS[@]}"

INDEX_HASH_AFTER=$(shasum -a 256 "$SCAN_OUT/index.sqlite" | awk '{print $1}')
test "$INDEX_HASH_BEFORE" = "$INDEX_HASH_AFTER"

ls -l "$PACKET_OUT"
```

## Packet bounds

All bounds must be positive integers. Defaults are intentionally finite so a
large legacy repository produces a bounded artifact rather than an unbounded
report.

| Option | Default | Meaning |
| --- | ---: | --- |
| `--max-surfaces` | 1000 | Maximum retained Web Forms surfaces. |
| `--max-event-chains` | 1000 | Maximum binding/handler/path chains. |
| `--max-boundaries` | 1000 | Maximum retained downstream boundary rows. |
| `--max-candidates` | 1000 | Maximum structural slice candidates. |
| `--max-gaps` | 1000 | Maximum structured gaps, including a limit gap. |
| `--max-depth` | 8 | Maximum legacy static-flow traversal depth. |
| `--max-paths` | 1000 | Maximum legacy static-flow paths considered. |

Increase a bound only after inspecting why the packet truncated. A binding
limit changes packet content and may materially increase run time and packet
size; raising a non-binding limit may leave the bytes unchanged. In v1,
`packetId` identifies the source snapshot and does not encode bound values.
Record the exact bound arguments with the packet when they differ from the
defaults. Reaching a bound sets `summary.truncated` and emits a corresponding
gap; it must not be treated as complete coverage.

Example:

```powershell
$LargerPacketOut = "$PacketOut-larger-bounds"
$LargerPacketBounds = @(
    "--max-surfaces", "2000",
    "--max-event-chains", "3000",
    "--max-boundaries", "3000",
    "--max-candidates", "2000",
    "--max-gaps", "2000",
    "--max-depth", "10",
    "--max-paths", "3000"
)
dotnet run --project $Cli -- webforms-modernization `
  --index (Join-Path $ScanOut "index.sqlite") `
  --out $LargerPacketOut `
  @LargerPacketBounds
```

## Required post-run checks

### PowerShell

```powershell
$Packet = Get-Content (Join-Path $PacketOut "webforms-modernization.json") -Raw |
  ConvertFrom-Json

$Packet | Select-Object schemaVersion, packetId, claimLevel, coverage
$Packet.sources |
  Select-Object repositoryId, scanId, commitSha, analysisLevel, buildStatus |
  Format-Table
$Packet.summary | Format-List
$Packet.downstreamBoundaries |
  Select-Object boundaryId, chainId, boundaryCategory, boundaryKind, classification |
  Format-Table
$Packet.gaps |
  Group-Object classification |
  Sort-Object Name |
  Select-Object Name, Count
$Packet.ownerQuestions
$Packet.limitations

if ($Packet.schemaVersion -ne "webforms-modernization-packet.v1") {
    throw "Unexpected packet schema"
}
if ($Packet.claimLevel -ne "local-only") {
    throw "Unexpected packet claim level"
}
```

### Bash with `jq`

```bash
jq '{schemaVersion, packetId, claimLevel, coverage, sources, summary}' \
  "$PACKET_OUT/webforms-modernization.json"
jq -r '.downstreamBoundaries[] | [.boundaryId, .chainId, .boundaryCategory, .boundaryKind, .classification] | @tsv' \
  "$PACKET_OUT/webforms-modernization.json"
jq -r '.gaps | group_by(.classification)[] | "\(.[0].classification)\t\(length)"' \
  "$PACKET_OUT/webforms-modernization.json"
jq -r '.ownerQuestions[], .limitations[]' \
  "$PACKET_OUT/webforms-modernization.json"
```

Before using the packet, confirm all of the following:

1. `schemaVersion` is `webforms-modernization-packet.v1`.
2. `claimLevel` is `local-only`.
3. The source commit matches the intended application commit.
4. `coverage` and the source `analysisLevel`/`buildStatus` are understood.
5. `summary.truncated` is reviewed.
6. Every gap classification is reviewed or explicitly accepted.
7. Structural candidates remain owner-unnamed until a human validates them.
8. The Markdown counts and identifiers agree with the JSON packet.

## Determinism check

Generate a second packet from the unchanged index into another new directory.

```powershell
$PacketOut2 = "$PacketOut-repeat"
dotnet run --project $Cli -- webforms-modernization `
  --index (Join-Path $ScanOut "index.sqlite") `
  --out $PacketOut2 `
  @PacketBounds

$Files = "webforms-modernization.json", "webforms-modernization.md"
foreach ($File in $Files) {
    $A = (Get-FileHash (Join-Path $PacketOut $File) -Algorithm SHA256).Hash
    $B = (Get-FileHash (Join-Path $PacketOut2 $File) -Algorithm SHA256).Hash
    if ($A -ne $B) { throw "Non-deterministic packet file: $File" }
}
```

```bash
PACKET_OUT_2="${PACKET_OUT}-repeat"
dotnet run --project "$TRACEMAP/src/dotnet/TraceMap.Cli" -- \
  webforms-modernization \
  --index "$SCAN_OUT/index.sqlite" \
  --out "$PACKET_OUT_2" \
  "${PACKET_BOUNDS[@]}"

cmp "$PACKET_OUT/webforms-modernization.json" \
  "$PACKET_OUT_2/webforms-modernization.json"
cmp "$PACKET_OUT/webforms-modernization.md" \
  "$PACKET_OUT_2/webforms-modernization.md"
```

The same index and exact bound arguments should produce byte-identical files.
A difference is a validation failure; do not normalize or hand-edit either
generated file.

## How to interpret the packet

| Field or section | Interpretation |
| --- | --- |
| `coverage` | `bounded-static-webforms-modernization` means no known packet gap or bound weakened the retained evidence. It is not a completeness claim. `reduced-static-webforms-modernization` means at least one upstream, provenance, build, analysis, path, or bound limitation applies. |
| `surfaces` | Declared Web Forms surfaces and their composition/control evidence. These are static declarations, not proof that a user can reach or render them. |
| `eventChains` | Bounded static chains from a surface/control event to a handler and, when evidence permits, an existing static terminal path. |
| `eventChains[].classification` | May be a legacy static-path classification, `NoBackendEvidence`, or `handler-unavailable`. It is evidence-relative, not a runtime result. |
| `downstreamBoundaries` | Bounded terminal projections from retained event chains. Each row keeps an opaque target identity, terminal evidence ID, path evidence, rules, tiers, coverage, and supporting IDs. It is not proof that the interaction ran or succeeded. |
| `structuralSliceCandidates` | Connected components derived only from declared surface composition. `ownerNamingRequired` remains true; TraceMap does not assign business capability names. |
| `gaps` | Explicit reasons evidence is missing, reduced, ambiguous, invalid, or bounded. A gap is part of the result, not an error to hide. |
| `ownerQuestions` | Questions a developer or product owner should answer before treating structural evidence as a modernization plan. |
| `limitations` | Packet-wide non-claims that must travel with any derived discussion. |

Every evidence-bearing row should retain a rule ID, evidence tier, coverage
label, commit SHA, repository-relative file span, extractor ID/version,
supporting IDs, and limitations. Missing required provenance fails closed as a
gap or removes the unsupported path conclusion.

The current scanner does not extract general file-operation boundaries. The
packet therefore emits `FileOperationBoundaryExtractionUnavailable` rather
than treating the absence of a file boundary as evidence that handlers do not
read or write files. Configuration boundaries classified as needs-review,
reduced, or unknown also emit `ConfigurationBoundaryNeedsReview`; a textual or
generic configuration match is not promoted to a proven endpoint.

## Common outcomes

### Reduced coverage

Inspect, in order:

1. `scan-manifest.json` for analysis and build status;
2. `logs/analyzer.log` for the stage that failed or fell back;
3. the packet's `gaps` and `summary.truncated`;
4. the scan `report.md` Web Forms sections;
5. the relevant evidence rows and supporting IDs.

A failed or partial build is not a clean scan. TraceMap preserves provable
syntax/structural evidence and labels the reduction instead of inventing
semantic certainty.

### `NoBackendEvidence`

The handler was retained, but the bounded snapshot did not prove a supported
terminal path. It does not mean the handler has no backend behavior. Possible
causes include unsupported frameworks, dynamic dispatch, reflection, missing
dependencies, reduced semantic loading, or traversal bounds.

### `handler-unavailable`

The binding was retained but no single handler identity was proven. Review
nearby gaps for missing, overloaded, dynamic, lambda, unknown-receiver, or
cross-file partial evidence. Do not select a handler by name manually and then
rewrite the packet as if TraceMap proved it.

### No surfaces or chains

Confirm that the intended `.aspx`, `.ascx`, and `.master` files were included
in the scan scope. Then inspect inventory/build gaps. An empty section is not
proof that the application lacks that behavior.

## Fail-closed errors

| Error/classification | Meaning and next action |
| --- | --- |
| `webforms-modernization requires --index <index.sqlite>` | Supply the scanner's `index.sqlite`. |
| `webforms-modernization requires --out <directory>` | Supply a new packet directory. |
| `unsupported webforms-modernization option(s)` | Correct the option name; unknown options are rejected. |
| `WebFormsModernizationIndexUnavailable` | The index path does not exist. |
| `WebFormsModernizationIndexUnsupported` | The SQLite schema is not a supported single-scan index, or the file is unreadable/incompatible. Do not use a combined index. |
| `WebFormsModernizationSnapshotInvalid` | The index does not contain exactly one scan manifest row. Rescan one repository/commit. |
| `WebFormsModernizationScanIdentityUnavailable` | Required scan identity is absent. Regenerate the scan with the current scanner. |
| `WebFormsModernizationCommitIdentityUnavailable` | The recorded commit is not a supported 40- or 64-character hexadecimal SHA. Fix the source/scan boundary; do not substitute a label. |
| `WebFormsModernizationSourceIdentityMismatch` | Facts disagree with the scan repository, scan ID, or commit. Treat the index as incompatible or corrupted and rescan. |
| `WebFormsModernizationOutputExists` | Choose a new output path. The command never overwrites a prior packet. |
| `WebFormsModernizationOutputInvalid` | Choose a normal directory path with a valid parent. |
| `Web Forms modernization bounds must be positive` | Correct any zero or negative bound. |

Do not bypass these errors by editing `index.sqlite`, copying manifest rows, or
manually changing generated JSON.

## Privacy, sharing, and retention

The packet deliberately omits source snippets, raw SQL, configuration values,
URLs, connection strings, credentials, source values, repository remotes, and
absolute local paths. It still contains private repository-relative paths,
static identifiers, hashes, topology, and modernization evidence.

Therefore:

- do not commit generated scans or packets;
- do not upload them to public issues, public PRs, public AI services, or
  unapproved storage;
- do not paste raw packet rows into public TraceMap fixtures;
- share only through owner-approved internal channels;
- reproduce product defects with independent synthetic fixtures before
  contributing fixes publicly;
- apply the owner's retention and deletion rules to the scan, packet, repeat
  packet, and any derived workbook or summary.

If a sanitized summary is needed, prefer counts by coverage and gap
classification. Review even that summary before sharing; identifiers and rare
counts can still disclose architecture.

## Non-claims

The packet does not prove:

- runtime reachability or execution;
- page rendering, control construction, postback, event firing, or event order;
- validation, authentication, authorization, session behavior, or persistence;
- service availability, SQL execution, database state, or production use;
- complete workflows, business intent, feature parity, or test completeness;
- migration effort, cloud readiness, target architecture, safety to change, or
  release approval.

Keep these limitations with every review, workbook, plan, or presentation
derived from the packet.

## Related documentation

- [Language adapter contract](LANGUAGE_ADAPTER_CONTRACT.md)
- [Validation guide](VALIDATION.md)
- [Self diagnostics](SELF_DIAGNOSTICS.md)
- [Static HTML evidence explorer](STATIC_HTML_EVIDENCE_EXPLORER.md)
