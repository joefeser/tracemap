# Site TraceMap Tools Manager Problem Brief Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

- [x] Confirm spec review passed and all Medium+ findings are resolved before beginning implementation.
- [x] Verify that `/proof-paths/`, `/validation/`, `/limitations/`, `/demo/`, and `/docs/` resolve, or identify current equivalent public routes.
- [x] Add a concept-level manager/problem brief route or article using existing site layout patterns.
- [x] Include `Public claim level: concept` and the shared site principle on the page.
- [x] Explain the recurring manager/lead problem: coordination friction, manual dependency questions, and review pressure across teams.
- [x] Explain how deterministic evidence packets reduce manual dependency-indexing and review burden.
- [x] Explain how teams can inspect change risk without treating static evidence as certainty.
- [x] Keep the page manager-readable, roughly 500 to 1000 words, and do not exceed 1500 words without a spec amendment.
- [x] Keep the origin-story framing professional and avoid blaming consultants, vendors, coworkers, teams, or specific organizations.
- [x] Avoid disparaging named tools, products, vendor categories, or existing CI/CD practices.
- [x] Follow the existing site accessibility baseline for heading hierarchy, color contrast, and alt text for non-text content.
- [x] Keep alt text and image metadata inside the same public claim boundaries as visible page copy.
- [x] Add visible claim boundaries for runtime behavior, production traffic, endpoint performance, outage cause, release safety, operational safety, AI impact analysis, LLM analysis, and complete product coverage.
- [x] Use public-safe generated summaries and demo evidence instead of raw facts, SQLite databases, analyzer logs, snippets, raw SQL, config values, secrets, local paths, raw remotes, generated scan directories, or private sample identities.
- [x] Include sanitized evidence-packet examples with rule IDs, evidence tiers, coverage labels, limitations, counts, hashes, or relative public proof paths where applicable.
- [x] Link to `/proof-paths/`, `/validation/`, `/limitations/`, `/demo/`, and relevant `/docs/` surfaces.
- [x] Confirm linked public routes still exist, or use current equivalent public routes before publishing.
- [x] Add page metadata with claim level `concept`.
- [x] Keep social metadata titles at 70 characters or less and descriptions at 160 characters or less unless the existing site metadata pattern requires a different limit.
- [x] Add sitemap and discovery-index metadata if comparable public pages are indexed there.
- [x] Add safe navigation links from relevant public pages without implying production proof, runtime proof, release approval, or operational safety.
- [x] Add focused page/content validation for required labels, required links, and forbidden private/raw artifact text.
- [x] Add focused rendered-copy validation for forbidden AI/LLM positioning using a pattern such as `/\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\b/i`, and do not reduce the pattern without a spec amendment.
- [x] Confirm rendered page word count is between 400 and 1500 words.
- [x] Run `git diff --check`.
- [x] Run `npm test` from `site/`.
- [x] Run `npm run validate` from `site/`.
- [x] Run `npm run build` from `site/`.
- [x] Run desktop and mobile browser sanity checks if the implementation changes layout or interaction.
- [x] Update `implementation-state.md` with implementation scope, validation, oddities, and follow-up items.
