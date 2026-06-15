# Legacy Baseline Regression Artifacts Tasks

## Implementation Tasks

- [ ] 1. Define baseline schemas and storage boundaries. Requirements: 1, 5.
  - [ ] Define `legacy-baseline-manifest.v1` with neutral sample labels, safe repo/commit identity, scanner/extractor versions, coverage labels, build status, safety classification, limitations, and deterministic count maps.
  - [ ] Define `legacy-baseline-comparison.v1` with movement labels, schema/extractor compatibility fields, review-needed markers, and human-readable summary metadata.
  - [ ] Use `.kiro/baselines/legacy/` as the tracked public-safe baseline path and `.tmp/legacy-baselines/` as the ignored local-only baseline path.
  - [ ] Derive `baselineId` deterministically from neutral label, purpose, and public-safe year-month or fixture-pinned creation metadata, and use that `baselineId` as the on-disk baseline directory segment.
  - [ ] Keep `.tmp/legacy-baselines/` ignored in `.gitignore` and add a guard proving the local-only path is ignored.
  - [ ] Add `legacy.baseline.redacted-manifest.v1`, `legacy.baseline.coverage-snapshot.v1`, `legacy.baseline.regression-comparison.v1`, and `legacy.baseline.safety-validation.v1` to `rules/rule-catalog.yml` with limitations before emitting them.
  - [ ] Add schema fixtures for public-safe, local-only, and rejected manifests.
  - [ ] Ensure serialization sorts keys, arrays, and count maps deterministically.
  - [ ] Make time fields injectable or fixture-pinned so repeated creates on the same input can be byte-identical for deterministic tests.

- [ ] 2. Implement redacted baseline creation. Requirements: 1, 2, 3, 7.
  - [ ] Read local scan artifacts from an ignored input directory only.
  - [ ] Extract scan status, coverage label, build status, TraceMap version, extractor versions, known gaps, output artifact existence, and safe identity fields from `scan-manifest.json`.
  - [ ] Aggregate `facts.ndjson` by fact type, rule ID, evidence tier, extractor, known gap code, and table-driven surface group.
  - [ ] Preserve semantic, structural, syntax/textual, and unknown/gap counts separately.
  - [ ] Record partial, failed, truncated, timeout, and deferred states with explicit limitations.
  - [ ] Avoid copying raw facts, SQLite rows, report prose, analyzer logs, source snippets, SQL, config values, remotes, paths, or private sample names into baseline output.

- [ ] 3. Implement safety classification and validation. Requirements: 5, 6.
  - [ ] Classify manifests and comparisons as `public-safe`, `local-only`, or `rejected`.
  - [ ] Reject unsafe raw absolute paths, home fragments, private sample identities, remotes, raw SQL, config values, connection strings, endpoint addresses, secrets, credentials, raw analyzer output, and source-like snippets.
  - [ ] Ensure validator diagnostics report sanitized categories without echoing unsafe values.
  - [ ] Implement deterministic redaction hashes for safe-to-hash identity fields with length-prefixed, escaped, encoded, or existing structured TraceMap hash input.
  - [ ] Define and implement the classifier boundary between safe-to-hash identity fields, low-entropy/enumerable private identities, and secret-like values.
  - [ ] Omit or category-only represent secret-like values instead of hashing them.
  - [ ] Add tests proving public-safe promotion passes and unsafe candidates fail.

- [ ] 4. Implement rule/fact coverage snapshots. Requirements: 2, 3.
  - [ ] Add rule coverage snapshots with observed rule IDs, evidence tier distribution, extractor versions, and limitation references.
  - [ ] Add fact coverage snapshots with counts by fact type, evidence tier, and contributing rule IDs.
  - [ ] Add table-driven surface grouping for C#, UI events, HTTP, WCF/service references, SQL/data, config, packages, build environment, and `other`.
  - [ ] Represent `not-observed`, `not-in-scope`, and `unknown` distinctly for important legacy surfaces.
  - [ ] Preserve syntax/config fallback counts separately when semantic analysis is reduced.
  - [ ] Fail or reject baselines when observed rule IDs lack catalog entries.

- [ ] 5. Implement regression comparison output. Requirements: 4.
  - [ ] Compare baseline and candidate redacted manifests by total counts, fact type, rule ID, evidence tier, extractor, surface, known gap, coverage label, and build status.
  - [ ] Emit movement labels: `increase`, `decrease`, `unchanged`, `new-category`, `removed-category`, `coverage-changed`, and `not-comparable`.
  - [ ] Emit `overallStatus: review-needed` and `reviewNeeded` entries separately from movement labels when human review is required.
  - [ ] Require explicit migration maps for renamed rule IDs, fact types, or incompatible schema versions.
  - [ ] Implement `legacy-baseline-migration-map.v1` with rule ID and fact type rename entries, schema version fields, reasons, and limitations.
  - [ ] Flag decreases, removed categories, reduced coverage, rejected safety state, and unmapped schema changes as review-needed.
  - [ ] Produce deterministic `comparison.json` and `comparison.md`.
  - [ ] Ensure comparison wording says static evidence counts changed and does not claim business impact.

