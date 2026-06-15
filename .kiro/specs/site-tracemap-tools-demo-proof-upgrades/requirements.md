# Site TraceMap Tools Demo Proof Upgrades Requirements

Public claim level: concept

## Objective

Create a queued site phase for a public `/demo/proof-upgrades/` page that explains how the deferred rows from the current public demo can become stronger public claims. The page should act as a proof ladder: each deferred item names the evidence required before it can move from concept or deferred status into demo status.

This spec is content planning only until implementation starts.

## Requirements

### Requirement 1: Publish a demo proof-upgrades page

The site shall publish a page at `/demo/proof-upgrades/` that explains the proof needed to upgrade current deferred demo rows.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states that deferred demo rows are not current public demo claims.
- The page links to `/demo/result/`, `/roadmap/`, `/packets/`, and `/capabilities/`.
- The page uses existing static site layout patterns and does not introduce a new runtime service.

### Requirement 2: Show the deferred-row proof ladder

The page shall include one row or section for each current deferred public demo area.

Acceptance criteria:

- The page covers `combine-and-dependency-report`.
- The page covers `paths-and-reverse`.
- The page covers `portfolio`.
- The page covers `diff`.
- The page covers `impact`.
- The page covers `release-review`.
- Each row includes current public status, required proof, expected public-safe artifact, and what the row must not claim.

### Requirement 3: Define promotion gates

The page shall explain what evidence is required before a row can move from concept or deferred status to demo status.

Acceptance criteria:

- A row can become `demo` only when it is reproducible from checked-in samples or public-safe generated summaries.
- Required proof includes rule IDs, evidence tiers, coverage labels, counts or supporting IDs where applicable, limitations, and a source path back to the checked-in workflow or generated summary.
- Rows that require before/after fixture pairs must stay concept-level until those fixtures exist and can be generated safely.
- Rows that rely on generated output must stay local-only unless the generated output passes the private-path and raw-value safety checks.

### Requirement 4: Keep public claim boundaries explicit

The page shall keep the static evidence boundary visible.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, deployment state, endpoint performance, release safety, or AI impact analysis.
- The page does not say deferred rows are shipped or currently demonstrated.
- The page does not publish raw scan artifacts, SQLite databases, fact streams, analyzer logs, source snippets, SQL text, config values, secrets, local absolute paths, raw repository remotes, or private sample identities.
- The page says generated public-safe summaries are a presentation layer over evidence, not a replacement for facts, reports, rule IDs, coverage labels, and limitations.

### Requirement 5: Make the page discoverable

The page shall be reachable from existing public demo and roadmap surfaces.

Acceptance criteria:

- `/demo/result/` links to `/demo/proof-upgrades/`.
- `/roadmap/` links to `/demo/proof-upgrades/`.
- `/demo/` links to `/demo/proof-upgrades/` near the current demo-result and roadmap callouts.
- `/demo/proof-upgrades/` is included in sitemap metadata.
- The implementation-state note records scope, branch, validation, and follow-up items when implementation begins.

## Deferred Implementation Notes

Suggested row framing:

| Deferred area | Current public status | Required proof before demo | Public-safe artifact |
| --- | --- | --- | --- |
| combine-and-dependency-report | deferred/concept | generated combined indexes plus dependency report assertions | summary row and scrubbed report excerpt |
| paths-and-reverse | deferred/concept | bounded path and reverse assertions over checked-in sample indexes | summary row with path counts and limitation labels |
| portfolio | deferred/concept | generated portfolio manifest from public sample indexes | manifest summary with counts and coverage labels |
| diff | deferred/concept | checked-in before/after fixture pair and generated diff summary | public-safe diff summary |
| impact | deferred/concept | checked-in before/after fixture pair plus reducer output tied to evidence | public-safe impact summary with caveats |
| release-review | deferred/concept | compatible before/after inputs, delta fixtures, and composed review packet | public-safe release-review packet summary |
