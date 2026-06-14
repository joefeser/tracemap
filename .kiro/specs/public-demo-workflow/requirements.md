# Public Demo Workflow Requirements

## Introduction

TraceMap has grown into a multi-language static evidence tool, but new users still need a simple way to see the whole story without private repositories, hand-built indexes, or a memorized command sequence.

This spec adds a one-command public demo workflow. The workflow should run from a clean checkout, use checked-in public fixtures by default, generate deterministic demo artifacts, and explain exactly which conclusions are evidence-backed and which sections are partial or unavailable.

This is a demo and validation layer. It must not add LLM calls, embeddings, vector databases, prompt-based classification, runtime traffic capture, package registry lookups, vulnerability scanning, service ownership inference, release approval, or private repo assumptions.

## Current State

- `tracemap scan` supports .NET, TypeScript, Python, and JVM adapters.
- `tracemap combine` can create combined indexes across scanned outputs.
- `tracemap report`, `paths`, `reverse`, `diff`, `impact`, `portfolio`, and release-review style reports exist or have related specs/implementations.
- Public samples already exist under `samples/` for .NET, TypeScript, Python, JVM, endpoint alignment, contract deltas, SQL shapes, and portfolio manifests.
- `scripts/smoke-combined-paths.sh` validates a narrower combined path workflow.
- `scripts/smoke-open-source-repos.sh` validates pinned public repositories separately.
- `docs/VALIDATION.md` documents validation commands, but there is no single public demo command that produces a coherent artifact bundle.

## Scope

Implement a public, deterministic demo workflow that:

- scans checked-in sample repositories,
- combines selected indexes,
- runs representative reports and queries,
- writes Markdown and JSON outputs under a generated or caller-provided output directory,
- verifies key artifacts and semantic assertions,
- produces a concise demo summary,
- documents expected outputs, caveats, and sample coverage.

The default workflow must not clone repositories, fetch external sample data, or call external analysis services. First-run dependency restore for local toolchains such as NuGet or npm may require network unless the user has already restored dependencies; docs must call this out as a prerequisite, not hide it as analysis behavior. Optional pinned open-source repository demos may be documented or implemented as an explicit opt-in mode, but they must not be required for the default demo.

## Requirements

### Requirement 1: One-Command Demo Entry Point

**User Story:** As a new user, I want one command that runs a meaningful public TraceMap demo so that I can understand the product without assembling many commands myself.

#### Acceptance Criteria

1. WHEN the user runs the demo command with no arguments THEN it SHALL create a temporary output directory using a non-predictable temp path.
2. WHEN the user passes an output directory THEN the workflow SHALL write generated artifacts under that directory without committing or modifying repository source files.
3. WHEN the command starts THEN it SHALL print a concise header naming the demo mode, output root, and the sample labels it will use.
4. WHEN the command finishes successfully THEN it SHALL print a concise console summary with output root, scanned source count, combined source count, endpoint finding count, path count, reverse result count, diff row count, impact item count, portfolio source count, report coverage, and gap count where those sections are available.
5. WHEN any required step fails THEN the workflow SHALL exit non-zero and print the failed step name plus sanitized diagnostic context.
6. WHEN a section is intentionally not run THEN the summary SHALL label it `not_requested`, `unavailable`, or `deferred`, not silently omit it.
7. WHEN the workflow uses generated paths in output messages THEN it MAY print the caller-provided or temp output root, but SHALL NOT include baked-in developer-local paths.
8. WHEN the workflow writes public-shareable `demo-summary.*` artifacts THEN those artifacts SHALL NOT include the absolute output root; they SHALL use an output-root label/hash plus relative artifact paths.
9. WHEN the caller provides an output directory inside the repository root THEN the workflow SHALL refuse it unless the path is already ignored by git, so generated SQLite, logs, and reports are not accidentally staged.

### Requirement 2: Toolchain Discovery And Failure Behavior

**User Story:** As a maintainer, I want the demo to fail clearly when required local tools are missing so that users can fix their environment without reading the script.

#### Acceptance Criteria

1. WHEN `dotnet` is missing THEN the workflow SHALL fail early with a clear message.
2. WHEN `node`, `npm`, or TypeScript build prerequisites are missing THEN the workflow SHALL fail early with a clear message.
3. WHEN `git` is missing THEN the workflow SHALL fail early with a clear message because repository-root and ignored-path checks require git.
4. WHEN Python sample scanning is requested or auto-enabled THEN the workflow SHALL create a fresh isolated temporary virtual environment under the output root and SHALL NOT install packages into the repository or any pre-existing system or user Python environment.
5. WHEN Java/JVM sample scanning is requested or auto-enabled THEN the workflow SHALL check Java 21 availability and include Homebrew discovery guidance on macOS, consistent with `AGENTS.md`.
6. WHEN optional tools are unavailable THEN the workflow SHALL either skip the optional section with an explicit gap/status or fail only if the caller requested that section as required.
7. WHEN a tool check fails THEN diagnostics SHALL avoid private paths, raw environment values, secrets, and source snippets.

