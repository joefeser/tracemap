# Legacy Sample Evidence Pack Implementation State

Status: implemented
Branch: codex/legacy-sample-evidence-pack
Scope: CLI/reporting implementation
Public claim level: hidden until promoted packs are reviewed for site/docs use
Readiness: implementation complete, pending PR review

## Summary

This implementation defines deterministic redacted evidence packs for legacy sample scan
review. Packs summarize TraceMap evidence from old or large codebases using
safe labels, counts, rule IDs, evidence tiers, coverage labels, limitations,
extractor versions, and sanitized command provenance.

Evidence packs are not raw scan outputs and are not impact analysis. They are a
public-safe handoff format for docs, site, demo, and review work after
validation and promotion gates pass.

## Implementation Scope

- Added `tracemap evidence-pack create`, `validate`, and `promote`.
- Added `legacy-evidence-pack.v1` JSON and Markdown output.
- Added redacted input readers for:
  - `legacy-validation-summary`
  - `public-demo-summary`
  - `legacy-baseline`
  - `scan-output`
- Added synthetic public-safe summary fixture under
  `samples/synthetic-legacy-evidence-pack/`.
- Added tracked promotion-root placeholder at
  `docs/evidence-packs/legacy/README.md`.
- Added six `legacy.evidence-pack.*` rule catalog entries.
- Added focused unit tests for deterministic creation, mandatory public-safe
  date handling, unsafe content diagnostics, row metadata, and promotion
  boundaries.
- TraceMap core remains deterministic. No LLM calls, embeddings, vector
  databases, prompt-based classification, or model-generated summaries are in
  scope.
- Raw scans, raw facts, SQLite indexes, analyzer logs, raw report prose, private
  local paths, raw remotes, raw SQL, config values, secrets, endpoint URLs, and
  source snippets must remain local-only.
- Evidence packs compose existing redacted summaries or raw local scan outputs
  into an explicitly classified artifact: `local-only`, `demo-safe`,
  `public-safe`, or `rejected`.
- Public-safe packs may be promoted to a tracked docs/tooling handoff location
  only after validation and private-path checks pass.
- Site page work that consumes promoted packs belongs in a future `site-*` spec.

## Review State

- Initial spec draft created.
- Kiro Opus first-pass review completed with reduced coverage because Kiro
  reported denied shell access, but it returned complete findings.
- Kiro Sonnet first-pass review completed with full coverage.
- Review fixes applied:
  - public-safe and demo-safe pack identity now requires explicit injected or
    fixture-pinned dates rather than wall-clock month defaults;
  - `.tmp/legacy-evidence-packs/` is ignored and tasks require a
    `git check-ignore` guard;
  - `git check-ignore` error or non-zero exits are refusals;
  - promotion destinations must be approved tracked roots and not ignored;
  - promotion copy/overwrite behavior is defined;
  - rule ID stubs with limitations are documented before implementation;
  - prohibited-claim detection is specified as recursive string scanning over
    pack JSON and generated Markdown text;
  - the pack safety validator is allowed to be the generated-output sentinel;
  - top-level aggregate summary rule requirements are clarified;
  - input kinds, synthetic fixture shape, command provenance redaction, secret
    patterns, low-entropy hash limits, and collision handling are clarified;
  - additional tests from both reviews were added to `tasks.md`.
- Sonnet re-review completed with reduced coverage because Kiro reported denied
  shell access, but it returned complete findings. Review fixes applied:
  - `sourceClassifications` was added to the schema example to match
    requirements;
  - `legacy.evidence-pack.input-availability.v1` was added to the rule catalog
    task list;
  - `--date` is now listed as required for public-safe and demo-safe `create`
    command validation;
  - dry-run behavior is specified for create and promote;
  - local `validation-result.json` has a minimum schema;
  - local-only date behavior, promotion allowlist ownership, and ignored
    destination rejection are testable.
- Final Sonnet re-review completed with full coverage. Review fixes applied:
  - implementation validation tasks now require verifying all six
    `legacy.evidence-pack.*` rule catalog entries before tests emit those rule
    IDs;
  - tasks require creating `docs/evidence-packs/legacy/README.md` as the tracked
    promotion-root placeholder;
  - design requires promote to rerun validation, safety sentinel, and
    `check-private-paths.sh` before copying;
  - collision behavior is fixed to deterministic suffixes for create and
    overwrite refusal for promote.
- Final review follow-up fixes applied:
  - the synthetic fixture and `docs/evidence-packs/legacy/README.md` are
    explicitly implementation PR deliverables, not spec-branch artifacts;
  - local-only `--date` behavior is clarified as excluded from byte-stability
    expectations;
  - tests now separately cover create suffix collisions, promote overwrite
    refusal, `validation-result.json` contents, and missing promotion root
    behavior.
