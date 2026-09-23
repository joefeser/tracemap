# TraceMap Validation Guide

This guide defines the repeatable checks used to validate language adapters and cross-index analysis. It complements `docs/ACCEPTANCE.md`: acceptance defines expected behavior, while this file describes the concrete sample and open-source smoke set.

For an operator-oriented multi-repository Angular/.NET scan, combination, and
interaction-query workflow, see
[Angular and .NET interaction mapping](ANGULAR_DOTNET_INTERACTION_RUNBOOK.md).

TraceMap validation must stay deterministic and evidence-backed. Do not add LLM calls, embeddings, or prompt-based classification to validation.

## Required Matrix

Every language adapter should have:

| Check | Purpose |
| --- | --- |
| local modern sample | proves full semantic path when compiler/project loading works |
| local broken sample | proves syntax fallback and reduced coverage labels |
| reducer fixture | proves contract delta matching through shared facts/index schema |
| SQLite relationship queries | proves `call_edges`, `object_creations`, `argument_flows`, symbols, and relationship tables are populated when facts exist |
| value-origin flow queries | proves direct parameter forwarding, bounded local aliases, and unique constructor field origins are represented without crossing ambiguous boundaries |
| integration facts | proves HTTP/API, config, SQL/DB, serializer, and package/dependency facts where supported |
| combine/report/paths/reverse/export smoke | proves shared schema compatibility, combined dependency reporting, static dependency path queries, and reverse dependency-surface queries across adapters |
| public OSS smoke | proves larger real-world repos complete without unchecked assumptions |
| private-path guard | proves generated docs/scripts do not leak developer-local paths |
| artifact conformance | proves manifest/fact shape, registered rules, extractor provenance, minimum SQLite columns, and JSONL-to-SQLite parity |

## Required Local Commands

Run these before opening or updating a PR that changes scanner behavior:

The build must not introduce compiler or analyzer warnings. Resolve warnings before pushing the PR; narrowly suppress a proven tool false positive only at the affected call and protect the intended behavior with a test.

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm run check --prefix src/typescript
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
PYTHON_BIN=/tmp/tracemap-python-venv/bin/python ./scripts/smoke-python-endpoints.sh
python3 scripts/test_validate_adapter_artifacts.py
./scripts/check-private-paths.sh
```

## Web Forms large-index report memory

For changes to packet input loading or serialization, run the memory regressions
alongside the full .NET suite:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter 'FullyQualifiedName~WebFormsReportMemoryTests'
TRACEMAP_MEMORY_TEST_FULL_READER=1 dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --no-build --filter 'FullyQualifiedName~Large_repetitive_fact_payload' \
  --logger 'console;verbosity=detailed'
TRACEMAP_MEMORY_TEST_ROWS=1000000 dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --no-build --filter 'FullyQualifiedName~Large_repetitive_fact_payload' \
  --logger 'console;verbosity=detailed'
```

The fixture inserts 1 KiB unused syntax properties directly into SQLite rather
than building a million in-memory test facts. Normal CI uses 100,000 noise rows;
the opt-in scale is clamped to 100,000–2,000,000. The full-reader comparison is
opt-in because it deliberately exercises the old allocation-heavy reader. Run
it in its own test process; do not enable it for the million-row check on a
memory-constrained host.

Assertions cover byte-equivalent path/provenance output, an unchanged index
hash, global symbol collisions, unknown/declared surfaces, referenced supporting
IDs, oversized rows, fact/edge/text/snapshot admission limits, explicit partial
coverage without incomplete-graph absence conclusions, 518 independent roots,
streamed JSON byte parity, and cancellation without partial publication.

The test logs retained text/row counts and process-wide managed allocation deltas
around each report call. These are not live heap or OS working-set quotas.
Temporary row allocations may grow with visited rows even while retained graph
input stays constant. Record process peak RSS separately with a platform profiler
and describe whether it includes the test runner, fixture construction, and child
processes. Synthetic results do not prove that an owner's full private index fits
the default limits; the existing-index Windows rerun remains required.

## Public Demo Workflow

Run the public demo when validating the open-source walkthrough or generated public artifacts:

```bash
./scripts/demo-public.sh
./scripts/demo-public.sh .tracemap-demo
```

The default demo uses only checked-in samples. It does not clone public repositories, read private repositories, call external analysis services, query package registries, or run vulnerability/license/compatibility analysis. First-run build restore may still need network access for local toolchains such as NuGet or npm.

Current default behavior:

- checks `git`, `.NET`, `node`, and `npm`
- builds the .NET solution and TypeScript adapter
- scans `samples/modern-sample`
- scans `samples/endpoint-server-aspnet`
- scans `samples/typescript-modern-sample`
- scans `samples/endpoint-client-angular`
- scans `samples/public-demo/before`
- scans `samples/public-demo/after`
- combines the endpoint stack with labels `public-ts-client` and `public-dotnet-server`
- combines a mixed stack with labels `public-dotnet-modern`, `public-dotnet-server`, `public-ts-modern`, and `public-ts-client`
- combines before/after public-demo snapshots with label `public-demo-api`
- runs the combined dependency report and asserts endpoint evidence from the combined report
- runs targeted `tracemap paths`, `tracemap route-flow`, and `tracemap reverse` over the generated endpoint stack
- generates `portfolio-manifest.json` from generated combined indexes and runs `tracemap portfolio`
- runs `tracemap diff`, `tracemap impact`, and `tracemap release-review` over the generated public-demo before/after snapshots
- writes `demo-summary.md` and `demo-summary.json`
- runs a generated-output sentinel scan over public-shareable summaries and reports
- marks Python as `not_requested` unless `--include-python` is passed; requested Python scanning is currently `deferred` to a follow-up slice
- marks JVM as `unavailable` when Java 21 is absent

For single-scan symbol change-impact work, run the focused artifact/query contract tests in addition to the full .NET suite:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter 'FullyQualifiedName~ReverseImpact'
```

The focused suite must cover a real scan persisted through `SqliteIndexWriter`, read-only artifact loading, exact snapshot identity, direction preservation, deterministic JSON, ambiguous selectors, bounded truncation, contained-member expansion, and explicit semantic HTTP/database opt-ins. This command is separate from the combined-index `tracemap reverse` smoke path.

The release-review section is available as a deterministic static evidence packet over the public-demo before/after snapshots. Contract-delta, SQL/schema, package compatibility, path context, and reverse context sections remain not requested, unavailable, or deferred inside the release-review report unless compatible inputs are explicitly supplied.

Troubleshooting:

- If the demo refuses an in-repo output directory, use `.tracemap-demo/` or add a generic ignored output path before running the script.
- If .NET or TypeScript build restore fails, run the build/test commands above directly to restore local toolchain dependencies and inspect their native diagnostics.
- Reduced sample scan and report coverage is expected for samples that intentionally rely on syntax fallback or missing framework packages. The summary labels those sections as partial while preserving rule-backed evidence counts.

## Local Review Progress Diagnostics Smoke

Run this when changing `LocalReviewCommand`, `ScanProgressReporter`,
`ScanEngine` progress instrumentation, or the Roslyn cancellation seams:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter 'FullyQualifiedName~ScanProgressDiagnosticsTests|FullyQualifiedName~LocalReviewCommandTests'
```

Expected coverage:

- progress lines reach the immediate progress console (stderr) before a
  blocking scan completes, and never route through the buffered scan output
  capture;
- heartbeats every 15 seconds report only categorical stage, elapsed
  milliseconds, last completed stage, and sequence, with bounded checkpoint
  history (heartbeats excluded, at most 32 events);
- the sanitized checkpoint contains no repository, project, path, or symbol
  values supplied by adversarial fixtures;
- the durable checkpoint exists before final publication and survives
  cancellation and timeout with the exact last successful categorical stage;
- `--timeout-seconds` returns the typed `LOCAL_REVIEW_TIMEOUT` failure and
  never publishes a successful review; invalid bounds and unsafe progress
  paths fail closed before scanning;
- solution, project, and compilation ordinals are deterministic across runs;
- enabled diagnostics leave deterministic evidence bytes unchanged (facts and
  report byte-identical; manifest and receipt compared after normalizing the
  pre-existing `scannedAt` and stage-duration observations).

For a manual synthetic Web Forms smoke with diagnostics enabled (the fixture
is synthesized on the fly; tests build equivalent ones):

```bash
mkdir -p /tmp/tracemap-webforms-smoke && cd /tmp/tracemap-webforms-smoke
cat > Sample.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
</Project>
EOF
printf '<%%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %%><asp:Button ID="Save" runat="server" OnClick="Save_Click" />' > Default.aspx
printf 'namespace Sample; public class Default { protected void Save_Click(object sender, System.EventArgs e) { } }' > Default.aspx.cs
git init -q . && git add . && git -c user.name=TraceMap -c user.email=smoke@example.invalid commit -qm smoke

dotnet run --project <repository-root>/src/dotnet/TraceMap.Cli -- local-review run \
  --repo /tmp/tracemap-webforms-smoke \
  --out /tmp/tracemap-review-smoke \
  --webforms-modernization \
  --diagnostic-progress /tmp/tracemap-review-progress.json \
  --timeout-seconds 600
```

The run must emit `tracemap-progress ...` lines to stderr immediately, keep
`/tmp/tracemap-review-progress.json` valid per
`docs/contracts/tracemap-scan-progress.v1.schema.json` throughout, and leave
the checkpoint behind with `local-review-publication` completed on success.
It must also leave
`/tmp/tracemap-review-progress.json.performance.json` valid per
`docs/contracts/tracemap-scan-performance.v1.schema.json`, with complete
specialized-extractor timing coverage on success, a bounded heartbeat count,
and no source identity. The slowest extractor is reported only from a
retained start/terminal pair. Both receipts are operational observations, not
evidence facts.
An empty `/tmp/tracemap-review-smoke` during execution is expected: work is
staged in a hidden sibling until the atomic publication rename.

## MSBuild Binary-Log Evidence

For changes to explicit `.binlog` ingestion, run the focused synthetic suite:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~MsBuildBinlogExtractorTests
```

For a pinned local product smoke, generate a binary log from the checked-in
modern sample and supply it explicitly with the current commit:

```bash
smoke_root="$(mktemp -d)"
dotnet build samples/modern-sample/ModernSample.csproj \
  -bl:"$smoke_root/modern-sample.binlog"
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo samples/modern-sample \
  --out "$smoke_root/scan" \
  --binlog "$smoke_root/modern-sample.binlog" \
  --binlog-commit-sha "$(git rev-parse HEAD)"
```

Confirm `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`
contain only the TraceMap-owned allowlist: artifact SHA-256, recorded build
result, repository-relative project/graph identities, safe diagnostic
code/severity/location, aggregate counts, provenance, limitations, and
categorical gaps. Do not publish or retain the raw binlog. The smoke observes
artifact contents only; it does not authenticate the artifact, prove the build
ran at the declared commit, prove tests passed, or grant deployment/release
approval.
- If endpoint, path, reverse, or portfolio assertions fail, inspect the generated JSON reports under `reports/`; accepted evidence rows must include rule IDs, evidence tiers, source labels, commit SHAs, and supporting fact or edge IDs where the report exposes them.
- If the generated public-report sentinel fails, inspect the relative file paths and category it prints. Keep scan manifests, SQLite files, facts, and logs local-only; public summaries and reports must use hashes, labels, or relative paths.

Generated outputs under `scans/**`, SQLite files, facts, manifests, and logs are local-only artifacts and may contain temporary execution details. Public-shareable `demo-summary.*` and `reports/**/*.md|json` artifacts must not contain raw scripts, SQL, snippets, config values, connection strings, raw URLs with credentials, private paths, or local absolute paths.

Use `.tracemap-demo/` for an in-repo output root; it is ignored by git. Other in-repo output directories are rejected unless `git check-ignore` proves they are ignored.

For JVM CLI smoke, also run:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
```

For Swift adapter changes, run the Swift package and checked-in sample scans.
The Swift package currently resolves a pinned SwiftSyntax dependency for
source declaration/call extraction, so first-run validation may need network
access for SwiftPM dependency restore:

```bash
swift build --package-path src/swift
swift test --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-dependency-surfaces --out /tmp/tracemap-swift-dependency-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-http-api-client-surfaces --out /tmp/tracemap-swift-http-api-client-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-ui-surfaces --out /tmp/tracemap-swift-ui-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-storage-data-surfaces --out /tmp/tracemap-swift-storage-data-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-diagnostics-reduced --out /tmp/tracemap-swift-diagnostics-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-reduced --out /tmp/tracemap-swift-metadata-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-unsupported --out /tmp/tracemap-swift-metadata-unsupported
swift run --package-path src/swift tracemap-swift scan --repo samples/no-swift --out /tmp/tracemap-no-swift
test -f /tmp/tracemap-swift-package-basic/scan-manifest.json
test -f /tmp/tracemap-swift-package-basic/facts.ndjson
test -f /tmp/tracemap-swift-package-basic/index.sqlite
test -f /tmp/tracemap-swift-package-basic/report.md
test -f /tmp/tracemap-swift-package-basic/logs/analyzer.log
test -f /tmp/tracemap-swift-dependency-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-http-api-client-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-http-api-client-surfaces/index.sqlite
test -f /tmp/tracemap-swift-http-api-client-surfaces/report.md
test -f /tmp/tracemap-swift-ui-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-ui-surfaces/index.sqlite
test -f /tmp/tracemap-swift-ui-surfaces/report.md
test -f /tmp/tracemap-swift-storage-data-surfaces/scan-manifest.json
test -f /tmp/tracemap-swift-storage-data-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-storage-data-surfaces/index.sqlite
test -f /tmp/tracemap-swift-storage-data-surfaces/report.md
test -f /tmp/tracemap-swift-storage-data-surfaces/logs/analyzer.log
test -f /tmp/tracemap-swift-diagnostics-reduced/scan-manifest.json
test -f /tmp/tracemap-swift-diagnostics-reduced/facts.ndjson
test -f /tmp/tracemap-swift-diagnostics-reduced/index.sqlite
test -f /tmp/tracemap-swift-diagnostics-reduced/report.md
test -f /tmp/tracemap-swift-diagnostics-reduced/logs/analyzer.log
test -f /tmp/tracemap-swift-metadata-reduced/scan-manifest.json
test -f /tmp/tracemap-swift-metadata-unsupported/scan-manifest.json
test -f /tmp/tracemap-no-swift/scan-manifest.json
dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-package-basic/index.sqlite --out /tmp/tracemap-swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-package-basic/index.sqlite --label swift --out /tmp/tracemap-swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-combined.sqlite --out /tmp/tracemap-swift-report
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-storage-data-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-storage-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-storage-combined.sqlite --out /tmp/tracemap-swift-storage-report
scripts/smoke-swift-route-flow.sh /tmp/tracemap-swift-route-flow-smoke
./scripts/check-private-paths.sh
git diff --check
```

For any adapter output produced by the commands above, run:

```bash
python3 scripts/validate-adapter-artifacts.py <scan-output>
```

## Microsoft Access Adapter Smoke

Source-neutral Access design-evidence contract changes can be validated on
macOS with synthetic bundles and do not require Access, COM, or a real
database:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~AccessDesignEvidenceReaderTests
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~AccessDesignEvidenceCompositionTests
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~Access
```

These tests validate the protected local input contract, immutable base-scan
binding, hash-only projection, deterministic enriched artifacts, validated
coordinates, explicit gaps, and downstream report/combine/docs/vault/release
review/local-review preservation. They do not prove that any Windows exporter
is safe or complete, and they do not claim that Access behavior, forms,
reports, VBA, or macros were executed or runtime-reachable.

A conforming owner-controlled bundle can be composed on macOS without the
database file:

```bash
dotnet run --project src/dotnet/TraceMap.Access.Cli -- enrich-design \
  --base-scan <completed-access-scan> \
  --design-evidence <protected-owner-controlled-directory> \
  --out <new-enriched-output-directory>
python3 scripts/validate-adapter-artifacts.py <new-enriched-output-directory>
```

The base scan and protected input are immutable inputs. The output must be a
new, non-overlapping path. Protected source and identities are consumed only
in-process; the standard output remains hash-only.

Functional form/report metadata changes additionally validate lookup,
subform/subreport, report group/sort, and query-output-field candidates:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter "FullyQualifiedName~AccessUiProjectionTests|FullyQualifiedName~AccessScreenDataFlowTests|FullyQualifiedName~AccessDesignEvidence"
```

An owner may explicitly create a separately deletable hidden-local identity
projection:

```bash
dotnet run --project src/dotnet/TraceMap.Access.Cli -- identity-project \
  --base-scan <completed-access-scan> \
  --design-evidence <protected-owner-controlled-directory> \
  --out <new-hidden-local-directory>
```

This output is not a standard artifact and is not accepted by combine, vault,
public-site, or release-publication workflows. It may contain owner-local
direct identifiers, but never raw design text, inline SQL, VBA, macro bodies,
credentials, or local paths.

Source-neutral screen-to-data composition is also Mac-only and reads only the
completed enriched index:

```bash
dotnet run --project src/dotnet/TraceMap.Access.Cli -- flow \
  --index <new-enriched-output-directory>/index.sqlite \
  --out <new-flow-output-directory>
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~AccessScreenDataFlowTests
```

Verify `access-flow.md` and `access-flow.json` contain deterministic bounded
candidate paths with supporting fact/rule/tier/commit/span/extractor/coverage/
limitation provenance. Missing startup identity, item-level evidence, dynamic
targets, unresolved declarations, cycles, and limits must remain explicit
gaps. Raw names, SQL, VBA, expressions, macro bodies, connections, credentials,
local paths, and customer identities must remain absent. This report does not
prove startup selection, event firing, user navigation, runtime reachability,
execution, row access, connectivity, correctness, completeness, or approval.

Conservative copy/clone candidate composition is also Mac-only and reuses the
same completed index and flow evidence:

```bash
dotnet run --project src/dotnet/TraceMap.Access.Cli -- copy-clone \
  --index <new-enriched-output-directory>/index.sqlite \
  --out <new-copy-clone-output-directory>
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~AccessCopyCloneCandidateTests
```

Verify `access-copy-clone.md` and `access-copy-clone.json` classify only
supported action-query shapes as `Candidate` or `NeedsReview`, never infer from
names, and preserve exact safe provenance. Source/target direction, field
correspondence, parent/child sequencing, generated keys, dynamic behavior, and
flow absence remain explicit gaps. The output must omit raw SQL, names, VBA,
macro bodies, values, row counts, connection material, customer identity, and
local paths. A candidate does not prove copying, cloning, business intent,
execution, row equivalence, correctness, completeness, or safety to run.

Access extraction requires Windows with installed Microsoft Access/DAO. Run it
in an isolated local VM with networking and broad host sharing disabled. Stage
only the self-contained CLI binaries and checked-in validation scripts through
a scoped read-only share; use a guest-local Git repository and output root.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/access-validation/Invoke-AccessSmoke.ps1 `
  -AccessCli <guest-local-tracemap-access.exe> `
  -TraceMapCli <guest-local-tracemap.exe> `
  -Generator <guest-local-New-SyntheticAccessFixture.ps1> `
  -SmokeRoot <guest-local-smoke-root> `
  -Phase9CheckpointPath <guest-local-sanitized-checkpoint.json>
```

The smoke creates and locally commits a disposable zero-row `.accdb`, removes
the linked source before scanning, runs sequential and concurrent scans, and
checks deterministic facts/report/log output, the unchanged original database,
startup-canary non-execution, protected-marker suppression, standard artifacts,
index export, combine, and combined reporting. Do not commit the generated
database or scan artifacts. Access evidence remains hidden, reduced static
design evidence: it does not prove row contents, query/macro/VBA execution,
runtime reachability, linked-source availability, permissions, production
state, release approval, or that a change is safe.

When `-Phase9CheckpointPath` is supplied, it must be outside the disposable
smoke root. After each gate, the smoke atomically creates an immutable,
monotonically sequenced snapshot containing only the closed Phase 9 status,
stage, failure classification, booleans, and protected-output match count. It
also updates the unnumbered latest-file pointer on a best-effort basis. Consumers
must select the valid snapshot with the highest `checkpointSequence` rather than
trusting an older unnumbered file. Checkpoints never store database hashes,
names, paths, exception text, or protected values. The harness also validates
the Access report, combined-index evidence-doc projection, the structured vault
unsupported-consumer gap, and the composed release-review Access design section
with explicit coverage gaps. Cleanup may
remove the smoke root while retaining this sanitized checkpoint. Delete the
checkpoint family only after its issue comment is confirmed posted.

The harness script, generator, and both CLI executables must also be staged
outside `-SmokeRoot`; the harness deletes that root before generation. Preflight
rejects a missing tool or a tool inside the disposable root before deletion and
records only `tool-missing` or `tool-inside-disposable-root`. Generator failures
use the closed classifications `generator-process-failed`,
`fixture-database-missing`, or `generation-canary-fired`.

Fixture generation runs in an output-suppressed child PowerShell process so an
Access/COM host failure cannot terminate the checkpoint coordinator. Private
working paths pass through the child's inherited environment rather than its
command line. After generation, the
checkpoint advances to `fixture-provenance`. Git
initialization/configuration/staging/commit,
the bounded incompatible-input fixture, baseline hashing, and boundary cleanup
each use a closed `fixture-*` failure classification before product scanning.

For an explicitly authorized representative `.accdb` or `.mdb`, use the
separate committed workflow; never substitute the synthetic harness or an
agent-authored probe:

```powershell
.\scripts\access-validation\Invoke-AccessRepresentativeSmoke.ps1 `
  -AccessCli <durable-tool-root>\tracemap-access.exe `
  -TraceMapCli <durable-tool-root>\tracemap.exe `
  -DatabasePath <authorized-local-database> `
  -ScratchRoot <restricted-disposable-root> `
  -CheckpointBasePath <durable-sanitized-checkpoint-base> `
  -InputExplicitlyAuthorized
```

