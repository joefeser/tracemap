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

Validation in progress: 75 focused package/CLI/scan cases pass, including 25 new
deps.json cases and a real bounded synthetic build during the scan. An initial --no-restore build on the fresh worktree found
no assets; locked restore succeeded. Initial focused tests exposed the leading
period in standard .NETCoreApp target names; corrected the target validator.

Next: full Debug suite, sample smoke, review and PR.
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
| Delivery | PR #848 targets dev; full Debug validation and ACK in progress |

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
