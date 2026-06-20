# Site TraceMap Tools Evidence Glossary Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-evidence-glossary`
Base: `origin/dev`
Target PR base: `dev`

## Scope

This phase creates a Kiro spec packet only for a future public-safe evidence
glossary/reference page, likely `/glossary/` or
`/docs/evidence-glossary/`.

This phase does not implement site code. Future implementation should explain
TraceMap vocabulary for engineers, reviewers, managers, architects, and agents
without implying that every term is fully shipped everywhere.

## Claim Boundary

The future page is concept-level vocabulary guidance. It must not claim runtime
behavior, production traffic, endpoint performance, outage cause, release
safety, operational safety, AI or LLM impact analysis, or complete product
coverage. It must not publish raw facts, raw SQLite indexes, analyzer logs, raw
source snippets, raw SQL, config values, secrets, local absolute paths, raw
remotes, generated scan directories, private sample names, or hidden validation
details.

## Route Decision Status

Not started. Future implementation must evaluate:

- `/glossary/`
- `/docs/evidence-glossary/`
- folding into an existing public-safe documentation, proof-path, or
  limitations route

The selected placement and rejected alternatives must be recorded here before
implementation is marked complete.

Future implementation must also record canonical-source decisions for
overlapping terms already described by `/evidence/`, `/proof-paths/`, and
`/proof-source-catalog/`.

Future implementation must record the minimum required link validation result,
the chosen `hintCategory`, whether `concept` is accepted by discovery and
validation tooling, and explicit numeric word-count bounds for either the
standalone route or folded section.

Accepted `hintCategory` values observed in `discovery.json` at spec-review
time: `start`, `evidence`, `proof`, `validation`, `output`, `legacy`,
`workflow`, `use-case`, `adoption`, `demo`, `audience`, `comparison`, and
`source`. Future implementation must choose from this list or document a new
value with rationale.

## Review Status

- `claude-opus-4.8` initial Kiro review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-glossary --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T202620-702Z-spec-claude-opus-4.8.clean.md`.
- `claude-sonnet-4.6` initial Kiro review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-glossary --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T202620-783Z-spec-claude-sonnet-4.6.clean.md`.
- Initial Medium findings patched: live route grounding, standalone/folded word
  count bounds, affirmative AI/LLM validation, discovery metadata shape,
  canonical vocabulary reconciliation, stable non-claims anchors, hint category
  selection, and validator anchors for word-count tests.
- `claude-opus-4.8` re-review command completed with full coverage. Clean
  artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T203052-334Z-spec-claude-opus-4.8.clean.md`.
- `claude-sonnet-4.6` re-review command exited 1 with reduced coverage because
  Kiro reported denied tool access. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T203052-284Z-spec-claude-sonnet-4.6.clean.md`.
- Re-review Medium findings patched: named word-count validator assertion,
  existing `hintCategory` vocabulary source, stable `#non-claims` anchor,
  specific affirmative AI primitive phrases, minimum required link set,
  private/raw-material sanctioned-section guard, and explicit folded-section
  numeric bounds.
- Final re-review command with `claude-opus-4.8` exited 1 with reduced coverage.
  Kiro reported denied tool access and then hit tool-approval/rate-limit
  handling while trying to inspect discovery metadata. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T203546-115Z-spec-claude-opus-4.8.clean.md`.
- Final re-review command with `claude-sonnet-4.6` exited 1 with reduced
  coverage because Kiro reported denied tool access. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T203546-180Z-spec-claude-sonnet-4.6.clean.md`.
- Final Medium findings patched after the last Sonnet review: folded-section
  word-count floor/ceiling and required literal private/raw token list.
  Last Sonnet review stated re-review was not required for those changes unless
  the patches introduced new ambiguity.
- Current Medium or higher findings: none known after patches. Review coverage
  remains partially reduced where noted above because Kiro denied tool access in
  some review runs.

## Validation

Spec-only local validation passed on 2026-06-20:

- `git diff --check`
- `./scripts/check-private-paths.sh` from the repo root: private path guard
  passed.

Site implementation validation is deferred because this phase creates the spec
packet only and does not change site source:

Future implementation validation:

- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- desktop and mobile browser sanity checks if route, layout, or interaction
  changes are made

## Follow-Ups

- None yet.
