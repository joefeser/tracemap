# Tasks

Status: implemented
Readiness: ready-for-review
Public claim level: concept

- [x] 1. Confirm promotion and proof state before writing public copy.
  - Requirements: 2, 4.
  - Checked whether each referenced legacy capability can be described from
    public-safe proof on `main`.
  - No referenced theme had enough public-safe proof for demo wording in this
    site phase, so every theme remains `hidden pending validation`.
  - Recorded the per-theme negative promotion-check result in
    `implementation-state.md`.

- [x] 2. Design the bounded legacy evidence story.
  - Requirements: 1, 5.
  - Chose a standalone concept route at `/legacy-evidence/`.
  - Pinned the rendered guard target to
    `site/dist/legacy-evidence/index.html`.
  - Kept the page out of top navigation and added bounded discovery links from
    existing roadmap/docs/proof/legacy surfaces.
  - Preserved the shared site principle: No public conclusion without evidence.

- [x] 3. Draft legacy theme copy with conservative labels.
  - Requirements: 2, 4.
  - Covered WCF/service references, WCF metadata normalization, Remoting,
    WebForms event flow, legacy data metadata, build diagnostics, and flow
    composition.
  - Each rendered theme row includes the adjacent label
    `hidden pending validation`.
  - Kept `concept` scoped to the page/story shape, not hidden capability
    support.

- [x] 4. Add public-safe boundaries.
  - Requirements: 3.
  - Added the canonical non-claim sentence for runtime, UI, production,
    deployment, endpoint, exploitability, database, package, incident, release
    approval, and release safety topics.
  - Kept public examples to labels, counts, hashes, rule IDs, evidence tiers,
    coverage labels, supporting IDs, limitations, commit provenance, extractor
    versions, and artifact names.
  - Avoided bare/internal spec links from the new route.

- [x] 5. Implement site discovery only after claim review.
  - Requirements: 4, 5.
  - Added the `/legacy-evidence/` static page using existing page classes.
  - Added sitemap and discovery metadata.
  - Added backlinks from `/legacy-validation/`, `/roadmap/`, `/docs/`, and
    `/proof-paths/`.
  - Did not edit scanner, reducer, or core extractor code.

- [x] 6. Validate the future implementation.
  - Requirements: 3, 5.
  - Added `site/scripts/legacy-story-safety.mjs` and fixture tests.
  - Wired the guard into `npm run validate` after `buildSite()` so it scans
    freshly built rendered output, not stale `site/dist`.
  - Scoped the guard to `legacy-evidence/index.html`; spec and fixture source
    files are excluded because only the rendered target is scanned.
  - Included fail/pass fixtures for hard leaks, local and internal paths,
    connection strings, credential assignments, private/local URLs, raw remotes,
    affirmative overclaims, sanctioned negated disclaimers, empty output,
    missing target output, hidden-theme enumeration without adjacent labels,
    legitimate artifact names, clean concept copy, boundary legacy terms, and
    boundary terms adjacent to forbidden content.
  - Ran `git diff --check`.
  - Ran `npm test` from `site/`.
  - Ran `npm run validate` from `site/`.
  - Ran `npm run build` from `site/`.
  - Ran `./scripts/check-private-paths.sh` from repo root.
  - Ran desktop and mobile browser sanity checks on `/legacy-evidence/`.
