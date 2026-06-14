# Public Demo Workflow Design

## Overview

Add a public demo workflow that turns a clean checkout into a deterministic artifact bundle showing the major TraceMap workflows.

The intended default command is:

```bash
./scripts/demo-public.sh
```

Optional output root:

```bash
./scripts/demo-public.sh /tmp/tracemap-public-demo
```

The default demo should use checked-in fixtures only and should not clone repositories or fetch external sample data. First-run dependency restore for local toolchains may require network unless the user has already restored NuGet/npm packages; docs must call that out clearly. The demo should produce a generated directory similar to:

```text
<out>/
  scans/
    dotnet-modern/
    dotnet-endpoint-server/
    typescript-modern/
    typescript-endpoint-client/
    python-fastapi/          # optional, available only when Python setup succeeds
  combined/
    endpoint-stack.sqlite
    mixed-stack.sqlite
  reports/
    dependency/
    endpoints/
    paths/
    reverse/
    diff/
    impact/
    portfolio/
    release-review/
  demo-summary.json
  demo-summary.md
```

Exact folder names can change during implementation, but they must be deterministic, safe, and documented.

## Goals

- Give new users one command to see what TraceMap does.
- Give maintainers one local smoke for public, non-private workflows.
- Reuse existing CLI commands and reports rather than adding a new analysis engine.
- Make partial and unavailable sections visible.
- Keep all generated output out of git.
- Keep the open-source repository free of private paths and private repo names.

## Non-Goals

- No new scanner semantics just to make the demo prettier.
- No runtime topology, traffic, deployment, auth, ownership, vulnerability, license, compatibility, or release-approval claims.
- No LLM calls, embeddings, vector databases, prompt classification, or generated executive narrative.
- No committed generated SQLite databases or reports.
- No external repository cloning or external sample-data access in the default demo.
- No private repo mode in the open-source default.

## Proposed Files

```text
scripts/
  demo-public.sh

docs/
  VALIDATION.md

README.md

samples/
  public-demo/
    README.md                  # optional manifest/notes if needed

src/dotnet/tests/
  TraceMap.Tests/
    PublicDemoWorkflowTests.cs # optional helper/assertion tests if logic moves into .NET
```

The implementation should prefer shell plus small inline Node assertions if the workflow remains a script. If assertion logic becomes complex, move reusable validation into a small checked-in script under `scripts/` with tests.

## Command Shape

```bash
./scripts/demo-public.sh [out_dir]
```

Initial flags should stay minimal. If implementation needs flags, prefer explicit long options:

```text
--keep-going              continue optional sections after a non-critical failure
--include-python          include Python sample scan using a fresh output-root venv
--require-jvm             fail if Java 21 is unavailable instead of marking JVM unavailable
--include-oss             opt-in pinned public repository mode, if implemented
--json-only               write summaries without verbose console tables
```

Do not add hidden private-repo flags. Reserve future generic environment variables only in docs if needed.

`--keep-going` allows optional and unavailable-eligible sections to continue after non-critical failures. Required sections still cause a non-zero exit; implementation may finish writing a failed summary, but it must not return success.

## Default Demo Flow

1. Resolve repo root from the script location.
2. Resolve output root:
   - caller-provided path, or
   - `mktemp -d`.
   - If a caller-provided path is inside the repository root, refuse it unless `git check-ignore` confirms the path is ignored.
3. Check required tools:
   - `git`
   - `dotnet`
   - `node`
   - `npm`
   - Python only when `--include-python` is supplied
   - Java 21 only when JVM scanning is requested or auto-detected
4. Build required CLIs:
   - .NET solution or CLI project
   - TypeScript adapter if needed
5. Scan checked-in samples:
   - .NET modern sample
   - .NET endpoint server sample
   - TypeScript endpoint client sample
   - TypeScript modern sample
   - Python FastAPI or Flask sample only when `--include-python` is supplied; create a fresh virtual environment under the output root to avoid stale installed code.
   - JVM sample when Java 21 is available; if absent, mark the JVM section `unavailable` unless `--require-jvm` was supplied.