`-ScratchRoot` must name a new, nonexistent, non-filesystem-root path beneath
the operator's restricted disposable parent. The harness refuses an existing
path instead of recursively deleting caller-owned contents.

The representative workflow hashes the original in memory, stream-copies it
under a generic name into a disposable no-remote Git repository, scans two
sequential and two concurrent working copies, validates standard artifacts and
the report/combine/docs/vault/release-review contracts, actively observes the
Access process for any visible surface during every scan, validates manifest
and per-fact rule/evidence/commit/extractor provenance, and persists only
allowlisted booleans, counts, labels, and gaps. It never records the input path,
name, hash, object identities, SQL, VBA, macro bodies, expressions, connections,
or exception text in its checkpoint. Raw scratch remains disposable; retain the
sanitized checkpoint family until its issue result is confirmed posted.

For ordinary single-file product use, `tracemap-access scan-file` owns the same
verified generic-copy and disposable local-commit ceremony internally; see
[`ACCESS_FILE_FIRST_SCAN.md`](ACCESS_FILE_FIRST_SCAN.md). Platform-neutral tests
must compare the deterministic internal commit and fact IDs across two synthetic
byte inputs, prove no remote, verify local-snapshot labeling and original
path/name suppression, and cover original mutation plus success, failure,
cancellation, and cleanup paths. A Windows smoke must use only the generated
zero-row synthetic fixture unless a representative input is separately
authorized.

For source builds in an isolated local Parallels Windows VM, follow
[`ACCESS_PARALLELS_SOURCE_RUNNER.md`](ACCESS_PARALLELS_SOURCE_RUNNER.md).
The host runner requires every configured VM network adapter to be disabled
and exactly two enabled host shares: read-only `access_input` and read/write
`access_output`. The guest attests an exact clean checkout with no Git remote,
pinned Git/.NET launcher hashes, reparse-free required path chains, and an
offline toolchain/package cache. Guest attestations are not independent host
proof of an uncompromised Windows runtime or complete SDK tree. The runner
supports `doctor`, `build`, `synthetic`, and metadata-producer validation. It does not accept a
representative database or change the Access extraction boundary.

For Access design-review composition changes, run the focused Access and
release-review tests and verify both single and combined indexes produce an
`Access Design Evidence` section with `available` status, upstream rule/tier/
commit/extractor/span provenance, allowlisted categorical metadata, and
structured count-only coverage gaps. Verify `--scope access-evidence` selects
the section. Protected names, SQL, hashes, connections, VBA, macro bodies,
captions, expressions, local paths, and infrastructure identities must remain
absent. These read-side composition changes do not require a new Windows probe
when Access COM, the product reader, and fixture generation are unchanged.

For Access local-review bundle changes, also run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter AccessLocalReviewBundleTests
```

Verify `access-review create` produces the Access-only release review,
`hidden-local` explorer, deterministic `access-review-manifest.json`, and
relative-link README. Repeat to two output directories and compare every file.
Verify manifest hashes, explicit count-only UI/VBA/macro gaps, protected-value
suppression, overlap rejection, non-Access rejection, guarded `--force`, a
custom `--max-findings` bound, and explicit deterministic truncation when that
bound is reached.

When the isolated Windows + Access VM is available, run the synthetic smoke
with `-ReviewBundlePath <new-durable-local-review-directory>` outside the
disposable smoke root. The harness must validate the bundle, include it in the
protected-marker scan, preserve false canaries and the original hash, and leave
the extraction boundary unchanged. A representative run additionally requires
`-InputExplicitlyAuthorized`; see
`docs/ACCESS_LOCAL_REVIEW_BUNDLE.md`.

The repository CI runs the existing .NET, TypeScript, Python, JVM, and Swift
test suites, validates one real output per adapter, and combines all five
indexes. Local environments with only Apple Command Line Tools may build and
run the Swift scanner but lack the `XCTest` module needed by `swift test`; use
a full Xcode or Swift toolchain for that package-test command and do not treat
the smoke executable as a substitute for the CI test gate.

Expected Swift behavior: scans remain deterministic static evidence over
checked-in files, emit repo and commit SHA provenance, rule IDs, evidence
tiers, extractor versions, coverage labels, public-safe repo-relative paths,
and syntax-backed declarations, call candidates, construction candidates,
direct source-local symbol relationships, HTTP/API client surfaces, UI
surfaces, and storage/data surfaces where supported. Swift storage/data
evidence remains static metadata or syntax/text evidence and must not claim
runtime persistence, query execution, stored values, schema existence,
migration success, Keychain item presence, Realm live schema, production data,
or impact. Swift relationship facts remain syntax-backed and must not claim compiler semantic coverage, build
success, package compatibility, Xcode scheme behavior, simulator/device
behavior, runtime behavior, protocol witness selection, Objective-C dispatch,
dependency vulnerability/license/freshness, or impact. Generated Swift
artifacts must not contain raw source snippets, manifest snippets, plist
values, raw URLs, hostnames, local absolute paths, raw remotes, credentials,
secrets, or private labels. Swift route-flow smoke outputs remain
coverage-relative static evidence and must not claim runtime endpoint
reachability, app execution, auth behavior, deployment, traffic, or user
actions.

### Swift Real-World API-Client Smoke

For Swift adapter changes that affect project inventory, dependency metadata,
HTTP/API client surfaces, UI surfaces, storage/data surfaces, reduced coverage,
or public Swift demo evidence, run the opt-in real-world Swift smoke:

```bash
scripts/smoke-swift-real-world.sh /tmp/tracemap-swift-real-world-cache /tmp/tracemap-swift-real-world-smoke
```

The smoke clones pinned public repositories into the cache directory, scans
them with `tracemap-swift`, verifies required artifacts, and writes sanitized
local summaries under the output directory. Generated summaries use public
repository slugs, pinned commit SHAs, artifact labels, counts, rule IDs,
coverage labels, and limitations. They must not include local absolute paths,
clone URLs, raw remotes, raw source snippets, raw SQL, credentials, config
values, hostnames, private labels, or runtime observations.

Pinned Swift real-world samples:

| Label | Repository | Pinned SHA | Why included |
| --- | --- | --- | --- |
| `icecubesapp` | `Dimillian/IceCubesApp` | `9c05a720597b3ff13de2e241bf58d3fba0863c09` | SwiftUI Mastodon client with real federated API client and UI surface evidence |
| `mastodon-ios` | `mastodon/mastodon-ios` | `95ac4a6d726ebf9fa867036dbf9d72f0a4b5f534` | Official Mastodon iOS app with real backend/API client and mobile app structure evidence |
| `kickstarter-ios` | `kickstarter/ios-oss` | `203971bdf40f3a3a5071ce0c1fbc4eb3cad5b094` | Product iOS app with real backend/API client, view model, dependency, and persistence-adjacent evidence |

Use `TRACEMAP_SWIFT_REAL_WORLD_REPOS=icecubesapp` or a comma-separated label
list for a focused smoke while developing the harness. Use
`TRACEMAP_SKIP_BUILD=1` only after `swift build --package-path src/swift` has
already succeeded for the current checkout. Use
`TRACEMAP_SWIFT_REAL_WORLD_OFFLINE=1` only when the cache already contains the
pinned commits; offline mode rejects missing commits instead of fetching from
GitHub. Unknown focused labels fail the smoke before scanning, and generated
Markdown summaries include only samples scanned in the current invocation.

Expected Swift real-world behavior: scans complete without Xcode builds,
SwiftPM dependency resolution, simulators, devices, app execution, network
calls, credentials, auth flows, or production telemetry. The smoke proves
artifact generation and static evidence extraction over messy public apps. It
does not prove runtime endpoint reachability, backend compatibility, complete
app navigation, package compatibility, production use, or impact.

For query-pattern report rendering changes, inspect generated scan reports from the affected adapters:

```bash
rg -n "Query Patterns|SQL shape|Query builder|static shape evidence|runtime execution" <scan-output>/report.md
rg -n "fields none" <scan-output>/report.md
```

`fields none` is acceptable for query-builder facts with no extracted field metadata. SQL-shape facts should render derived operation/table/column/source/hash metadata instead, and reports must not render raw SQL text, literal values, unsafe identifiers, or developer-local absolute paths.

For legacy data metadata changes in the .NET adapter, run the focused extractor
tests plus the normal .NET scanner checks:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataMetadataExtractorTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Focused validation should cover DBML, EDMX, typed DataSet/TableAdapter,
NHibernate `.hbm.xml`, config provider metadata, generated-code linkage, legacy
data model identity keys, unrelated XSD gating, malformed XML, DTD/entity
rejection, deterministic fact IDs, report redaction, and SQLite property
redaction. Any local legacy smoke
must stay ignored/local-only and use neutral labels/counts only; do not commit
raw facts, SQLite indexes, analyzer
logs, raw SQL, connection strings, config values, raw remotes, private sample
names, local absolute paths, or source snippets.

For legacy data model surface-projection or `surfaceSubtype` reporting changes,
also run focused report/query/export coverage:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "LegacyDataModelDescriptorProjectionTests|CombinedDependencyReportTests|CombinedDependencyPathTests|CombinedRouteFlowTests|CombinedReverseQueryTests|CombinedDependencyDiffTests|VaultExportTests"
```

For combined dependency report, path-query, route-flow, reverse-query, diff, contract-diff, or snapshot-diff changes, run a combine/report/paths/route-flow/reverse/diff/contract-diff/snapshot-diff smoke over any two existing local scan outputs:
For combined change-impact changes, include the `impact` command in the same smoke.
For release-review changes, include `release-review` in the same smoke and verify `release-review.md` plus `release-review.json` are produced. For review-priority scoring changes, also run release-review with `--include-priority` and verify the Markdown Review Priority section plus JSON `reviewPriority` and `reviewPriorityRows` sidecar fields.

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index <first>/index.sqlite --label first \
  --index <second>/index.sqlite --label second \
  --out <tmp>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/combined.sqlite --out <tmp>/combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index <tmp>/combined.sqlite --out <tmp>/combined-paths
dotnet run --project src/dotnet/TraceMap.Cli -- route-flow --index <tmp>/combined.sqlite --from-source first --out <tmp>/route-flow
dotnet run --project src/dotnet/TraceMap.Cli -- property-flow --index <tmp>/combined.sqlite --property fact:<combinedFactId> --out <tmp>/property-flow
dotnet run --project src/dotnet/TraceMap.Cli -- reverse --index <tmp>/combined.sqlite --surface sql-query --to endpoints --out <tmp>/combined-reverse
dotnet run --project src/dotnet/TraceMap.Cli -- diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/combined-diff
dotnet run --project src/dotnet/TraceMap.Cli -- contract-diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/contract-diff
dotnet run --project src/dotnet/TraceMap.Cli -- snapshot-diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/snapshot-diff
dotnet run --project src/dotnet/TraceMap.Cli -- impact --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/combined-impact
dotnet run --project src/dotnet/TraceMap.Cli -- release-review --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/release-review
test -f <tmp>/combined-report/dependency-report.md
test -f <tmp>/combined-report/dependency-report.json
test -f <tmp>/combined-paths/paths-report.md
test -f <tmp>/combined-paths/paths-report.json
test -f <tmp>/route-flow/route-flow-report.md
test -f <tmp>/route-flow/route-flow-report.json
test -f <tmp>/property-flow/property-flow-report.md
test -f <tmp>/property-flow/property-flow-report.json
test -f <tmp>/combined-reverse/reverse-report.md
test -f <tmp>/combined-reverse/reverse-report.json
test -f <tmp>/combined-diff/diff-report.md
test -f <tmp>/combined-diff/diff-report.json
test -f <tmp>/contract-diff/contract-diff-report.md
test -f <tmp>/contract-diff/contract-diff-report.json
test -f <tmp>/snapshot-diff/snapshot-diff-report.md
test -f <tmp>/snapshot-diff/snapshot-diff-report.json
test -f <tmp>/combined-impact/impact-report.md
test -f <tmp>/combined-impact/impact-report.json
test -f <tmp>/release-review/release-review.md
test -f <tmp>/release-review/release-review.json
```

For route-flow SQL-context composition changes, assemble a temporary repository
from the checked-in public demo endpoint and SQL operator-runbook fixtures. This
keeps the endpoint/data surface and the cataloged SQL context under one source
label without adding a purpose-built fixture:

```bash
mkdir -p <tmp>/sql-route-repo
cp samples/public-demo/after/OrdersController.cs \
  samples/public-demo/after/PublicDemoAfter.csproj \
  samples/sql-operator-runbook/setup.sql \
  <tmp>/sql-route-repo/
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo <tmp>/sql-route-repo --out <tmp>/sql-route-scan
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index <tmp>/sql-route-scan/index.sqlite --label public-demo \
  --out <tmp>/sql-route-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- route-flow \
  --index <tmp>/sql-route-combined.sqlite \
  --route "GET /api/public/orders/{orderId}" --to-surface sql-query \
  --out <tmp>/sql-route-flow
rg -n 'sql-context|database.sql.context.declaration.v1|contextOrder|permissionPrerequisites|stopConditions|upstreamExtractorVersions' \
  <tmp>/sql-route-flow/route-flow-report.json
! rg -i 'private-host-leak-sentinel|private-password-leak-sentinel|raw-scheduled-command-leak-sentinel|safe to run' \
  <tmp>/sql-route-flow/route-flow-report.md \
  <tmp>/sql-route-flow/route-flow-report.json
```

The SQL-context groups must remain additive and ordered between query and data
surface context. They preserve cataloged rule/tier/coverage/span/commit/
extractor/supporting-fact provenance, categorical context transitions,
permission prerequisite statuses, and stop-condition codes. They do not prove
SQL execution, runtime reachability, database state, permission effectiveness,
release approval, or execution safety.

For release-review SQL runway composition changes, scan the checked-in operator
runbook sample and use its index as the selected after snapshot. Verify the
separate `SQL Runway Evidence` section is `available`, preserves rule/tier/
coverage/span/commit/extractor/supporting-fact provenance, and does not expose
the planted host, password, or scheduled-command sentinels:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/sql-operator-runbook --out <tmp>/sql-after
dotnet run --project src/dotnet/TraceMap.Cli -- release-review --before <tmp>/sql-after/index.sqlite --after <tmp>/sql-after/index.sqlite --out <tmp>/sql-release-review
rg -n "SQL Runway Evidence|Status: `available`|database.sql.secret-bearing-step.v1" <tmp>/sql-release-review/release-review.md
! rg -i "private-host-leak-sentinel|private-password-leak-sentinel|raw-scheduled-command-leak-sentinel|safe to run" <tmp>/sql-release-review/release-review.md <tmp>/sql-release-review/release-review.json
```

For docs-export changes, run the focused tests plus the normal .NET and safety
gates:

```bash
dotnet test src/dotnet/TraceMap.sln --filter EvidenceDocs
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

When validating against a local combined index, generate docs into ignored
temporary storage only:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- docs-export --index <tmp>/combined.sqlite --out <tmp>/evidence-docs
test -f <tmp>/evidence-docs/manifest.json
test -f <tmp>/evidence-docs/chunks.jsonl
test -f <tmp>/evidence-docs/README.md
```

Docs-export output must preserve rule IDs, evidence tiers, source labels,
commit SHAs, coverage labels, supporting IDs, gaps, and limitations. It must
not contain raw SQL, raw config values, connection strings, raw URLs, endpoint
addresses, local absolute paths, raw remotes, source snippets, credentials,
private sample names, production data, prompt text, embeddings, vector database
configuration, or natural-language answer templates. Demo/public output
requires reviewed claim metadata plus `--date YYYY-MM`; hidden output without a
date uses `local-only`.

For property-flow changes, run the focused .NET and TypeScript tests plus the
normal report safety gates:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PropertyFlowTests
npm run check --prefix src/typescript
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

For semantic ASP.NET Core MVC/Razor Pages model-binding producer changes, also
run the signed-metadata, reducer-isolation, and reporter-isolation regressions:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter "FullyQualifiedName~RazorSemanticModelBindingTests|FullyQualifiedName~Reduce_excludes_semantic_razor_binding|FullyQualifiedName~Property_flow_ignores_semantic_razor_targets"
```

The positive fixture uses the locked `net10.0` ASP.NET Core shared-framework
metadata. Confirm admission records the exact framework metadata type, assembly
name, and Microsoft public-key token; framework version is fixture provenance,
not a product trust key. Source-declared lookalikes must produce no Tier1 target.
The resulting semantic facts remain available in NDJSON/SQLite but must not
change generic reducer or legacy name-based property-flow output until the
exact-identity composition slice lands.

For direct property-mapping producer changes, also run the producer, collision,
direction, bounds, and isolation regressions:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter "FullyQualifiedName~PropertyMappingTests|FullyQualifiedName~Reduce_excludes_semantic_property_mapping|FullyQualifiedName~Property_flow_ignores_direct_property_mapping"
```

Expected behavior: supported shapes emit `PropertyMappingDeclared` Tier1 facts
with canonical source/target property identities, containing-type and method
identities, closed mapping shape, direction, span, coverage label, and fixed
limitations; transforming, dynamic, ambiguous, conversion-requiring, indexer,
and compound-assignment counterparts fail closed as rule-backed
`AnalysisGap` rows with closed PascalCase gap kinds and `shapeState` values,
never storing expression text; per-method/per-document bounds fold suppressed
emissions into one aggregated `PropertyMappingTruncated` gap whose
`shapeState=truncation` row remains inside the 100-gap document bound; record
`with` initializers and getter-only, inaccessible, or otherwise invalid target
writes fail closed rather than producing Tier1 evidence; same-name
cross-assembly properties keep distinct canonical identities through extern
aliases; partial/non-compiling projects keep healthy-file evidence byte-stable
while the manifest remains truthfully reduced. Mapping facts and gaps stay
available in NDJSON/SQLite but must not change generic reducer or legacy
name-based property-flow output until exact-ID composition lands in PR 3.

Expected behavior: Angular template fixtures emit `UiTemplateBinding`,
`UiFormControlBinding`, `UiEventBinding`, `UiTemplateVariable`, and
`UiBindingGap` facts with rule IDs and safe metadata only; Razor fixtures emit
`RazorBinding`, `RazorFormTarget`, and `RazorBindingGap` facts; property-flow
reports reject single-language indexes, keep input SQLite files read-only, emit
route-flow/schema gaps where needed, and write deterministic Markdown/JSON
without source snippets, raw SQL, raw URLs, connection strings, secrets, remotes,
or local absolute paths. If `--observed-evidence <path>` is used, the observed
rows remain demo metadata only, reject unsafe keys/values, and do not change
static lineage classifications.

For value-origin flow changes, also inspect the source `parameter_forward_edges` table from a semantic .NET sample or focused fixture:

```bash
sqlite3 <out>/index.sqlite "select source_method_symbol, source_parameter_symbol, target_method_symbol, target_parameter_name, rule_id from parameter_forward_edges order by source_method_symbol, target_method_symbol;"
```

Expected behavior: direct parameter forwarding is present, same-method aliases are bounded to 3 hops, and ambiguous constructor/member origins are omitted or represented as gaps by future reporting slices rather than being promoted to forwarding edges.

For callback/lambda/async boundary changes, inspect semantic .NET fixtures for `CallbackBoundary` and `AsyncBoundary` facts under `csharp.semantic.flowboundary.v1`. Expected behavior: direct calls inside callback bodies may still emit normal `ArgumentPassed` rows, captured outer parameters/locals are labeled review-tier boundary evidence, expression-tree lambdas use expression-tree metadata instead of delegate-callback metadata, and event subscriptions, delegate arguments on invocations/object creation, `await`, `await foreach`, `await using`, task scheduling/continuation calls, thread-pool queueing calls, and iterator `yield` are boundaries rather than proof of runtime invocation, ordering, async disposal, async-stream enumeration, or task completion.

For TypeScript/JVM/Python value-origin adapter alignment, inspect adapter fixtures for shared `ArgumentPassed` role properties:

- TypeScript semantic facts should include `argumentSymbolId`, `argumentSymbolLanguage`, `argumentSymbolDisplayName`, `parameterSymbolId`, `parameterSymbolLanguage`, and `parameterSymbolDisplayName` when the compiler resolves both sides.
- Java semantic facts should include parameter role properties for resolved calls and argument role properties only when javac resolves the argument expression to a symbol.
- Python AST facts should mark unresolved callee parameters with `parameterIdentityStatus=unresolvedOrdinalPlaceholder` while still emitting shared role metadata for syntax-visible arguments, local aliases, and `self.field = parameter` aliases.

For changes to `combine`, `report`, `paths`, `reverse`, endpoint extraction, call edges, SQL/query extraction, or dependency-surface projection, run the public combined-path smoke:

```bash
./scripts/smoke-combined-paths.sh
```

The smoke is sample-only and does not clone repositories or read external application paths. It scans `samples/endpoint-client-angular` and `samples/endpoint-server-aspnet`, combines them as `sample-client` and `sample-server`, runs `report`, runs default and targeted `paths` queries, runs a reverse SQL-surface query, and verifies:

- required scan, combined, report, and paths artifacts exist
- the combined report has exactly `sample-client` and `sample-server`
- the sample endpoint `/api/admin/runner/get-by-id/{}` has endpoint alignment evidence; duplicate syntax/semantic server route facts may classify this as review-tier `AmbiguousMatch`
- a targeted path reaches a `sql-query` terminal from the client through an endpoint match, server call edge, source-local symbol reconciliation edge, and surface evidence edge
- `DatabaseColumnMapping` facts, when present, are selectable as `sql-persistence` terminal surfaces rather than `sql-query` terminal surfaces
- path edges and gaps carry rule IDs and evidence tiers
- a reverse SQL-surface query finds endpoint roots and path evidence with rule IDs and evidence tiers
- a bogus endpoint selector returns a valid zero-path report with a rule-backed gap
- repeated targeted `paths` JSON output is byte-stable
- generated Markdown does not render the synthetic SQL sentinel or developer-local absolute paths

