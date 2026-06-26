# Site TraceMap Tools Owner Follow-Up Map Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

Ordering note: this is a spec-only packet. Do not start implementation tasks
until spec review findings are handled and readiness is updated to
`ready-for-implementation`.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or the best available Opus
  model at review time, or record the exact unavailable-tool/model error in
  `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Patch Medium or higher spec findings and rerun re-review with the same
  models, Opus and Sonnet, where feasible, or record why a subset model pass
  is sufficient in `implementation-state.md`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Update `Readiness` to `ready-for-implementation` only after review
  findings are patched or exact unavailable-tool/model errors are recorded.

## Future Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with
  branch, scope, final placement, public claim level, review status,
  validation plan, oddities, and follow-ups before changing site code.
- [x] Choose the final placement from `/owners/follow-up/`,
  `/review-room/owners/`, a section on `/team-evidence-handoff/`, a section
  on `/questions/`, or a recorded equivalent.
- [x] Record rejected placement alternatives and rationale before editing site
  source.
- [x] Add the concept-level owner follow-up map using existing static site
  layout, navigation, accessibility, and metadata patterns.
- [x] Include visible `Public claim level: concept`.
- [x] Include visible `No public conclusion without evidence`.
- [x] State that TraceMap routes static-evidence questions to owner
  categories and does not know real organizational ownership.
- [x] Distinguish the surface from `/team-evidence-handoff/`,
  `/incident-evidence-handoff/`, `/reviewer-quickstart/`, `/questions/`,
  `/questions/objections/`, `/packets/assembly/`, and `/manager-packet/`.
- [x] Include the required rows: code path question, test coverage question,
  runtime behavior question, data/schema question, config/deployment
  question, release decision question, architecture decision question, and
  evidence gap question.
- [x] Ensure each row includes static evidence trigger, what TraceMap can
  show, what TraceMap cannot show, next owner, handoff wording, proof path,
  limitation, and stop condition.
- [x] Limit next owner labels to code owner, reviewer, test owner,
  service/runtime owner, database owner, release reviewer, architect, and
  manager unless a spec update records an approved addition.
- [x] Keep handoff wording synthetic, public-safe, non-blaming, and attached
  to proof path, limitation, and stop condition.
- [x] Avoid claims of real org ownership, production ownership proof, runtime
  behavior proof, release approval, operational safety, complete coverage,
  AI/LLM analysis, or replacement of human judgment.
- [x] Avoid publishing raw facts, SQLite database content, analyzer logs,
  source snippets, SQL, configuration values, secrets, local paths, repository
  remotes, generated scan directories, private sample names, command output,
  hidden validation details, or credential-like values.
- [x] Add required links or documented substitutions for
  `/team-evidence-handoff/`, `/incident-evidence-handoff/`,
  `/reviewer-quickstart/`, `/questions/`, `/questions/objections/`,
  `/packets/assembly/`, `/manager-packet/`, `/proof-paths/`, `/limitations/`,
  and `/validation/`.
- [x] If standalone, add title, description, canonical URL, Open Graph fields,
  route metadata, discovery metadata, route-index metadata, and sitemap
  metadata with `publicClaimLevel: concept`.
- [x] If embedded, update host discovery or record why section-level
  discovery is not used. Standalone route selected; embedded handling is not
  applicable.
- [x] Add focused validation for required rows, required row fields, required
  links, route metadata, discovery/sitemap metadata if standalone, forbidden
  claims, private/raw material, blame language, word count bounds, and
  desktop/mobile browser sanity expectations.
- [x] Add negative tests for missing required rows, missing fields, missing
  labels, unresolved links, missing metadata, forbidden claims, private/raw
  material, blame language, word count violations, unsubstituted handoff
  placeholder tokens, and an embedded ceiling set below the content minimum
  for all required rows, row fields, and boundary statements when standalone
  fallback is not recorded. Standalone fallback is recorded by the placement
  decision, so the embedded-ceiling negative case is not applicable.
- [x] Run `git diff --check`.
- [x] Run `npm run build` from `site/`.
- [x] Run focused route validation or the full site validation entrypoint.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run desktop and mobile browser sanity checks for the selected route or
  host page.
- [x] Update this spec's `implementation-state.md` with final implementation
  scope, validation results, oddities, and follow-up items.
