# Public Demo Workflow Tasks

## Task Checklist

- [x] 1. Confirm implementation boundary. Requirements: 1, 3, 4.
  - [x] Decide whether the first implementation is script-only or includes a small reusable assertion helper.
  - [x] Record that JVM runs only when Java 21 is available or explicitly required.
  - [x] Record that Python is opt-in for the first implementation and uses a fresh output-root venv.
  - [x] Record that diff, impact, and release-review are deferred until a concrete before/after fixture pair exists.
  - [x] Create and maintain `.kiro/specs/public-demo-workflow/implementation-state.md`.

- [x] 2. Add demo script skeleton. Requirements: 1, 2, 6.
  - [x] Add `scripts/demo-public.sh`.
  - [x] Resolve repo root from script location.
  - [x] Resolve caller-provided output root or create `mktemp -d`.
  - [x] Refuse caller-provided output directories inside the repository root unless `git check-ignore` confirms they are ignored.
  - [x] Add a generic `.gitignore` pattern for the recommended in-repo output root `.tracemap-demo/` if implementation supports that path.
  - [x] Add section status tracking.
  - [x] Add concise console header and footer.
  - [x] Keep generated output roots for inspection and document manual cleanup.
  - [x] Avoid baked-in local absolute paths.

- [x] 3. Add toolchain checks. Requirements: 2, 8.
  - [x] Check `dotnet`.
  - [x] Check `node`.
  - [x] Check `npm`.
  - [x] Check `git`.
  - [x] Check Python tooling only when `--include-python` is supplied.
  - [x] Check Java 21 when JVM scanning is available or `--require-jvm` is supplied.
  - [x] Use clear failure messages and Homebrew guidance where appropriate.

- [x] 4. Build required local CLIs. Requirements: 2, 4, 8.
  - [x] Build the .NET solution or CLI project.
  - [x] Build/install the TypeScript adapter as required by existing validation docs.
  - [x] Keep build outputs and generated environments out of git.

- [x] 5. Scan default public samples. Requirements: 3, 4, 5.
  - [x] Scan .NET modern sample.
  - [x] Scan .NET endpoint server sample.
  - [x] Scan TypeScript endpoint client sample.
  - [x] Scan TypeScript modern sample.
  - [x] Scan Python FastAPI or Flask sample only when `--include-python` is supplied; otherwise mark the Python section `not_requested`.
  - [x] Scan JVM sample if Java 21 is available; otherwise mark the JVM section unavailable unless `--require-jvm` is supplied.
  - [x] Assert required scan artifacts exist.
  - [x] Assert expected fact outputs are non-empty.

- [x] 11. Add demo summary artifacts. Requirements: 1, 4, 5.
  - [x] Write `demo-summary.json`.
  - [x] Write `demo-summary.md`.
  - [x] Include section statuses, counts, relative artifact paths, coverage, and gaps.
  - [x] Store output-root hash/label and relative artifact paths only; do not write the absolute output root into public summary artifacts.
  - [x] Keep stable counts/statuses deterministic and keep run-variable fields out of byte comparisons.
  - [x] Ensure unavailable/deferred sections are visible.

- [x] 12. Add semantic assertion helpers. Requirements: 5, 6, 8.
  - [x] Prefer Node or .NET helpers over brittle shell text parsing.
  - [x] Assert JSON schema fields used by the demo.
  - [x] Assert no unsafe sentinel strings in generated public-shareable reports and summaries.
  - [x] Define sentinel scan globs for `demo-summary.*`, `reports/**/*.md`, and `reports/**/*.json`.
  - [x] Exclude local-only scan artifacts, SQLite files, facts, and logs from public-report sentinel scanning.
  - [x] Fail sentinel leaks with step name `public-report-sentinel-scan` and sanitized relative paths.
  - [x] Add negative assertion coverage for a missing required artifact.
  - [x] Add negative assertion coverage for output directory inside the repository root when it is not git-ignored.
  - [x] Add positive assertion coverage for output directory inside the repository root when it is git-ignored.
  - [x] Add negative assertion coverage for a planted public-report sentinel leak.
  - [x] Add negative assertion coverage proving caller-provided output roots do not false-trip the sentinel scan, for example by checking absolute path patterns rather than naive basenames, while genuine home-path leaks do.
  - [x] Add section-status assertion coverage for at least one deferred section.
  - [x] Add schema assertion coverage for required `demo-summary.json` fields.