The smoke writes generated manifests, logs, SQLite files, and reports under a caller-provided directory or `mktemp -d`. Generated manifests/logs may contain absolute paths to the checked-in samples or temporary output roots; they must not be committed.

For portfolio report changes, run the .NET solution build and test suite plus `./scripts/check-private-paths.sh` and `git diff --check`. The focused portfolio tests cover direct inputs, manifest inputs, combined-source expansion, before/after manifest source comparison, projected surface/edge comparison, deterministic output, read-only input handling, and public-output redaction. Run the public combined-path smoke only when the portfolio change also modifies language adapters, combine/report behavior, endpoint extraction, dependency-surface projection, paths, reverse, diff, impact, or release-review code shared outside `tracemap portfolio`.

## Legacy Baseline Regression Artifacts

When changing `tracemap baseline` creation, validation, or comparison behavior,
run the .NET build/test suite plus the baseline smoke over the checked-in
synthetic scan fixture:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose original-parser-snapshot \
  --out .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06 \
  --created-at 2026-06 \
  --dry-run

dotnet run --project src/dotnet/TraceMap.Cli -- baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose original-parser-snapshot \
  --out .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06 \
  --created-at 2026-06

dotnet run --project src/dotnet/TraceMap.Cli -- baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose candidate \
  --out .tmp/legacy-baselines/synthetic-alpha__candidate__2026-07 \
  --created-at 2026-07

dotnet run --project src/dotnet/TraceMap.Cli -- baseline compare \
  --baseline .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06/baseline-manifest.json \
  --candidate .tmp/legacy-baselines/synthetic-alpha__candidate__2026-07/baseline-manifest.json \
  --out .tmp/legacy-baselines/comparisons/synthetic-alpha \
  --generated-at 2026-07

dotnet run --project src/dotnet/TraceMap.Cli -- baseline validate \
  --manifest .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06/baseline-manifest.json

git check-ignore .tmp/legacy-baselines/example
./scripts/check-private-paths.sh
git diff --check
```

Baseline manifests and comparisons are redacted summaries. Do not commit raw
scan outputs, facts, SQLite files, analyzer logs, source snippets, SQL text,
config values, remotes, endpoint addresses, connection strings, secrets, local
absolute paths, or private sample identities. Local-only outputs must remain
under ignored `.tmp/legacy-baselines/`. Public-safe promotion requires
`tracemap baseline validate`, the redaction validator, and the private-path
guard over the promoted files.

## Legacy WCF/SVC Metadata Smoke

When changing legacy WCF extraction, service-reference metadata parsing, or WCF operation normalization, run the .NET build/test suite plus the validation summary unit tests:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

If the ignored local WCF/SVC smoke manifest exists, also run:

```bash
python3 scripts/legacy_codebase_validation.py \
  .tmp/legacy-codebase-validation/wcf-svc-smoke.local.json \
  .tmp/legacy-codebase-validation/wcf-svc-smoke-out
```

The summary must stay label-only. Do not commit local sample paths, raw scan outputs, raw WSDL/DISCO/XSD contents, endpoint addresses, SOAP actions, namespace URIs, config values, secrets, or generated smoke outputs. WCF metadata facts are static checked-in design-time evidence; they do not prove runtime reachability, deployment, service version compatibility, authorization, binding compatibility, or branch feasibility.

The checked-in Web Forms packet regression also covers the C# composition
shape `inline AJAX -> ASHX handler -> local helper -> generated WCF client
operation`. It must end at `wcf-operation` and must not infer remote service or
database behavior.

## Legacy WebForms Event Flow Smoke

When changing WebForms markup, code-behind, designer, handler-resolution, or event-flow extraction, run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

Checked-in fixtures should cover explicit markup event bindings, Tier3 bounded static `OnX` event-like candidates, client-side/dynamic attribute gaps, bounded named control subscriptions, lambda/dynamic and unknown-receiver gaps, missing or stale designer files, exact semantic or linked structural code-behind resolution, proven and unproven cross-file partial handlers, overload/ambiguity gaps, explicit `AutoEventWireup="true"` for `Page_Load`/`Page_Init`, false or unknown auto-wireup gaps, master/content declarations, markup and ancestor-`web.config` user-control registrations, conflicting inherited registration gaps, registered and nested user controls, validator/data-source/command metadata, same-named surfaces in separate folders, surface-qualified combined-path resolution, extractor-to-NDJSON-to-SQLite identity/direction persistence, missing composition targets, direct WCF/SQL reachability, reduced coverage, no-backend-evidence cases, sanitized legacy workspace failure categories, static logic signals, UI-boilerplate signals, deterministic duplicate bindings, and privacy redaction.

Useful inspection queries:

For `legacy-webforms/0.7.0`, `WebFormsBoundedCoverageTests` additionally covers
case-insensitive markup type names with strict namespace/project identity and
case-collision ambiguity, positive versus negative postback candidates, unchanged
negative-only script attribution, shadowing/compound/comparison gaps, and separate
OnClient/non-identifier event-value gaps. These fixtures intentionally target
non-compiling Framework 4.5 projects; gap reductions do not establish runtime
binding or branch execution.

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts where fact_type like 'WebForms%' group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select fact_type, rule_id, evidence_tier, file_path, start_line, properties_json from facts where fact_type like 'WebForms%' order by fact_type, file_path, start_line;"
grep -E "WebForms Events|WebForms Event Flow|WebForms Static Logic Signals" <out>/report.md
```

WebForms smoke summaries must remain hidden public-claim level until reviewed. Do not commit local sample paths, raw remotes, raw markup/code snippets, raw SQL, config values, endpoint URLs, secrets, or generated private outputs. WebForms event-flow evidence is static and does not prove runtime page lifecycle execution, event firing, event bubbling, service reachability, SQL execution, branch feasibility, deployment, or production usage.

For WebForms graph or packet hardening, also run the adversarial fixture matrix:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~LegacyWebFormsAdversarialFixtureTests
```

The matrix must keep non-SDK/partial builds useful but reduced, preserve
same-named cross-project surface/control/handler identities, retain explicit
event-source to handler direction through NDJSON/SQLite and packet composition,
and prove full-snapshot rename/delete/exclude/failure transitions do not retain
stale WebForms evidence. It does not authorize an in-place incremental updater.

For the bounded local modernization packet, run the scan first and then:

See the complete operator workflow, PowerShell and Bash examples, output
interpretation, deterministic comparison, fail-closed errors, and privacy
requirements in
[`WEBFORMS_MODERNIZATION_PACKET.md`](WEBFORMS_MODERNIZATION_PACKET.md).

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- webforms-modernization \
  --index <scan-output>/index.sqlite \
  --out <new-local-output-directory>
```

Verify both `webforms-modernization.json` and `webforms-modernization.md`, repeat
to a second new directory, and compare bytes. The input index must remain
unchanged. Inspect `coverage`, `gaps`, and `truncated` before using the packet;
`NoBackendEvidence` is coverage-relative and never proves absence. Do not
publish local-only packet output or treat structural candidates as named
business capabilities, migration estimates, parity, target architecture, or
release approval.

For projectless VB Web Forms correlation changes, also run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter ProjectlessVisualBasicWebFormsDiagnosticsTests
```

The public fixture intentionally contains no `.vbproj` or solution. Confirm
direct calls are retained by exact handler identity and containing span, the
UI-only handler remains call-free, and terminal-free gaps distinguish observed
downstream edges, bounded traversal truncation, and no retained backend edge.

When changing Web Forms batch/data-movement extraction or packet composition,
run the focused `LegacyBatchDataMovementExtractorTests` and
`WebFormsModernizationPacketTests`. Confirm scheduled entry points, Windows
service and console-job declarations, file movement, queue/message evidence,
stored-procedure loops, bulk-copy, ETL packages, missing configuration, reduced
builds, and ambiguous owners remain deterministic. Verify raw schedules, paths,
destinations, SQL, config values, and source values are absent from both packet
formats, and confirm every retained row preserves rule, tier, commit, span,
extractor version, supporting IDs, coverage, gaps, and limitations.

## Legacy WinForms Event Navigation Smoke

When changing WinForms form/control inventory, designer parsing, event binding,
handler resolution, navigation, callback, resource metadata, or handler-flow
projection, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyWinFormsExtractorTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

Useful inspection queries:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts where fact_type like 'WinForms%' group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select fact_type, rule_id, evidence_tier, file_path, start_line, properties_json from facts where fact_type like 'WinForms%' order by fact_type, file_path, start_line;"
grep -E "WinForms Static Evidence|WinForms Events|WinForms Navigation And Callbacks|WinForms Handler Flow" <out>/report.md
```

WinForms smoke summaries must remain hidden public-claim level until reviewed.
Use checked-in or temporary synthetic fixtures only. Do not commit local sample
paths, private sample names, raw remotes, raw source snippets, raw SQL, config
values, resource values, endpoint URLs, hostnames, secrets, or generated private
outputs. WinForms evidence is static and does not prove runtime event firing,
form visibility, user reachability, branch feasibility, auth/role outcome,
scheduling, service reachability, SQL execution, database existence, deployment,
or production usage.

## Legacy ASP.NET Route And Navigation Smoke

When changing classic ASP.NET route, config, handler, PageMethod, sitemap, or
navigation extraction, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyAspNetExtractorTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Focused fixtures should cover `.aspx`, `.ascx`, `.master`, `.ashx`,
`Global.asax`, code-behind partial classes, designer files, checked-in
`web.config` structures, `MapPageRoute`, simple static route registration,
dynamic route gaps, config handlers/modules/pages/controls/urlMappings,
PageMethods, ScriptMethods, ScriptService classes, static markup navigation,
sitemap nodes, C# `Response.Redirect`/`Server.Transfer`, ambiguous or unsafe
targets, malformed files, reduced semantic coverage, deterministic output, and
redaction.

Useful inspection queries:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts where fact_type like 'AspNet%' group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select fact_type, rule_id, evidence_tier, file_path, start_line, properties_json from facts where fact_type like 'AspNet%' or rule_id like 'legacy.aspnet.%' order by fact_type, file_path, start_line;"
grep -E "Legacy ASP.NET Static Surface Evidence|Legacy ASP.NET Surface Limitations|route candidate|navigation reference candidate" <out>/report.md
```

No pinned public route/navigation smoke baseline exists yet beyond checked-in
synthetic unit fixtures. Any local legacy ASP.NET smoke output must stay
ignored/local-only, and any future catalog entry must use neutral labels,
rule IDs, tiers, states, sanitized command templates, and reviewed public or
synthetic identity metadata only. Do not commit raw scan outputs, local sample
paths, raw remotes, raw routes, raw endpoint URLs, hostnames, config values,
query strings, fragments, source snippets, credentials, secrets, or generated
private outputs. ASP.NET route/navigation evidence is static and does not prove
runtime route matching, IIS deployment, URL rewriting, authorization, browser
behavior, JavaScript execution, request handling, page rendering, user
reachability, or runtime impact.

## Legacy Remoting Smoke

When changing .NET Remoting API, `MarshalByRefObject`, channel, registration, activation, config, or Remoting report extraction, run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/dotnet-remoting-sample --out <tmp>/dotnet-remoting-scan
./scripts/check-private-paths.sh
git diff --check
```

The synthetic sample scan should produce the standard scan artifacts plus `Remoting*` facts in `facts.ndjson`, `index.sqlite`, and `report.md`. Inspect with:

```bash
sqlite3 <tmp>/dotnet-remoting-scan/index.sqlite "select fact_type, count(*) from facts where fact_type like 'Remoting%' group by fact_type order by fact_type;"
grep -E "Legacy Remoting Static Evidence|Legacy Remoting Limitations" <tmp>/dotnet-remoting-scan/report.md
```

No pinned public Remoting smoke baseline exists yet. Public-repository Remoting baselines require a separate reviewed baseline task or spec. Remoting evidence is static only and must not claim host activation, runtime reachability, endpoint availability, deployment, exploitability, security posture, or production usage. Generated scan artifacts are local-only and must not be committed.

## Legacy Static Flow Reporting Smoke

When changing `tracemap paths --include-legacy-roots`, legacy flow classification, WCF operation terminal handling, legacy data metadata terminal handling, path output redaction, or related path selectors, run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln --filter LegacyFlowCompositionTests
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Focused fixtures should cover WebForms event roots, WebForms lifecycle roots, direct handler-to-service paths, WCF service-reference paths with `wcf-operation` terminals, SQL/query terminals, legacy data metadata terminals when available, reduced coverage, missing extractor availability, selector no-match and classification-filter gaps, truncation, deterministic JSON, and privacy suppression.

Legacy static flow reports use `legacy-flow.v1` schema metadata and must phrase results as static evidence or possible static paths. They must not claim runtime execution, guaranteed backend reachability, SQL execution, database existence, production dependency, or impact. Generated Markdown and JSON must omit or hash local absolute paths, raw remotes, private labels, raw SQL, WSDL/SOAP/endpoint URLs, connection strings, config values, source snippets, and secret-looking values.

## Legacy Data Metadata Smoke

When changing DBML, EDMX, typed DataSet/TableAdapter, legacy data config, generated data-code linkage, XML parser safety, or safe identifier redaction, run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

When changing the EF6 EDMX symbol composition (`legacy.data.edmx.symbol-composition.v1`), also run the focused suites:

```bash
dotnet test src/dotnet/TraceMap.sln --filter "FullyQualifiedName~LegacyDataEdmxSymbolCompositionTests|FullyQualifiedName~LegacyDataMetadataExtractorTests|FullyQualifiedName~LegacyDataModelRuleCatalogTests|FullyQualifiedName~CSharpSemanticExtractorTests|FullyQualifiedName~ReverseImpactTraversalTests"
```

Checked-in fixtures should cover DBML entities/tables/columns/associations/routines, EDMX CSDL/SSDL/MSL mappings and unsupported shapes, typed DataSet XSD gating, TableAdapter command hashing, normalized model identity keys, config provider/connection metadata, generated-code links, unsupported old ORM descriptor gaps, malformed XML, DTD/entity rejection, deterministic output, and privacy suppression in facts, reports, logs, and SQLite. EF6 composition fixtures additionally cover the F1-F18 matrix: namespace-parity and attribute-bridged composition, decoy type names, SSDL storage-type identity joins, same simple names across namespaces, identical assembly name/version across compilation scopes, scope decoys in sibling directories and prefix siblings, per-EDMX compiler availability in multi-project scans, ambiguous and unsupported fail-closed gaps, persistence round-trip through `symbol_relationships` and `combined_dependency_edges`, and reverse-impact traversal with hop provenance and mid-traversal member expansion.

Useful inspection queries:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts where fact_type like 'LegacyData%' group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select fact_type, rule_id, evidence_tier, file_path, start_line, properties_json from facts where fact_type like 'LegacyData%' or rule_id like 'legacy.data.%' order by fact_type, file_path, start_line;"
grep -E "Legacy Data Metadata|Legacy Data Metadata Limitations" <out>/report.md
```

Legacy data metadata smoke summaries remain hidden public-claim level until reviewed. Do not commit local sample paths, raw remotes, raw DBML/EDMX/XSD/XML snippets, raw SQL, connection strings, config values, provider secrets, URLs, local absolute paths, or generated private outputs. Legacy data metadata evidence is static design-time evidence and does not prove runtime data access, SQL execution, database existence, provider compatibility, transform selection, generated-code freshness, branch feasibility, deployment, or production usage.

## Public OSS Smoke

Use `scripts/smoke-open-source-repos.sh` to clone pinned public repositories into a cache directory and scan them into a separate output directory:

```bash
scripts/smoke-open-source-repos.sh /tmp/tracemap-oss-cache /tmp/tracemap-oss-smoke
```

The script uses exact commit SHAs so results are comparable over time.

| Label | Language | URL | Commit SHA | Expected coverage |
| --- | --- | --- | --- | --- |
| `ProjectExtensions.Azure.ServiceBus` | C# | `https://github.com/ProjectExtensions/ProjectExtensions.Azure.ServiceBus.git` | `2a8e72c8f5680edf2096b05ac08c39d47a95cef8` | usually `Level1SemanticAnalysisReduced` |
| `fluentjdf` | C# | `https://github.com/joefeser/fluentjdf.git` | `9490e699a89bb21f4aabf198173fc6382f84a53f` | usually `Level1SemanticAnalysisReduced` |
| `community-visual-basic` | VB.NET | `https://github.com/CommunityVB/Community.VisualBasic.git` | `20d2a51dfc9f342848ad134952ceaa8d79302559` | `Level1SemanticAnalysisReduced`; see the VB.NET adapter section |
| `scip-typescript` | TypeScript | `https://github.com/sourcegraph/scip-typescript.git` | `891eb4293709a6a587bf4468dfa1b45a85182fd9` | usually `Level1SemanticAnalysisReduced` |
| `axios-npm-lock` | JavaScript/TypeScript | `https://github.com/axios/axios.git` | `84a9f3b9a4f3244b8c8e818f557d64c7b964fb25` | usually `Level1SemanticAnalysisReduced`; committed npm `package-lock.json` v3 evidence |
| `scip-java` | JVM | `https://github.com/sourcegraph/scip-java.git` | `825463cb15d540d45c680593aad1f634330435cf` | usually `Level1SemanticAnalysisReduced` |
| `spring-petclinic` | JVM | `https://github.com/spring-projects/spring-petclinic.git` | `a2c2ef994340d3970eb6db51247456a51bb161f8` | usually `Level1SemanticAnalysisReduced` |
| `okio` | JVM/Kotlin | `https://github.com/square/okio.git` | `cad7ff1057307142149b1a28dfcb49117e89b0d3` | usually reduced or syntax fallback for Kotlin-heavy areas |
| `full-stack-fastapi-template` | Python | `https://github.com/fastapi/full-stack-fastapi-template.git` | `1c1175eb5045e6e8fca3bcbc4134630f3ae640ba` | `Level1SemanticAnalysisReduced` |
| `microblog` | Python | `https://github.com/miguelgrinberg/microblog.git` | `a975ef64864354867c88e0ed3a17ba7d17dca752` | `Level1SemanticAnalysisReduced` |
| `sqlalchemy` | Python | `https://github.com/sqlalchemy/sqlalchemy.git` | `bfe559a7e4d69e5699c390ac9cafd2a5a2d38078` | `Level1SemanticAnalysisReduced` |

Reduced coverage is acceptable for OSS smoke when project/dependency/classpath gaps are recorded as `AnalysisGap` facts. A successful smoke means the scan completes, artifacts exist, the manifest is honest about coverage, and important relationship tables can be queried.

`axios-npm-lock` is a modest MIT-licensed, widely used JavaScript HTTP client.
The pinned revision contains both `package.json` and a committed npm lockfile v3
with direct and transitive package entries. It is a metadata-only fixture: the
smoke neither runs package-manager commands inside the checkout nor fetches,
executes, or verifies package content.

## JVM Smoke Expectations

The JVM modern sample is the minimum high-signal fixture. It should produce:

- `Level1SemanticAnalysis`
- `buildStatus = "Succeeded"`
- exactly one Java route binding: `GET /api/orders/{id}` mapped to `com.example.orders.OrderController.getOrder`
- semantic call edges from `OrderController.getOrder` to `OrderResponse.setStatus` and `OrderService.calculateTotal`
- object creation rows for `OrderService`, `OrderResponse`, and `OrderRepository`
- argument-flow rows from controller/service calls into callee parameters
- SQL facts for the JDBC `prepareStatement` literal and `schema.sql`
- config key facts for `application.properties`
- a reducer `DefiniteImpact` finding for `OrderResponse.status`

Example query set:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select count(*) from call_edges;"
sqlite3 <out>/index.sqlite "select count(*) from object_creations;"
sqlite3 <out>/index.sqlite "select count(*) from argument_flows;"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='HttpRouteBinding';"
```

## VB.NET Adapter

The VB.NET adapter follows the same matrix: local modern/legacy/Web Forms
fixtures, a reducer-compatible shared index, the pinned OSS smoke, and the
private-path guard. Adapter scope, extractor identities, fact families,
fallback behavior, supported project types, and limitations are documented in
[`VBNET_ADAPTER.md`](VBNET_ADAPTER.md); the fixture corpus and the pinned
smoke repository are documented in
[`VBNET_FIXTURES.md`](VBNET_FIXTURES.md).

Required local validation for VB.NET adapter changes:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln

# Focused VB.NET suites: extraction facts, foundation inventory/loading, the
# synthetic validation matrix, event/Web Forms composition, and fixture syntax checks.
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter 'FullyQualifiedName~VisualBasic|FullyQualifiedName~VbNetFixture'

# Initial compiler-backed ADO.NET command/adapter/Fill/Execute evidence.
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~VisualBasicDataBoundaryTests

# Compiler-backed HTTP/config/file evidence, explicit service gaps, privacy,
# determinism, and packet/docs/query-recipe/WITS handoff consumption.
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~VisualBasicExternalBoundaryTests

# VB scan -> Web Forms packet -> batch inspection -> private/anonymous source
# review, plus language-aware source navigation and privacy assertions.
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter 'FullyQualifiedName~VisualBasicWebFormsCompositionTests|FullyQualifiedName~WebFormsCodePathReviewTests'

# Modern (semantic success), legacy (fallback/reduced), and Web Forms
# (inventory + reduced) CLI fixture scans.
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/vb-modern-sample --out <tmp>/vb-modern
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/vb-legacy-sample --out <tmp>/vb-legacy
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/vb-webforms-sample --out <tmp>/vb-webforms

# Shared artifact conformance (also enforces rule registration) for each scan.
python3 scripts/validate-adapter-artifacts.py <tmp>/vb-modern
python3 scripts/validate-adapter-artifacts.py <tmp>/vb-legacy
python3 scripts/validate-adapter-artifacts.py <tmp>/vb-webforms

# Determinism: two consecutive modern-fixture scans are byte-identical.
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/vb-modern-sample --out <tmp>/vb-modern-repeat
cmp <tmp>/vb-modern/facts.ndjson <tmp>/vb-modern-repeat/facts.ndjson

# Useful inspection queries.
sqlite3 <tmp>/vb-modern/index.sqlite "select fact_type, count(*) from facts where rule_id like 'vb.%' group by fact_type order by fact_type;"
sqlite3 <tmp>/vb-modern/index.sqlite "select count(*) from symbols where language = 'visualbasic';"
sqlite3 <tmp>/vb-modern/index.sqlite "select count(*) from call_edges where rule_id like 'vb.semantic.%';"
sqlite3 <tmp>/vb-modern/index.sqlite "select count(*) from call_edges where rule_id like 'vb.syntax.%';"

./scripts/check-private-paths.sh
git diff --check
```

