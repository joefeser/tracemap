# Site TraceMap Tools Roadmap Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

`codex/site-roadmap`

## Scope

This phase adds a site-facing roadmap page at `/roadmap/`. The page frames site work as a public claim ledger, not a delivery promise board.

## Claim Boundaries

- Safe to say: public site work is gated by current `main` evidence, demo evidence, concept framing, and hidden evidence that is not suitable for public copy yet.
- Safe to say: future public upgrades need proof paths such as checked-in samples, generated public-safe summaries, rule IDs, evidence tiers, coverage labels, and limitations.
- Not safe to say: TraceMap proves runtime behavior, production traffic, deployment state, endpoint performance, release safety, or AI impact analysis.
- Not safe to publish: raw local validation details, raw scan artifacts from private repositories, source snippets, SQL/config values, secrets, local absolute paths, or raw repository remotes.

## Validation

- `npm test`
- `npm run validate`
- Desktop and mobile browser sanity checks against local site preview

## Follow-Up

Upgrade roadmap rows only when the relevant proof lands on `main` or exists as a reproducible public demo artifact.

## Swift V0 Site Backlog

Swift v0 was promoted to `main` in PR #425 at merge commit
`e8813daaf763e277e7c5d88a2c0b2ad0b570f25a`. The following queued site specs
capture the next public Swift story slices:

- `site-tracemap-tools-swift-v0-evidence-lane` - shipped overview for the Swift
  v0 static evidence lane.
- `site-tracemap-tools-swift-static-inventory-story` - shipped static inventory
  story for packages/projects, source files, module-ish metadata, and reduced
  coverage.
- `site-tracemap-tools-swift-symbol-call-evidence-story` - shipped
  syntax-backed declarations, call candidates, construction candidates, and
  relationship evidence story.
- `site-tracemap-tools-swift-surface-discovery-story` - shipped/demo HTTP/API,
  UI, package, and dependency surface story.
- `site-tracemap-tools-swift-storage-data-surfaces-story` - shipped/demo
  CoreData, UserDefaults, Keychain, SQLite, and Realm surface story.
- `site-tracemap-tools-swift-evidence-safety-story` - shipped evidence safety,
  rule ID, tier, coverage-label, and non-claim story.

Keep these bounded to static evidence. Do not say Swift v0 proves runtime
behavior, app navigation, production usage, build success, deployment state,
release safety, stored values, or AI impact analysis.
