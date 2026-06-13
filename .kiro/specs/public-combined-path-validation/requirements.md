# Public Combined Path Validation Requirements

## Introduction

TraceMap now has enough cross-index functionality to tell a compelling story: scan multiple codebases, combine their indexes, align endpoint evidence, and trace static dependency paths to SQL, config, package, and outbound HTTP surfaces.

The next step is not another analyzer. It is a public, repeatable validation and demo layer that proves the workflow with safe fixtures and pinned open-source repositories. The goal is to make `combine -> report -> paths` easy for maintainers, reviewers, and potential users to run without relying on private repositories or developer-local paths.

This validation layer remains deterministic. It must not use LLM calls, embeddings, vector databases, runtime traffic capture, private source paths, or non-pinned repository state.

## Current State

- `tracemap scan` exists for .NET, TypeScript, JVM, and Python adapters.
- `tracemap combine` creates combined SQLite indexes from multiple scan outputs.
- `tracemap report` summarizes endpoint alignment, dependency surfaces, coverage, and known gaps.
- `tracemap paths` traces bounded static evidence paths through a combined index.
- `docs/VALIDATION.md` documents language-adapter validation and a minimal combine/report/paths smoke using pre-existing local scan outputs.
- `scripts/smoke-open-source-repos.sh` clones and scans pinned public repositories.
- `scripts/smoke-typescript-endpoints.sh` proves endpoint alignment on checked-in samples.
- The current endpoint server sample is route-only. It must be extended with a small public controller-to-repository-to-SQL path before SQL path assertions can pass.

What is missing is a first-class, public smoke/demo workflow that:

- builds the relevant adapters,
- scans a client and server fixture,
- combines their indexes,
- runs `report` and `paths`,
- checks meaningful path evidence,
- avoids private names and local absolute paths,
- documents exactly what was tested.

## Scope Decisions

- Add a public validation/demo workflow for combined dependency paths.
- Use checked-in samples for deterministic endpoint-to-surface assertions.
- Keep pinned OSS scans in the existing OSS smoke workflow. They are broader scanner confidence, not the default path assertion source.
- Do not commit generated SQLite databases or reports.
- Do not hardcode private repository names, private paths, personal usernames, or machine-specific paths.
- Defer optional external client/server validation. Reserve generic environment variable names for a follow-up, but do not implement that mode in this slice.
- Keep this as a validation/docs/script slice. Do not change path-search semantics unless the smoke exposes a bug.

## Requirements

### Requirement 1: Public Combined Paths Smoke Script

**User Story:** As a maintainer, I want one script that proves the combined dependency path workflow so that I can validate PRs without remembering every command.

#### Acceptance Criteria