Expected fixture postures:

- `vb-modern-sample`: `Level1SemanticAnalysis` / `Succeeded`, compiler-resolved
  Tier1 families, no file-wide fallback duplicates, and no
  `vb.semantic.workspace.v1` gaps. Individual unresolved call sites may retain
  bounded Tier3 facts under `vb.syntax.invocation.v1`,
  `vb.syntax.callgraph.v1`, or `vb.syntax.objectcreation.v1`.
- `vb-legacy-sample`: `Level1SemanticAnalysisReduced` / `FailedOrPartial`,
  partial Tier1 evidence over the readable files, no file-wide fallback
  duplicates, bounded Tier3 evidence for individual unresolved call sites,
  and category-only gaps carrying bounded `BCxxxxx` ids.
- `vb-webforms-sample`: `Level1SemanticAnalysisReduced` / `FailedOrPartial`
  on cross-platform SDKs; bounded VB Handles/AddHandler/RemoveHandler,
  RaiseEvent, WithEvents designer, linked control/handler, and IsPostBack
  evidence remains available. Event evidence is never an executed call edge.

The external-boundary synthetic matrix additionally proves shared HTTP and
configuration facts, shared batch/file projection, compiler-proven WCF/ASMX
proxy mappings, and explicit mapping gaps for unsupported service shapes.
Recognized service-framework identities require their expected strong-name public
key token; unsigned assemblies with the same simple name do not produce Tier 1
service facts. The matrix also covers renamed and inherited WCF operations,
ASMX service-context admission, same-name helper rejection, unresolved command
construction gaps, classic .NET Framework HTTP/configuration assembly names,
ASMX Web Forms projection, and the shared `sql-query` classification for
data-adapter Fill.

VB.NET pinned OSS smoke (`community-visual-basic` —
`CommunityVB/Community.VisualBasic`, MIT, pinned at
`20d2a51dfc9f342848ad134952ceaa8d79302559`):

```bash
TRACEMAP_OSS_SMOKE_REPOS=community-visual-basic \
  scripts/smoke-open-source-repos.sh /tmp/tracemap-oss-cache /tmp/tracemap-oss-smoke
```

The script clones the pin, resets the working tree with `git clean -fdx`
(required: design-time builds write `obj/` state inside the clone, which
changes later design-time loads and gap counts if it is not cleaned), scans
it, and asserts the required artifacts. Recorded expectations at the pin:
`Level1SemanticAnalysisReduced` / `FailedOrPartial` with 110,726 facts,
6,314 `visualbasic` symbols, 928 `vb.semantic` call edges, 517 object
creations, 175 argument flows, 98 symbol relationships, and 75,455
category-only `AnalysisGap` rows. Reduced coverage is expected at this pin
(unrestored packages and out-of-support target frameworks); the smoke proves
artifact generation and static evidence extraction over a real VB.NET
repository, not that the repository builds or that coverage is complete.

VB.NET evidence is static and bounded: it never proves compilation success,
runtime reachability, event firing, execution, deployment, or impact.
Cross-language symbol-identity joins between VB-scan symbols and C#-declared
symbols are not established. VB/Web Forms event evidence is static and does
not establish runtime attachment, firing, ordering, postback behavior, or
execution.
Focused review can annotate retained `.vb` spans when raw source is explicitly
enabled. The supplied source root need not be a Git checkout, and TraceMap does
not claim that working-tree source equals the recorded scan commit. Anonymous
HTML/JSON contains structural aliases and safe rule/tier provenance only.

## What SQL Means Here

SQL should be treated as a first-class cross-language data dependency surface, not as the next application language adapter. The SQL layer should eventually parse:

- query text from application code
- `.sql` files and migration files
- tables, columns, joins, projections, predicates, and write operations
- stored procedures, views, and function calls where dialect support exists
- query-to-schema relationships across app indexes and database artifacts

SQL validation should therefore plug into every app-language adapter, because C#, TypeScript, JVM, and Python can all emit SQL evidence.

## Python Adapter

Python validation fixtures should cover:

- FastAPI routes and Pydantic DTOs
- Flask routes where syntax can prove them
- SQLAlchemy declared columns and direct SQL literals
- direct SQL literals
- environment/config reads
- requests/httpx client calls

Python follows the same matrix: modern sample, broken sample, reducer fixture, relationship tables, integration facts, public OSS smoke, and private-path guard.

Python MVP no-match reducer outcomes are expected to be `NoEvidenceReducedCoverage` because MVP scans use reduced AST/package/config coverage, not full type-checker semantic coverage.

## Python Smoke Expectations

The Python FastAPI sample is the minimum high-signal fixture. It should produce:

- `Level1SemanticAnalysisReduced`
- `buildStatus = "FailedOrPartial"`
- route facts for FastAPI/Flask decorators when static decorator syntax is visible
- serializer contract member facts for Pydantic and dataclass-like DTO fields
- SQLAlchemy column mapping facts for declarative mapped columns
- SQL file and direct SQL literal facts with hashed SQL text
- query-pattern facts with operation, table, column, text hash, and query shape hash metadata when simple static SQL is visible
- config key facts for config module assignments and static `os.getenv` or `os.environ[...]` reads
- HTTP client facts for `requests` and `httpx` static URL calls
- endpoint alignment smoke from `samples/python-client-sample` to `samples/python-fastapi-sample` produces at least one `MatchedEndpoint`
- shared SQLite rows for `call_edges`, `object_creations`, `argument_flows`, `symbol_relationships`, and `symbols`
- a reducer `ProbableImpact` or stronger structural finding for `OrderResponse.status`

Example query set:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select count(*) from call_edges;"
sqlite3 <out>/index.sqlite "select count(*) from object_creations;"
sqlite3 <out>/index.sqlite "select count(*) from argument_flows;"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='HttpRouteBinding';"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='DatabaseColumnMapping';"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='QueryPatternDetected';"
sqlite3 <out>/index.sqlite "select rule_id, json_extract(properties_json, '$.sqlSourceKind'), json_extract(properties_json, '$.queryShapeHash') from facts where fact_type='QueryPatternDetected' and json_extract(properties_json, '$.sqlSourceKind') is not null order by rule_id, fact_id;"
sqlite3 <combined>/combined.sqlite "select sources.label, facts.fact_type, json_extract(facts.properties_json, '$.sqlSourceKind'), json_extract(facts.properties_json, '$.queryShapeHash'), json_extract(facts.properties_json, '$.textHash') from combined_facts facts join combined_sources sources on sources.source_index_id = facts.source_index_id where facts.fact_type in ('SqlTextUsed','QueryPatternDetected','DatabaseColumnMapping','DapperCallDetected','SqlCommandDetected') order by sources.label, facts.combined_fact_id;"
grep "orm-text" <out>/report.md
grep "orders" <out>/report.md
```

For SQL dependency-surface changes, also inspect hash-only and weak-identity behavior:

```bash
sqlite3 <combined>/combined.sqlite "select sources.label, facts.fact_type, facts.properties_json from combined_facts facts join combined_sources sources on sources.source_index_id = facts.source_index_id where facts.fact_type in ('SqlTextUsed','QueryPatternDetected') order by sources.label, facts.combined_fact_id;"
dotnet run --project src/dotnet/TraceMap.Cli -- diff --before <before-combined.sqlite> --after <after-combined.sqlite> --out <tmp>/sql-diff --scope surfaces --surface sql-query --format json
grep -E "HashOnlyEvidence|VolatileIdentity" <tmp>/sql-diff/diff-report.json
```

When checking mapping-only persistence evidence, use `--to-surface sql-persistence`, `--surface sql-persistence`, or `--scope surfaces --surface sql-persistence`; these surfaces do not claim that a SQL query executes.
# SQL Execution Context Smoke

SQL execution-context changes should run the focused .NET tests and scan the
checked-in public-safe fixture without any database connection:

```bash
dotnet test src/dotnet/TraceMap.sln --filter FullyQualifiedName~SqlExecutionContextExtractorTests
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/sql-execution-context --out /tmp/tracemap-sql-context-smoke
```

Inspect `facts.ndjson`, `index.sqlite`, `report.md`, and
`logs/analyzer.log`. Expected facts include
`SqlExecutionContextDeclared`, `SqlExecutionContextCandidate`, and cataloged
`AnalysisGap` rows. Output must retain rule IDs, tiers, repo-relative spans,
commit SHA, extractor version, coverage, and limitations while omitting raw SQL,
directive/sidecar bodies, connection data, credentials, infrastructure names,
scheduled command bodies, and local absolute paths. The report must describe
static intended context and manual verification needs without claiming runtime
state or that a step is safe to run.

## SQL Protected-Material Safety Smoke

Protected-material changes should run the focused leak tests and scan the
checked-in placeholder-only fixture without connecting to a database:

```bash
dotnet test src/dotnet/TraceMap.sln --filter FullyQualifiedName~SqlSecretSafetyExtractorTests
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/sql-secret-safety --out /tmp/tracemap-sql-secret-safety-smoke
```

Inspect every generated artifact and the combined/export paths. Expected output
is category-only `SecretBearingSqlStep` and `AnalysisGap` evidence with rule IDs,
tiers, relative spans, coverage, and `secret-owner-review`. Raw SQL, placeholder
names, connection material, values, and secret-derived hashes must be absent.
The report must say that absence of a finding does not prove absence of secrets
and must not certify execution safety or replace operator approval.

## PostgreSQL Archive-Link Evidence Smoke

Archive-link changes should run the focused tests and scan the checked-in
placeholder-only fixture:

```bash
dotnet test src/dotnet/TraceMap.sln --filter FullyQualifiedName~PostgresArchiveLinkExtractorTests
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/postgres-archive-link --out /tmp/tracemap-postgres-archive-smoke
```

Expected output includes `DatabaseLinkSurfaceDeclared`,
`DatabasePrerequisiteCandidate`, `DatabaseLinkEdgeCandidate`, and cataloged
archive-link gaps. Inspect NDJSON, SQLite, Markdown, and logs for rule IDs,
tiers, commit SHA, extractor version, coverage, limitations, supporting fact
IDs, and category-only context/direction. Connection inputs, user-mapping
values, subscription data, scheduled bodies, infrastructure identifiers, and
local paths must not appear. The report must not claim connectivity, applied
state, permissions, replication health, scheduling success, or archive
correctness.

## PostgreSQL Permission Prerequisite Evidence Smoke

Permission-evidence changes should run the focused tests and scan the checked-in
public-safe fixture:

```bash
dotnet test src/dotnet/TraceMap.sln --filter FullyQualifiedName~PostgresPermissionEvidenceExtractorTests
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/postgres-permission-evidence --out /tmp/tracemap-postgres-permission-smoke
```

Expected output includes `DatabasePermissionDeclared`, permission-owned
`DatabasePrerequisiteCandidate`, `DatabasePrerequisiteEvidence`, and cataloged
permission gaps. Inspect NDJSON, SQLite, Markdown, and logs for the registry
version, closed capability/status/reason codes, supporting or contradicting fact
IDs, safe spans, rule/tier, coverage, and limitations. Raw SQL, role/object/
infrastructure names, credentials, connection data, and local paths must be
absent. `present-in-scripts` must be described as checked-in evidence only and
must never claim effective or sufficient runtime access.

## PostgreSQL Schema/Migration Evidence Smoke

Schema/migration extractor changes should run the focused tests and a
disposable checked-in-style fixture scan without connecting to PostgreSQL:

```bash
dotnet test src/dotnet/TraceMap.sln --filter FullyQualifiedName~PostgresSchemaMigrationExtractorTests
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/postgres-schema-migration --out /tmp/tracemap-postgres-schema-smoke
```

Expected output may include `PostgresMigrationFileDeclared`,
`PostgresMigrationOperation`, `PostgresSchemaTableDeclared`,
`PostgresSchemaColumnDeclared`, `PostgresSchemaConstraintDeclared`,
`PostgresSchemaIndexDeclared`, and cataloged gaps. Inspect generated output for
rule IDs, tiers, repository-relative spans, commit SHA, extractor version,
coverage, and limitations. Raw SQL, expressions, predicates, literals, quoted
or unsupported identifiers, connection material, and local paths must not
appear. Static facts must not claim migration execution, live objects, index
selection, uniqueness, referential integrity, compatibility, rollback, or
release safety.

## SQL Project Refactor Intent Smoke

SQL project refactor-intent changes should run the focused tests and scan the
checked-in static fixture without building the project, opening a DACPAC,
invoking SqlPackage, or connecting to SQL Server:

```bash
dotnet test src/dotnet/TraceMap.sln --filter FullyQualifiedName~SqlProjectRefactorTests
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/sql-project-refactor --out /tmp/tracemap-sql-project-refactor-smoke
```

Expected output includes `SqlProjectRefactorLogDeclared` and bounded
`SqlProjectRefactorOperation` facts under
`database.sql-project.refactor-intent.v1`. Inspect generated output for rule
IDs, Tier 2 structural evidence, repository-relative spans, commit SHA,
extractor version, coverage, hashed operation keys, and limitations. Raw XML,
SQL, local absolute paths, connection material, and unhashed operation keys
must not appear. The facts must not claim project build, DACPAC packaging,
deployment-plan generation, target `__RefactorLog` state, execution,
application, compatibility, rollback, production state, release approval, or
execution safety.

# SQL operator runbook packet smoke

Run the deterministic public-safe fixture and verify standard scan artifacts plus
the standalone packet outputs:

```bash
rm -rf /tmp/tracemap-sql-runbook-smoke
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo samples/sql-operator-runbook \
  --out /tmp/tracemap-sql-runbook-smoke
test -f /tmp/tracemap-sql-runbook-smoke/sql-runbook.md
test -f /tmp/tracemap-sql-runbook-smoke/sql-runbook.json
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~SqlRunbookPacketTests
```

The focused tests cover deterministic ordering, schema fields, context
transitions, scheduled and validation groups, permission/protected projections,
partial gaps, planted-value leakage, forbidden runnable SQL, and CLI artifacts.

## SQL validation-summary ingestion smoke

Run the focused offline ingestion and composition tests:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~SqlValidationSummaryTests
```

The suite generates only synthetic categorical summaries and covers valid,
expired, source/commit mismatch, context mismatch, unsupported validator,
unsupported assertion, exact duplicate, conflict, digest tamper, malformed, and
planted-secret cases. It also runs `scan --sql-validation-summary` and release
review composition, proves static evidence tiers remain unchanged, and verifies
that rejected summaries flow into rule-backed packet gaps.

Do not substitute terminal output, screenshots, SQL text, connection material,
target names, ticket notes, or human-authored pass/fail prose for the versioned
summary. TraceMap does not run the validator or connect to PostgreSQL during
this smoke.

## SQL validation harness smoke

Run the standalone producer tests and the no-connection dry run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~SqlValidationHarnessTests
rm -f /tmp/tracemap-sql-validation-summary.json
dotnet run --project src/dotnet/TraceMap.SqlValidation.Cli -- validate \
  --plan samples/sql-validation-harness/plan.example.json \
  --out /tmp/tracemap-sql-validation-summary.json \
  --dry-run
```

The tests use a synthetic executor and make no network connection. They cover
strict local-plan parsing, deterministic summary generation, categorical pass,
fail, indeterminate, and not-run behavior, canonical-digest compatibility with
the ingestion reader, CLI error classification, create-new output semantics,
and planted private-value non-disclosure.

Do not use a live PostgreSQL target as a routine CI smoke. Live use is an
explicit operator action under [`SQL_VALIDATION_HARNESS.md`](SQL_VALIDATION_HARNESS.md)
and requires a least-privilege connection appropriate for the selected catalog
checks.

To validate the same compiled-in probes against a disposable synthetic
PostgreSQL 16.8 server, with Docker running locally, use:

```bash
./scripts/smoke-sql-validation-postgres.sh
```

This opt-in integration smoke pins the official image by digest, exposes a
random loopback port, uses no host volume, asserts pass/fail/not-run behavior
and deterministic summaries, checks identifier and connection-data exclusion,
and cleans all container and scratch state. It is not a substitute for an
authorized target-specific operator validation.

## Database design-review packet

For changes to the single- or combined-index database design-review packet, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~DatabaseDesignReviewTests
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo samples/postgres-schema-migration \
  --out <tmp>/postgres-scan
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index <tmp>/postgres-scan/index.sqlite --label postgres-sample \
  --out <tmp>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- database-design-review \
  --index <tmp>/postgres-scan/index.sqlite \
  --out <tmp>/database-design-review-single
dotnet run --project src/dotnet/TraceMap.Cli -- database-design-review \
  --index <tmp>/combined.sqlite \
  --out <tmp>/database-design-review-combined
```

Confirm both packet files are deterministic and contain rule IDs, evidence
tiers, source labels, commit SHAs, repository-relative spans, extractor
provenance, supporting fact/edge/rule IDs, coverage, limitations, and explicit
gaps. Verify declaration rows remain separate from migration-operation rows,
query/table correlation is exact and source-scoped, and route references come
only from existing bounded path evidence. For the single-index packet, confirm
route references are zero and `SingleIndexRoutePathUnavailable` labels the
missing combined graph/path contract without per-query route-absence claims.

The smoke must not require PostgreSQL or network access. Confirm the packet does
not render raw SQL, snippets or snippet hashes, credentials, connection
strings, scheduled command bodies, local paths, private server identities, or
validation output. It must not claim SQL execution, runtime reachability,
production state, design correctness, release approval, or that a script is
safe to run.

### Generated C# semantic source paths

For changes to C# semantic source-location projection, run the focused generated
source test and the complete .NET suite:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter "FullyQualifiedName~GeneratedSemanticPathTests|FullyQualifiedName~CSharpSemanticExtractorTests"
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
```

The focused fixture loads a source document from a synthetic SDK/package-cache
shape outside the repository. Confirm semantic evidence is preserved under the
same deterministic `__external__/csharp-<kind>-<hash>` identity across distinct
host roots and that `csharp.semantic.workspace.v1` emits an
`ExternalSourcePathProjected` gap. All five standard scan outputs must exclude
the temporary root, home directory, SDK/package-cache root, raw external source
path, and package identity. The synthetic fallback intentionally does not prove
that external source is checked in, immutable, complete, or available on another
machine; unrecognized same-named external files can share an identity.

### EF Core mapping evidence

For changes to EF/EF Core model mapping extraction or design-review
composition, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter "FullyQualifiedName~CSharpSemanticExtractorTests|FullyQualifiedName~DatabaseDesignReviewTests"
```

Use a compiling fixture with framework-shaped semantic symbols and confirm
`DbSet<TEntity>`, `Table`/`Column`, constant `ToTable`/`HasColumnName`, dynamic
mapping names, and assembly-scanned configuration boundaries. Confirm the
design-review packet links mappings only through exact schema/table identity or
a unique same-source table name when the EF schema is unspecified. Ambiguous,
unmatched, dynamic, and assembly-driven cases must remain explicit gaps.

This validation does not execute application startup, `OnModelCreating`,
migrations, generated SQL, or a database. It must not claim convention-derived
names, runtime provider behavior, model completeness, database correspondence,
query execution, or release safety.

### Database operation call-pattern evidence

For changes to application database-operation extraction or database
design-review composition:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter "FullyQualifiedName~CSharpSemanticExtractorTests.Scan_emits_bounded_database_operation_candidates|FullyQualifiedName~DatabaseDesignReviewTests"
```

Use compiling EF/EF Core, Dapper, and ADO.NET/Npgsql fixtures. Confirm
candidates preserve rule/tier/span/commit/extractor provenance, constant SQL
contributes only safe operation/table shape metadata, dynamic or unresolved
targets remain explicit gaps, and the packet never renders SQL text, command
text, parameter values, connection material, or local paths.

Also cover a C# file without a loadable project and confirm recognizable
operation names emit Tier 4 operation-rule gaps rather than candidates.
Qualified constant SQL targets must retain safe one- or two-part identity;
unsafe or multipart identity must remain unlinked. Custom operation-named
methods declared only on application `DbContext` subclasses must not become EF
candidates. When operation route-path traversal is truncated, the packet must
emit reduced-coverage gaps instead of claiming that no path exists.

Treat every application operation as a static candidate. This validation does
not prove execution, affected rows, database state, transaction outcome, or
success.

### PostgreSQL enum and routine declaration evidence

For changes to the bounded PostgreSQL enum/routine projector, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~PostgresSchemaMigrationExtractorTests
```

Use checked-in synthetic SQL containing `CREATE TYPE ... AS ENUM`,
`CREATE FUNCTION`, and `CREATE PROCEDURE`, including dollar-quoted bodies and
quoted-identifier negative cases. Confirm facts retain rule ID, Tier 2,
repository-relative span, commit SHA, extractor version, coverage label, and
limitations while omitting enum labels, routine signatures, parameters,
return declarations, languages, bodies, literals, and raw SQL. Unsupported
recognized-family shapes must emit Tier 4 categorical gaps.

### PostgreSQL destructive migration evidence