### Requirement 3: Public Fixture Strategy

**User Story:** As an open-source maintainer, I want demo fixtures that are stable and safe so that the public demo does not depend on private code or upstream repository churn.

#### Acceptance Criteria

1. WHEN deterministic assertions are required THEN the default workflow SHALL use checked-in fixtures under `samples/`.
2. WHEN a fixture needs a contract delta or query selector THEN it SHALL use checked-in sample files with generic names and synthetic data. Dynamically generated inputs, such as demo-run portfolio manifests that point at generated indexes, are not fixtures and SHALL be produced under the output root during the demo.
3. WHEN fixture source code contains SQL, config, package, endpoint, or contract examples THEN values SHALL be synthetic and safe to publish.
4. WHEN optional public OSS repositories are used THEN their URLs and commit SHAs SHALL be pinned in documentation and the mode SHALL be opt-in.
5. WHEN optional OSS repos fail to clone, restore, or build THEN the workflow SHALL label that mode partial and SHALL NOT change the default demo result.
6. WHEN docs list tested repositories THEN they SHALL avoid developer-local absolute paths, private organization names, and unpinned branch names.
7. WHEN generated outputs include scan manifests or analyzer logs with temporary paths THEN those files SHALL remain generated artifacts only and SHALL NOT be committed.

### Requirement 4: Demo Coverage

**User Story:** As a prospective user, I want the demo to show the main TraceMap workflows without overclaiming what static analysis proves.

#### Acceptance Criteria

1. WHEN the demo scans sample repositories THEN it SHALL produce required scan outputs: `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`.
2. WHEN the demo combines indexes THEN it SHALL use deterministic labels and produce a combined SQLite index.
3. WHEN the demo runs dependency reporting THEN it SHALL produce Markdown and JSON dependency reports.
4. WHEN endpoint samples are present THEN the demo SHALL run combined reporting as the default endpoint assertion target and assert at least one rule-backed endpoint finding; the separate `tracemap endpoints` command is an optional follow-up per the design.
5. WHEN path samples are present THEN the demo SHALL run `paths` and assert at least one rule-backed path or a rule-backed gap explaining why no path exists.
6. WHEN reverse query samples are present THEN the demo SHALL run `reverse` for at least one endpoint, table, package, or surface selector and assert rule-backed output or rule-backed gaps.
7. WHEN before/after samples are absent THEN the demo SHALL mark `diff` and `impact` as `deferred` with a documented reason; WHEN a concrete before/after fixture pair is added THEN the demo SHALL run `diff` and `impact` on compatible combined snapshots.
8. WHEN portfolio inputs are available THEN the demo SHALL run `portfolio` and assert source coverage, dependency surfaces, and gaps are deterministic.
9. WHEN release-review-compatible before/after inputs and contract deltas are absent THEN the demo SHALL mark release review as `deferred` with a documented reason; WHEN those inputs exist THEN the demo SHOULD run release review over generated artifacts and label unavailable sections honestly.
10. WHEN a workflow cannot run because the current samples do not support it THEN the demo SHALL emit a documented `unavailable` or `deferred` section rather than fake evidence.

### Requirement 5: Semantic Assertions

**User Story:** As a reviewer, I want the demo to prove the generated artifacts contain meaningful evidence, not just that files were written.

#### Acceptance Criteria

1. WHEN scan outputs are generated THEN assertions SHALL verify non-empty `facts.ndjson` for samples expected to produce facts.
2. WHEN JSON reports are generated THEN assertions SHALL verify required top-level schema fields, deterministic arrays, rule IDs, evidence tiers, source labels, commit SHAs, and coverage statuses.
3. WHEN endpoint findings are asserted THEN the assertion SHALL check normalized method/path evidence and supporting fact IDs where available.
4. WHEN paths or reverse rows are asserted THEN each accepted row SHALL include rule IDs, evidence tiers, source labels, supporting fact IDs or edge IDs where available, and a classification that is not stronger than the underlying evidence.
5. WHEN diff or impact rows are asserted THEN the assertion SHALL distinguish added/removed/changed evidence from analysis gaps and reduced coverage; WHEN diff or impact is deferred THEN the assertion SHALL verify the deferred status and explanation metadata.
6. WHEN portfolio rows are asserted THEN the assertion SHALL verify source identity, commit SHA, coverage, and safe dependency surface metadata.
7. WHEN no evidence is found for an expected selector THEN the workflow SHALL fail unless the report contains a rule-backed gap that the spec explicitly allows.
8. WHEN generated JSON is produced twice from the same inputs THEN the workflow SHOULD compare byte-stable outputs for the sections expected to be deterministic.
9. WHEN commit SHA fields are asserted for checked-in samples THEN assertions SHALL verify presence and SHA-like shape, not a pinned literal value, because samples are scanned from the current TraceMap checkout.

