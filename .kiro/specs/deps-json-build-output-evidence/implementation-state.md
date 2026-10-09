# Implementation state

Branch: `codex/deps-json-evidence`, from merged `origin/dev` at `357332ec`.
Scope: read existing bin/**/*.deps.json only when --index-deps-json is enabled.
No restore/build is triggered by this option. #847 remains independent and open.

The request's proposed JSON paths do not match the SDK format. Resolved versions
are the suffix of exact target/library keys; dependencies are edges within each
target library. A unique zero-incoming project root plus complete exact-version
edges establishes emitted-graph direct/transitive relations. No project filename
or display-name heuristic establishes identity. Ambiguous/missing graph context
preserves package/version rows with unknown relation and an explicit gap.

This is emitted dependency evidence, not full restore closure or proof of direct
PackageReference declarations. Runtime manifests can omit build/compile-only
packages. Scan commit identifies the observation context; buildCommitSha and
freshness remain unknown. The manifest's sha512 is never a registry artifact digest.

Validation: 79 focused package/CLI/scan cases pass, including 29 new
deps.json cases and a real bounded synthetic build during the scan. An initial --no-restore build on the fresh worktree found
no assets; locked restore succeeded. Initial focused tests exposed the leading
period in standard .NETCoreApp target names; corrected the target validator.

Next: settle the consolidated review batch and deliver validated follow-up fixes.
Private estate scans and downstream upgrade-authority behavior are not acceptance
claims of this public-fixture slice.

## Acceptance map

| Requirement | Evidence / remaining validation |
| --- | --- |
| Default-off CLI option, existing files only | CLI off/on regression; no build/restore call added by option |
| Discover bin at arbitrary depth, separate from snapshot | Multi-configuration fixture; source snapshot unchanged after observed deps change and real mid-scan synthetic build |
| Correct SDK format and project exclusion | Exact target/library keys; synthetic direct/transitive graph; actual SDK-generated CLI manifest smoke emitted 31 transitive package rows with no deps gaps |
| Package fact shape with build-output provenance | Rule/tier/extractor, package/version/target/relation, exact input/generator hashes, JSON pointer, unknown build commit/freshness |
| No overstated closure or source directness | Graph ambiguity/missing-edge unknowns; emitted-target scope documented; unsupported metadata yields explicit gaps |
| Unique file/target/package rows | Multi-target/BOM positive; duplicate property, case collision and multiple-version negative cases |
| Bounded and malformed cases | File, byte, total-byte, discovery and library limits; malformed/truncated/unsafe/unsupported/missing/symlink cases |
| Delivery | PR #848 targets dev; full Debug validation passed; ACK review blocked as recorded below |

Public CLI smoke and artifact validator pass. Actual SDK manifest smoke uses the
public TraceMap CLI build output, not a customer artifact. No language adapter
behavior changed; pinned external OSS adapter runs are deferred. Private estate
acceptance and downstream planner mapping remain out of scope.

The initial full Debug run reported an existing MW-DETERMINISM root-beta repeat
scan mismatch. The run finished with 3,456 passed, one failed and one Windows-only skip in
19m03s. The failed test then passed in isolation on the same binaries in 11s.
Root cause is not established; the failed run is not recorded as a pass.
New integration tests now join the existing Git metadata sensitive nonparallel
collection. Self-review fixes allow an existing build writer handle and reject trailing-newline
identities. All 79 focused tests pass, including 29 deps.json cases. The next full
Debug run validates the isolated integration tests and these fixes.

The second full Debug run passed: 3,461 passed, zero failed, one Windows-only
skip, total 3,462, in 21m41s. Command:
`dotnet test src/dotnet/TraceMap.sln --no-restore -warnaserror`.
This run tested implementation commit `6bca5b8a`. A subsequent narrow scan-ID
change binds both generator and bounded-input hashes when opt-in is enabled;
the 79-case focused suite passed again after that change (18s), using:
`dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-restore -warnaserror --filter 'FullyQualifiedName~DepsJsonEvidenceTests|FullyQualifiedName~PackageDecisionLockfileEvidenceTests|FullyQualifiedName~ScanEngineTests|FullyQualifiedName~CliTests'`.

Owner clarified that Qodo is retired and Baz replaces it. Lane version 0.3.0
explicitly disables Qodo and admits exact-head Baz evidence through ACK's
trustedHostedReviewerQuorum capability, preserving required Codex batching and
two bounded Baz fix cycles. No Baz/Qodo request was issued.

Review blocker on published head `0da74d7df7bd451c1d8b4bfd49a72ae5d474903c`:
ACK 0.5.5 (stable, ab39833, fresh distribution) sees Baz finding threads but does
not admit Baz's empty-body COMMENTED review. `gh pr view --json reviews` omits
its URL; GitHub REST returns review 5463905446 with the exact published commit
and URL. ACK's Baz interaction classifier requires that URL to corroborate root
finding threads; its supplemental history reader currently hydrates Qodo only.
The live gate therefore holds the consolidated findings pending Baz, despite a
completed exact-head review being visible upstream. No review-finding repair or
resolution is claimed while ACK withholds patch authority. A separate bounded
ACK reader repair needs owner direction; no trust check is weakened here.