- [x] 13. Update documentation. Requirements: 7.
  - [x] Add README public demo quickstart.
  - [x] Update `docs/VALIDATION.md`.
  - [x] Document prerequisites.
  - [x] Document generated outputs.
  - [x] Document which artifacts are public-shareable and which scan artifacts are local-only.
  - [x] Document static-analysis limitations.

- [x] 14. Validate implementation. Requirements: 8.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] TypeScript build/check commands required by the demo
  - [x] New public demo command
  - [x] `./scripts/smoke-combined-paths.sh` or explicit deferral
  - [x] `./scripts/check-private-paths.sh`
  - [x] Generated public-report sentinel scan
  - [x] `git diff --check`

## Suggested PR Slices

- [x] PR 1a: script skeleton, tool checks, .NET/TypeScript scans, summary skeleton, generated public-report sentinel scan.
- [x] PR 1b: combine and dependency report assertions, docs, optional JVM availability behavior.
- [x] PR 2: path/reverse semantic assertions and shared assertion helpers.
- [x] PR 3: portfolio section plus deferred diff/impact/release-review summary entries.
- [x] PR 4: concrete before/after fixture pair that promotes diff/impact/release-review to available.
- PR 5: optional OSS mode or CI wiring if desired.

## Shipped Follow-Up Slice

- [x] Combined endpoint-stack and mixed-stack indexes use deterministic public labels.
- [x] Combined SQLite outputs are generated under the demo output root.
- [x] Combined dependency reports are generated for endpoint-stack and mixed-stack indexes.
- [x] The endpoint-stack dependency report is the default endpoint assertion target.
- [x] Dependency report assertions verify source labels, SHA-shaped commits, rule IDs, evidence tiers, endpoint findings, dependency surfaces, and dependency edges.
- [x] Targeted `tracemap paths` runs over the endpoint stack and verifies rule-backed paths, source labels, supporting IDs, and deterministic repeated JSON.
- [x] Targeted `tracemap reverse` runs over the endpoint stack and verifies selected surfaces, endpoint roots, rule/evidence metadata, supporting IDs, and deterministic repeated JSON.
- [x] A demo-run `portfolio-manifest.json` is generated from generated combined index paths.
- [x] `tracemap portfolio` runs over the generated manifest and verifies source coverage plus dependency surface metadata.
- [x] Public generated reports and summaries remain covered by the sentinel scan.
- [x] Checked-in `samples/public-demo/before` and `samples/public-demo/after` fixtures provide deterministic public before/after evidence.
- [x] The demo scans and combines the public before/after fixtures with deterministic label `public-demo-api`.
- [x] `tracemap diff` runs over the generated before/after combined snapshots and verifies route surface, SQL surface, rule, evidence-tier, commit, and supporting-ID metadata.
- [x] `tracemap impact` runs over the generated before/after combined snapshots and verifies surface impact evidence without runtime-impact claims.
- [x] `tracemap release-review` runs over the generated before/after combined snapshots and verifies static release evidence, checklist rules, and explicit not-requested/unavailable/deferred subsections.

## Definition Of Done

- The default demo does not clone repositories, fetch external sample data, or call external analysis services.
- The demo command exits non-zero on failed required assertions.
- Generated artifacts are not committed.
- Public-shareable Markdown/JSON outputs are safe to share publicly and pass the generated sentinel scan.
- Every evidence-bearing assertion relies on rule IDs and evidence tiers.
- Partial/unavailable sections are explicitly labeled.
- Docs explain what the demo proves and what it does not prove.

## Deferred Follow-Ups

- Prepare a fresh Python temporary environment under the output root only when Python scans are requested.
- Leave separate `tracemap endpoints` output as optional/follow-up unless implementation explicitly adds it.
- Document troubleshooting.
- Document optional OSS workflow or state that it remains separate.
