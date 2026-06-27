# Event Message Flow Composition Implementation State

Status: spec-ready-for-implementation

## Branch And Base

- Spec branch: `codex/event-message-flow-composition-spec`
- Target branch: `dev`
- Base checked from `origin/dev`: `6bec000244340311cc385e4ebdeee4655a7251d4`
- Public claim level: hidden

## Scope

This is a spec-only session. Product code is intentionally untouched.

The spec defines a follow-up for composing existing event/message evidence into
downstream static review context. It does not claim broker execution,
production traffic, runtime publish/subscribe delivery, delivery guarantees,
payload compatibility, impact proof, AI/LLM analysis, or complete coverage.

Recommended PR 1 implementation slice:

1. Add or reuse shared event/message flow context and gap vocabulary with rule
   catalog entries before any new emitted rule IDs or gap strings.
2. Wire exactly one deterministic report/query consumer path. The spec
   recommends hidden `messageReviewContext` in `tracemap report` because the
   combined report already consumes message surfaces, candidate edges, source
   coverage, rule IDs, evidence tiers, file spans, commit SHAs, and extractor
   versions.
3. Defer reducer, release-review, route-flow async boundary rendering, deeper
   path/reverse graph composition, Tier1 message extraction, cross-language
   adapter support, and public-safe promotion.

## Current Context Notes

- `.kiro/specs/event-message-surfaces/implementation-state.md` says
  `Status: implemented-v1-with-follow-ups`.
- Existing message surface follow-ups explicitly include route-flow async
  message-hop rendering, reducer context over message surfaces, Roslyn semantic
  Tier1 extraction, and TypeScript/Python/JVM adapter slices.
- Live `rules/rule-catalog.yml` contains `message.surface.*` entries including
  publisher, consumer, binding, identity, combine, candidate-edge, paths,
  reducer, and gap rules with limitations.
- Live .NET constants include the same `message.surface.*` rule IDs and the
  `MessagePublisherSurface`, `MessageConsumerSurface`, and
  `MessageBindingDeclared` fact types.
- Live combined reporting code already groups message publisher and consumer
  rows by safe static destination identity and emits bounded
  `message-publish-consume` candidate edges.
- Live path and reverse code already accept message surface selectors and
  message direction filters.

## Decisions

- Keep PR 1 composition report/query-only unless implementation discovers that
  an even smaller existing consumer path is safer.
- Keep all classifications review-tier, unknown, partial, unavailable, or
  no-compatible-evidence. Do not introduce impact or runtime delivery language.
- Require closed gap strings and rule catalog limitations before emitted code.
- Require safe output by default: no raw payload values, secrets, config,
  connection strings, raw remotes, local paths, source snippets, raw broker
  URLs, raw hostnames, or unsafe raw destinations.

## Validation

Completed:

- `git fetch origin dev && git pull --ff-only`: passed; branch fast-forwarded
  to `6bec000244340311cc385e4ebdeee4655a7251d4`.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- diff-scope check command:
  `git diff --name-only origin/dev...HEAD && git status --short`
  - Before commit, only `.kiro/specs/event-message-flow-composition/` is
    expected to appear as local status.

Deferred as spec-only:

- `dotnet test`: not run because no product code or test code changed.

## Review Artifacts

- Opus spec review:
  - Command:
    `node scripts/kiro-review.mjs --phase event-message-flow-composition --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  - Result: completed with reduced coverage because Kiro reported denied tool
    access.
  - Clean artifact:
    `.tmp/kiro-reviews/event-message-flow-composition/2026-06-27T164938-370Z-spec-claude-opus-4.8.clean.md`
  - Meta artifact:
    `.tmp/kiro-reviews/event-message-flow-composition/2026-06-27T164938-370Z-spec-claude-opus-4.8.meta.json`
- Sonnet spec review:
  - Command:
    `node scripts/kiro-review.mjs --phase event-message-flow-composition --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Result: completed with full coverage.
  - Clean artifact:
    `.tmp/kiro-reviews/event-message-flow-composition/2026-06-27T165148-177Z-spec-claude-sonnet-4.6.clean.md`
  - Meta artifact:
    `.tmp/kiro-reviews/event-message-flow-composition/2026-06-27T165148-177Z-spec-claude-sonnet-4.6.meta.json`
- Bounded Sonnet re-review after patches:
  - Command:
    `node scripts/kiro-review.mjs --phase event-message-flow-composition --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Result: completed with reduced coverage because Kiro reported denied tool
    access; no blockers found.
  - Clean artifact:
    `.tmp/kiro-reviews/event-message-flow-composition/2026-06-27T165434-662Z-re-review-claude-sonnet-4.6.clean.md`
  - Meta artifact:
    `.tmp/kiro-reviews/event-message-flow-composition/2026-06-27T165434-662Z-re-review-claude-sonnet-4.6.meta.json`

## Review Patches

- Clarified composition gaps under `message.flow.gap.v1` versus extraction gaps
  under `message.surface.gap.v1`.
- Added closed status, coverage-label, and classification vocabularies.
- Tightened PR 1 default behavior for `tracemap report` hidden
  `messageReviewContext`.
- Added default caps, always-visible Markdown status behavior when the section
  is requested/enabled, and stronger JSON field-set stability requirements.
- Added missing tests for forbidden wording, candidate-edge not-call-edge
  labeling, gap-rule distinctness, no-compatible evidence versus reduced
  coverage, direction-filter deferral, duplicate/no-static-match gaps, hidden
  claim level, and Tier4 composition-rule ceiling.
- Added Task 1 catalog-reuse verification before adding new rule IDs.

## Readiness

Ready for implementation as a hidden/static review-context spec. Implementation
must still perform the Task 1 live-context check and decide whether to add new
`message.flow.*` rules or reuse an existing catalog entry before product code
emits any new rule ID, context kind, gap kind, or classification string.
