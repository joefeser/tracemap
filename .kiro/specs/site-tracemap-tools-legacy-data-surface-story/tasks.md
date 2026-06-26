# Site TraceMap Tools Legacy Data Surface Story Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

Ordering note: this packet began as spec-only and now tracks the completed
site implementation. Do not implement scanner code, reducer code, runtime data
lineage, SQL execution, or database inspection workflows in this site packet.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model blocker in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model blocker in `implementation-state.md`.
- [x] Patch Medium or higher actionable spec findings; patch Low findings only
  when narrow and safe.
- [x] Run one bounded re-review if findings were patched.
- [x] Update `Readiness` to `ready-for-implementation` only after review
  findings are patched or exact blockers are recorded.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm the diff is limited to
  `.kiro/specs/site-tracemap-tools-legacy-data-surface-story/`.

## Future Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  placement decision, route choice, proof-path choices, validation plan,
  oddities, and follow-up items before changing site code.
- [x] Implement the future route or section, preferably
  `/legacy-data-surface/`, using existing static site layout patterns.
- [x] Include visible `Public claim level: concept` and
  `No public conclusion without evidence`.
- [x] Present the surface as a concept-level legacy data evidence story, not a
  shipped support page, runtime data lineage tool, migration validator, SQL
  executor, or database inspection workflow.
- [x] Relate the page to `/legacy-dotnet/evidence/`, `/legacy-evidence/`, and
  `/legacy-modernization/evidence-map/` without promoting hidden rows or
  private validation detail.
- [x] Cover design-time metadata, data model metadata, ORM/mapping clues,
  SQL/query-facing references, storage/persistence context, and limitations.
- [x] Add an evidence-status matrix with evidence family, possible static
  evidence, evidence status, proof path requirement, limitation, owner
  follow-up, allowed wording, and forbidden wording.
- [x] Keep every public conclusion connected to deterministic rule IDs or rule
  families, evidence tiers, coverage labels, limitations, and public-safe proof
  paths when evidence exists.
- [x] Use hidden, future, dev, concept, partial, reduced, gap, and unknown
  wording according to the proof posture documented in the requirements.
- [x] Avoid claims of raw data access, database execution, runtime SQL
  behavior, data contents, database existence, schema compatibility, migration
  success, endpoint performance, production traffic, outage cause, release
  safety, operational safety, AI impact analysis, LLM analysis, embeddings,
  vector databases, prompt-based classification, or complete coverage.
- [x] Avoid publishing raw source snippets, raw SQL, raw config values,
  secrets, credentials, tokens, connection strings, database contents, table
  dumps, raw facts, raw SQLite content, analyzer logs, raw remotes, local
  absolute paths, generated scan directories, private sample names, hidden
  validation details, raw command output, private URLs, or credential-like
  values.
- [x] Add title, description, canonical URL, Open Graph metadata, and
  concept-level claim metadata.
- [x] Add discovery metadata with public claim level `concept`, preferred proof
  path, limitations, non-claims, neighboring route hints, and relation to the
  legacy .NET evidence lane.
- [x] Add sitemap metadata if a standalone route is implemented.
- [x] Add focused validation for required visible copy, required evidence
  families, evidence-status matrix columns, required links, route/discovery
  metadata, forbidden claims, private/raw material, and bounded rendered word
  count.
- [x] Run `npm run build` from `site/`.
- [x] Run the relevant site validation command from `site/`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks for the new public surface,
  or document an explicit deferral.
- [x] Update this spec's `implementation-state.md` with final implementation
  scope, validation results, oddities, and follow-up items.
