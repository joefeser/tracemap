# Site TraceMap Tools Public Visual Proof Assets Requirements

Status: implemented
Readiness: implemented

Public claim level: demo

## Objective

Create the next public site phase after `/demo/proof-upgrades/`: a static
visual proof-assets surface that makes the public demo evidence easier to scan
without publishing raw local artifacts or increasing claim risk.

The route shall be `/demo/proof-assets/`. The page shall use public-safe
HTML/CSS representations of generated report outputs from the public demo,
rather than raw screenshots, raw generated scan directories, `facts.ndjson`,
SQLite indexes, analyzer logs, raw source snippets, raw SQL, config values, or
private sample identities.

This is site-only work under `site/` plus this spec directory unless validation
reveals a tiny adjacent metadata update is required.

## Why This Phase Exists

`/demo/proof-upgrades/` already records demo-level evidence rows for combined
reports, paths/reverse lookup, portfolio, diff, impact, and release-review. That
ledger is accurate but dense. A reviewer still has to translate report names,
counts, coverage labels, and limits into a mental picture.

This phase adds visual orientation: screenshot-style cards that show what a
safe report summary, path card, diff strip, impact row, and review checklist can
look like. The visuals are not new evidence. They are a human-readable
orientation layer over the existing public demo summaries and generated
reports.

## Claim Boundaries

Safe public claims:

- The page is demo-level because it is based on checked-in public samples and
  the already published proof-upgrades evidence ledger.
- The visual cards are illustrative, public-safe representations of generated
  public demo outputs.
- The source of truth remains generated reports, `demo-summary.*`, rule IDs,
  evidence tiers, coverage labels, counts, limitations, `facts.ndjson`, and
  `index.sqlite`.
- Visuals help humans scan the evidence shape; they do not replace rule-backed
  artifacts.

Out-of-bounds public claims:

- Runtime behavior, production traffic, deployment state, endpoint performance,
  production dependency understanding, release safety, release approval, or AI
  impact analysis.
- Full impact proof, runtime reachability, production ownership, package
  compatibility, vulnerability detection, or CI policy enforcement.
- Claims that screenshot-style assets are the evidence source of truth.

Do not publish:

- Raw source snippets, raw SQL, config values, secrets, local absolute paths,
  raw repository remotes, private sample identities, `facts.ndjson`,
  `index.sqlite`, combined SQLite files, generated scan directories, analyzer
  logs, or raw local report archives.

## Requirements

### Requirement 1: Publish a demo proof-assets page

The site shall publish a page at `/demo/proof-assets/` that presents public-safe
visual examples of the public demo proof outputs.

Acceptance criteria:

- The page says `Public claim level: demo`.
- The page states that visuals are orientation only and that generated reports,
  rule IDs, evidence tiers, coverage labels, limitations, facts, and indexes
  remain the source of truth.
- The page uses existing static site layout and styling patterns.
- The page introduces no runtime service, tracking, embedded private artifacts,
  generated scan directories, or external image dependencies.
- The page links to `/demo/proof-upgrades/`, `/demo/result/`, `/packets/`,
  `/capabilities/`, `/limitations/`, and `/roadmap/`.

### Requirement 2: Show public-safe visual proof examples

The page shall include multiple visual blocks that help humans scan demo proof
output without exposing raw artifacts.

Acceptance criteria:

- Include a report summary visual with safe labels such as status, rule ID,
  evidence tier, coverage, counts, and limitation.
- Include a path/reverse visual that shows bounded static path orientation and
  visible gaps.
- Include a diff/impact visual that shows before/after and impact rows with
  status framing and limitations.
- Include a release-review visual that shows checklist-style static findings
  without claiming release approval.
- Each visual names its rule/status framing and at least one limitation.
- Each visual avoids raw code, SQL, config values, local paths, repo remotes,
  private identities, raw facts, raw SQLite contents, and analyzer logs.

### Requirement 3: Keep evidence and limitations visible

The page shall make public-safe evidence framing visible beside the visual
examples.

Acceptance criteria:

- The page explains that visuals summarize demo evidence from checked-in public
  samples and public-safe generated summaries.
- The page states that `PartialAnalysis` and gaps remain part of the evidence.
- The page states that source artifacts are generated locally and raw internals
  stay local.
- Public conclusions are tied to a rule/status frame and explicit limitations.
- Copy stays bounded to deterministic static evidence and does not imply AI
  classification or prompt-based impact analysis.

### Requirement 4: Make the route discoverable

The proof-assets page shall be reachable from relevant existing site surfaces.

Acceptance criteria:

- `/demo/` links to `/demo/proof-assets/`.
- `/demo/proof-upgrades/` links to `/demo/proof-assets/`.
- `/demo/result/` links to `/demo/proof-assets/`.
- `/packets/` links to `/demo/proof-assets/` where visual packet orientation
  helps.
- `/roadmap/` or `/capabilities/` links to `/demo/proof-assets/` if the copy can
  stay bounded and non-duplicative.
- `/demo/proof-assets/` is included in `site/src/_site/pages.json`.

### Requirement 5: Validate as a site layout change

The implementation shall be validated with the existing static site workflow
and a browser sanity check.

Acceptance criteria:

- `git diff --check` passes.
- `npm test` from `site/` passes.
- `npm run validate` from `site/` passes.
- Desktop and mobile browser sanity checks confirm the page has no obvious
  horizontal overflow or incoherent overlap.
- Any intentionally deferred validation is recorded in
  `implementation-state.md` with the reason.

## Artifact Safety Rules

Safe to represent in page copy or CSS/HTML visual blocks:

- Public demo section names.
- Status labels such as `available`, `demo`, `PartialAnalysis`, and
  `NeedsReview`.
- Rule IDs and evidence tiers.
- Public-safe counts already surfaced in `/demo/proof-upgrades/`.
- Relative report-family names such as `reports/paths/**`.
- Limitation labels and non-claim language.

Unsafe to publish:

- Raw generated fact streams, SQLite rows, combined index contents, analyzer
  logs, local output roots, raw snippets, raw SQL, raw config values, secrets,
  private sample names, raw repository remotes, or copied local report archives.

## Validation Plan

- Run the repo-supported Kiro spec review if available.
- Commit the spec separately after addressing Medium+ spec findings.
- Implement the static route and cross-links.
- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run desktop and mobile browser sanity checks for `/demo/proof-assets/`.
