# Public Demo Workflow Tasks

## Task Checklist

- [x] 1. Confirm implementation boundary. Requirements: 1, 3, 4.
  - [x] Decide whether the first implementation is script-only or includes a small reusable assertion helper.
  - [x] Record that JVM runs only when Java 21 is available or explicitly required.
  - [x] Record that Python is opt-in for the first implementation and uses a fresh output-root venv.
  - [x] Record that diff, impact, and release-review are deferred until a concrete before/after fixture pair exists.
  - [x] Create and maintain `.kiro/specs/public-demo-workflow/implementation-state.md`.

- [ ] 2. Add demo script skeleton. Requirements: 1, 2, 6.
  - [ ] Add `scripts/demo-public.sh`.
  - [ ] Resolve repo root from script location.
  - [ ] Resolve caller-provided output root or create `mktemp -d`.
  - [ ] Refuse caller-provided output directories inside the repository root unless `git check-ignore` confirms they are ignored.
  - [ ] Add a generic `.gitignore` pattern for the recommended in-repo output root `.tracemap-demo/` if implementation supports that path.
  - [ ] Add section status tracking.
  - [ ] Add concise console header and footer.
  - [ ] Keep generated output roots for inspection and document manual cleanup.
  - [ ] Avoid baked-in local absolute paths.

- [ ] 3. Add toolchain checks. Requirements: 2, 8.
  - [ ] Check `dotnet`.
  - [ ] Check `node`.
  - [ ] Check `npm`.
  - [ ] Check `git`.
  - [ ] Check Python tooling only when `--include-python` is supplied.
  - [ ] Check Java 21 when JVM scanning is available or `--require-jvm` is supplied.
  - [ ] Use clear failure messages and Homebrew guidance where appropriate.

- [ ] 4. Build required local CLIs. Requirements: 2, 4, 8.
  - [ ] Build the .NET solution or CLI project.
  - [ ] Build/install the TypeScript adapter as required by existing validation docs.
  - [ ] Prepare a fresh Python temporary environment under the output root only when Python scans are requested.
  - [ ] Keep build outputs and generated environments out of git.

- [ ] 5. Scan default public samples. Requirements: 3, 4, 5.
  - [ ] Scan .NET modern sample.
  - [ ] Scan .NET endpoint server sample.
  - [ ] Scan TypeScript endpoint client sample.
  - [ ] Scan TypeScript modern sample.
  - [ ] Scan Python FastAPI or Flask sample only when `--include-python` is supplied; otherwise mark the Python section `not_requested`.
  - [ ] Scan JVM sample if Java 21 is available; otherwise mark the JVM section unavailable unless `--require-jvm` is supplied.
  - [ ] Assert required scan artifacts exist.
  - [ ] Assert expected fact outputs are non-empty.

- [ ] 6. Combine demo indexes. Requirements: 4, 5.
  - [ ] Create endpoint stack combined index with deterministic labels.
  - [ ] Create mixed stack combined index for portfolio/report examples.
  - [ ] Assert combined SQLite indexes exist.
  - [ ] Assert combined source labels are present and deterministic.

- [ ] 7. Run dependency and endpoint reports. Requirements: 4, 5, 6.
  - [ ] Run combined dependency report.
  - [ ] Use combined dependency report as the default endpoint assertion target.
  - [ ] Leave separate `tracemap endpoints` output as optional/follow-up unless implementation explicitly adds it.
  - [ ] Assert Markdown and JSON outputs exist.
  - [ ] Assert endpoint findings include rule IDs, evidence tiers, commit SHAs, and source labels.
  - [ ] Assert no unsafe sentinel values render.

- [ ] 8. Run path and reverse demos. Requirements: 4, 5, 6.
  - [ ] Run default or targeted `tracemap paths`.
  - [ ] Assert path rows or allowed rule-backed gaps.
  - [ ] Run `tracemap reverse` for at least one useful selector.
  - [ ] Assert reverse rows or allowed rule-backed gaps.
  - [ ] Compare byte-stable JSON for deterministic repeated path/reverse outputs where supported.

- [ ] 9. Represent deferred diff and impact demos. Requirements: 4, 5, 6.
  - [ ] Mark `diff` deferred in first implementation because no concrete before/after fixture pair exists yet.
  - [ ] Mark `impact` deferred in first implementation because no concrete before/after fixture pair exists yet.
  - [ ] Assert deferred status and explanation metadata in `demo-summary.json`.
  - [ ] Document the future before/after fixture strategy needed to promote these sections to available.