For changes to bounded PostgreSQL drop/rename projection, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~PostgresSchemaMigrationExtractorTests
```

Use checked-in synthetic SQL containing single-object `DROP TABLE`,
single-subcommand `ALTER TABLE ... DROP COLUMN`, `RENAME COLUMN`, and
`RENAME TO`. Confirm facts retain safe source/new identities, categorical drop
behavior, rule ID, Tier 2, repository-relative span, commit SHA, extractor
version, coverage label, and limitations. Quoted identifiers, multi-object
drops, and multi-subcommand alterations must emit Tier 4 categorical gaps
without raw SQL or unsupported identities.

### PostgreSQL checked-in schema snapshot evidence

For changes to schema-snapshot recognition and coverage, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~PostgresSchemaMigrationExtractorTests
```

Use synthetic checked-in SQL with an active standard
`-- PostgreSQL database dump` header or the exact
`-- tracemap-postgres-schema-snapshot: v1` directive. Verify snapshot format,
recognized bounded-DDL count, aggregate unsupported-DDL count, coverage label,
source-database-identity omission, rule, tier, span, commit, extractor version,
and limitations. Confirm filename-only candidates and marker text inside SQL
strings do not establish snapshot identity. Unsupported DDL must produce
categorical Tier 4 snapshot gaps without retaining object names, comments,
database/server identity, or raw SQL.

### Framework migration evidence

Generic consumer audit:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~FrameworkMigrationConsumerAuditTests
```

Expected behavior: the Markdown report counts framework migration fact types
without rendering protected fact properties; the static HTML explorer
preserves only the exact bounded coverage and limitation contract; snapshot
diff, vault export, and evidence-docs export emit rule-backed gaps with
supporting fact IDs where no dedicated framework migration projection exists.
None of these consumers claims migration application, ordering, provider
selection, generated SQL, database state, rollback, approval, or safety.

For changes to the bounded EF Core migration producer, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~FrameworkMigrationEvidenceExtractorTests

dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo samples/framework-migration-v0 \
  --out .tmp/framework-migration-scan \
  --restore
```

Inspect all five required scan artifacts. Confirm declarations and operations
use the framework-migration rule family, Tier 1 semantic evidence, exact spans,
canonical symbol roles, provider scope `unknown`, and the bounded static
migration coverage label. Protected SQL, data, annotation, default, and
computed content must produce categorical Tier 4 gaps without source values or
digests, and must not reappear through SQL text/shape extraction. The generic
contract-delta reducer must ignore this fact family until its composition
contract preserves the upstream evidence and limitations. Migration coverage
gaps reduce `analysisLevel`, but do not change a successfully loaded and
compiled repository's `buildStatus` to `FailedOrPartial`; those are separate
claims. Confirm syntax-fallback facts use their dedicated extractor provenance
and categorical migration messages in `knownGaps`.

For framework-migration database design-review or release-review composition
changes, also run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~FrameworkMigrationCompositionTests
```

Verify both reports preserve rule, tier, span, commit, extractor, coverage,
supporting fact IDs, and upstream limitations. Generic operations must remain
global application-side evidence with explicit provider-unknown gaps; they must
not attach to PostgreSQL objects. Confirm outputs contain no protected source
symbol, raw SQL, local path, or claims of application, ordering, rollback,
generated SQL, compatibility, safety, database state, or approval.

### Package decision correlation (PR1 + PR2)

The external `package-decision.v1` reader and single/combined/portfolio
correlation command are deterministic, read-only, and offline. TypeScript npm
lockfile evidence is registry-declared metadata only; TraceMap does not fetch
or verify package content. Run the focused suite and CLI help check before
reviewing generated artifacts:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~PackageDecision
dotnet run --project src/dotnet/TraceMap.Cli -- package-decision --help
npm run check --prefix src/typescript
```

For a synthetic scan output, run:

```bash
tracemap package-decision --decision <package-decision.json> \
  --index <index.sqlite> --out <report-directory> [--format json] [--exit-code]

tracemap package-decision --decision <package-decision.json> \
  --index <web.sqlite> --label web \
  --index <api.sqlite> --label api \
  --out <report-directory> --include-paths --include-reverse

tracemap package-decision --decision <package-decision.json> \
  --manifest <portfolio.json> --out <report-directory>
```

Verify `package-decision-report.json` and `.md` preserve separate exact,
digest-mismatch, possible, ambiguous, excluded, and unknown sections; every
row carries rule, tier, span, and commit provenance; and missing digest or
direct/transitive capability is an explicit gap. Lockfile rows preserve the
resolved version, host-only registry origin, lockfile path/hash, declared
integrity digest, direct/transitive relation, and proven path depth. Optional
path/reverse context is bounded graph evidence and never upgrades a rung.
`--exit-code` is nonzero only
for an exact match tied to an external `reject` or `revoke` record. The command
does not fetch, execute, authenticate, approve, block, or enforce packages.

#### Pinned npm-lockfile admission and composition smoke

After `npm run check --prefix src/typescript`, use the built adapter and the
`axios-npm-lock` entry in `scripts/smoke-open-source-repos.sh`. Clone and
checkout may use the network; after checkout all scanning is offline. Do not
run npm, lifecycle scripts, builds, tests, binaries, or any package-manager
command in the scanned checkout. Keep the clone and every generated artifact
under one temporary directory and delete that directory after recording the
results. Pass the script paths below rather than using the script's persistent
default cache/output roots:

```bash
smoke_root="$(mktemp -d)"
trap 'test -n "${smoke_root:-}" && test -d "$smoke_root" && rm -rf -- "$smoke_root"' EXIT
TRACEMAP_SKIP_BUILD=1 TRACEMAP_OSS_SMOKE_REPOS=axios-npm-lock \
  scripts/smoke-open-source-repos.sh "$smoke_root/cache" "$smoke_root/out"
axios_npm_lock_scan="$smoke_root/out/axios-npm-lock"
```

The selector accepts a comma-separated list of documented labels; omit it to
run the complete OSS matrix. Keep this shell open through the validation below
so the trap removes only the mktemp-created root after results are recorded.

The pinned scan must contain all five standard scan artifacts and pass:

```bash
python3 scripts/validate-adapter-artifacts.py "$axios_npm_lock_scan"
```

Inspect the lockfile-sourced `PackageReferenced` facts. Require
`sourceKind=lockfile`, exact `resolvedVersion`, `lockfilePath`, `lockfileHash`,
host-only `registryOrigin` when the lock entry records one,
`artifactDigestAlgorithm=sha512-base64` plus an eligible declared integrity
digest, and proven direct/transitive `dependencyRelation`. Each row must retain
the `typescript.package.v1` rule, Tier 2 evidence, repository-relative lockfile
span, pinned full commit SHA, extractor identity/version, and deterministic fact
ID. Registry-declared integrity is metadata only, never downloaded or content
verified by TraceMap.

For downstream composition, derive one temporary `package-decision.v1` reject
record from an eligible public lockfile row (copy only its ecosystem, package
name, exact version, digest algorithm, and digest). Scan the checked-in
`samples/typescript-modern-sample` with the same built adapter and validate
that second scan output before combining it, then run:

```bash
synthetic_typescript_scan="$smoke_root/synthetic-typescript-scan"
node src/typescript/dist/src/cli.js scan \
  --repo samples/typescript-modern-sample --out "$synthetic_typescript_scan"
python3 scripts/validate-adapter-artifacts.py "$synthetic_typescript_scan"
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index "$axios_npm_lock_scan/index.sqlite" --label axios-npm-lock \
  --index "$synthetic_typescript_scan/index.sqlite" --label synthetic-typescript \
  --out "$smoke_root/combined.sqlite"
dotnet run --project src/dotnet/TraceMap.Cli -- report \
  --index "$smoke_root/combined.sqlite" --out "$smoke_root/combined-report"
dotnet run --project src/dotnet/TraceMap.Cli -- package-decision \
  --decision <temporary-decision.json> --index "$smoke_root/combined.sqlite" \
  --out "$smoke_root/decision-report" --include-paths --include-reverse
```

Require one `ExactArtifactMatch` for the generated record. The combined report
must preserve both source labels. If the selected package-config fact has no
graph attachment, the path/reverse result must be a typed unavailable gap, not
an invented path or reverse relationship. Repeat the scan-independent combine,
report, and package-decision commands with identical inputs and require
byte-identical report outputs.

### Package decision correlation (PR3: NuGet + Swift resolved evidence)

NuGet `packages.lock.json` and Swift lockfile evidence never prove an artifact
digest (NuGet `contentHash` is package-content metadata, a podspec checksum is
a podspec SHA-1, and SwiftPM/Carthage lockfiles carry no digest at all), so
both ecosystems correlate at `PossibleNameVersionMatch` with
`matchBasis=resolved-version` plus a `LockfileDigestUnavailable` gap and never
produce `ExactArtifactMatch`. Run the focused suites:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~PackageDecision
swift run --package-path src/swift tracemap-swift-smoke-tests
```

For the synthetic NuGet lockfile end-to-end smoke (offline, no restore):

```bash
smoke_root="$(mktemp -d)"
mkdir -p "$smoke_root/repo/src"
cp samples/package-decisions/nuget-lock-fixture/App.csproj "$smoke_root/repo/src/"
cp samples/package-decisions/nuget-lock-fixture/packages.lock.json "$smoke_root/repo/src/"
git -C "$smoke_root/repo" init -q
git -C "$smoke_root/repo" add .
git -C "$smoke_root/repo" -c user.email=tracemap@example.invalid \
  -c user.name=TraceMap commit -qm fixture
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo "$smoke_root/repo" --out "$smoke_root/scan"
dotnet run --project src/dotnet/TraceMap.Cli -- package-decision \
  --decision samples/package-decisions/nuget-lock-fixture/decision-nuget.json \
  --index "$smoke_root/scan/index.sqlite" \
  --out "$smoke_root/decision-report" --exit-code
```

Confirm: the direct record yields `resolved-version` possible rows for both
target frameworks with `dependencyRelation=direct`, the transitive record
yields `dependencyRelation=transitive`, `LockfileDigestUnavailable` and
`DirectTransitiveUnavailable`-bounded behavior stay explicit, the lockfile
`contentHash` values never appear in facts or reports, the exit code stays 0
(possible matches never trigger `--exit-code`), and repeated runs are
byte-identical.

For the Swift composed-consumer smoke, scan the checked-in dependency-surfaces
sample and correlate it through the Swift lockfile projection seam:

```bash
swift run --package-path src/swift tracemap-swift scan \
  --repo samples/swift-dependency-surfaces \
  --out /tmp/tracemap-swift-dependency-surfaces-pr3
python3 scripts/validate-adapter-artifacts.py /tmp/tracemap-swift-dependency-surfaces-pr3
dotnet run --project src/dotnet/TraceMap.Cli -- package-decision \
  --decision samples/package-decisions/swift-possible.json \
  --index /tmp/tracemap-swift-dependency-surfaces-pr3/index.sqlite \
  --out /tmp/tracemap-swift-decision-report
```

Confirm: SwiftPM `Package.resolved` pins, `Podfile.lock` PODS entries, and
`Cartfile.resolved` semver literals carry `resolvedVersion` (v1 and v2
`Package.resolved` schemas); revision-only, branch-only, and unsafe values
stay hashed with no `resolvedVersion`; `SPEC CHECKSUMS` render only as
explicitly labeled `specChecksum`/`specChecksumKind=podspec-sha1` metadata and
never as `artifactDigest`; the swift decision records correlate only as
`resolved-version` possible matches with the Swift lockfile rule IDs and tiers
on the evidence rows; no `ExactArtifactMatch` is possible; unsupported
`Package.resolved` schemas keep their gap; and outputs stay byte-deterministic
with no raw URLs, revisions, or non-hex checksum values.

### Package decision correlation (PR4: Python + JVM resolved evidence)

Python `uv.lock`/`poetry.lock` and JVM `gradle.lockfile` rows carry resolved
versions, lockfile path/hash, and (Python only, where proven) a direct or
transitive relation. None of these formats can prove an artifact digest against
a `package-decision.v1` record: Python lockfile hashes are wheel/sdist
artifact-form specific, `gradle.lockfile` has no hashes, and
`gradle/verification-metadata.xml` checksums cannot be tied to the record's
unnamed artifact form. Both ecosystems therefore correlate at
`PossibleNameVersionMatch` with `matchBasis=resolved-version` plus
`LockfileDigestUnavailable` (and `DirectTransitiveUnavailable` where the
relation is unproven) and never produce `ExactArtifactMatch` or
`ArtifactDigestMismatch`.

Adapter validation (Python): the temp venv pytest suite above covers the
uv/poetry happy paths, malformed/truncated/unsupported lockfiles, unsafe names
and versions, non-registry sources, duplicate entries, wheel-versus-sdist hash
ambiguity, uv development groups and qualifier-aware same-name resolution,
Poetry main/development/named groups, incomplete declaration gaps, absent
relation proof, Pipfile `unsupported-metadata`, and repeated deterministic
output. Adapter validation (JVM): `gradle -p src/jvm test`
covers gradle.lockfile rows, malformed/unsupported/unsafe rows, duplicate and
conflicting coordinates, Maven capability gaps, verification-metadata
non-consumption, and repeat-scan determinism.

For the Python end-to-end smoke, copy the fixture into a temporary git repo,
scan it with the Python adapter, validate the artifacts, and correlate:

```bash
smoke_root="$(mktemp -d)"
mkdir -p "$smoke_root/repo"
cp samples/package-decisions/python-lock-fixture/pyproject.toml \
   samples/package-decisions/python-lock-fixture/uv.lock "$smoke_root/repo/"
git -C "$smoke_root/repo" init
git -C "$smoke_root/repo" add .
git -C "$smoke_root/repo" -c user.email=tracemap@example.invalid \
  -c user.name=TraceMap commit -m fixture
/tmp/tracemap-python-venv/bin/python -m tracemap_py.cli scan \
  --repo "$smoke_root/repo" --out "$smoke_root/scan"
python3 scripts/validate-adapter-artifacts.py "$smoke_root/scan"
dotnet run --project src/dotnet/TraceMap.Cli -- package-decision \
  --decision samples/package-decisions/python-lock-fixture/decision-python.json \
  --index "$smoke_root/scan/index.sqlite" \
  --out "$smoke_root/decision-report" --exit-code
```

Confirm: `requests` (whose revoke record carries the sha256 value that equals
the lockfile's synthetic sdist hash) and `urllib3` correlate only as
`resolved-version` possible matches; the `requests` rows carry
`dependencyRelation=direct` and `urllib3` `transitive` (proven from well-typed
dependency declarations on the uv.lock root entry); every matched pairing reports
`LockfileDigestUnavailable`; the exit code stays 0; no `artifactDigest`
appears anywhere; and re-running the correlation produces byte-identical
outputs. A repository with an inventoried `Pipfile` emits an
`unsupported-metadata` analysis gap instead of silence.

For the Gradle end-to-end smoke, install the JVM scanner distribution, scan
the fixture repo, validate the artifacts, and correlate:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home \
  gradle -p src/jvm installDist
src/jvm/build/install/tracemap-jvm/bin/tracemap-jvm scan \
  --repo samples/package-decisions/gradle-lock-fixture \
  --out /tmp/tracemap-gradle-lock-fixture
python3 scripts/validate-adapter-artifacts.py /tmp/tracemap-gradle-lock-fixture
dotnet run --project src/dotnet/TraceMap.Cli -- package-decision \
  --decision samples/package-decisions/gradle-lock-fixture/decision-gradle.json \
  --index /tmp/tracemap-gradle-lock-fixture/index.sqlite \
  --out /tmp/tracemap-gradle-decision-report --exit-code
```

Confirm: `org.springframework:spring-web` (whose reject record carries a
sha256 digest) and `com.example:fixture-lib` correlate only as
`resolved-version` possible matches under `jvm.buildfile.v1` with the
`GradleLockfileExtractor` provenance; every matched pairing reports
`LockfileDigestUnavailable` and `DirectTransitiveUnavailable`; the exit code
stays 0; a scanned `pom.xml` additionally emits a `MavenLockfileUnavailable`
capability gap while its declared build-file rows are unchanged; and repeated
correlation runs are byte-identical.

### Package decision correlation (PR5: comparison, advisory, deployment references)

The before/after comparison mode, external advisory claims, and deployment
references are deterministic, read-only, and offline. Comparison rows are
cross-snapshot portfolio evidence, advisory claims are external producer
opinions, and deployment references are runtime-unproven lineage metadata.
Run the focused suite:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter FullyQualifiedName~PackageDecision
```

The committed fixtures are `samples/package-decisions/comparison/`
(before/after portfolio manifests plus `decision-comparison.json`),
`advisory-profile-example.json`, and `deployment-references-example.json`;
the focused tests build the synthetic indexes in a temp directory, copy the
committed manifests next to them, and assert the committed expected shapes.

For a synthetic comparison smoke, create two scan outputs (or any two
portfolio-manifest-paired index sets), then run:

```bash
tracemap package-decision \
  --decision samples/package-decisions/comparison/decision-comparison.json \
  --before-manifest <before-portfolio.json> \
  --after-manifest <after-portfolio.json> \
  --out <report-directory> [--exit-code]
```

Confirm: `ArtifactReplaced` appears only when both sides are digest-bound
with equal name and exact version and differing digests (each change row
carries both evidence chains and the fixed wording "cross-snapshot
portfolio evidence, not a single coherent release state"); digest-absent
evidence yields possible-only change rows that never claim replacement;
unchanged digest pairs produce no change row; added/removed evidence is
labeled possible; same-label repo-identity differences emit an
`IdentityAmbiguous` gap and downgrade the change classification; mixing
`--before-manifest`/`--after-manifest` with `--index`/`--manifest`, or
supplying only one of the pair, fails closed; the snapshot-mode
exact/possible/mismatch rungs are unchanged by comparison context.

For the advisory and deployment-reference smoke over any snapshot input:

```bash
tracemap package-decision --decision <package-decision.json> \
  --index <index.sqlite> \
  --advisory-profile samples/package-decisions/advisory-profile-example.json \
  --deployment-references samples/package-decisions/deployment-references-example.json \
  --out <report-directory> [--exit-code]
```

Confirm: the Advisory Claims (external) section renders producer identity,
profile version, canonical profile digest, and the external-opinion
limitation, and the claims never appear as facts or correlation rows and
never change rung counts, summary counts, path/reverse context, or the
exit code; every deployment reference renders as `RuntimeUnprovenReference`
with the fixed limitation "TraceMap did not verify the build, deployment,
installation, reachability, or runtime load", carries hashed source-repo
provenance, a bounded digest or name-version join detail, and never counts
as an exact match; `runtime-load`/`observed-execution` reference kinds and
severity/CVE-shaped advisory fields are rejected with closed-set input
gaps; repeat runs are byte-identical (JSON and Markdown).

### Cross-adapter scan-truth conformance

For changes to adapter inventory, scan identity, snapshot verification,
include/exclude matching, or artifact publication, run the offline synthetic
matrix from a Python environment containing the adapter test dependencies:

```bash
python scripts/scan-truth-conformance.py \
  --out /tmp/tracemap-scan-truth/readiness.json
```

The command builds the shipped .NET, JVM, Python, TypeScript, and Swift
adapters, runs each deterministic during-scan mutation fixture, and emits a
sanitized JSON and Markdown readiness report. It returns nonzero if any required
capability is unsupported or not run. Review each adapter row for concrete Git
authority, analyzed-byte identity, repeat determinism, same-size dirty changes,
inaccessible and mid-scan mutation truth, host-filesystem exclusion semantics,
reduced-analysis preservation, five-artifact publication, NDJSON/SQLite parity,
malformed-schema rejection, and repository-relative evidence. The matrix uses
only generated repositories and never proves semantic parity, runtime behavior,
build success, or complete dependency coverage.

### Legacy Web Forms static composition

For changes to Web Forms lifecycle context, client-script registration,
postback-target, or declarative data-binding evidence, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter LegacyWebFormsExtractorTests
```

The focused suite includes a synthetic, non-compiling .NET Framework 4.5 Web
Application fixture. Confirm that `!IsPostBack` context, supported literal
client-script registrations, literal `__doPostBack` targets, exact same-surface
`DataSourceID` matches, and literal `Eval`/`Bind` expressions emit deterministic,
rule-backed candidates. Confirm that dynamic or ambiguous shapes emit explicit
gaps, literal script and binding payloads are retained only as hashes, and
existing `.ashx`, handler, redirect/transfer, markup-event, lifecycle, and
reduced-compilation evidence remains present. These candidates do not prove
runtime reachability, execution, branch selection, rendering, postback dispatch,
or successful data binding.

### Web Forms annotated private source views

For changes to focused Web Forms code-path source navigation, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter WebFormsCodePathReviewTests
pwsh -NoProfile -File scripts/tests/Test-FocusedWebFormsCodePathReviewSet.ps1
```

Confirm that explicit raw-source opt-in creates deterministic sibling annotated
HTML files with complete bounded working-tree source, stable line anchors,
categorical retained-evidence highlighting, and bidirectional evidence links.
Confirm that default runs create no source views and anonymous HTML/JSON contain
neither source nor private navigation. Highlighting is static evidence
navigation; it does not prove runtime coverage, execution, branch feasibility,
correctness, or completeness.

### Web Forms private agent evidence handoffs

For changes to review-set evidence discovery metadata, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --filter 'FullyQualifiedName~WebFormsCodePathReviewTests|FullyQualifiedName~WebFormsAgentEvidenceHandoffTests'
pwsh -NoProfile -File scripts/tests/Test-FocusedWebFormsCodePathReviewSet.ps1
```

Confirm that each private case has a deterministic adjacent handoff, the root
handoff validates an explicitly supplied index and docs corpus against the
inspection scan and commit, and every retrieval hint names a closed read-only
TraceMap query recipe. Confirm that mismatches fail before root publication and
anonymous artifacts contain no handoff links, private identities, local paths,
fact IDs, or source-of-truth locators. Application-database questions must never
contain credentials, configuration, raw SQL, or execution instructions.

