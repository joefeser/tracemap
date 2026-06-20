# Legacy Data Model Reporting Integration Implementation State

Status: `spec-reviewed`

Branch: `codex/spec-legacy-data-model-reporting-integration`

Target base: `dev`

Scope: spec-only PR for integrating legacy data model metadata into
user-facing reports and exports.

## Current Decisions

- Use the existing `legacy-data` dependency surface kind. Do not introduce a
  parallel `legacy-data-model` surface kind in this spec.
- Treat `legacy.data.model.surface.v1` as projection-only for report/export
  rows and projection gaps. Source extractors keep their source rule IDs.
- Render `AnalysisGap` facts under `legacy.data.*` rules as gaps, caveats, or
  limitations, not terminal surfaces.
- Define compatibility for both current `LegacyData*` facts and near-term
  additive model identity fields.
- Keep release-review and review-priority usage bounded to deterministic static
  inputs.
- Keep vault/RAG export deterministic. No embeddings, vector database writes,
  prompt summaries, or AI classifications are part of TraceMap core.

## Privacy And Safety Notes

- Spec examples are synthetic and schema-shaped only.
- The spec avoids raw SQL, snippets, config values, connection strings, raw
  remotes, URLs, hostnames, local absolute paths, private sample labels, and
  secrets.
- Public/demo descriptor display names are allowed only with synthetic fixture
  status or reviewed claim-level proof; otherwise labels are omitted or hashed.

## Review Commands

```text
node scripts/kiro-review.mjs --phase legacy-data-model-reporting-integration --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-data-model-reporting-integration --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-data-model-reporting-integration --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-data-model-reporting-integration --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

## Review Results

All Kiro review runs reported reduced coverage because Kiro reported denied tool
access. The exact wrapper gap was:

```text
Kiro reported denied tool access; review coverage is reduced.
```

Review artifacts:

- Opus initial review:
  `.tmp/kiro-reviews/legacy-data-model-reporting-integration/2026-06-20T202252-256Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`. Coverage: `Reduced`; `toolDenied: true`.
- Sonnet initial review:
  `.tmp/kiro-reviews/legacy-data-model-reporting-integration/2026-06-20T202414-725Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`. Coverage: `Reduced`; `toolDenied: true`.
- Sonnet re-review cycle 1:
  `.tmp/kiro-reviews/legacy-data-model-reporting-integration/2026-06-20T202701-734Z-re-review-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`. Coverage: `Reduced`; `toolDenied: true`.
- Sonnet re-review cycle 2:
  `.tmp/kiro-reviews/legacy-data-model-reporting-integration/2026-06-20T203100-006Z-re-review-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`. Coverage: `Reduced`; `toolDenied: true`.

Dispositions:

- Initial Sonnet High findings patched:
  - Route-flow credible bridge definition added.
  - Reverse no-path gap rule ownership tightened.
- Initial Sonnet Medium findings patched:
  - Commit SHA stable-ID category made canonical.
  - Persisted derived-surface discriminator defined.
  - Explorer client-side search restricted to safe rendered fields.
  - Follow-up PR annotations added to large tasks.
  - Markdown escaping made actionable in tasks.
- Initial Sonnet Low findings patched where narrow:
  - Vault example separates `displayName` from `displayNameHash`.
  - `unknown` artifact vocabulary behavior clarified.
  - `projectionRuleId` wording tightened and JSON null behavior added.
- Re-review cycle 1 Medium findings patched:
  - Gap-row stable ID fallback and canonical absence tokens added.
  - Route-flow all-`AnalysisGap` behavior added.
  - Reverse no-path rule wording strengthened.
  - Ambiguous diff identity behavior added.
  - Claim-level rendering context requirement added.
  - Markdown-sensitive safety tests expanded.
- Re-review cycle 2 Medium+ findings patched:
  - Reverse no-path rule catalog gate added to requirements and tasks.
  - Projection model now includes `displayClearance` and
    `claimLevelContextId`; tasks require hash-only default tests.
  - Vault graph example now includes source provenance placeholders.
  - Diff ambiguous-identity criterion split into a separate acceptance
    criterion.
  - Route-flow zero-symbol bridge behavior added.
  - Release-review checklist safety tests added.
  - Stable ID input explicitly includes `sourceIndexId`.
  - Unknown-vocabulary gaps now cite `legacy.data.model.surface.v1` unless a
    narrower rule is registered.

No further Kiro re-review was run because the requested cap was at most two
re-review cycles.

## Validation

```text
git diff --check
./scripts/check-private-paths.sh
```

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed with `Private path guard passed.`
- Dedicated spec lint/check: no dedicated spec lint script found. Search found
  existing specs documenting the same absence pattern, but no runnable spec lint
  command beyond `scripts/kiro-review.mjs`.
- Additional safety grep over this spec found only prohibition/safety-language
  references, not concrete private paths, raw remotes, hostnames, connection
  strings, or secrets.

## Follow-Up Implementation Boundary

Recommended first product-code PR:

1. Add shared legacy data model descriptor projection helpers.
2. Integrate the helper with combined report/path and reverse readers.
3. Update route-flow rendering only where existing legacy-data surfaces already
   appear.
4. Add focused tests for safe rendering, optional-field absence, optional-table
   absence, `AnalysisGap` exclusion, deterministic ordering, duplicate identity,
   selector ambiguity, and public/demo redaction.

Defer extractor changes, persisted derived tables, release-review scoring
expansion, vault/RAG specialization, and static explorer UI filters unless a
future implementation PR can keep the change small and independently verified.

## Oddities And Open Questions

- The data model extraction implementation may be in flight. This spec treats
  model identity fields as optional and requires rule-backed gaps when absent.
- Model-specific reverse selectors should start with stable ID or hash selectors
  if descriptor labels are hidden by claim-level policy.
- If future implementations persist derived legacy data model surfaces, readers
  need explicit double-projection tests.
