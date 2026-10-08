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