After diagnosing the missing-URL reader defect, the original read-only wait was
interrupted and a bounded 60-second confirmation run returned
`human_decision_required / REQUIRED_REVIEW_QUORUM_NOT_MET`,
`workerMayStop=true`, eight held findings, no failed/pending checks, and CLEAN
merge state on published head `0da74d7d`. This is not merge readiness. The
follow-up commits remain local to avoid invalidating the pending review batch.

## Owner-authorized consolidated review repair

Owner explicitly requested fixes and fresh Codex review on both #847 and #848,
with Baz re-reviewing pushes automatically. This authorizes repair despite the
known ACK 0.5.5 Baz URL reader blockage; it does not authorize merge or bypass
final review. All eight raw findings were inspected on published head 0da74d7d.

Repairs: align include/project scope, ignore noncandidate file links, enforce
absolute caps and protected output roots, preserve the old positional ScanOptions
constructor via init-only opt-in properties, and report dependency gaps accurately.
The two overlapping freshness findings share one invariant: observations of an
unknown build cannot become current-source declaration evidence. The requested
PackageReferenced contract stays intact; correlation/impact consumers exclude
these observations with explicit gaps. Public regression cases cover each family.

Validation command includes DepsJsonEvidenceTests, PackageDecisionLockfileEvidenceTests,
PackageUpgradeImpactTests, CliTests, and ScanEngineTests with --no-restore -warnaserror.
An initial 99-test pass also emitted xUnit2031; the assertion was corrected before
rerunning. Final validation and review results follow in the PR readback.

Final repair validation: 99 focused tests pass, zero warnings/errors (15s).
Public deps.json CLI smoke passes with four facts. Private-path and diff guards pass.
Full final-head validation is delegated to the existing hosted .NET workflow;
the prior local full result remains 3,461 passed / one Windows-only skip.

## Second returned review batch

Receipt authorization fingerprints now bind the opt-in flag and effective limits.
Blank include entries are normalized consistently. Existing include-over-project
precedence is documented and regression-tested, not reversed in response to a
conflicting reviewer suggestion. Freshness gaps are emitted once per selected
source even with no decision pairings. The Qodo-era runbook and positive lane
regression were updated to Codex/Baz and verified ACK 0.5.5 capability usage.

Validation: 120 focused tests passed with warnings as errors, including receipt
fingerprints and no-pairing freshness coverage; three lane tests passed. Two new
test setups initially lacked receipt binding / used an inadmissible empty decision
file; corrected to bind identity and use an unmatched valid decision selector.
Public CLI smoke and artifact validation pass. Final-head full tests remain hosted.

The final review found a framing collision shared by the Codex and Baz findings:
additional authorized input text could mimic the appended deps marker. Receipt
scope now hashes a structured envelope containing the prior-scope SHA-256,
explicit opt-in boolean and effective limits. Adversarial marker/list/newline
regressions prove the build-output authorization cannot be forged this way.
This intentionally changes receipt scope fingerprints without changing schema.

Framing regression validation: 121 focused Debug cases passed with warnings as
errors (16s). The two current-head review comments describe the same collision
and are addressed by the single structured-envelope repair.

## Combined-source warning provenance repair

ACK admitted Codex finding discussion_r4226666963 on 33b38dbd. Coverage warnings
now retain the exact emitting source through gap construction, including source
label, scan ID and commit. This also fixes the same misattribution for existing
per-source coverage warnings; correlation already retains source identity.
The regression matrix covers only the later source emitting build evidence,
both sources emitting it, source filtering and bounded gap output.
Validation: dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj
--no-restore -warnaserror --filter
'FullyQualifiedName~PackageUpgradeImpactTests|FullyQualifiedName~PackageDecisionCorrelationTests|FullyQualifiedName~DepsJsonEvidenceTests|FullyQualifiedName~ScanExecutionReceiptTests'
passed 73 tests (7s). Final-head hosted checks and ACK freshness remain required.

## 2026-10-09 current-head repair

Branch: codex/deps-json-evidence. Owner renewed repair of both PRs after hosted
results settled. On b78582be all hosted build, adapter and Windows checks passed;
Baz returned receipt compatibility and per-gap provenance findings.
Disabled deps indexing now preserves the legacy v1 authorization fingerprint;
enabled indexing retains collision-resistant structured framing. A legacy scope
vector and existing injection/limit tests pin both contracts. Package impact
freshness gaps serialize sorted, deduplicated original supporting fact IDs,
scoped by their existing source label, scan ID and commit. Additive init-only
properties preserve positional construction. No source impact claims added.

Validation: dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj
--no-restore -warnaserror --filter
'FullyQualifiedName~PackageUpgradeImpactTests|FullyQualifiedName~ScanExecutionReceiptTests|FullyQualifiedName~DepsJsonEvidenceTests|FullyQualifiedName~PackageDecisionCorrelationTests'
passed 74 tests (5s). The JSON regression verifies serialized supporting IDs,
multiple contributing facts and deterministic order. Hosted final-head validation
and exact-head review remain required; no merge authorization.