### Requirement 6: Privacy And Redaction

**User Story:** As a project owner, I want public demo artifacts to be safe to share so that the workflow can run in open-source PRs.

#### Acceptance Criteria

1. WHEN the demo finishes THEN `./scripts/check-private-paths.sh` SHALL pass for committed repository files.
2. WHEN generated public-shareable Markdown, JSON, or summaries are inspected THEN they SHALL NOT contain raw SQL text, source snippets, connection strings, config values, raw URLs with secrets, tokens, private repo names, or developer-local absolute paths.
3. WHEN generated local-only scan artifacts such as `scan-manifest.json`, `facts.ndjson`, and `logs/analyzer.log` contain temporary build or output paths THEN they SHALL remain under the generated output root, SHALL NOT be committed, and SHALL NOT be treated as public-shareable artifacts.
4. WHEN a generated public report needs to refer to a path THEN it SHALL use safe relative paths, labels, or hashes according to existing TraceMap helpers.
5. WHEN a generated public report needs to refer to SQL or config values THEN it SHALL use safe shape, key, or hash metadata, not raw values.
6. WHEN diagnostics include command output THEN they SHALL redact or avoid unsafe raw values.
7. WHEN new ignore rules are required THEN they SHALL be generic output patterns, not machine-specific paths.
8. WHEN the workflow writes temporary files THEN it SHALL keep them under the chosen output root or system temp directory.
9. WHEN public-shareable generated artifacts are created THEN the demo SHALL run a sentinel scan over those generated public reports and summaries; `check-private-paths.sh` alone is not sufficient because it only checks tracked files.
10. WHEN sentinel scanning public artifacts THEN `demo-summary.md`, `demo-summary.json`, report Markdown, and report JSON SHALL be included; local-only scan artifacts SHALL be excluded.

### Requirement 7: Documentation

**User Story:** As a new user, I want docs that explain what the public demo proves and what it does not prove.

#### Acceptance Criteria

1. WHEN the README introduces TraceMap THEN it SHALL include the public demo command and a short explanation of the output bundle.
2. WHEN `docs/VALIDATION.md` is updated THEN it SHALL describe demo prerequisites, commands, expected outputs, semantic assertions, and cleanup behavior.
3. WHEN demo limitations are documented THEN they SHALL state that static paths are evidence trails, not runtime execution traces.
4. WHEN demo sections are unavailable or deferred THEN docs SHALL explain why and how to run deeper workflows manually.
5. WHEN optional OSS mode is documented THEN it SHALL include pinned commit SHAs and reduced-coverage caveats.
6. WHEN troubleshooting is documented THEN it SHALL include missing toolchains, reduced coverage, missing endpoint facts, unlinked surfaces, path selector misses, and private-path guard failures.
7. WHEN docs mention sample outputs THEN they SHALL use sanitized labels and relative paths only.

### Requirement 8: Tests And Validation

**User Story:** As a maintainer, I want the demo workflow to be testable without making normal PRs too slow.

#### Acceptance Criteria

1. WHEN script helper logic exists THEN it SHOULD have lightweight unit tests or deterministic shell assertions where practical.
2. WHEN the demo script is added THEN it SHALL be runnable locally from a clean checkout with required toolchains installed.
3. WHEN implementation finishes THEN `dotnet build src/dotnet/TraceMap.sln` SHALL pass.
4. WHEN implementation finishes THEN `dotnet test src/dotnet/TraceMap.sln` SHALL pass unless the implementation is docs/script-only and the reason is documented.
5. WHEN implementation finishes THEN the new public demo command SHALL pass.
6. WHEN implementation finishes THEN relevant existing smoke scripts, especially `scripts/smoke-combined-paths.sh`, SHALL pass or be explicitly deferred with a reason.
7. WHEN implementation finishes THEN `./scripts/check-private-paths.sh` and `git diff --check` SHALL pass.
8. WHEN the workflow is too slow for default CI THEN docs SHALL label it as a local or optional CI smoke, not a required fast unit test.
9. WHEN a required artifact is removed or a public-report sentinel leak is planted in a test fixture THEN the demo assertion helper SHALL fail non-zero with a sanitized step name.

## Deferred Follow-Ups

- CI workflow wiring for the public demo.
- Hosted demo artifacts or screenshots.
- Static HTML evidence explorer integration.
- Optional pinned OSS demo mode with cache reuse.
- Optional private-enterprise validation guide outside the open-source repository.
- Performance budgets across large real repositories.
- Package vulnerability/license/compatibility overlays.
- Runtime telemetry import as externally labeled evidence.