### Web Forms full application workbench

For changes to all-surface packet review, run:

```bash
pwsh -NoProfile -File scripts/tests/Test-FocusedWebFormsApplicationWorkbench.ps1
```

Confirm that one deterministic report/handoff pair is generated per selected
surface, reports return to the root index, raw source remains opt-in and bounded,
and an explicitly supplied evidence-docs corpus remains byte-unchanged. The
workbench is navigation over retained evidence, not a scan, BRD, runtime claim,
or human-review overlay. Confirm that call projections, unique call facts, and
normalized source call sites remain distinct; matching syntax/semantic evidence
collapses only in the normalized view; compiler-resolved declaring type, assembly,
and technology-family metadata survives into the private handoff; and reaching the
256-fact call-evidence ceiling is visible separately from traversal truncation.
Ceiling detection is a bounded-coverage warning and must not be rendered as proof of
an exact source-call count.

For inline Web Forms client behavior, also confirm that supported jQuery event
bindings, mutations, and numeric maximum-length constraints retain Tier3 rule
IDs and exact markup spans through the modernization packet, docs export, and
page handoff. Trigger rows must render their retained binding and handler spans,
and source excerpts must remain readable without inheriting inline-code block
background styling.

### Web Forms one-root review pipeline

For changes to clean-run setup, persisted configuration, bounded project
selection, resume behavior, or the one-root artifact layout, run:

```bash
pwsh -NoProfile -File scripts/Invoke-FocusedWebFormsReview.Tests.ps1
pwsh -NoProfile -File scripts/tests/Test-FocusedWebFormsPipeline.ps1
pwsh -NoProfile -File scripts/tests/Test-FocusedWebFormsApplicationWorkbench.ps1
```

Confirm the generated config has exactly seven operational settings; explicit
solution, project, discovery, and projectless modes remain distinct; discovery
does not escape the three configured roots; and all-page mode does not create a
surface list. Confirm an unescaped Windows path fails before JSON parsing with
forward-slash guidance, including a path containing `\t` that the JSON parser
could otherwise silently interpret as a tab. A clean end-to-end fixture must
publish scan, packet, evidence-docs, and workbench folders under one review
root. Immediately rerun
the unchanged command and confirm every completed stage reports `state=reused`
under the same run ID. Changing the config, source commit, TraceMap commit,
pipeline generator, or any retained artifact must fail resume validation.

Inspect `run-receipt.json` and confirm it records the config and generator
SHA-256 values, source and TraceMap commits, and the exact relative path, byte
count, and SHA-256 for every retained stage artifact. Public/shareable
regression checks must continue to reject private paths, symbols, source, scan
identity, commit identity, and private-input fingerprints.

## Required multi-language canonical-identity corpus

This required track is now scoped by issue #767 and
`.kiro/specs/compiled-dotnet-evidence-foundation/`. Its fixture and platform
matrix is authoritative; do not maintain a second case list here.

The validation floor remains: checked-in public C#, VB.NET, and F# fixtures;
semantic and failed-build/projectless lanes where supported; explicit F# source
coverage gaps until an adapter exists; exact full-signature identities; bounded
ambiguity gaps; validated `facts.ndjson` and `index.sqlite`; deterministic
repeat scans; and unchanged source-derived evidence when compiled inputs are
missing, stale, ambiguous, unbound, mismatched, unreadable, or unsupported.

The default fast suite runs portable managed fixtures on macOS and Windows.
Legacy .NET Framework builds, Windows PDB behavior, ILAsm/ILDAsm, Web Forms
build behavior, the historical `dotnetperf` corpus, and C++/CLI remain explicit
Windows lanes. A macOS pass must report those checks as not run rather than
implying coverage.

### Compiled .NET evidence foundation

The first compiled-evidence slice accepts only assemblies named explicitly by
`--compiled-input` and dependencies named explicitly by
`--compiled-dependency`. Both options are repeatable. It does not discover or
load dependencies from the host, NuGet cache, runtime directory, application
base, or `PATH`. Run the portable fixture matrix with:

```bash
dotnet restore src/dotnet/TraceMap.sln --locked-mode
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --no-restore --filter FullyQualifiedName~ManagedMetadataExtractorTests
dotnet test src/dotnet/TraceMap.sln --no-restore
```

The focused matrix builds public C#, VB.NET, and F# fixtures and checks exact
assembly, module, type, field, method, constructor, property, and event
identities. It also covers global and colliding namespaces, nested and generic
types, overloads, generated members, full CLR signatures, deterministic repeat
output, metadata-location round trips, missing/malformed/native/over-budget
inputs, reader disagreement, unbound/stale/mismatched provenance, duplicate
assemblies, metadata-bearing secondary modules, delimiter-bearing identity
components, filesystem-semantic receipt-path deduplication, source-analysis
level isolation, and zero/multiple declared dependency candidates. The
`local-distribution-validation.yml` macOS and Windows jobs run this same focused
matrix; Windows-specific PDB, legacy framework, Web Forms build, historical
corpus, and C++/CLI lanes remain deferred.

A representative local scan is:

```bash
compiled_fixture="$(pwd)/samples/compiled-dotnet-evidence/csharp/bin/Debug/net10.0/CompiledEvidence.CSharp.dll"
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo samples/modern-sample \
  --out /tmp/tracemap-compiled-scan \
  --compiled-input "$compiled_fixture"
python3 scripts/validate-adapter-artifacts.py /tmp/tracemap-compiled-scan
```

Relative compiled-input paths are resolved against `--repo`; use an absolute
path when the admitted binary is outside that repository root.

Inspect all five required artifacts. `scan-manifest.json` must contain
`compiledInputProvenance` with expected inputs, effective limits, ordered
outcomes, generator and bounded-input SHA-256 values, coverage, and
`artifactVisibility=local-only`. Compiled facts must use safe locators,
`evidenceLocationKind=managed-metadata-v1`, module-local metadata tokens, and a
`1..1` non-source sentinel with no source snippet hash. Adding compiled inputs
must not change normalized source facts.

Optional receipts use `compiled-input-binding-set.v1` with a `bindings` array.
Each `compiled-input-binding.v1` entry names the exact `safeLocator`, artifact
SHA-256, optional exact assembly identity, binary source repository, 40-hex
source commit, and binary build identity. When the source commit differs from
the scan commit, `binarySourceCommitRelation` must be exactly
`ancestor-of-scan` for the input to be classified as stale; an unequal commit
without that externally validated relation is a mismatch, because inequality
alone does not prove ancestry. A receipt is bound only when the artifact and
optional assembly identity match and all source/build fields are complete;
otherwise the lane emits an explicit incomplete, stale, mismatch, or unbound
gap. Receipt paths, raw repository names, and raw build identities are not
emitted; local facts retain SHA-256 commitments for repository/build identity
plus the validated source commit and categorical binding state. Any receipt
read, limit, ambiguity, or schema gap makes compiled coverage partial even when
all admitted binaries have otherwise bound receipts.

The admission budget defaults to 32 artifacts, 64 MiB per file, 50,000 types,
250,000 members, 4,096 characters per retained text value, and 500,000 total
work units. Override these only with the positive `--compiled-max-artifacts`,
`--compiled-max-file-bytes`, `--compiled-max-types`,
`--compiled-max-members`, `--compiled-max-text`, and `--compiled-max-work`
options. All limits must be positive, and `--compiled-max-text` must be at least
71 characters so a privacy-projected locator can retain its complete SHA-256
identity. A limit failure is partial coverage, never a clean or complete result.
When declarations exceed the artifact limit, provenance retains no more than
the configured number of per-input rows and records the omitted declaration
count plus a SHA-256 commitment over their privacy-projected identities.
Receipt paths use the same file/count/text/work budget and a maximum nesting
depth of 16; metadata-row work for both independent readers is charged from the
total-work budget before either reader materializes observations.

### Exact source-to-metadata reconciliation

Task 8 activates `dotnet.compiled.source-identity.v1`. The reconciler consumes
compiler-resolved C# and Visual Basic declaration identities without changing
their ordinary source facts, and compares them only to the complete normalized
managed metadata identity. A positive `SourceMetadataIdentityReconciled` edge
is Tier1 semantic evidence and requires exactly one metadata candidate plus a
validated `bound` compiled-input receipt. The edge retains the source and
metadata endpoint identities, source and compiled supporting fact IDs, rule
and extractor versions, bounded-input and generator SHA-256 values, receipt
binding SHA-256, compiled provenance state, relationship proof, and limitation.
The reconciliation-only source endpoint is a complete source-derived normalized
metadata shape, including enclosing generic arity, generic-parameter ordinals,
method arity, ref/ByRef modes, constructed enclosing-type arguments, and the
complete signature. The original Roslyn
declaration identity is retained separately as `sourceDeclarationIdentity`;
source-only scans and their ordinary source fact identities are unchanged.
Named signature types include their full assembly-reference scope on both the
Roslyn and managed-metadata paths, preventing same namespace/name types from
different assemblies from comparing equal. Primitive signature codes retain
their intrinsic ECMA identity; `System.Decimal`, which metadata encodes as a
scoped value-type reference rather than a CLI primitive, retains that scope.
Other Roslyn special types that lack CLI primitive signature codes, including
`System.DateTime`, also follow the scoped named-type path.
Roslyn error types and unavailable type scopes
fail closed as incomplete identities and can never produce a Tier1 edge.
Top-level C# statements are not declarations and do not enter the candidate
lane. If Roslyn cannot resolve a declaration symbol, its syntax-located
observation is Tier3 rather than Tier1 and remains paired with an explicit
Tier4 incomplete-identity gap.

The following never select a candidate: display strings, simple names,
equal arity, path proximity, timestamps, or metadata tokens. Zero candidates,
multiple candidates, incomplete source identities, optional-parameter state
disagreement, and unbound, stale, mismatched, ambiguous, disputed, unsupported,
or incomplete compiled evidence emit Tier4 `AnalysisGap` facts and no edge.
When an exact metadata candidate is rejected for optional-parameter mismatch,
the gap retains that candidate's compiled provenance state and receipt-binding
digest.
Compiler-generated members remain separate except for Roslyn's explicit
associated property/event accessor relationship. State machines, lambda
methods, backing fields, and generated types are not inferred back to source.

The public cases are versioned in
`samples/compiled-dotnet-evidence/fixture-cases.json`. They record stable case
IDs, exact expected source and metadata identities, expected rule and tier,
expected gaps, and non-claims. The focused tests cover namespaces, nested and
generic types, overloads with complete signatures, constructors,
properties/indexers, events and accessors, `ref`/`ByRef`, optional parameters,
explicit interfaces where representable, scoped decimal signatures,
scoped non-primitive special types, constructed nested signatures with
outermost-first arguments even when the nested type declares no parameters, and
same-looking declarations across assemblies and languages. The C# matrix also
pins class, struct, record-class, and record-struct primary constructors, ref
field signatures, and the intrinsic `System.TypedReference` metadata shape.
Source custom modifiers fail closed rather than producing a partial identity.
F# has no source adapter: its compiled identities
remain available, one explicit unsupported-source-adapter gap is emitted, and
no source join is guessed.

Run the reconciliation lane with:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --no-restore --filter FullyQualifiedName~SourceMetadataReconciliationTests
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --no-restore --filter FullyQualifiedName~ManagedMetadataExtractorTests
```

For positive CLI scans, pass the exact fixture assembly and its validated
`compiled-input-binding-set.v1` receipt. Inspect `facts.ndjson` and
`index.sqlite` for the exhaustive edge/gap rows. `scan-manifest.json`,
`report.md`, and `scan-receipt.json` carry the bounded
`source-metadata-reconciliation.v1` summary; each retained entry keeps both
endpoint identities and provenance, while any receipt-view overflow is
committed by omitted count and SHA-256. Repeated-scan checks compare
`facts.ndjson`, the human report, the reconciliation summary, and the indexed
fact rows byte-for-byte; the operational wall-clock `scannedAt` field and
receipt stage durations are intentionally not evidence identifiers.

Summary entries also retain their own evidence fact ID, source file and line
span, commit SHA, exact total join/gap counts, and known rejected-provenance
state. Per-entry compiled supporting IDs are bounded to 256 with an omitted
count and digest; summary overflow hashing uses length-framed canonical values
for every serialized entry field. The Markdown report independently discloses
its 50-row display bound and points to exhaustive `facts.ndjson` and
`index.sqlite` rows. If semantic source identity collection is unavailable,
reconciliation coverage is `source-metadata-partial` even when no candidate row
could be emitted. `Level1SemanticAnalysisReduced` is also partial because a
failed project may have omitted declarations even when every retained candidate
joins; this does not change compiled-input coverage.

Reconciliation coverage is independent of `analysisLevel`. Missing or partial
compiled inputs never erase, re-tier, or otherwise change source-derived facts.
Task 8 does not itself read PDBs or sequence points, inspect IL bodies or calls,
perform rewrite analysis, execute private or historical corpora, add legacy
Framework/Web Forms or C++/CLI support, or introduce fuzzy/AI matching. The
separate Task 9 contract below adds only the PDB layer.

The C# and Visual Basic changes in this slice are restricted to the internal
candidate lane activated by explicit compiled inputs. The full .NET suite and
source-only partial-compilation regression are required to prove ordinary
adapter facts stay unchanged. The pinned C#/VB public OSS smoke is explicitly
deferred for this slice because it does not supply admitted compiled inputs and
therefore cannot exercise source-to-metadata reconciliation; no public-smoke
coverage claim is made.

### Independent source canonical-identity matrix

The compiled-evidence matrix does not replace the existing source-side adapter
regressions. The source matrix remains independently required and must cover:

- identical simple type and member names across namespaces, assemblies,
  projects, and languages;
- nested/repeated and partial types, VB root namespaces, aliases/imports, and
  linked source files;
- equal-arity overloads with different complete signatures, constructors,
  properties/indexers, operators, inheritance, and cross-language candidates;
- reflection, runtime assembly loading, generated/dynamic assemblies, and
  unresolved factory or registration paths that must fail closed; and
- end-to-end repository scans, `facts.ndjson`/`index.sqlite` validation,
  multi-source combine, reducers/reports, bounded ambiguity gaps, and
  deterministic repeat outputs.

Where an adapter lacks a source lane, including F# until its adapter exists,
the matrix must assert explicit unsupported coverage and zero inferred source
joins. Task 8 of the compiled-evidence foundation may consume these source
fixtures for reconciliation, but it must not redefine or remove their
source-only acceptance contract.

### PDB identity and sequence-point evidence

Task 9 activates `dotnet.compiled.pdb-input.v1`,
`dotnet.compiled.pdb-identity.v1`, `dotnet.compiled.sequence-point.v1`, and
`dotnet.compiled.pdb-gap.v1`. PDB discovery is never ambient. Supply each
candidate explicitly with `--pdb-input`; the scanner applies the positive
artifact, byte, document, method, sequence-point, source-file, source-byte,
text, and total reconciliation-work limits controlled by the `--pdb-max-*`
options. Paths are resolved and deduplicated using the checkout filesystem's
case semantics before admission, so relative, absolute, and `./` aliases of
one present or missing PDB count as one input. PDB binding reads are derived
only from the admitted compiled-input
descriptors retained by the compiled evaluator; omitted paths are never
reopened or allowed to reenter candidate selection. Every SRM document/method
row and sequence point is charged before work proceeds. Cecil type traversal is
iterative, and every type, method, and retained sequence point is charged so
deep empty nesting cannot bypass the work budget or consume the process stack.
Binding admission retains only a verified artifact path, digest, and CodeView
identities, not every compiled assembly's bytes. The uniquely matched assembly
is reread under the compiled file-size bound and its admitted digest is
reverified immediately before the independent Cecil comparison. A changed,
missing, oversized, or unreadable matched assembly emits
`PdbCompiledArtifactChangedOrUnreadable` and no positive PDB facts. Compiled
binding and PDB file reads poll scan cancellation between bounded chunks.
Source files are streamed once into reusable
SHA-1/SHA-256 indexes, poll scan cancellation during file reads, and are never
reread once per PDB document. Post-admission metadata-method reconciliation
uses a single index keyed by assembly locator and MethodDef token; sequence
points use a per-PDB document-row index. Neither lookup repeatedly scans the
full fact or document collection. The manifest and execution
receipt retain `pdb-input-provenance.v1`, including the exact generator SHA-256,
canonical bounded-input SHA-256, safe locators, effective limits, per-input
outcomes, omissions, and input-only coverage state. Source or metadata
reconciliation never rewrites this input commitment. They also retain the bounded
`pdb-evidence-summary.v1` endpoint/support summary and its omitted-entry digest.
The summary input digest additionally commits the source snapshot, scan commit,
and retained PDB fact set because source-document edges depend on those inputs.
The PDB digest participates in `scanId` before PDB fact IDs are derived.

A portable PDB is admitted only when its exact portable content GUID/stamp
matches exactly one CodeView directory entry from exactly one explicitly admitted managed
assembly and that assembly has acceptable `bound` compiled provenance. File
names, path proximity, timestamps, display strings, and metadata tokens alone
never establish this binding. Duplicate matching CodeView entries in one PE
remain multiple candidates and emit `AmbiguousPdbAssemblyMatch`; they are not
collapsed by identical identity text. System.Reflection.Metadata reads the portable
document, method-debug-information, and ordered sequence-point rows; Mono.Cecil
independently reads the bound assembly/PDB pair. TraceMap compares complete
per-method sequence-point shapes including token, ordinal, IL offset, document
checksum, hidden state, and exact source range. Reader disagreement withholds
all positive PDB facts for that input and emits `PdbReaderDisagreement`. The
comparison uses duplicate-sensitive shape counts, not uncharged sorting;
each observed comparison consumes a PDB work unit.

An admitted input emits separate document and method facts. A
`MetadataPdbMethodReconciled` edge requires exactly one eligible metadata
method from the same bound assembly and the exact module-local MethodDef row.
Every positive fact retains the PDB content identity, raw local PDB digest,
bounded-input and generator digests, matched assembly identity and safe
locator, compiled receipt-binding digest, provenance state, rule, tier,
extractor version, and limitation. Document, method, and sequence-point rows
also retain their supporting PDB input/document/method fact IDs; method edges
retain the exact compiled fact ID; sequence points retain the exact
metadata/PDB reconciliation fact ID. No sequence-point fact is emitted unless
that exact one-candidate method reconciliation exists. Summary endpoints also
retain file path, structured line span, and commit SHA directly. These are evidence relationships, not IL
body or call extraction.

PDB document names are not emitted. Source-document reconciliation compares a
supported SHA-1 or SHA-256 document checksum to inventoried C# and VB
source bytes, including the specialized C#/VB source kinds already classified
by `FileInventory` such as code-behind, designer, generated, and assembly-info
files. This is checksum indexing, not additional legacy-framework analysis, and
emits a Tier2 structural checksum edge only for exactly one candidate. Zero candidates,
multiple candidates, unsupported checksum algorithms, and F# source documents
emit explicit gaps and no edge. F# still retains its compiled PDB document,
method, metadata reconciliation, and sequence-point facts; because no F# source
adapter exists, it emits `PdbSourceReconciliationUnsupportedLanguage` and zero
guessed source-document joins. PDB coverage and source `analysisLevel` remain
independent, and missing or partial PDB evidence never changes source facts.
Any source-, method-, reader-, or input-reconciliation gap makes final PDB
coverage `pdb-partial` in the manifest's evidence summary, report, and receipt.
`PdbInputProvenance.CoverageState` describes only the admitted PDB/assembly
inputs committed by its bounded-input digest; it can remain `pdb-complete`
when source or method reconciliation is partial. The summary commits the
source snapshot, scan commit, and retained PDB fact IDs. Its omitted endpoints
are digested incrementally in the same canonical JSON order without retaining
exhaustive summary records.

The v3 public fixture catalog adds stable PDB case IDs, expected identity
formats, rule/tier expectations, gaps, and non-claims. The portable C#/VB/F#
matrix covers exact binding, mismatched/unbound inputs, zero and multiple
source-checksum candidates, hidden points, multi-document methods,
non-monotonic ranges, async/iterator/lambda generated members, malformed and
missing inputs, limit exhaustion, and deterministic repeat output. Generated
state-machine and lambda members retain their own metadata/PDB identities and
are not collapsed back to a source declaration.

Native Windows PDBs are recognized only by the complete MSF 7.00 container
signature and are intentionally fail-closed in this contract. Other
non-portable bytes are `MalformedPdbInput`. On macOS
and Linux they emit `WindowsPdbRequiresWindows`; on Windows they emit
`WindowsPdbIndependentReaderUnavailable`. The Windows CI lane builds real C#
and VB native PDBs with the Windows desktop Roslyn compilers and `/debug:full`,
then proves that no positive document,
method, or sequence-point facts escape that gap. Mono.Cecil's native reader is
not accepted as a sole identity oracle. Positive native Windows PDB support
requires a separately documented independent reader/cross-check contract; no
portable-equivalence claim is made.

Run the PDB lane with:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --no-restore --filter FullyQualifiedName~PortablePdbExtractorTests
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --no-restore --filter FullyQualifiedName~SourceMetadataReconciliationTests
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj \
  --no-restore --filter FullyQualifiedName~ManagedMetadataExtractorTests
```

For a positive CLI scan, pass the exact fixture assembly, its validated
`compiled-input-binding-set.v1` receipt, and the matching portable PDB. Verify
all five required scan artifacts plus `scan-receipt.json`. Repeat scans must
have byte-identical `facts.ndjson` and `report.md`, identical PDB provenance,
and equivalent indexed PDB rows. Operational timestamps and receipt durations
remain non-evidence diagnostics. Task 9 performs no IL body/call extraction,
rewrite analysis, private or `dotnetperf` corpus execution, legacy
Framework/Web Forms build, C++/CLI work, graph database work, fuzzy matching,
or AI classification.

