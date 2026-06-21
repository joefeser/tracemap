# Site TraceMap Tools Reviewer Quickstart Implementation State

Status: implemented
Readiness: implemented-pending-pr-loop
Public claim level: concept
Last updated: 2026-06-21
Branch: codex/impl-site-reviewer-quickstart
Worktree: isolated implementation worktree; local absolute path omitted from
tracked spec
Base: origin/dev
PR target: dev

## Summary

Implemented the public reviewer quickstart as a standalone concept-level route
at `/reviewer-quickstart/`. The page helps code reviewers inspect a TraceMap
evidence packet or public-safe proof trail in about five minutes before
repeating, upgrading, or routing a claim.

The implementation adds site source only under `site/src/` plus route-specific
validators and tests under `site/scripts/`. It does not change scanner,
reducer, generated artifact, runtime, AI/LLM, embedding, vector database,
prompt-classification, autonomous approval, or release-approval behavior.

## Scope Decisions

- Final route: `/reviewer-quickstart/`.
- Public claim level: `concept`.
- Rendered page visibly includes `Public claim level: concept` and `No public
  conclusion without evidence`.
- The route uses existing static-site page layout patterns: page hero, hero
  actions, split sections, workflow grid, evidence table, boundary sections,
  owner output list, and link grid.
- The page was not added to primary navigation. Discovery uses sitemap
  metadata, discovery metadata, and contextual inbound links from
  `/review-room/` and `/packets/assembly/`.
- All required adjacent routes existed at implementation time. The live
  manager-script route is `/demo/manager-script/`, so no substitution was
  needed.

## Placement Decision

Selected `/reviewer-quickstart/` remains valid.

Rejected alternatives retained:

- `/review-room/quickstart/`: too tied to a meeting-room session; this guide
  serves any reviewer inspecting a packet.
- Section on `/review-room/`: would blur first-visit orientation with the
  deeper review-room agenda.
- Section on `/packets/assembly/`: packet assembly prepares handoff material;
  this quickstart inspects a packet or proof trail.

## Implemented Surface

- Added `site/src/reviewer-quickstart/index.html`.
- Added sitemap metadata in `site/src/_site/pages.json`.
- Added discovery metadata in `site/src/_site/discovery.json` with
  `publicClaimLevel: concept`, `hintCategory: use-case`, `sourceType:
  site-page`, and `preferredProofPath: /proof-paths/`.
- Added contextual inbound links from `/review-room/` and
  `/packets/assembly/`.
- Added `site/scripts/reviewer-quickstart.mjs` and
  `site/scripts/reviewer-quickstart.test.mjs`.
- Registered the validator in `site/scripts/validate.mjs`.
- Updated aggregate validation fixture coverage in
  `site/scripts/validate.test.mjs`.

## Required Copy Coverage

The route includes:

- Sections: `Start Here`, `Five-Minute Review`, `Evidence Fields`, `Stop
  Conditions`, `Safe Review Language`, `Escalation Owners`, and `Non-Claims`.
- Quickstart steps: `identify the claim`, `find the proof path`, `check public
  claim level`, `read rule ID/family`, `inspect evidence tier and coverage
  label`, `check commit/extractor context`, `read limitations/non-claims`,
  `name next owner`, and `stop on missing evidence`.
- Evidence fields: claim, proof path, public claim level, rule ID or rule
  family, evidence tier, coverage label, commit SHA or source revision
  context, extractor version or extractor family, file path and line span when
  public-safe, limitation, non-claim, validation evidence, unresolved gap, and
  next owner.
- Stop conditions for missing proof path, rule, tier, coverage, limitation,
  claim level, commit/extractor context without limitation, validation
  evidence, next owner, private-only public proof, raw artifact leakage, and
  unsupported runtime/release/safety/production/AI/LLM/autonomous/complete
  wording.
- Safe review language and owner categories without blame wording.
- Explicit non-claims for runtime behavior, production traffic, endpoint
  performance, outage cause, release approval, release safety, operational
  safety, complete coverage, AI/LLM analysis, embeddings, vector database
  analysis, prompt classification, autonomous approval, and replacement of
  tests, code review, source review, runtime observability, or human judgment.

## Validation

- `git diff --check`: passed on 2026-06-21.
- `./scripts/check-private-paths.sh`: passed on 2026-06-21.
- `npm test` from `site/`: passed on 2026-06-21.
- `npm run validate` from `site/`: passed on 2026-06-21. The validator built
  the static site and reported 54 HTML files, 1802 internal references, 53
  sitemap URLs, 1 legacy story safety target, and 13 legacy modernization
  evidence-map rows.
- `npm run build` from `site/`: passed on 2026-06-21.
- Desktop browser sanity: passed on 2026-06-21 for
  `/reviewer-quickstart/` served from the local static server. The rendered
  page title was `Reviewer Quickstart | TraceMap`.
- Mobile browser sanity: passed on 2026-06-21 at 390px width. Browser check
  reported body width equal to viewport width, no horizontal overflow, and 10
  related-surface links.

## Validator Coverage

The route-specific validator checks:

- Rendered route existence, sitemap entry, generated routes-index entry, and
  route metadata fields.
- Required phrases, sections, quickstart steps, evidence field rows, stop
  conditions, safe-language terms, owner categories, required adjacent links,
  and inbound links from adjacent surfaces.
- Page metadata: title, description, canonical URL, Open Graph type/title/
  description/URL.
- Rendered word count between 500 and 1400 words.
- Forbidden public claims and private/raw material in rendered text, decoded
  HTML, raw HTML attributes, metadata, discovery entries, and tests.
- Negative fixtures for missing step copy, missing route metadata, metadata
  regressions, missing adjacent link, `data-href` link regressions, positive
  runtime proof claims, split forbidden claims, raw material outside boundary
  sections, encoded hard-private text, word-count failures, and missing inbound
  links.

## Oddities

- Browser screenshots were captured under ignored `site/output/playwright/`.
  They are local sanity artifacts and are intentionally not tracked.
- Port `4173` was already in use during browser sanity. The local static server
  was run on port `4174` instead.
- The page intentionally repeats nearby vocabulary from review-room, packet
  assembly, proof-path tour, claim checklist, questions, demo runbook, and
  manager script pages, but keeps a distinct first-five-minutes reviewer
  inspection purpose.

## PR Loop

- Pending. Run after the ready PR is created:
  `agent-control pr-loop --repo joefeser/tracemap --pr <PR_NUMBER> --base dev --require-codex-review --quiet --json`

## Residual Risks

- None known before PR review loop.
- If PR-loop returns a clean `dev` stale-Codex docs/spec/test follow-up state,
  report exact head and residual stale-review risk as an owner-ready handoff
  per repo policy rather than retagging reviewers by hand.

## Follow-Ups

- Create ready PR to `dev`.
- Wait 3 minutes after PR creation, then run the required repo-local
  `agent-control pr-loop` command.
- Patch only still-actionable findings returned by the PR-loop JSON.
