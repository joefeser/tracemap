# Legacy Sample Smoke Catalog Implementation State

Status: spec-review
Branch: codex/spec-legacy-sample-smoke-catalog
Scope: spec-only
Public claim level: hidden
Readiness: ready-for-implementation-after-reduced-coverage-review

## Summary

This spec defines a public-safe/operator-safe catalog for old-code sample smoke
validation families and pinned expectations. The catalog records neutral sample
labels, source classification, checked-out commit or fixture identity, expected
evidence families, validation command templates, redaction rules, limitations,
claim level, and relationships to nearby legacy validation artifacts.

The catalog is not raw scan output, not a baseline artifact, not an evidence
pack, not a site page, and not an impact-analysis result. It is a maintainer
inventory for coordinating validation coverage across WCF, Remoting, WebForms,
DBML/EDMX/typed DataSet, build diagnostics, huge repository stress, fallback
syntax scans, and analysis gap reporting.

## Scope Decisions

- Spec-only branch; no source code, docs outside this spec, existing specs, or
  site files are changed.
- Tracked catalog storage is specified as
  `docs/validation/legacy-sample-smoke-catalog/` for a future implementation.
- Local operator inputs and candidate drafts are specified under ignored
  `.tmp/legacy-sample-smoke-catalog/`.
- `catalog.json` is the future source of truth; `catalog.md` is generated from
  JSON.
- The catalog may reference redacted validation summaries, redacted baseline
  snapshots, and evidence packs by safe schema name, neutral ID, and claim
  level only.
- Raw scan artifacts, raw validation outputs, raw baseline manifests, raw
  evidence-pack contents, SQLite indexes, analyzer logs, raw reports, raw SQL,
  config values, snippets, remotes, local paths, private names, and secrets stay
  out of tracked catalog files.
- Public claim level remains hidden by default. Entry-level `demo-safe` or
  `public-safe` requires pinned source identity, reviewed proof, and passing
  validation in a future implementation.
- TraceMap core remains deterministic. No LLM calls, embeddings, vector
  databases, prompt-based classification, or model-generated catalog decisions
  are in scope.

## Review State

- Initial spec files drafted:
  - `requirements.md`
  - `design.md`
  - `tasks.md`
  - `review-prompts.md`
  - `implementation-state.md`
- Tasks are intentionally unchecked because this branch delivers the spec only.
- Kiro Sonnet first-pass review completed with full coverage.
- Review fixes applied:
  - demo-safe and public-safe tracked output now fails when hidden entries are
    present unless an explicit render filter has already omitted them;
  - validators reject any catalog whose top-level classification is higher than
    the least-safe included entry;
  - `redacted-sha256` commit identity is prohibited in tracked output and
    reserved for future ignored local-only drafts;
  - non-public commit proof uses category-only `shaPresent: true`;
  - command templates require placeholders for identity-bearing string option
    values and allow literals only for booleans or fixed closed vocabularies;
  - generated Markdown uses a canonical JSON hash sentinel for stale detection;
  - committed `generatedAt` values are reviewed render dates and validation
    rejects unintended date mismatches;
  - rule ID fields are explicitly scanned as ordinary strings;
  - relationship references can be deferred when sibling schemas are not yet
    implemented and cannot serve as public-safe proof in that state;
  - `--force` is only an overwrite permission and cannot bypass any safety
    gate;
  - missing tests from review were added to `tasks.md`.
- Sonnet re-review completed with reduced coverage because Kiro reported denied
  shell access, but it returned complete findings.
- Re-review fixes applied:
  - command template tests now include positive boolean and closed-vocabulary
    literal cases;
  - first tracked writes require explicit `--date YYYY-MM` with no wall-clock
    fallback;
  - canonical JSON for Markdown hash sentinels is defined as UTF-8 without BOM,
    LF endings, two-space indentation, final newline, ordinal key sorting, and
    schema-defined array ordering;
  - hidden-entry omission is only via explicit
    `--minimum-entry-claim-level <demo-safe|public-safe>` render filtering;
  - `rawRemoteAllowed` was removed from the tracked schema example;
  - the tracked schema excludes `redacted-sha256`;
  - task coverage now includes `public-sha` source classification misuse,
    display-name prohibited claims, literal date values in command templates,
    rule-catalog entries, Markdown diagnostics with line numbers, empty
    catalogs, duplicate labels, empty family/limitation arrays, and `--force`
    with stale Markdown sentinels.