- [ ] 6. Add CLI workflow. Requirements: 1, 4, 5, 7.
  - [ ] Add `tracemap baseline create`, `tracemap baseline validate`, and `tracemap baseline compare` commands, or document a temporary script fallback only if CLI architecture blocks the command shape.
  - [ ] Support dry-run validation that reports safety classification without writing tracked files.
  - [ ] Require neutral sample labels and reject labels that look like local paths, remotes, private repo names, or user/machine identifiers.
  - [ ] Reject labels containing path separators, URI schemes, `@`-prefixed identifiers, `.git` suffixes, home-directory fragments, Windows drive patterns, raw hostnames, or organization/user identifiers.
  - [ ] Keep local-only outputs under ignored `.tmp/legacy-baselines/`.
  - [ ] Keep comparison output local-only until a separate promotion step reruns baseline validation, the redaction validator, and `scripts/check-private-paths.sh`.
  - [ ] Document the public-safe promotion step as manual validation plus safety checks, or add `tracemap baseline promote` if implementation evidence shows a dedicated command is needed.
  - [ ] Document the workflow without naming private samples or absolute paths.

- [ ] 7. Add tests and fixtures. Requirements: 1, 2, 3, 4, 5, 6, 7.
  - [ ] Add `samples/synthetic-legacy-scan/` with a minimal synthetic `scan-manifest.json` using neutral labels, version placeholders, partial coverage/build status, extractor versions, and no private paths, names, remotes, snippets, SQL, config values, or secrets.
  - [ ] Add a small synthetic `facts.ndjson` under `samples/synthetic-legacy-scan/` with rows across at least two rule IDs, two fact types, two evidence tiers, and two extractors, with only safe relative fixture paths or no paths.
  - [ ] Test deterministic schema serialization and count ordering.
  - [ ] Test two baseline creates on the same fixture produce byte-identical JSON when the clock is fixture-pinned.
  - [ ] Test public-safe, local-only, and rejected safety classifications.
  - [ ] Test `.tmp/legacy-baselines/` is git-ignored.
  - [ ] Test partial build, failed build, syntax fallback, timeout, truncated, and deferred baseline states.
  - [ ] Test public-safe manifests do not emit full timestamps and use the approved year-month or pinned fixture time policy.
  - [ ] Test private-source manifests omit or category-only represent private repo identity instead of emitting `repoIdentityHash`.
  - [ ] Test fact/rule/evidence/extractor/surface aggregation from synthetic `facts.ndjson`.
  - [ ] Test rule catalog coverage failure for unknown rule IDs.
  - [ ] Test unsafe values are rejected without echoing the unsafe value.
  - [ ] Test classifier boundary cases for safe-to-hash public identity, low-entropy/enumerable private identity, neutral labels, and secret-like values.
  - [ ] Test secret-like values are omitted or category-only, not hashed.
  - [ ] Test context-separated hash input is stable and prevents delimiter ambiguity across fields.
  - [ ] Test comparison movement labels for increases, decreases, new categories, removed categories, unchanged counts, coverage changes, and schema mismatches.
  - [ ] Test schema mismatches become `not-comparable` and `review-needed` without a migration map.
  - [ ] Test migration maps for renamed rule IDs and fact types.
  - [ ] Test comparison Markdown avoids impact, safety, reachability, and production-usage claims.
  - [ ] Test comparison Markdown against an explicit prohibited phrase list including `impacted`, `safe`, `unsafe`, `reachable`, `production`, and `business`.
  - [ ] Test an empty in-scope scan records explicit zeros while out-of-scope categories remain `not-in-scope`.
  - [ ] Test dry-run validation writes no tracked files.
  - [ ] Test `--label` values containing path separators, URI schemes, `@`-prefixed identifiers, `.git` suffixes, home-directory fragments, Windows drive patterns, raw hostnames, or organization/user identifiers are rejected.
  - [ ] Test promotion reruns the baseline validator before tracked output is accepted.
  - [ ] Test `tracemap baseline validate` independently against a checked-in public-safe fixture under `.kiro/baselines/legacy/`.
  - [ ] Test promoted public-safe files pass `scripts/check-private-paths.sh`.

- [ ] 8. Validate implementation. Requirements: 7.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run dry-run baseline creation against checked-in synthetic fixtures, for example `tracemap baseline create --scan-output samples/synthetic-legacy-scan --label synthetic-alpha --purpose original-parser-snapshot --out .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06 --dry-run`, and verify the reported safety classification without writing tracked files.
  - [ ] Run non-dry-run baseline creation to `.tmp/legacy-baselines/synthetic-alpha/` and verify `baseline-manifest.json` and `baseline-summary.md`.
  - [ ] Run baseline comparison between `.tmp/legacy-baselines/` manifests and verify `comparison.json` and `comparison.md`.
  - [ ] Run relevant pinned smoke checks from `docs/VALIDATION.md` if scan, report, or adapter behavior changes, or record an explicit deferral rationale.
  - [ ] Run `git check-ignore .tmp/legacy-baselines/example`.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.

## Deferred Follow-Ups

- Add portfolio-level baseline comparison across multiple neutral sample labels
  after single-baseline comparison is stable.
- Add optional local-only path-hash drilldowns if count-only comparison is not
  enough for private legacy validation.
- Add site or docs promotion only after public-safe baseline examples are
  reviewed separately.