1. WHEN the user runs the new smoke script with no arguments THEN it SHALL create a temporary output directory or use a caller-provided output directory.
2. WHEN the script runs THEN it SHALL build the required local CLIs before scanning.
3. WHEN the script runs THEN it SHALL scan a TypeScript client fixture and a .NET server fixture that are safe to publish.
4. WHEN scans complete THEN the script SHALL assert that required scan artifacts exist: `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
5. WHEN indexes exist THEN the script SHALL run `tracemap combine` with deterministic labels.
6. WHEN the combined index exists THEN the script SHALL run `tracemap report` and `tracemap paths`.
7. WHEN reports are generated THEN the script SHALL assert that Markdown and JSON outputs exist and THEN it SHALL run the semantic JSON assertions from Requirement 2.
8. WHEN path output is generated THEN the script SHALL assert at least one endpoint-to-`sql-query` path exists for the checked-in fixture with non-empty nodes, edges, rule IDs, evidence tiers, and source labels.
9. WHEN the script finishes THEN it SHALL print a concise summary of output location, source labels, endpoint classifications, path count, gap count, and report coverage.
10. WHEN any assertion fails THEN the script SHALL exit non-zero.
11. WHEN required toolchains are missing THEN the script SHALL fail early with a clear message naming the missing command, including `dotnet`, `npm`, `node`, and the TypeScript build prerequisites.
12. WHEN the script uses its default output root THEN it SHALL use `mktemp -d` only and SHALL NOT create predictable directories under the repository working tree.

### Requirement 2: Meaningful Path Assertions

**User Story:** As a reviewer, I want the smoke to prove the value of `paths`, not merely that files were written.

#### Acceptance Criteria

1. WHEN the fixture contains a matched client call and server route THEN the smoke SHALL assert a `MatchedEndpoint` finding appears in the dependency report.
2. WHEN the fixture contains a server call chain to SQL evidence THEN the smoke SHALL assert a path reaches `sql-query`.
3. WHEN the fixture contains config or package evidence attached to reachable symbols THEN the smoke SHOULD assert a path can reach `package-config`; this assertion is deferred until the public fixture proves such a reachable surface.
4. WHEN a path crosses from client to server THEN JSON assertions SHALL prove at least one edge connects nodes with different source labels.
5. WHEN JSON output is inspected THEN the smoke SHALL verify path records include non-empty nodes, edges, rule IDs, evidence tiers, and source labels.
6. WHEN gaps are present THEN the smoke SHALL verify gap records include rule IDs and evidence tiers.
7. WHEN raw SQL, raw config values, raw snippets, or local absolute paths would be unsafe THEN the smoke SHALL assert sentinel strings from the fixture are not rendered in Markdown.
8. WHEN no path is found for an expected fixture route THEN the smoke SHALL fail and print enough sanitized report context to debug the missing evidence.
9. WHEN the dependency report is inspected THEN the smoke SHALL assert exactly two combined sources with labels `sample-client` and `sample-server`.
10. WHEN endpoint alignment is inspected THEN the smoke SHALL assert the same normalized path key appears in client and server endpoint evidence for the matched route.
11. WHEN path classifications are inspected THEN at least one returned path SHALL be `StrongStaticPath`, `ProbableStaticPath`, or `NeedsReviewPath`; a report made only of `UnknownAnalysisGap` conclusions SHALL NOT satisfy the smoke.
12. WHEN path output is generated twice for the same combined index THEN the smoke SHALL compare deterministic JSON after stripping no fields; the path report SHOULD contain no volatile timestamp fields.
13. WHEN a deliberately bogus endpoint selector is queried THEN the smoke SHALL assert the command succeeds with a valid report containing zero paths and a `SelectorNoMatch`, `NoPathFound`, or `UnknownAnalysisGap` gap rather than silently treating it as a passing path assertion.

### Requirement 3: Public Fixture and OSS Strategy

**User Story:** As a project owner, I want public validation assets that demonstrate the feature without leaking private app details.

#### Acceptance Criteria

1. WHEN deterministic assertions are required THEN they SHALL use checked-in samples under `samples/`.
2. WHEN checked-in samples lack reachable SQL evidence THEN the implementation SHALL extend those samples with small, realistic code that emits the missing evidence before adding a mandatory SQL path assertion.
3. WHEN public OSS repos are used THEN their URLs and commit SHAs SHALL be pinned in `docs/VALIDATION.md`.
4. WHEN OSS repos are scanned THEN reduced coverage SHALL be acceptable only if the manifest and reports clearly label the gaps.
5. WHEN OSS repos are used for smoke confidence THEN they SHALL remain in `scripts/smoke-open-source-repos.sh` or another explicit OSS smoke and SHALL NOT be part of the default combined path smoke.
6. WHEN optional external repos are discussed THEN docs SHALL say external validation is deferred and SHALL reserve generic names such as `TRACEMAP_EXTERNAL_CLIENT_REPO`, `TRACEMAP_EXTERNAL_SERVER_REPO`, and `TRACEMAP_EXTERNAL_SERVER_PROJECT`.
7. WHEN future external repo paths are printed in diagnostics THEN the implementation SHALL use basenames, labels, or hashes only, never full absolute paths.
8. WHEN documentation lists tested repos THEN it SHALL avoid developer-local absolute paths and private organization names.
9. WHEN fixture SQL/config sentinel values are added THEN they SHALL be synthetic and generic, not modeled on a private schema or secret.

### Requirement 4: Documentation and Demo Readability

**User Story:** As a new user, I want docs that show the end-to-end dependency path workflow with concrete commands and expected outputs.

#### Acceptance Criteria

1. WHEN the README describes TraceMap capabilities THEN it SHALL include a short public `combine -> report -> paths` workflow.
2. WHEN `docs/VALIDATION.md` describes combined path validation THEN it SHALL include the new script, expected assertions, output files, and how to interpret reduced coverage.
3. WHEN docs mention public OSS repos THEN they SHALL include commit SHAs and expected coverage labels.
4. WHEN docs mention optional external validation THEN they SHALL state it is deferred, use generic environment variable names only, and SHALL NOT mention private repo paths.
5. WHEN examples show report snippets THEN they SHALL use sanitized source labels, relative paths, and non-secret hashes.
6. WHEN limitations are described THEN docs SHALL state that paths are static evidence trails, not runtime execution traces.
7. WHEN troubleshooting guidance is provided THEN it SHALL list common causes: missing endpoint facts, reduced coverage, unlinked surfaces, route normalization mismatch, and missing call edges.

### Requirement 5: Privacy and Repository Hygiene

**User Story:** As a maintainer, I want validation additions to be safe for open source so that PRs cannot reintroduce private details.

#### Acceptance Criteria

1. WHEN files are added or edited THEN `./scripts/check-private-paths.sh` SHALL pass.
2. WHEN generated outputs are produced THEN they SHALL be written under caller-provided or temporary directories and SHALL NOT be committed.
3. WHEN `.gitignore` needs updates for smoke outputs THEN those updates SHALL use generic patterns; however, the default implementation SHOULD use `mktemp -d` and need no `.gitignore` update.
4. WHEN scripts print paths THEN they MAY print caller-provided output roots, but SHALL NOT include baked-in developer-local paths.
5. WHEN script defaults are provided THEN they SHALL be relative to the repo root or use `mktemp`.
6. WHEN validation fails THEN diagnostic output SHALL avoid raw source snippets, raw SQL text, connection strings, and config values.
7. WHEN caller-provided output roots are printed THEN the script SHALL print them only because the caller explicitly provided them; baked-in developer-local paths remain forbidden.

### Requirement 6: Tests and CI Fit

**User Story:** As a maintainer, I want the validation workflow to be useful locally and not overly expensive for normal PRs.

#### Acceptance Criteria

1. WHEN unit tests are needed for script helper behavior THEN they SHOULD be lightweight and deterministic.
2. WHEN the smoke script is added THEN it SHALL be runnable locally on a clean checkout after required toolchains are installed.
3. WHEN the script needs network access for OSS repos THEN that portion SHALL be outside this script in the existing OSS smoke workflow.
4. WHEN CI adoption is considered THEN the default smoke SHOULD be short enough to run as an optional PR check later.
5. WHEN language adapters change THEN `docs/VALIDATION.md` SHALL make clear whether the combined path smoke should be rerun.
6. WHEN implementation finishes THEN the following checks SHALL pass or be explicitly documented as deferred:
   - `dotnet build src/dotnet/TraceMap.sln`
   - `dotnet test src/dotnet/TraceMap.sln`
   - TypeScript build/check commands required by the smoke
   - the new combined path smoke script
   - `./scripts/check-private-paths.sh`
   - `git diff --check`

## Deferred Follow-Ups

- CI workflow wiring for the public combined path smoke.
- HTML or graph visualization demos.
- Hosted demo artifacts.
- Snapshot comparison across two commits.
- Persisted path query fixtures with golden JSON.
- Public real full-stack OSS pair discovery beyond checked-in samples.
- Optional private-enterprise validation guide outside the open-source repo.