- Final Sonnet re-review completed with full coverage.
- Final re-review fixes applied:
  - render is now defined as the only reviewed date-update operation;
  - validation cannot override an existing catalog date with a new date;
  - requirements now name
    `--minimum-entry-claim-level <demo-safe|public-safe>` as the explicit
    hidden-entry omission mechanism;
  - tracked schemas now exclude both `redacted-sha256` and `local-only` commit
    identity kinds;
  - tests now cover `--minimum-entry-claim-level public-safe` zero-entry
    failure and the render-with-new-date happy path.
- Clean re-review attempt completed with reduced coverage because Kiro reported
  denied shell access, but it returned complete findings.
- Clean re-review fixes applied:
  - tracked `commitIdentity.kind` enum is now exactly `public-sha`,
    `fixture-version`, and `category-only`;
  - ignored local-draft schema variants are the only place where
    `redacted-sha256` or `local-only` may appear;
  - render date behavior is idempotent when `generatedAt` already matches
    `--date`, and updates date plus Markdown only during explicit render;
  - `--minimum-entry-claim-level` now explicitly creates a new candidate output
    set and promotion never performs filtering;
  - relationship artifact kinds, command input kinds, timeout buckets,
    artifact-size buckets, extractor gap codes, and `safeArtifactId` syntax are
    defined;
  - Markdown hash sentinel format is exact;
  - render/promote dry-run behavior is defined;
  - `displayName` redaction and prohibited-claim scanning is explicit;
  - missing schema, vocabulary, dry-run, large-repo, and relationship tests were
    added to `tasks.md`.

## Validation

Planned for this spec branch:

```bash
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
./scripts/check-private-paths.sh
git diff --check
```

Completed:

```bash
node scripts/kiro-review.mjs --self-test
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-sample-smoke-catalog --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

First-pass review output:

- Coverage: Full
- Blocking findings: mixed claim-level promotion ambiguity, tracked
  `redacted-sha256` risk, and identity-bearing literal command option values.
- Result: patched in spec files; re-review pending.

Re-review output:

- Coverage: Reduced due denied Kiro tool access.
- Blocking findings: missing positive command-template literal test,
  underspecified first-write date bootstrap, undefined canonical JSON hash
  input, and unnamed hidden-entry filter mechanism.
- Result: patched in spec files; final re-review pending.

Final re-review output:

- Coverage: Full
- Blocking finding: "reviewed update" date escape hatch was undefined.
- Important non-blocking findings: requirements did not name the
  `--minimum-entry-claim-level` mechanism, `public-safe` zero-entry filtering
  was not explicitly tested, and `local-only` needed the same tracked-schema
  exclusion as `redacted-sha256`.
- Result: patched in spec files; clean re-review pending.

Clean re-review attempt output:

- Coverage: Reduced due denied Kiro tool access.
- Blocking findings: tracked commit identity enum needed structural exclusion,
  render date idempotency needed explicit behavior, render filtering needed
  separation from promotion, relationship artifact kinds needed a closed
  vocabulary, and command input-kind literals needed a closed vocabulary.
- Result: patched in spec files. No further Kiro review loop was run to avoid
  churn after addressing the actionable Medium+ findings.

No .NET implementation validation is required for this branch because it should
only add files under `.kiro/specs/legacy-sample-smoke-catalog/`.

## Follow-Ups For Implementation

- Create the future tracked docs/validation catalog root and ignored local root
  only in an implementation PR.
- Add catalog schema/tooling, generated Markdown, validators, fixtures, and
  tests before checking task boxes.
- Record any CLI fallback decision here before implementing around it.
- Keep hidden/operator-local entries out of public-safe tracked catalog output
  unless reviewed public/demo-safe proof exists.
- Run or explicitly defer relevant pinned smoke checks from
  `docs/VALIDATION.md` when implementation touches catalog tooling, scanner
  behavior, report generation, public demo, release review, or language
  adapters.