### IL body and call evidence (Task 10 first slice)

The first Task 10 slice activates `dotnet.compiled.il-body.v1`,
`dotnet.compiled.il-call.v1`, and `dotnet.compiled.il-gap.v1` behind the
explicit `--il-body-evidence` flag. The lane is otherwise inert: without the
flag a scan produces no IL facts, no `ilBodyProvenance` manifest section, no IL
known gaps, and unchanged source, compiled-metadata, and PDB behavior. IL
inputs are never discovered; the lane processes only the artifacts the
compiled evaluator itself admitted, rereads each one under the compiled
file-size bound, reverifies its admitted SHA-256 immediately, and emits
`IlCompiledArtifactChangedOrUnreadable` when the bytes changed, went missing,
or grew too large. Requesting the flag without any admitted compiled input
emits the rule-backed `IlCompiledEvidenceUnavailable` gap.

The canonical body identity is operand-aware by construction:
`<exact metadata method identity>|il-body:instructions:<n>:sha256:<digest>`,
where the digest commits the complete canonical encoding — opcode sequence,
resolved direct-call target identities with module-local tokens, branch and
switch target offsets, string-literal length and SHA-256 digests, numeric
constant bit patterns, variable and argument indexes, raw non-call token
operands, ordered local-variable signatures, exception-region boundaries with
catch-type identities and filter offsets, and max stack. Equal opcode
sequences with different member, string, constant, or branch-target operands
therefore produce different identities, and the fixture matrix proves each
pair. The digest is byte-layout sensitive by design; this slice makes no
semantic-equivalence or rewrite claim in either direction.

Short integer operands preserve signed `ldc.i4.s` constants and unsigned
prefix bytes such as `unaligned.`. User-string digests hash the exact UTF-16
code units in little-endian order, including unpaired surrogates; replacement
fallback must not collapse distinct literal operands. Regression fixtures
cover all three valid `unaligned.` alignments and distinct high-surrogate,
low-surrogate, and replacement-character operands.

Mono.Cecil is not the sole oracle. Mono.Cecil and an independent
System.Reflection.Metadata single-pass raw-IL reader (opcode tables plus
metadata token resolution) each rebuild the complete canonical encoding for
every admitted input, and the two results must agree on the assembly and
module identity, the method identity, every body digest, every call-site
offset, opcode, reference kind, reference token, and target identity, the
locals, the exception regions, and max stack. Any disagreement withholds that
input's positive IL facts behind `IlReaderDisagreement`. Fields that cannot
yet be independently verified are not promoted to positive evidence:
non-call token operands such as field and signature tokens are committed by
raw module-local token only, and string literals are committed by digest only
and never retained verbatim because literal text is unbounded and may contain
secrets. The `constrained.` prefix target is cross-checked as a
`constrainedtype` call observation, but its module-local token stays
explicitly unclaimed because Mono.Cecil cannot reproduce the raw TypeSpec
token after resolving the operand. `--il-max-text` bounds user strings,
resolved target identities, and body identities on both readers; a violation
emits `IlTextLimitExceeded` and withholds the input. The raw reader runs
first, validating opcode bytes, operand extents, and switch jump tables —
including overflow-safe table-extent checks and per-target work charges —
before Mono.Cecil materializes the same operand.

Positive facts keep every required commitment: exact assembly identity, module
name and MVID, module-local MethodDef token, `evidenceLocationKind=
managed-il-v1` with the documented `1..1` non-source sentinel, provenance
state, compiled receipt-binding digest when bound, extractor version,
generator SHA-256, IL bounded-input SHA-256, and the rule limitation. Body
facts additionally retain instruction, local, and exception-region counts and
digests and the supporting compiled `ManagedMethodDeclared` fact ID when
exactly one candidate exists; call facts retain the IL offset, opcode,
reference kind, raw reference token, complete member-reference identity as
encoded in the containing module, and the supporting IL body fact ID. IL user
strings are digested, not stored. Body facts are separate evidence nodes from
source, metadata, PDB, and future rewritten-member identities, and no
source-to-IL or rewrite-equivalence edge is emitted.

Body, instruction-per-body, local-per-body, exception-region-per-body, and
total-work limits are enforced before retention, with every body,
instruction, local, region, and call charged to the shared work budget across
both readers. Exhaustion, malformed IL, unsupported operand encodings, and
unreadable inputs emit Tier4 `dotnet.compiled.il-gap.v1` gaps for that input
with no partial positive set. Abstract, external, PInvoke, and bodyless
methods emit no body fact as a structural observation, not an absence claim.

Run the focused lane with:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj   --no-restore --filter FullyQualifiedName~IlBodyEvidenceExtractorTests
```

For a positive CLI scan, pass an admitted assembly plus the flag:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- scan   --repo samples/modern-sample   --out /tmp/tracemap-il-scan   --compiled-input "$(pwd)/samples/compiled-dotnet-evidence/csharp/bin/Debug/net10.0/CompiledEvidence.CSharp.dll"   --il-body-evidence
```

Repeat scans must produce byte-identical `facts.ndjson` and `report.md`,
identical `ilBodyProvenance` (schema `il-body-provenance.v1`) in the manifest
and execution receipt, and matching `dotnet.compiled.il-*` rows in
`index.sqlite`. The IL bounded-input digest participates in `scanId`. The
fixture catalog records stable IL case IDs under
`samples/compiled-dotnet-evidence/fixture-cases.json` (schema v5 or later),
covering operand-distinct pairs, signature and assembly scoping, call kinds,
locals and exception regions, hostile corrupted IL, and limit exhaustion.

This slice explicitly defers and makes no claim about: rewritten-member
identity, metadata-token retargeting across rewrites, rewritten PDB offsets,
ILAsm/ILDAsm parity, and the extended ECMA-335 mutation matrix from #766. It
performs no rewrite generation, no call-graph or transitive reachability
analysis, no runtime loading or execution, and no cross-assembly resolution
beyond the reference rows encoded in the containing module. The next slice
below begins the bounded rewrite work; everything else stays deferred.

### IL rewrite evidence (Task 10 second slice)

The second Task 10 slice activates `dotnet.compiled.il-rewrite.v1` and
`dotnet.compiled.il-rewrite-gap.v1` behind the explicit
`--il-rewrite-evidence` flag with ordinal `--il-rewrite-before` and
`--il-rewrite-after` inputs (equal counts required; the CLI rejects unflagged
or unpaired declarations) and `--il-max-rewrite-pairs`. The scanner never
performs or attributes a rewrite: both sides are operator-declared bounded
inputs, admitted under the compiled file-size/text bounds with safe external
locators and raw SHA-256 commitments. The shared compiled-input preflight
rejects native/mixed-mode inputs, netmodules, and multi-module manifests, and
enforces type/member row limits plus a compiled metadata work budget shared
across both sides and all pairs. Pair count is governed by
`--il-max-rewrite-pairs`; metadata work and IL body work have separate budgets.
A file that grows past its byte limit during reading produces a side gap.
Each side must independently pass
the full first-slice dual-reader IL body contract (raw
System.Reflection.Metadata decode first, then Mono.Cecil, then exact
comparison) before any join is attempted. A side that is missing, unreadable,
oversized, malformed, disputed, unsupported, or over-limit withholds the whole
pair behind Tier4 gaps; every side-scoped failure is emitted as its own gap
fact carrying that failing side's `side`, `cause`, and evidence locator, and
outcome summaries retain the exact `side:cause` pairing. No partial edge set
is emitted, positive edges always keep the `managed-il-rewrite-v1` location
kind, and pair outcome labels are exact (`unavailable`, `malformed`,
`disputed`, `mismatched`, `ambiguous`, `unsupported`, `invalid`,
`membership-delta`, or `limit-exhausted`) rather than a generic fallback.
The lane validates the reused body and compiled limits identically to their
owning extractors before any provenance exists, and the omitted-membership
digest commits exactly the identities beyond the retained prefix.

An edge is emitted only when the complete exact assembly-scoped method
identity text occurs exactly once on each side. The edge records both assembly
identities, both module-local tokens (with `tokenRetargeted`), both canonical
body identities and digests, a relationship kind — `unchanged`,
`operand-only-change`, `body-structure-change`,
`operand-and-body-structure-change`, or `instruction-stream-change` — and
`opcodeSequencePreserved`. `operand-only-change` additionally requires every
non-instruction body component (locals, exception regions, max stack,
init-locals) to match, so structural body changes are never mislabeled as
operand-only. Call-site retargets are recorded per ordinal
alignment (both tokens, both target identities, both IL offsets) only when
instruction counts and opcode sequences are exactly equal; the shared
opcode-name digest computed by both readers proves the alignment. Zero or
one-side-only membership emits bounded `IlRewriteMethodBeforeOnly` /
`IlRewriteMethodAfterOnly` gaps (retained identities capped at eight per side
with an omitted-count digest commitment) rather than guessed insertion or
removal edges; more than one candidate on either side emits
`IlRewriteIdentityAmbiguous`; differing assembly identities emit
`IlRewriteAssemblyIdentityMismatch` with no joins. Join work is charged to the
shared `--il-max-work` budget; exhaustion is atomic per pair and emits only
`IlRewriteTotalWorkLimitExceeded` — no partial edges, membership deltas, or
other gap kinds survive a join-phase exhaustion. Requesting the flag without
pairs emits `IlRewritePairUnavailable`; count mismatches emit
`IlRewritePairDeclarationInvalid`; malformed declared paths fail closed to
`IlRewriteSideUnavailable` with cause `IlRewriteSideDeclarationInvalid` and a
privacy-projected locator instead of aborting the scan. Repeated identical
`(before, after)` declarations are not deduplicated: every declared ordinal
keeps its own outcome, and the bounded-input digest — which also commits the
effective `CompiledInputLimits` admission policy alongside the rewrite and
body limits — stays distinct from a single-declaration scan. Rejected blank
or unequal-length declarations also commit the ordered privacy-projected
slots, so changed paths or blank positions cannot share a bounded-input
digest. Execution-receipt scope fingerprints preserve the same declaration
order and blank slots with unambiguous framing.

The lane is otherwise inert: without the flag a scan produces no rewrite
facts, no `ilRewriteProvenance` manifest section, no rewrite known gaps, and
unchanged source, compiled-metadata, PDB, and IL body/call behavior including
declared-but-unused pair paths. The manifest, execution receipt, and report
gain `il-rewrite-provenance.v1` with generator SHA-256, canonical
bounded-input SHA-256, effective limits, and per-pair outcomes; the digest
participates in `scanId`. Outcomes never contain raw paths; external inputs
use the established privacy-projected `__external__/<role>/` locators, and the
local-only visibility contract is unchanged.

Mono.Cecil is used only to generate deterministic synthetic mutations in the
public test suite (constant operand change, inserted member that renumbers
MethodDef tokens, rewired call target, duplicated identity, renamed
assembly); the scanner itself only verifies. Identical before/after inputs
must prove every body `unchanged` with zero gaps. Run the focused lane with:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj   --no-restore --filter FullyQualifiedName~IlRewriteEvidenceExtractorTests
```

For a positive CLI scan, pass an explicit pair:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- scan   --repo samples/compiled-dotnet-evidence/csharp   --out /tmp/tracemap-ilrewrite-scan   --il-rewrite-evidence   --il-rewrite-before "<before.dll>"   --il-rewrite-after "<after.dll>"
```

Repeat scans must produce byte-identical `facts.ndjson` and `report.md`,
identical `ilRewriteProvenance` in the manifest and execution receipt, and
matching `dotnet.compiled.il-rewrite*` rows in `index.sqlite`. The fixture
catalog records stable rewrite case IDs under `ilRewriteCases` in
`samples/compiled-dotnet-evidence/fixture-cases.json` schema v5, covering
same-opcode operand changes, token retargeting, call retargeting, unchanged
bodies, duplicate identity, hostile malformed sides, reader disagreement, and
budget exhaustion.

This slice explicitly defers and makes no claim about: rewritten PDB offsets
and sequence-point validity after a rewrite, ILAsm/ILDAsm parity,
evaluation-stack-sensitive rewrites, netmodules, type forwarding, duplicate
assembly identities, insertion/removal relationships, the extended ECMA-335
mutation matrix from #766, and Task 11's legacy Windows, `dotnetperf`, and
C++/CLI lanes. It performs no rewrite generation by the scanner, no semantic
equivalence or behavior-preservation conclusion, no runtime loading or
execution, and no cross-assembly resolution beyond the rows encoded in each
containing module.

### Messy .NET workspace regression (Task 10 third slice)

Public-safe synthetic fixtures under `samples/messy-dotnet-workspace/`
reproduce the workspace shapes observed in real Web Forms/.NET scans without
copying any private source, names, paths, or artifacts. Three roots are
scanned independently: `root-alpha` (C# Web Forms site with a twelve-call-edge deep
chain ending in an ADO.NET-style SQL terminal at graph distance 14, a
three-node cycle plus a self-cycle with its own handler, and ten same-name
`Process`/`Core` members in one file), `root-beta` (a second C# root reusing those simple
names), and `vb-projectless` (loose VB files with no `.vbproj`/`.sln`).

The stable case catalog is `samples/messy-dotnet-workspace/case-catalog.json`
(schema `messy-workspace-case-catalog.v1`). Cases are marked `implemented` or
`deferred`; deferred cases record their exact blocker or next-slice owner
(overload ambiguity, receiver ambiguity, C#/VB/F# boundaries, generated
members, and the source→metadata→IL/PDB chain owned by #766). The catalog is
an inventory and does not claim deferred cases are proven.

Pinned behaviors, asserted per catalog case id and pipeline stage
(extraction, combining, reconciliation, traversal) by
`MessyWorkspaceRegressionTests`:

- Deep chain: at `--max-depth 12` path enumeration truncates with the
  `depth` reason while the terminal inventory stays complete with minimum
  terminal distance 14 and identical boundary identity sets at depths 12 and
  16 — no false absence from depth truncation. At depth 10 the distance-14
  terminal falls outside the depth-bounded retained closure; the observation
  scopes its completeness claim to the retained graph and invents nothing.
- Cycles: the three-node cycle and the self-recursive branch each get their
  own handler chain; both terminate, record `cycle` truncation honestly,
  inventory zero terminals, and surface an explicit
  `DownstreamWithoutSupportedTerminal` gap scoped to their own binding.
- Same-name members: ten container-distinct Tier1 identities, no semantic
  edge crossing engines, each boundary supporting exactly its own engine's
  terminal fact and class line range with distinct tables and query shape
  hashes; the handler inventories exactly ten distinct terminal witnesses
  with no cross-joined evidence.
- Merged roots: combine preserves the union of sources, facts, and symbols
  with per-source namespacing, no symbol deduplication, and no identity that
  blends namespaces; every terminal stays attributed to its own source label
  (11 alpha, 1 beta, 1 vb); the merged dependency report lists all three
  labeled sources. Call edges are compared to the original scans as exact
  (label, caller, callee) tuples, and terminals as exact (label, original fact
  id, source symbol, table name) tuples, including multiplicity. The Web Forms
  packet stays single-page-source by design
  (`WebFormsModernizationPrimarySourceAmbiguous` for multi-page-source
  combined indexes), so cross-source attribution is asserted over the merged
  index directly.
- Projectless VB: `Level3SyntaxAnalysis` with the fail-closed
  `NoVisualBasicProjectOrSolution` and per-file `SemanticAnalysisUnavailable`
  Tier4 gaps, a Tier3 `vb.syntax.database-operation.v1` terminal, and a
  handler chain that reaches its `sql-query` boundary. No blocker exists for
  projectless VB in ordinary CI.
- Determinism: repeat CLI scans of each root produce byte-identical
  `facts.ndjson`.

Each implemented case also checks the catalog's expected rules, tiers, and
positive gap expectations against its produced evidence. The merged-roots case
checks preserved Tier1 callgraph evidence; it does not claim to exercise the
cross-source symbol-reconciliation rule.

Run the focused lane with:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj   --no-restore --filter FullyQualifiedName~MessyWorkspaceRegressionTests
```

The slice adds no new derived machine-readable artifact, so generator and
bounded-input hash pinning stay with the existing manifest provenance; the
catalog's `artifactPinning` contract records this decision. This slice does
not complete Task 10: the remaining #766 ILAsm/rewritten-PDB matrix and
Task 11's private Windows/`dotnetperf` lane stay out of scope.

### IL rewrite PDB evidence (Task 10 fourth slice)

The bounded rewrite-PDB lane activates `dotnet.compiled.il-rewrite-pdb.v1`
and `dotnet.compiled.il-rewrite-pdb-gap.v1` behind the explicit
`--il-rewrite-pdb-evidence` flag, which requires `--il-rewrite-evidence` and
ordinal `--il-rewrite-pdb-before`/`--il-rewrite-pdb-after` declarations that
must align with the declared assembly pairs. The lane is inert without the
flag: no `ilRewritePdbProvenance` manifest section, no rewrite-PDB facts or
known gaps, and no receipt provenance. The CLI additionally rejects declared
PDB lists without the flag, so the inertness pin — an identical scan
identity, rewrite digest, and fact bytes between a disabled-lane scan and one
that carries the unflagged declarations — is asserted through the
`ScanOptions` API in the focused suite, not through a CLI invocation.

Each declared PDB side must bind its own paired assembly through the exact
portable content GUID/stamp against the re-read and re-hashed assembly's PE
CodeView entries; duplicate matching entries, cross-side matches, and changed
or unreadable matched assemblies fail closed. Both PDB sides must
independently satisfy the standalone PDB dual-reader contract
(System.Reflection.Metadata method/sequence-point observations cross-checked
against Mono.Cecil shape counts), every PDB method row must correspond to a
dual-reader-proven body on its own side, and every sequence-point IL offset
must fall inside that body's proven extent. Only fully proven pairs emit
per-method `ManagedIlRewritePdbObserved` relationships recording the original
and rewritten member identity, both body identities and digests, both PDB
method identities and content ids, per-side sequence-point digests, and an
exact offset classification: `sequence-point-offsets-unchanged` when the
ordered IL offset vectors are equal, `sequence-point-offsets-changed`
otherwise. The classification compares IL offset vectors only; lines,
columns, documents, and hidden flags are committed by per-side digests and
never imply preserved or correct debugging behavior, behavioral equivalence,
source ownership, or rewrite attribution. Methods whose debug information
exists on exactly one side emit a bounded
`IlRewritePdbMethodDebugInformationAbsent` gap with a retained identity
prefix and omitted-identity digest; methods with no debug information on
either side emit nothing, mirroring the bodyless-method structural
observation.

The public synthetic matrix lives in
`samples/compiled-dotnet-evidence/fixture-cases.json`
(`compiled-dotnet-fixture-cases.v6`, `ilRewritePdbCases`): the deterministic
compiler-produced `CompiledEvidence.CSharp` pair is the before side, and
Mono.Cecil 0.11.6 — reading and writing portable PDBs, never used as the
sole oracle — generates the after side inside the test suite. The matrix
proves operand-only rewrites with stable instruction offsets (every
relationship offsets-unchanged), IL insertions that shift later offsets
(exactly one offsets-changed plus unaffected methods unchanged),
byte-identical pairs, a missing after PDB, a content-identity mismatch
without cross-side re-binding, a stripped one-side debug-information delta,
truncated and native-Windows PDB sides, an unavailable parent rewrite pair,
budget exhaustion, and declaration validation. Windows-native PDBs remain
unsupported on every host
(`WindowsPdbRequiresWindows`/`WindowsPdbIndependentReaderUnavailable`).

Pinned local commands:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter FullyQualifiedName~IlRewritePdbEvidenceExtractorTests
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter "FullyQualifiedName~IlRewritePdbEvidenceExtractorTests|FullyQualifiedName~IlRewriteEvidenceExtractorTests|FullyQualifiedName~PortablePdbExtractorTests|FullyQualifiedName~IlBodyEvidenceExtractorTests|FullyQualifiedName~ManagedMetadataExtractorTests"
tracemap scan --repo samples/compiled-dotnet-evidence/csharp --out <out> \
  --il-rewrite-evidence --il-rewrite-before <before.dll> --il-rewrite-after <after.dll> \
  --il-rewrite-pdb-evidence --il-rewrite-pdb-before <before.pdb> --il-rewrite-pdb-after <after.pdb>
python3 scripts/validate-adapter-artifacts.py <out>
```

ILAsm/ILDAsm parity was assessed on 2026-09-22 and is deferred with exact
prerequisites recorded in the fixture catalog
(`ILRWPDB-ILASM-PARITY-012`): the tools were absent from PATH, the .NET SDK
10.0.201 installation, and the NuGet cache on the assessment host, Homebrew
bottles mono 6.14.1 but it was not installed, and ordinary CI provides no
pinned ILAsm toolchain. No ILAsm or ILDAsm parity claim is made by any test
in this slice, and embedded portable PDBs remain unclaimed
(`ILRWPDB-EMBEDDED-PORTABLE-013`). This slice does not complete Task 10: the
remaining #766 evaluation-stack-sensitive rewrites, netmodules, type
forwarding, duplicate assembly identities, insertion/removal relationship
edges, the extended ECMA-335 mutation matrix, and ILAsm parity stay open, and
Task 11's private Windows/`dotnetperf` lane remains separate.

### Control-flow and exception-handling rewrite suite (Task 10 fifth slice)

The public ECMA-335 control-flow/exception-handling rewrite matrix extends
the `dotnet.compiled.il-rewrite.v1` and `dotnet.compiled.il-rewrite-gap.v1`
coverage without changing either rule's join, admission, or classification
behavior. The before side of every pair is the deterministic
compiler-produced fixture assembly
`samples/compiled-dotnet-evidence/csharp/bin/Debug/net10.0/CompiledEvidence.CSharp.ControlFlow.dll`
(built from `IlRewriteControlFlowShapes.cs` with `Deterministic=true`); the
after sides are deterministic Mono.Cecil 0.11.6 mutations or bounded
single-byte patches produced inside the public test suite
(`IlRewriteControlFlowEvidenceExtractorTests`), never hand-written binaries.
Both sides still pass through the dual-reader IL body contract
(System.Reflection.Metadata first, Mono.Cecil second, disagreement fails
closed), so Cecil is never the sole oracle.

Covered shapes (`samples/compiled-dotnet-evidence/fixture-cases.json` schema
v7, `ilRewriteCases`):

- `CS-ILRW-CFLOW-010` branch retarget: a for-loop's forward branch operand
  changes to a different in-range instruction boundary; classified
  `operand-only-change` with the opcode stream preserved.
- `CS-ILRW-CFLOW-011` switch jump-table permutation: the five-target `switch`
  vector reorders; classified `operand-only-change` (the fixed-size table
  keeps every offset stable).
- `CS-ILRW-CFLOW-012` leave retarget: a try-region `leave.s` retargets to the
  post-region code; classified `operand-only-change`.
- `CS-ILRW-CFLOW-013` nested exception-region rebinding: the inner catch's
  try start rebinds to the outer finally's try start (regions stay properly
  nested); classified `body-structure-change` with identical instructions.
