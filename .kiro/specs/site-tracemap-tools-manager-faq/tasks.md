# Site TraceMap Tools Manager FAQ Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

- [x] Confirm spec review passed and all Medium+ findings are resolved before beginning implementation.
- [x] Choose the final route: `/manager-faq/` or `/faq/manager/`, and record the choice in `implementation-state.md`.
- [x] Verify that `/manager-brief/`, `/manager-packet/`, `/review-room/`, `/limitations/`, `/validation/`, and `/proof-paths/` resolve, or identify current equivalent public routes.
- [x] Add the concept-level manager FAQ page using existing static site layout and navigation patterns.
- [x] Include `Public claim level: concept` and the shared site principle on the page.
- [x] Add FAQ answers for what TraceMap can say from deterministic static evidence.
- [x] Add FAQ answers for what TraceMap cannot prove from static evidence alone.
- [x] Add FAQ answers covering runtime behavior, production traffic, endpoint performance, outage cause, release safety, and operational safety as non-claims.
- [x] Add FAQ answers explaining that TraceMap does not replace telemetry, logs, traces, tests, ownership, human review, or release process.
- [x] Add FAQ answers explaining rule IDs, evidence tiers, coverage labels, limitations, proof paths, and reduced or partial coverage labels.
- [x] Add FAQ answers explaining how managers should use TraceMap during review, prioritization, incident follow-up, and stakeholder communication.
- [x] Add FAQ answers explaining what should be escalated to engineering owners, telemetry, tests, logs, traces, or release review.
- [x] Add FAQ answers explaining that core scanner/reducer claims do not use AI, LLMs, embeddings, vector databases, or prompt-based classification.
- [x] Keep all answers tied to static evidence, coverage labels, rule IDs, limitations, or proof paths.
- [x] Avoid forbidden copy that implies runtime proof, production proof, release approval, operational safety, AI impact analysis, LLM analysis, or complete product coverage.
- [x] Avoid scare framing, blame language, and competitor-first positioning.
- [x] Keep the page free of raw source snippets, raw SQL, config values, secrets, local absolute paths, raw remotes, generated scan directories, raw facts, raw SQLite content, analyzer logs, and private sample names.
- [x] Link to `/manager-brief/`, `/manager-packet/`, `/review-room/`, `/limitations/`, `/validation/`, and `/proof-paths/`.
- [x] Add optional supporting links to `/docs/`, `/demo/`, `/demo/result/`, `/packets/`, or `/capabilities/` only where they support a specific FAQ answer.
- [x] Add page metadata with claim level `concept`.
- [x] Add sitemap and discovery metadata if comparable public pages are indexed there.
- [x] Keep social metadata titles at 70 characters or less and descriptions at 160 characters or less unless the existing site pattern requires a different limit.
- [x] Add focused page/content validation for required labels, required links, forbidden private/raw artifact text, and forbidden AI/LLM positioning.
- [x] Add focused validation that the final route appears in discovery metadata, using `site/src/_site/discovery.json` or the equivalent path confirmed at implementation time, with `publicClaimLevel: concept` and sitemap or page metadata where comparable manager-facing pages are indexed.
- [x] Add focused validation for runtime, production, and release overclaim wording outside sanctioned non-claim or disclaimer blocks.
- [x] Confirm this spec-only `tasks.md` contains no checked boxes before merging the spec PR.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks if the implementation changes layout or interaction.
- [x] Update `implementation-state.md` with implementation scope, validation, oddities, review-loop outcomes, and follow-up items.