- [ ] 10. Run portfolio and release-review demos. Requirements: 4, 5, 6.
  - [ ] Generate a demo-run portfolio manifest from actual generated index paths.
  - [ ] Run `tracemap portfolio` over generated indexes.
  - [ ] Assert source coverage and dependency surfaces.
  - [ ] Mark release-review deferred in first implementation because compatible before/after inputs and contract deltas are absent.
  - [ ] Assert release-review deferred status and explanation metadata.

- [ ] 11. Add demo summary artifacts. Requirements: 1, 4, 5.
  - [ ] Write `demo-summary.json`.
  - [ ] Write `demo-summary.md`.
  - [ ] Include section statuses, counts, relative artifact paths, coverage, and gaps.
  - [ ] Store output-root hash/label and relative artifact paths only; do not write the absolute output root into public summary artifacts.
  - [ ] Keep stable counts/statuses deterministic and keep run-variable fields out of byte comparisons.
  - [ ] Ensure unavailable/deferred sections are visible.

- [ ] 12. Add semantic assertion helpers. Requirements: 5, 6, 8.
  - [ ] Prefer Node or .NET helpers over brittle shell text parsing.
  - [ ] Assert JSON schema fields used by the demo.
  - [ ] Assert rule IDs and evidence tiers on evidence and gaps.
  - [ ] Assert no unsafe sentinel strings in generated public-shareable reports and summaries.
  - [ ] Define sentinel scan globs for `demo-summary.*`, `reports/**/*.md`, and `reports/**/*.json`.
  - [ ] Exclude local-only scan artifacts, SQLite files, facts, and logs from public-report sentinel scanning.
  - [ ] Fail sentinel leaks with step name `public-report-sentinel-scan` and sanitized relative paths.
  - [ ] Add negative assertion coverage for a missing required artifact.
  - [ ] Add negative assertion coverage for output directory inside the repository root when it is not git-ignored.
  - [ ] Add positive assertion coverage for output directory inside the repository root when it is git-ignored.
  - [ ] Add negative assertion coverage for a planted public-report sentinel leak.
  - [ ] Add negative assertion coverage proving caller-provided output roots do not false-trip the sentinel scan, for example by checking absolute path patterns rather than naive basenames, while genuine home-path leaks do.
  - [ ] Add section-status assertion coverage for at least one deferred section.
  - [ ] Add schema assertion coverage for required `demo-summary.json` fields.
  - [ ] Add lightweight tests if helper logic is substantial.

- [ ] 13. Update documentation. Requirements: 7.
  - [ ] Add README public demo quickstart.
  - [ ] Update `docs/VALIDATION.md`.
  - [ ] Document prerequisites.
  - [ ] Document generated outputs.
  - [ ] Document which artifacts are public-shareable and which scan artifacts are local-only.
  - [ ] Document static-analysis limitations.
  - [ ] Document troubleshooting.
  - [ ] Document optional OSS workflow or state that it remains separate.

- [ ] 14. Validate implementation. Requirements: 8.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] TypeScript build/check commands required by the demo
  - [ ] New public demo command
  - [ ] `./scripts/smoke-combined-paths.sh` or explicit deferral
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] Generated public-report sentinel scan
  - [ ] `git diff --check`

## Suggested PR Slices

- [ ] PR 1a: script skeleton, tool checks, .NET/TypeScript scans, summary skeleton, generated public-report sentinel scan.
- [ ] PR 1b: combine and dependency report assertions, docs, optional JVM availability behavior.
- [ ] PR 2: path/reverse semantic assertions and shared assertion helpers.
- [ ] PR 3: portfolio section plus deferred diff/impact/release-review summary entries.
- [ ] PR 4: concrete before/after fixture pair that promotes diff/impact/release-review to available.
- [ ] PR 5: optional OSS mode or CI wiring if desired.

## Definition Of Done

- [ ] The default demo does not clone repositories, fetch external sample data, or call external analysis services.
- [ ] The demo command exits non-zero on failed required assertions.
- [ ] Generated artifacts are not committed.
- [ ] Public-shareable Markdown/JSON outputs are safe to share publicly and pass the generated sentinel scan.
- [ ] Every evidence-bearing assertion relies on rule IDs and evidence tiers.
- [ ] Partial/unavailable sections are explicitly labeled.
- [ ] Docs explain what the demo proves and what it does not prove.