- `CS-ILRW-CFLOW-014` handler-kind change: the nested catch becomes a fault
  handler with the catch-type token removed; classified
  `body-structure-change`.
- `CS-ILRW-CFLOW-015` max-stack-only header change: a byte patch bumps the
  recorded max-stack in the fat method-body header; classified
  `body-structure-change` with every other component identical.
- `CS-ILRW-CFLOW-016`/`017` evaluation-stack-sensitive rewrites: an inserted
  `dup`/`pop` pair (transiently deeper, net stack-neutral) and an inserted
  constant/`add` sequence (depth-profile reshaping) both classify as
  `instruction-stream-change` with `opcodeSequencePreserved=false`, no
  per-instruction claim, and no runtime-equivalence or stack-neutrality
  conclusion.
- `ILRW-CFLOW-HOSTILE-018`/`019` bounded malformed operands: a patched short
  branch delta pushing the target past the body extent and a patched
  `switch` count overrunning the jump table each withhold the whole pair as
  an `IlRewriteMalformedInput` Tier4 gap.
- `ILRW-CFLOW-LIMIT-020` exception-region limit: an
  `IlBodyLimits(MaxExceptionRegionsPerBody: 1)` scan over the fixture fails
  closed per side with `IlRewriteExceptionRegionLimitExceeded`.

Every rewrite fact carries `ilRewriteGeneratorSha256` (SHA-256 of the exact
extractor assembly) and `ilRewriteBoundedInputSha256` (canonical digest over
the schema, policy, generator, extractor identities, effective limits,
declared pairs, and per-pair outcomes), pinned by test along with the rule
ID, tier, limitation text, and extractor version. Repeat scans of the same
declared pair are byte-identical in `facts.ndjson` and `report.md`, the
manifest differs only in `scannedAt`, and no artifact contains a local
absolute path. The plain `samples/modern-sample` source scan is unchanged:
`Level1SemanticAnalysis` with a null `ilRewriteProvenance` when the lane is
not declared.

Pinned local validation on 2026-09-22 (macOS): focused
`IlRewriteControlFlowEvidenceExtractorTests` 16/16; combined compiled-lane
filter (rewrite PDB, rewrite, PDB, IL body, managed metadata,
source/metadata reconciliation) 221/221; full `dotnet test
src/dotnet/TraceMap.sln` 2,190/2,190 with zero failed/skipped and zero build
warnings; two repeat CLI scans of a branch-retarget pair produced 434 facts
with 7 `dotnet.compiled.il-rewrite.v1` rows (one `operand-only-change`, six
`unchanged`), zero gap rows, and byte-identical artifacts;
`scripts/validate-adapter-artifacts.py`, `scripts/check-private-paths.sh`,
`node scripts/kiro-review.mjs --self-test`, and `git diff --check` all
passed. The suite runs in ordinary CI with no ILAsm/ILDAsm dependency: all
mutations are Cecil-based or single-byte patches that index into the PE
byte array directly, so they are host-endianness independent.

ILAsm/ILDAsm parity was re-assessed on 2026-09-22 and remains deferred with
the slice-4 prerequisites (`ILRWPDB-ILASM-PARITY-012`): neither tool is on
PATH, present in the .NET SDK 10.0.201 installation, or installed via
Homebrew (mono is not installed), and no pinned ILAsm toolchain exists in
ordinary CI. Ordinary CI must not depend on an unpinned ILAsm/ILDAsm
installation; a pinned Windows SDK/Visual Studio `ilasm.exe`+`ildasm.exe`
lane or a pinned mono/dotnet-runtime ILAsm build plus an independent
disassembly oracle is the separately documented prerequisite. This slice
does not complete Task 10: the remaining #766 scope (member/type token,
constant, string, signature, generic, custom-modifier, function-pointer/
`calli`, property/event-accessor shapes, netmodules, type forwarding,
duplicate assembly identities, insertion/removal edges, embedded portable
PDBs, the extended mutation matrix, and ILAsm parity) stays open, and Task
11's private Windows/`dotnetperf` lane remains separate.

### Metadata operands and member shapes (Task 10 sixth slice)

The public compiler fixture `CompiledEvidence.CSharp.MemberShapes` plus bounded
Mono.Cecil mutations exercises case IDs `CS-ILRW-MEMBER-TOKEN-021` through
`CS-ILRW-CALLI-VARARG-BOUNDARY-032` in `fixture-cases.json` schema v8. A separate
compiler variant `CompiledEvidence.CSharp.VarArgCall` pins
`CS-ILRW-VARARG-CALL-028`. No fixture loads or executes an after
assembly. The scanner reports static, exact assembly/module and full
method-signature relationships; it does not attribute the rewrite.

`InlineField`, `InlineTok`, and `InlineSig` require the independent
System.Reflection.Metadata reader to validate the referenced row kind and
decode its signature before the Cecil body is admitted. Field and general
token operands bind the full decoded type/member identity as well as the
module-local row number; a field signature change at the same row changes
the body digest. `calli` standalone
signatures preserve calling-convention number, `hasThis`, `explicitThis`,
required-parameter count, return type, and parameter types in the canonical body operand and call-site
retarget fact, cross-checked by both readers. The compiler fixture supplies MethodSpec, TypeSpec, generic
type/method, function pointer, property/event, accessor, and vararg
declaration shapes. Same-opcode field, InlineTok member, TypeSpec,
MethodSpec, and `calli` convention changes are paired with exact unchanged
method identities. A compiler-produced vararg MemberRef call with a
MethodDef parent retains its required-parameter boundary in its full static
signature after both readers agree. A vararg `calli` sentinel-only boundary
change keeps its token and parameter types but changes the body identity.
Corrupted field/signature token row IDs
withhold the entire pair as `IlRewriteMalformedInput`; a reserved `calli`
calling convention withholds the pair as a malformed signature gap.

Required and optional custom modifiers alter complete method signatures,
so the pair reports explicit before-only and after-only memberships. The
same relationship represents an inserted and removed MethodDef in one
pair. Property and event metadata accessor handles are independently checked
with System.Reflection.Metadata on both sides of getter and adder body
mutations. These are declaration and body observations, not event delivery
or runtime behavior claims.

The focused suite is
`dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj
--filter FullyQualifiedName~IlRewriteMemberShapeEvidenceExtractorTests`.
Ordinary `local-distribution-validation.yml` runs it on Windows, Ubuntu,
and macOS using the SDK compiler and Mono.Cecil only; ILAsm/ILDAsm remains
outside ordinary CI. Each emitted rewrite fact retains the exact generator
SHA-256 and privacy-projected bounded-input SHA-256, with rule ID, tier,
limitations, and a corresponding stable public fixture case ID.

### Public ECMA-335 rewrite integration matrix (Task 10, #766)

The ordinary CI subset runs `IlRewriteAssemblyTopologyTests`,
`IlRewriteEmbeddedPdbTests`, and `IlRewritePublicIntegrationTests` on Linux,
macOS, and Windows through `local-distribution-validation.yml`. The separate
PR and manual `compiled-dotnet-extended-validation.yml` lane runs those tests together with
all prior public rewrite, control-flow, member-shape, and rewrite-PDB suites.
Its Windows job discovers `ilasm.exe` and `ildasm.exe` by absolute path and
records file versions and help output. Discovery is evidence, not parity.
The extended lane uses only synthetic public fixtures and has no private
`dotnetperf`, Web Forms, or C++/CLI input.

Local commands for the bounded and extended public lanes are:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~IlRewriteAssemblyTopologyTests|FullyQualifiedName~IlRewriteEmbeddedPdbTests|FullyQualifiedName~IlRewritePublicIntegrationTests'
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore --filter 'FullyQualifiedName~IlRewriteEvidenceExtractorTests|FullyQualifiedName~IlRewriteControlFlowEvidenceExtractorTests|FullyQualifiedName~IlRewriteMemberShapeEvidenceExtractorTests|FullyQualifiedName~IlRewritePdbEvidenceExtractorTests|FullyQualifiedName~IlRewriteAssemblyTopologyTests|FullyQualifiedName~IlRewriteEmbeddedPdbTests|FullyQualifiedName~IlRewritePublicIntegrationTests'
tracemap scan --repo <public-fixture-repo> --out <out> --il-rewrite-evidence --il-rewrite-before <before.dll> --il-rewrite-after <after.dll> --il-rewrite-pdb-evidence --il-rewrite-pdb-before <before-embedded.dll> --il-rewrite-pdb-after <after-embedded.dll>
```

The embedded PDB declarations name assembly carriers, not extracted private
`.pdb` files. Each carrier must be byte-identical to its paired declared
assembly. On Windows, discover candidate tools and their exact file versions
before invoking them:

```powershell
Get-ChildItem "$env:WINDIR\Microsoft.NET\Framework64","$env:WINDIR\Microsoft.NET\Framework",'C:\Program Files\Microsoft SDKs','C:\Program Files (x86)\Microsoft SDKs','C:\Program Files (x86)\Windows Kits','C:\Program Files\Microsoft Visual Studio' -Recurse -File -Include ilasm.exe,ildasm.exe -ErrorAction SilentlyContinue | Select-Object FullName,@{N='Version';E={$_.VersionInfo.FileVersion}}
& '<discovered-absolute-ilasm.exe>' /?
& '<discovered-absolute-ildasm.exe>' /?
```

| #766 requirement | Public case or explicit gap | Admission limit |
| --- | --- | --- |
| Branch, `switch`, `leave`, nested exception/filter/finally/fault regions, locals, max stack, and stack-sensitive edits | `CS-ILRW-CFLOW-010`–`020` and existing IL body/rewrite suites | Static body relationship; no behavioral equivalence claim. |
| Member/type tokens, strings, constants, signatures, generics, custom modifiers, function pointers/`calli`, properties/events/accessors | `CS-ILRW-MEMBER-TOKEN-021`–`CS-ILRW-CALLI-VARARG-BOUNDARY-032` and earlier operand cases | Complete assembly/module/member signatures and operands require SRM/Cecil agreement. |
| Netmodules and metadata-bearing multi-module manifests | `ILRW-TOPO-001` and `002`: `IlRewriteUnsupportedShape` | Secondary modules are not loaded or inferred. |
| Type forwarding and exported-type work limits | `ILRW-TOPO-003`: `TypeForwardingManagedAssemblyUnsupported`; `006`: `IlRewriteTotalWorkLimitExceeded` | Forwarded targets are not resolved or joined; every exported-type row is charged before traversal. |
| Duplicate assembly/member identities | `ILRW-TOPO-004` ambiguity gap; `005` proves ordinal pair isolation | No first-candidate or cross-pair join. |
| Portable PDB document/method/sequence-point identity and rewritten offsets | Existing portable and rewrite-PDB suites plus `ILRWPDB-EMBEDDED-PORTABLE-013` | Offset classifications and hashes do not prove debug behavior. |
| Missing, mismatched, or over-limit debug evidence | `ILRWPDB-EMBEDDED-MISSING-014`, `MISMATCH-015`, and `TEXT-LIMIT-016`, plus existing PDB binding and reader-disagreement gaps | Withhold the disputed PDB relationship while retaining independent parent IL evidence; the rewrite-PDB locator limit applies independently. |
| Windows-native PDB | Existing `CS-ILRWPDB-WINDOWS-009` unsupported gap | Requires an independent Windows PDB reader before admission. |
| Same opcodes, different operands; token retargets and one-sided members | `CS-ILRW-OPERAND-001`, token/member cases, and SRM raw-IL/runtime integration case | Operand-insensitive hashes are non-unique heuristics; no identity edge from them. |
| Valid, invalid, and hostile bounded PE/metadata shapes | Existing malformed/limit cases and topology unsupported/ambiguous cases | Reader disagreement withholds the entire disputed relationship. |
| ILAsm/ILDAsm parity | `ILASM-PARITY-TOOLS-001`, `CFLOW-002`, `EH-003`, `MEMBER-004`, and `MUTATE-005` are proven in the extended Windows lane; `ILASM-PARITY-PDB-006` records the typed PDB-oracle gap | Parity is proven only for the public fixture matrix on the pinned .NET Framework 4.8 ILAsm + Windows SDK NETFX ILDAsm toolchain, with ILDAsm as the independent oracle; sequence-point parity stays unclaimed until a work-machine ILDAsm observes portable PDBs and the recorded receipt matches; no general equivalence claim. |

Each new positive assertion and gap uses the existing rule ID and evidence
tier, complete assembly/module/member signature where applicable, locations,
extractor version, documented limitation, exact extractor generator SHA-256,
and privacy-projected bounded-input SHA-256. The fixture catalog binds stable
case IDs to expected outcomes. SRM reads the admitted PE/PDB independently of
Mono.Cecil; a disagreement never votes in a relationship. The sole runtime
corroboration invokes a synthetic parameterless constant method in a
collectible load context after static admission; it proves only the observed
return values of that one fixture.

On the 2026-09-22 macOS arm64 host, .NET SDK `10.0.302` is installed, but
`ilasm`, `ildasm`, and `mono` are absent from PATH and the checked SDK,
Homebrew, and local .NET locations. `brew info mono` offered version `6.14.1`
but it was not installed. An available bottle does not pin an independent
disassembly oracle or prove parity. The PR #782 Windows 2025 VS2026 runner
discovery on 2026-09-22 found `ildasm.exe` 4.8.3928.0 in both x86 and x64
directories under `C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX
4.8 Tools` and `NETFX 4.8.1 Tools`; `ilasm.exe` was unavailable in the
searched SDK, Visual Studio, and PATH locations. The exact discovery command
is in `.github/workflows/compiled-dotnet-extended-validation.yml`; the runner
log is https://github.com/joefeser/tracemap/actions/runs/35782144244/job/106929998051.
The smallest remaining Windows action is to identify or install a pinned
`ilasm.exe` compatible with the discovered `ildasm.exe`, then execute
the same public before/after matrix through assembly and disassembly. Record
exact invocation commands, independently compare IL operands, offsets, and
PDB sequence points to SRM and TraceMap, and leave a typed gap for any
unavailable or disagreeing shape. Task 10 stays open until that evidence
exists; Task 11's private work-machine lane is separate.

### Public ILAsm/ILDAsm parity gate (Task 10, #766)

The missing prerequisite was the search scope, not the tool: `ILAsm.exe`
ships with the .NET Framework runtime itself under
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\` (and the x86 `Framework`
twin), which the PR #782 discovery never searched. The extended lane's
discovery step searches, in order: the .NET Framework `Framework64`/
`Framework` runtime directories, the Windows SDK `Microsoft SDKs` NETFX
4.8/4.8.1 Tools directories (x64 first), Windows Kits, Visual Studio, and
PATH; it then selects one ILAsm and one ILDAsm by that same order and hands
their absolute paths to the test process through
`TRACEMAP_PARITY_ILASM`/`TRACEMAP_PARITY_ILDASM`. The in-test discovery consumes
and re-validates that handoff first, then its own ordered candidates
(runtime directories, NETFX tools, PATH). Both tools are pinned by absolute
path with a recorded `4.8.`-prefixed file version AND a non-empty product
version, plus the runner image identity (`ImageOS`, `ImageVersion`,
architecture); a discovery hit without either version, or either pinned
tool failing its own `/?`, fails the case rather than passing silently.

The parity matrix runs only on the extended Windows lane
(`compiled-dotnet-extended-validation.yml`, `public-mutation-matrix
(windows-latest)`) through `IlAsmIldasmParityGateTests`; ordinary CI never
depends on ILAsm or ILDAsm. ILDAsm `/out=... /nobar` text and an ILAsm
`/dll /nologo /output=...` round trip are the independent oracles, parsed by
the test-local `IlDasmTextParser` (whose own tests run on every OS); Mono.Cecil
is never the parity oracle because it is one of TraceMap's two internal
readers.

The canonical comparison retains complete method declarations (including
calling conventions, generic constraints and custom modifiers), local
signatures and initialization, and wrapped instruction operands. Quoted
literal whitespace stays significant. Exception ends already observed at an
instruction boundary are never extended to the method end; sibling handlers
share their protected range, and lexical-scope braces do not close EH blocks.
Parser regressions cover offsets beyond `0xffff` and reject unsupported
offset-form EH clauses, malformed `.line` directives and incomplete switches.
This remains a parser for the bounded public fixtures, not a general ILAsm
grammar or an ECMA-335 verifier.

The six catalog cases live in `fixture-cases.json` schema v9
(`ilasmParityCases`):

- `ILASM-PARITY-TOOLS-001` — pinned discovery, versions, and invocability.
  Observed on the `win25-vs2026` runner image `20260907.229.1` (AMD64):
  `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\ilasm.exe` file version
  `4.8.9221.0` (built by `NET481REL1LAST_25H2`) and
  `C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1
  Tools\x64\ildasm.exe` file version `4.8.3928.0` (built by `NET48REL1`).
- `ILASM-PARITY-CFLOW-002` — the control-flow fixture round trips through
  ILDAsm → ILAsm → ILDAsm with identical canonical member bodies, and the
  bound before/after scan joins every method (branches, the dense switch
  table, leave targets, nested exception regions) as `unchanged` with
  `tokenRetargeted=false`, zero gaps, and instruction, local, max-stack,
  and call-offset counts equal to the ILDAsm observation on both sides.
- `ILASM-PARITY-EH-003` — nested try regions, catch/fault handler kinds and
  catch type identities, leave targets, and the dense switch keep identical
  canonical exception-clause structure on instruction boundaries, with
  ILDAsm clause counts equal to TraceMap exception-region counts on both
  sides. The parser also retains filter start offsets when present.
- `ILASM-PARITY-MEMBER-004` — the member-shape fixture's generic method
  specifications, `ldtoken` type tokens, `calli` standalone signatures,
  static field operands, explicit `modopt` parameters and `modreq` returns,
  accessors, and vararg
  declarations round trip with every symbolic operand preserved verbatim in
  the canonical ILDAsm body. ILAsm renumbers raw module-local reference rows
  on re-emission (observed for cross-assembly MemberRefs), and TraceMap's
  operand-aware digests commit raw tokens by documented contract, so those
  methods classify exactly `operand-only-change` with every call-retarget
  identity preserved; constructors are joined under their own
  `constructor:` identity kind.
  The modifier-bearing input is constructed before disassembly: C#'s
  non-virtual `in int` alone does not supply a custom-modifier signature.
  A platform-neutral test checks fixture construction; only the Windows
  ILDAsm observation establishes independent modifier preservation.
- `ILASM-PARITY-MUTATE-005` — the branch-retarget, handler-kind, and
  stack-neutral insertion mutations keep their exact TraceMap relationship
  classification when the mutated after side passes through the independent
  round trip first, with identical canonical method bodies, not whole-file
  normalized disassembly, for the raw and round-tripped after sides.
- `ILASM-PARITY-PDB-006` — **deferred (Tier4Unknown)**. The non-hidden sequence points of an embedded
  portable PDB fixture, observed independently via ILDAsm's documented
  `/linenum` switch with the extracted PDB adjacent to the carrier copy,
  must equal TraceMap's declared sequence-point tuples (offset, start/end
  line, start/end column). ILDAsm 4.8.3928.0 has no `/pdbpath` option, and
  on 2026-09-23 the hosted `win25-vs2026` ILDAsm accepted `/linenum`,
  disassembled the carrier, and emitted no `.line` directives for the
  adjacent extracted portable PDB: the typed oracle-availability gap is
  recorded as `IldasmPortablePdbLineOracleUnavailable` in the fixture catalog,
  with the precise work-machine command and expected receipt and
  no PDB parity is claimed. Hidden (`0xfeefee`) points are outside the
  claim either way. Closing this one prerequisite requires a Windows work
  machine whose ILDAsm symbol reader observes portable PDBs (for example a
  Visual Studio/SDK ILDAsm bound to a portable-PDB-capable diasymreader)
  and the same tuple comparison, or an equally independent documented
  oracle; Task 10's checkbox stays open on exactly that gap.

Every round-trip leg scans a bound compiled-input pair (binding receipt over
a temporary git fixture repository) with both `il-body` and `il-rewrite`
evidence, so the compared facts retain the exact generator SHA-256 and
privacy-projected bounded-input SHA-256, rule IDs, tiers, locations, and
limitations of the underlying rules. A repeat scan must be byte-identical.
The parity claim is bounded to these public fixtures, that pinned 4.8
toolchain, and those observations; it is not a general IL equivalence,
execution, or debug-behavior claim. The green exact-head run is
https://github.com/joefeser/tracemap/actions/runs/35802803285/job/106996742841
(Windows, all eight gate tests and seven parser tests passed; Ubuntu and
macOS lanes green; ordinary lanes including all three package-smoke jobs
green). Task 10's checkbox stays open on the single PDB-prerequisite gap
above.
