# Site Access Form-to-Field Lineage Implementation State

- Status: implementation and local validation complete; ready stacked PR #626
  open; ACK pending
- Branch: `codex/site-access-form-field-lineage`
- Stack base: `codex/site-access-safe-acquisition` / PR #625
- Base SHA: `667fa94633d9f195c8fba418d5388e876f8675fa`
- Public claim level: `demo`

## Scope

Site-only article, companion cross-link, registry, discovery metadata, focused
validator/tests, and this spec. No scanner changes, protected Access artifact,
sample database, generated output edit, deployment, or promotion.

## Grounding

The article is grounded in issue #619, checked-in Access form/report and VBA
export documentation, and the `ui-surface`, `binding`, `vba`, `event-binding`,
and `screen-data-flow` Access rule families.

## Boundary

The article describes declared and candidate static lineage. It does not claim
runtime execution, query results, selected branches, event firing, navigation,
row access, correctness, effective permissions, production behavior, complete
coverage, reconstruction, release approval, or operational safety.

## Validation

- `cd site && npm run build`: passed.
- `cd site && npm test`: 808 passed, 0 failed.
- `cd site && npm run validate`: passed for 102 HTML files, 3,471 internal
  references, and 101 sitemap URLs.
- Focused validator: 11 passed, including planted runtime, write, safety,
  reconstruction, executable-material, credential, private-path,
  cross-link, and discovery-level failures.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Desktop at 1,440 × 1,000 and mobile at 390 × 844 passed for `/blog/` and
  `/blog/access-form-to-field-lineage/`; the blog card navigation worked, all
  11 required sections and the reciprocal companion link were present, no
  horizontal document overflow appeared, and no browser warnings or errors
  were observed.