6. Assert scan artifacts exist for every completed scan.
7. Combine selected indexes:
   - endpoint client/server stack
   - mixed dependency stack for portfolio/report demos
8. Run reports:
   - combined dependency report
   - combined dependency report is the authoritative endpoint assertion target for the default demo; the separate `tracemap endpoints` command can be added as an optional output format later.
   - paths
   - reverse
   - diff, marked `deferred` in the first implementation until a concrete before/after fixture pair exists.
   - impact, marked `deferred` in the first implementation until a concrete before/after fixture pair exists.
   - portfolio
   - release-review, marked `deferred` in the first implementation until compatible before/after inputs and contract deltas exist.
9. Run semantic assertions over generated JSON.
10. Write `demo-summary.json` and `demo-summary.md`.
11. Print a concise console summary.

## Section Status Model

Every demo section should land in one of these statuses:

```text
available       command ran and semantic assertions passed
not_requested   caller disabled the section
unavailable     prerequisites were not present or current samples cannot support it
deferred        feature exists but demo integration is intentionally postponed
failed          command or assertion failed
```

The summary must include rule-backed gaps or explanation metadata for `unavailable`, `deferred`, and `failed`.

Required versus optional sections:

| Section | Required by default? | Failure behavior |
| --- | --- | --- |
| tool checks for git, .NET, Node, npm | yes | fail non-zero |
| .NET sample scans | yes | fail non-zero |
| TypeScript sample scans | yes | fail non-zero |
| Python sample scan | no, opt-in | not_requested unless `--include-python`; fail if requested and setup fails |
| JVM sample scan | no, auto-available | unavailable if Java 21 is absent; fail if `--require-jvm` is supplied |
| combine and combined dependency report | yes | fail non-zero |
| paths and reverse | yes after compatible combined index exists | fail non-zero unless report contains an explicitly allowed rule-backed gap |
| portfolio | yes for the portfolio slice | fail non-zero after the slice is implemented |
| diff, impact, release-review | no in first implementation | deferred until before/after fixtures exist |
| optional OSS mode | no | unavailable or partial unless explicitly required by a future flag |

## Artifact Assertions

For each scan:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

For combined/report commands:

- expected Markdown exists
- expected JSON exists where the command supports JSON
- generated SQLite exists where applicable
- no generated report contains private sentinel strings

Use semantic JSON assertions with Node.js or .NET tests instead of brittle text grep where practical.

## Semantic Assertions

Minimum default assertions:

- At least two scan outputs exist with non-empty facts.
- Combined dependency report contains the expected source labels.
- Endpoint report or combined report contains at least one endpoint finding with rule ID, evidence tier, commit SHA, and source labels.
- Path report contains at least one path or a rule-backed gap for an explicitly allowed unavailable case.
- Reverse report contains at least one result or a rule-backed gap for an explicitly allowed unavailable case.
- Diff report contains deterministic rows for before/after generated snapshots or a rule-backed unavailable section.
- Impact report contains deterministic items/gaps for before/after generated snapshots or a rule-backed unavailable section.
- Portfolio report contains source coverage and dependency surfaces.
- All report JSON arrays that affect output are deterministically sorted.
- Re-running deterministic report commands over the same generated indexes produces byte-identical JSON where the underlying command promises stable output.

Initial implementation note: diff, impact, and release-review should be represented in `demo-summary.*` as `deferred` until the implementation adds a concrete checked-in before/after fixture strategy. A future slice can add `samples/public-demo/before` and `samples/public-demo/after`, or another deterministic source-variant generator, then promote those sections to `available`.

Byte-stability scope:

| Artifact kind | Byte comparison expectation |
| --- | --- |
| Public report JSON from deterministic commands | SHOULD be byte-compared after volatile-key checks when re-running report commands over the same generated SQLite indexes. |
| `demo-summary.json` stable counts/statuses | Assert semantically only; do not byte-compare `demo-summary.json` because `outputRootHash` and future run metadata are run-variable. |
| Output root, durations, command paths | Run-variable; do not byte-compare. Public `demo-summary.*` must not include absolute output root paths; use `outputRootHash`, labels, and relative artifact paths. |
| `scan-manifest.json`, `facts.ndjson`, `logs/analyzer.log` | Local scan artifacts; do not byte-compare because manifests can contain scan timestamps and logs can contain local execution details. |
| Diff/impact/portfolio JSON | Before byte comparison, assert no volatile keys such as `generatedAt`, `timestamp`, or `scannedAt` appear in the public report section being compared. |

Commit SHA fields from checked-in sample scans are stable only within one checkout. Assertions should verify presence and SHA-like shape, not hardcode a literal current commit. Default source identity assertions should use source labels and scan-root-relative paths rather than assuming unique git-root hashes across samples.

The volatile-key check should follow the pattern already used by `scripts/smoke-combined-paths.sh`: reject unexpected `generatedAt`, `timestamp`, `scannedAt`, or similar volatile fields before byte-comparing deterministic public report JSON. Byte comparison applies only to report commands over the same generated SQLite indexes; re-running the full scan step is not expected to be byte-stable.

## Demo Summary Schema

Minimum `demo-summary.json` shape:

```json
{
  "version": "1.0",
  "outputRootHash": "path-hash:<hash>",
  "sections": [
    {
      "name": "dependency-report",
      "status": "available",
      "classification": "ActionableStaticEvidence",
      "evidenceTier": "Tier2Structural",
      "ruleIds": ["public.demo.summary.v1"],
      "reportCoverage": "FullEvidenceAvailable",
      "artifactPaths": ["reports/dependency/dependency-report.json"],
      "counts": {
        "sources": 2,
        "endpoints": 1,
        "paths": 0,
        "gaps": 0
      },
      "reason": ""
    }
  ]
}
```

Rules:

- `outputRootHash` is allowed; the absolute output root is not allowed in public summary artifacts.
- `artifactPaths` are relative to the output root.
- `sections[].status` must be one of the Section Status Model values.
- `sections[].evidenceTier` must be present. Deferred, unavailable, and failed sections use `Tier4Unknown`.
- `sections[].ruleIds` must be present and non-empty; summary workflow rows use `public.demo.summary.v1`.
- Deferred, unavailable, and failed sections must include a non-empty `reason`.
- Counts may omit irrelevant keys, but present keys must be deterministic for the same generated inputs.

## Safety And Redaction

The workflow must reuse existing report redaction behavior. `check-private-paths.sh` protects committed files through `git grep`; it does not inspect generated output directories. The demo must run its own sentinel scan over public-shareable generated reports and summaries.

Generated artifact classes:

| Class | Examples | Redaction expectation |
| --- | --- | --- |
| Public summary artifacts | `demo-summary.md`, `demo-summary.json` | Must pass sentinel scan and must not contain absolute output roots or unsafe values. |
| Public-shareable reports | `dependency-report.md`, `dependency-report.json`, `paths-report.md`, `reverse-report.json`, `portfolio-report.json` | Must pass sentinel scan and must not contain raw private paths or unsafe values. |
| Local-only scan artifacts | `scan-manifest.json`, `facts.ndjson`, `logs/analyzer.log` | Must remain generated and uncommitted; may contain temp output details, but should not be used as public demo snippets. |

The public-report sentinel scan should check for:

- raw SQL text from fixtures,
- connection-string-like values,
- config values,
- source snippets,
- raw URLs with tokens,
- local absolute paths,
- private repo names,
- known developer home path patterns.

Sentinel scan mechanics:

- Run after all public report files and `demo-summary.*` are written.
- Scan public-shareable files only: `demo-summary.md`, `demo-summary.json`, `reports/**/*.md`, and `reports/**/*.json`.
- Exclude local-only scan artifacts under `scans/**`, combined SQLite files, logs, and facts files.
- Implement as a reusable helper, preferably a small Node script if shell globs become brittle.
- On failure, exit non-zero with step name `public-report-sentinel-scan` and print only sanitized relative file paths plus sentinel category.