- Reduced-coverage Sonnet re-review found final wording gaps. Review fixes
  applied:
  - `--force` is explicitly limited to destination overwrite and cannot bypass
    validation gates;
  - promotion runs inverse `git check-ignore` and rejects ignored destinations;
  - task 9 validation commands are marked dependent on the task 7 fixture;
  - pack ID suffix content is defined as a normalized input fingerprint so it is
    not self-referential;
  - expected claim-level mismatches fail validation;
  - prohibited phrase lists are versioned with `safety.validatorVersion`;
  - rejected included sections force top-level rejected classification;
  - public-safe provenance omits or fixed-placeholder represents `--label`;
  - tests cover those boundaries.
- PR review loop fixes applied:
  - Codex requested a file-targeted safety check for promoted files because
    `check-private-paths.sh` only scans tracked files;
  - Codex requested adding `--date` to the public-safe dry-run validation
    command;
  - Qodo requested preserving commit identity proof instead of allowing
    public-safe packs to omit commit identity;
  - Codex requested row-level evidence metadata on count rows, so the schema
    example now includes rule ID, evidence tier, source label, coverage label,
    and safe provenance on per-section counts.
- Tasks are intentionally unchecked because this branch delivers a spec only.

## Implementation Notes

- The evidence-pack validator is the generated-output sentinel for pack JSON and
  Markdown. It scans generated files directly before promotion, because
  `scripts/check-private-paths.sh` only protects tracked files.
- `--force` only replaces an existing promotion destination. It does not bypass
  validation, claim-level, tracked-root, ignored-destination, or unsafe-content
  gates.
- `public-safe` and `demo-safe` creation require `--date <yyyy-MM>`. `local-only`
  can omit the date and is intentionally excluded from byte-stability promises
  when it does.
- Public-safe commit identity is represented as hash proof plus
  `commitShaPresent`; raw remotes and local paths are never copied into packs.
- The implementation is intentionally aggregate-first. It packages the evidence
  TraceMap already emitted; it does not infer runtime execution, production use,
  vulnerability status, release approval, or business impact.

## Validation

Completed:

```bash
node scripts/kiro-review.mjs --self-test
node scripts/kiro-review.mjs --phase legacy-sample-evidence-pack --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-evidence-pack --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-evidence-pack --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-evidence-pack --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-evidence-pack --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git check-ignore .tmp/legacy-evidence-packs/example
./scripts/check-private-paths.sh
git diff --check
dotnet test src/dotnet/TraceMap.sln --filter LegacyEvidencePackTests
dotnet run --project src/dotnet/TraceMap.Cli -- evidence-pack create --input samples/synthetic-legacy-evidence-pack --input-kind legacy-validation-summary --label synthetic-legacy-alpha --purpose legacy-validation-proof --claim-level public-safe --date 2026-06 --out .tmp/legacy-evidence-packs/synthetic-legacy-alpha --dry-run
dotnet run --project src/dotnet/TraceMap.Cli -- evidence-pack create --input samples/synthetic-legacy-evidence-pack --input-kind legacy-validation-summary --label synthetic-legacy-alpha --purpose legacy-validation-proof --claim-level public-safe --date 2026-06 --out .tmp/legacy-evidence-packs/synthetic-legacy-alpha
dotnet run --project src/dotnet/TraceMap.Cli -- evidence-pack validate --pack .tmp/legacy-evidence-packs/synthetic-legacy-alpha/evidence-pack.json --expected-claim-level public-safe
dotnet run --project src/dotnet/TraceMap.Cli -- evidence-pack promote --pack .tmp/legacy-evidence-packs/synthetic-legacy-alpha/evidence-pack.json --markdown .tmp/legacy-evidence-packs/synthetic-legacy-alpha/evidence-pack.md --out docs/evidence-packs/legacy/synthetic-legacy-alpha-dry-run --dry-run
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
```

Notes:

- Opus first-pass review and one Sonnet re-review reported reduced coverage due
  denied Kiro shell access, but both returned complete findings.
- Full implementation validation is run in the implementation PR after code
  changes are complete. The implementation branch currently passes full build
  and test validation with 358 .NET tests.
- Pinned language-adapter smoke checks from `docs/VALIDATION.md` were deferred:
  this change adds evidence-pack reporting/CLI behavior and does not change
  scanner extraction, language adapters, public demo generation, release review,
  or shared language smoke scripts.

## Follow-Ups

- Site pages that consume promoted packs remain out of scope and should use a
  future `site-*` spec.
- Portfolio-level evidence packs across multiple public-safe sample labels are
  still deferred.
- If future reviewers want deeper drilldown, add optional local-only drilldown
  packs without weakening public-safe defaults.
