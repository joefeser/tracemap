# Legacy Baseline Regression Artifacts Implementation State

Status: not-started
Branch: codex/legacy-baseline-regression-artifacts-spec
Scope: spec-only
Public claim level: hidden
Readiness: ready-for-implementation

## Summary

This spec defines redacted baseline manifests and regression comparison outputs
for legacy sample scans. It preserves an original parser snapshot as safe
aggregate evidence so future TraceMap improvements can be compared against it
without committing raw scan artifacts, private sample identities, local paths,
remotes, source snippets, raw SQL, config values, connection strings, endpoint
addresses, secrets, or analyzer logs.

## Scope Decisions

- Spec-only branch; no scanner, reducer, CLI, or script implementation in this
  worktree.
- Baselines are redacted summaries, not raw scan outputs.
- Comparisons report changed static evidence counts and coverage labels; they do
  not prove business impact, runtime reachability, safety, production usage, or
  reducer conclusions.
- Public-safe artifacts use neutral sample labels and aggregate counts only.
- Local-only artifacts stay under ignored `.tmp/` storage.
- Private legacy sample names, absolute paths, raw remotes, raw SQL, config
  values, secrets, endpoint addresses, source snippets, and raw analyzer output
  must not be committed.
- No LLM, embedding, vector database, or prompt-based classification belongs in
  TraceMap core.

## Review State

- Initial spec draft created.
- Kiro Opus first-pass review completed with reduced coverage because Kiro
  reported denied shell access, but it returned complete findings. Blocking
  issues were `.tmp/legacy-baselines/` not being git-ignored and timestamp
  determinism.
- Kiro Sonnet first-pass review completed with full coverage. Blocking issues
  were ambiguous hash input construction, underspecified secret-like handling,
  missing classifier-boundary tests, and overbroad optional SQLite reads.
- Review fixes applied:
  - `.tmp/legacy-baselines/` is ignored and implementation tasks require a
    guard for the ignored path.
  - time fields are injectable or fixture-pinned, with year-month precision for
    public-safe manifests.
  - tracked storage is fixed to `.kiro/baselines/legacy/` and local-only storage
    is fixed to `.tmp/legacy-baselines/`.
  - redaction hashing requires length-prefixed, escaped, encoded, or existing
    structured TraceMap hash input.
  - secret-like and low-entropy/enumerable private identity handling is defined.
  - SQLite reads are restricted to aggregate count queries over safe columns.
  - candidate manifests are produced by the same create workflow.
  - migration-map schema is specified.
  - proposed rule IDs use the `legacy.baseline.*` namespace and tasks require
    catalog entries before emission.
  - comparison promotion is separate from compare output and gated by validation.
  - missing tests from both reviews were added to `tasks.md`.
- Sonnet re-review completed with full coverage. It confirmed the previous
  blockers were resolved and identified final readiness issues around clarifying
  catalog-entry timing and defining the synthetic fixture required by the future
  smoke command.
- Final re-review fixes applied:
  - design clarifies `legacy.baseline.*` rule IDs are to be added during
    implementation before emission, not pre-committed by this spec branch.
  - tasks define the minimum `samples/synthetic-legacy-scan/` fixture contents
    for future implementation.
  - manifest limitations are split between scanner/coverage and safety scopes.
  - identity kind variants are documented.
  - dry-run, non-dry-run, and comparison smoke commands are separated.
  - validation tasks include an independent `baseline validate` fixture test.
- Final Sonnet re-review completed with full coverage and no blocking issues.
  It suggested non-blocking clarifications for local-only neutral-label identity
  hashes, label rejection examples, comparison schema shape, tracked synthetic
  fixtures, and migration-map wording; those were folded into requirements,
  design, and tasks.
- PR review loop found three Gemini medium comments:
  - add artifact metadata to the baseline manifest shape;
  - keep `review-needed` as an overall status/review flag rather than a movement
    label;
  - define `factTypeRenames` entries in the migration-map example.
- Gemini findings were addressed in `design.md` and `tasks.md`.
- PR review loop then found a Qodo bug around inconsistent baseline directory
  naming. `design.md` now states that `baselineId` is the canonical on-disk
  directory segment derived from label, purpose, and public-safe time metadata,
  and examples use the full baseline ID consistently.
- Spec is ready for implementation.

## Validation

Completed:

- Kiro Opus spec review.
- Kiro Sonnet spec review.
- `node scripts/kiro-review.mjs --self-test`.
- Sonnet re-review after first fixes.
- Sonnet final re-review after final readiness fixes.
- Repo spec validation command discovery. No broader non-site spec validator was
  found beyond `scripts/kiro-review.mjs --self-test`.
- `git check-ignore .tmp/legacy-baselines/example`.
- `./scripts/check-private-paths.sh`.
- `git diff --check`.

## Follow-Ups For Implementation

- Keep implementation tasks unchecked until code lands.
- Record any implementation-time deviation from the CLI-first workflow in this
  file before coding around it.
- Update this file and check off tasks only in the implementation PR.
- Run or explicitly defer relevant pinned smoke checks from `docs/VALIDATION.md`
  when implementation changes scan, report, adapter, or CLI behavior.