The default output root can be printed because it is generated or caller-provided. No source file should bake in a local absolute path.

Recommended in-repo output root, if a user wants one, is `.tracemap-demo/`. The repository's `.gitignore` should be updated to include a generic pattern for that path as part of implementation, before the script runs the `git check-ignore` guard. The script must not modify tracked repository files at runtime.

## Relationship To Existing Smokes

`scripts/smoke-combined-paths.sh` remains the focused path smoke. The public demo may call it or reproduce parts of it, but the responsibilities differ:

- `smoke-combined-paths.sh`: focused regression for `scan -> combine -> report -> paths`.
- `demo-public.sh`: product walkthrough and broad artifact bundle across major workflows.
- `smoke-open-source-repos.sh`: optional networked confidence against pinned public repositories.

The implementation should avoid duplicating complex assertions. If shared assertion helpers are needed, introduce a small helper script and update both smokes to use it.

## Optional OSS Mode

If `--include-oss` is implemented in the first slice, it must:

- be opt-in,
- use pinned URLs and commit SHAs,
- write under the output root,
- label reduced coverage explicitly,
- avoid making OSS success required for the default demo,
- document expected runtime and prerequisites.

It is acceptable to defer this mode and only document the existing OSS smoke.

## Error Handling

The script should fail fast for required default sections. Optional sections can emit `unavailable` if prerequisites are absent and the spec allows that absence.

Failures should include:

- section name,
- command label,
- exit code,
- path to sanitized generated logs under the output root,
- next suggested validation command.

Failures should not include:

- raw source snippets,
- raw SQL,
- secret-looking values,
- private local paths baked into the script.

## Implementation Slices

### Slice 1: Demo Skeleton And Summary

- Add script command.
- Resolve output root.
- Check tools.
- Build .NET and TypeScript CLIs.
- Run a small subset of scans.
- Write summary with available/unavailable sections.

### Slice 2: Combined Report, Paths, Reverse

- Combine endpoint samples.
- Run report, paths, reverse.
- Add semantic assertions.
- Reuse or call focused combined path smoke where sensible.

### Slice 3: Portfolio And Deferred Change Context

- Generate a demo-run portfolio manifest from the actual generated index paths. Do not reuse `samples/portfolio.example.json` directly because its paths are documentation examples, not output-root paths.
- Generate the portfolio manifest under the output root using an inline shell here-doc or Node helper. It may contain absolute generated index paths because it is a local-only generated input, not a public-shareable report.
- Run portfolio over generated indexes.
- Mark diff, impact, and release-review `deferred` with explicit reasons until a concrete before/after fixture pair exists.
- In a later slice, add a checked-in before/after fixture strategy and then run diff, impact, and release-review where compatible.

### Slice 4: Docs And Validation

- Update README.
- Update `docs/VALIDATION.md`.
- Add troubleshooting.
- Ensure private path guard passes.

## Scope Decisions

1. The public demo script may call existing smoke scripts only when their output and assertions fit the public summary model; otherwise use shared assertion helpers to avoid brittle duplication.
2. JVM runs only when Java 21 is available or explicitly required. If Java 21 is absent and not required, the JVM section is `unavailable`.
3. Python is opt-in for the first implementation through `--include-python`; when enabled, it uses a fresh virtual environment under the output root to avoid stale installed code.
4. Diff, impact, and release-review are `deferred` in the first implementation until a concrete before/after fixture pair exists.
5. Generated output roots are kept for inspection and printed at the end; the script does not auto-delete them. Docs must mention manual cleanup.

## Limitations

- Demo evidence is static. It does not prove runtime execution, production traffic, deployment behavior, auth behavior, package restore success, SQL execution, or business impact.
- Checked-in samples are intentionally small and do not cover every language feature.
- Deterministic demo facts assume a reasonably clean checkout; stray generated files under `samples/` or output written inside the repository can change scans and should be rejected or cleaned.
- Optional OSS scans may be reduced coverage and are not part of the default deterministic demo.
- Generated reports are examples of TraceMap output, not release approval or merge recommendations.
