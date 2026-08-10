# Site Access Safe Evidence Acquisition Implementation State

- Status: implementation and local validation complete; publication pending
- Branch: `codex/site-access-safe-acquisition`
- Base: `origin/dev`
- Base SHA: `1b2435f734af28cb4d6012cc63491022b5acd97b`
- Public claim level: `demo`

## Scope

Site-only article, registry, discovery metadata, focused validator/tests, and
this spec. No core scanner changes, protected artifacts, sample database,
proof-packet fabrication, generated output edits, deployment, or promotion.

## Grounding

The copy is grounded in issue #620, the file-first, form/report metadata, and
VBA source-export documentation, plus
`legacy.access.database.inventory.v1`,
`legacy.access.design-input.v1`, and
`legacy.access.coverage-gap.v1`.

## Boundary

The article does not claim runtime execution, selected branches, event firing,
query results, production behavior, complete coverage, correctness, effective
permissions, reconstruction success, release approval, or operational safety.
Protected and private inputs remain omitted.

## Validation

- `cd site && npm run build`: passed.
- `cd site && npm test`: 797 passed, 0 failed.
- `cd site && npm run validate`: passed for 101 HTML files, 3,444 internal
  references, and 100 sitemap URLs.
- Focused validator: 10 passed, including planted runtime/reconstruction claim,
  executable material, credential, private-path, and discovery-level failures.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Desktop at 1,440 × 1,000 and mobile at 390 × 844 passed for `/blog/` and
  `/blog/reverse-engineering-access-without-running-it/`; the article card
  navigation worked, all nine required sections and required links were
  present, no horizontal document overflow appeared, and no browser warnings
  or errors were observed.
