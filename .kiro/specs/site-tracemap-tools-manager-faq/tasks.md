# Site TraceMap Tools Manager FAQ Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

- [ ] Confirm spec review passed and all Medium+ findings are resolved before beginning implementation.
- [ ] Choose the final route: `/manager-faq/` or `/faq/manager/`, and record the choice in `implementation-state.md`.
- [ ] Verify that `/manager-brief/`, `/manager-packet/`, `/review-room/`, `/limitations/`, `/validation/`, and `/proof-paths/` resolve, or identify current equivalent public routes.
- [ ] Add the concept-level manager FAQ page using existing static site layout and navigation patterns.
- [ ] Include `Public claim level: concept` and the shared site principle on the page.
- [ ] Add FAQ answers for what TraceMap can say from deterministic static evidence.
- [ ] Add FAQ answers for what TraceMap cannot prove from static evidence alone.
- [ ] Add FAQ answers covering runtime behavior, production traffic, endpoint performance, outage cause, release safety, and operational safety as non-claims.
- [ ] Add FAQ answers explaining that TraceMap does not replace telemetry, logs, traces, tests, ownership, human review, or release process.
- [ ] Add FAQ answers explaining rule IDs, evidence tiers, coverage labels, limitations, proof paths, and reduced or partial coverage labels.
- [ ] Add FAQ answers explaining how managers should use TraceMap during review, prioritization, incident follow-up, and stakeholder communication.
- [ ] Add FAQ answers explaining what should be escalated to engineering owners, telemetry, tests, logs, traces, or release review.
- [ ] Add FAQ answers explaining that core scanner/reducer claims do not use AI, LLMs, embeddings, vector databases, or prompt-based classification.
- [ ] Keep all answers tied to static evidence, coverage labels, rule IDs, limitations, or proof paths.
- [ ] Avoid forbidden copy that implies runtime proof, production proof, release approval, operational safety, AI impact analysis, LLM analysis, or complete product coverage.
- [ ] Avoid scare framing, blame language, and competitor-first positioning.
- [ ] Keep the page free of raw source snippets, raw SQL, config values, secrets, local absolute paths, raw remotes, generated scan directories, raw facts, raw SQLite content, analyzer logs, and private sample names.
- [ ] Link to `/manager-brief/`, `/manager-packet/`, `/review-room/`, `/limitations/`, `/validation/`, and `/proof-paths/`.
- [ ] Add optional supporting links to `/docs/`, `/demo/`, `/demo/result/`, `/packets/`, or `/capabilities/` only where they support a specific FAQ answer.
- [ ] Add page metadata with claim level `concept`.
- [ ] Add sitemap and discovery metadata if comparable public pages are indexed there.
- [ ] Keep social metadata titles at 70 characters or less and descriptions at 160 characters or less unless the existing site pattern requires a different limit.
- [ ] Add focused page/content validation for required labels, required links, forbidden private/raw artifact text, and forbidden AI/LLM positioning.
- [ ] Add focused validation that the final route appears in discovery metadata, using `site/src/_site/discovery.json` or the equivalent path confirmed at implementation time, with `publicClaimLevel: concept` and sitemap or page metadata where comparable manager-facing pages are indexed.
- [ ] Add focused validation for runtime, production, and release overclaim wording outside sanctioned non-claim or disclaimer blocks.
- [ ] Confirm this spec-only `tasks.md` contains no checked boxes before merging the spec PR.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run `npm test` from `site/`.
- [ ] Run `npm run validate` from `site/`.
- [ ] Run `npm run build` from `site/`.
- [ ] Run desktop and mobile browser sanity checks if the implementation changes layout or interaction.
- [ ] Update `implementation-state.md` with implementation scope, validation, oddities, review-loop outcomes, and follow-up items.
