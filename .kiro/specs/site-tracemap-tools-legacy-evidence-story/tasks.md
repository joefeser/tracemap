# Tasks

Status: ready for implementation. Not started.

Public claim level: concept

Tasks 1-6 are deferred to the future implementation PR. This spec-prep PR only
creates the spec files and leaves all tasks unchecked.

- [ ] 1. Confirm promotion and proof state before writing public copy.
  - Requirements: 2, 4.
  - Check whether each referenced legacy capability is on `main`, `dev-only`,
    hidden, or still concept-only.
  - Confirm whether checked-in public-safe artifacts justify demo wording.
  - Keep hidden or unverified capabilities omitted, concept-labeled, or
    dev-only-labeled.
  - Record the per-theme promotion-check result in `implementation-state.md`,
    including negative results.

- [ ] 2. Design the bounded legacy evidence story.
  - Requirements: 1, 5.
  - Choose the smallest site surface for the first implementation: a concept
    page, section, or linked cards.
  - Pin the concrete rendered target and guard scope before writing validation:
    page file/glob for a standalone page or section anchor/extraction strategy
    for an existing page.
  - Account for existing top-navigation validation if adding a new standalone
    page; avoid adding a top-nav entry unless the broader all-pages change is
    deliberate.
  - Preserve the shared site principle: No public conclusion without evidence.
  - Use rule IDs, evidence tiers, coverage labels, limitations, and safe proof
    paths as the core vocabulary.
  - Keep substantive public content focused on the evidence model, claim ledger,
    promotion gate, and safety boundaries while all themes remain hidden.

- [ ] 3. Draft legacy theme copy with conservative labels.
  - Requirements: 2, 4.
  - Cover WCF/service references, Remoting, WebForms event flow, legacy data
    metadata, build diagnostics, and flow composition.
  - Default to the current theme claim ledger labels.
  - Deviate from the ledger only when task 1 records checked-in public-safe
    proof and updated promotion state.
  - Use `concept` only for the page/story shape, not hidden capability support.
  - Avoid shipped/support wording unless the capability has landed on `main`
    and has public-safe proof.

- [ ] 4. Add public-safe boundaries.
  - Requirements: 3.
  - State that static evidence does not make affirmative overclaim phrases from
    `requirements.md`, including runtime behavior or UI reachability.
  - Avoid all content forbidden by the canonical content-safety rules from
    `requirements.md`.
  - Treat public spec source as a published docs page or public URL, never an
    internal `.kiro/specs/...` path.
  - Label reduced coverage and analysis gaps clearly.

- [ ] 5. Implement site discovery only after claim review.
  - Requirements: 4, 5.
  - Add route/page or section using existing site patterns.
  - Add any required discovery links and sitemap metadata.
  - Do not edit scanner, reducer, or core extractor code.

- [ ] 6. Validate the future implementation.
  - Requirements: 3, 5.
  - Run `npm test` from `site/`.
  - Run `npm run validate` from `site/` for structural site validation.
  - Add an automated rendered-content safety check for the canonical
    content-safety rules from `requirements.md`, and wire it into `npm test` or
    `npm run validate` so CI fails on a violation.
  - Scope the content-safety check to the rendered legacy story page or
    containing page only, excluding `.kiro/**`, spec source, fixture definition
    files, and other non-rendered source files.
  - Ensure the check runs after a fresh site build, fails when zero rendered
    HTML files are found, and asserts that the rendered legacy story page or
    section is included in the scanned set.
  - Prefer wiring through `npm run validate` after `buildSite()` or through an
    isolated temp-output test that builds before scanning; do not scan stale or
    shared `site/dist` from `npm test`.
  - Include content-safety fixtures proving a hard leak token fails, a
    `.kiro/specs/...` or local path leak fails, connection-string,
    credential-assignment, private/local URL, and raw-remote examples fail, an
    affirmative overclaim fails, a negation false-positive fails if the
    proximity heuristic is used, a sanctioned negated disclaimer passes,
    mixed-case or normalized hard leak tokens fail, an empty-output scan fails,
    hidden theme enumeration without an adjacent hidden/omission label fails,
    legitimate artifact-name documentation passes, clean concept copy passes,
    and the boundary terms defined in `requirements.md` do not fail by
    themselves or mask adjacent forbidden rendered content.
  - Run `git diff --check`.
  - Run desktop and mobile browser sanity checks for layout or interaction
    changes.
  - Record validation and follow-up items in `implementation-state.md`.
