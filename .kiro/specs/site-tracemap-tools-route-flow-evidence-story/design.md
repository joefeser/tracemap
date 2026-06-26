# Site TraceMap Tools Route-Flow Evidence Story Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Design Purpose

This design describes the implemented public-safe page that explains the
route-flow evidence story in TraceMap terms. It gives readers a bounded way to
read route-centered static evidence from endpoint/root selection through
service, data, dependency, value-origin, gap, and limitation context.

The selected implementation is the standalone `/proof-paths/route-flow/`
route. This design does not implement scanner behavior, route-flow behavior,
reducer behavior, generated scan artifacts, runtime probes, or release gates.

## Starting Boundary

The page is concept-level even though route-flow core work has shipped and
in-progress pieces. The site surface must treat current route-flow behavior as
evidence-conditioned:

- If current checked-in evidence proves a narrow route-flow behavior, the page
  may say that behavior is available and cite or link to the public-safe proof.
- If current evidence is incomplete, private-only, in progress, or absent, the
  page must use future-facing or illustrative language.
- If evidence cannot be made public-safe, the page must stop, downgrade, or
  keep the statement internal.

## Placement Options

Preferred starting options:

- `/proof-paths/route-flow/`: recommended default. It keeps the story near
  proof paths and avoids making route-flow sound like a public demo result.
- `/route-flow/`: allowed only when implementation-state records an
  information-architecture decision that route-flow is a first-class public
  concept route.
- Section on `/proof-paths/`: allowed when the story is compact enough to live
  in the broader proof-path overview.
- Section on `/evidence/`: allowed when the story mainly explains row fields,
  vocabulary, and evidence boundaries.
- Section on `/capabilities/`: allowed only when the copy stays
  concept-level and avoids shipped-capability overclaiming.

Rejected-by-default options unless implementation-state records otherwise:

- Replacing `/proof-paths/`, because route-flow is one proof-path family, not
  the whole proof-path model.
- Replacing `/proof-paths/tour/`, because a tour should remain a guided
  reading sequence rather than the route-flow reference.
- Replacing `/evidence/`, because evidence vocabulary applies to more than
  route-flow.
- Replacing `/limitations/`, because limitations and non-claims need a
  canonical boundary surface.
- Replacing `/static-vs-runtime/`, because static/runtime boundaries should
  stay broader than route-flow.
- Adding the page to primary navigation before an information-architecture
  review.

## Page Model

Recommended sections:

1. Opening boundary: visible `Public claim level: concept`, visible
   `No public conclusion without evidence`, and a short statement that this is
   a static evidence story, not runtime or release proof.
2. Route-flow story at a glance: endpoint/root selector to static rows,
   context, gaps, classifications, limitations, and owner handoff.
3. Proof path fields: the required reading order and the evidence fields that
   must stay attached.
4. Row and status vocabulary: selected rows, context rows, gaps,
   classifications, coverage labels, and stop states.
5. Evidence-conditioned current branch note: how implementation must separate
   cited current-branch proof from illustrative or future-only copy.
6. Safe and unsafe wording: bounded examples and rejected overclaim patterns.
7. Adjacent surfaces: links to proof paths, tour, evidence, limitations,
   static/runtime boundaries, review checklist, and glossary where they exist.
8. Stop conditions: missing proof path, missing rule ID, missing tier, missing
   coverage, missing limitation, private-only proof, hidden details, unjoined
   context, unsupported demo claim, or forbidden wording.

The page may use a static diagram, table, timeline, step list, or linked
sections. If progressive disclosure is used, full answer and row text must be
present in static HTML so validation, bots, crawlers, and readers inspect the
same content.

## Required Story Flow

| Step | Public-safe meaning | Required fields | Stop condition |
| --- | --- | --- | --- |
| Claim level | How strongly the public page may talk about the story. | `Public claim level: concept` for the page; row-level source claim where cited. | Stop if copy implies demo/shipped behavior without public-safe proof. |
| Selector/root | The selected route, client call, endpoint, or root method. | Selector kind, safe display label or hash, source context. | Stop if selector is private-only, ambiguous, or unsupported. |
| Route/root evidence | Evidence that the selector maps to a route/root. | Rule ID or rule family, tier, coverage, supporting IDs, limitations. | Stop if no public-safe route/root evidence exists. |
| Bridge state | Static bridge from route/root to symbol, path node, or fallback. | Bridge state, classification, supporting fact or edge IDs. | Stop or downgrade if bridge is missing, ambiguous, reduced, or private-only. |
| Static rows | Selected route-flow rows and context. | Row kind, rule ID, tier, coverage, classification, source context, supporting IDs. | Do not attach rows by textual or same-file proximity alone. |
| Context rows | Service/data/query/dependency/value-origin context. | Context kind, join basis, supporting IDs, limitations. | Label a gap when context is adjacent but unjoined. |
| Gaps | Evidence that analysis cannot prove or disprove a stronger statement. | Gap kind, rule ID, tier, coverage, affected scope, limitation. | Preserve the gap; do not normalize it upward. |
| Handoff | What a reviewer or owner should do next. | Stop state, owner role, non-claim, next action. | Do not repeat the claim after dropping the handoff. |

## Row Vocabulary

Allowed row and section labels include:

- `selector`
- `endpoint/root`
- `route/root evidence`
- `bridge state`
- `static flow row`
- `context group`
- `service/helper`
- `repository/data`
- `query or SQL shape`
- `dependency surface`
- `value origin`
- `implementation candidate`
- `gap`
- `limitation`
- `owner follow-up`

The implementation may add narrower labels if they map to checked-in
route-flow vocabulary or are clearly illustrative. New labels must not sound
like runtime execution, release approval, incident causality, or operational
proof.

## Classification And Coverage Copy

The page may name the existing route-flow classifications:

- `StrongStaticRouteFlow`
- `ProbableStaticRouteFlow`
- `NeedsReviewStaticRouteFlow`
- `NoRouteFlowEvidence`
- `UnknownAnalysisGap`

Copy must say these are static classifications. Even the strongest
classification does not prove runtime request execution, dependency-injection
target selection, branch feasibility, SQL execution, database state,
production traffic, endpoint performance, release safety, outage cause, or
business impact.

Coverage labels are boundaries. Use full, partial, reduced, unknown,
unavailable, future-only, and gap-labeled states without converting reduced or
unknown evidence into stronger wording.

## Safe Copy Patterns

Use bounded wording:

- `A reader can inspect this static route-flow row under <rule ID or rule family>, with <evidence tier>, <coverage label>, supporting IDs, and limitations.`
- `The row is selected only because it joins through the route-flow proof path; adjacent unjoined context becomes a gap or limitation.`
- `The classification is static evidence and remains separate from runtime behavior or release decisions.`
- `The current branch proves this narrow row family through checked-in public-safe evidence; other row families remain concept-level or deferred.`

Preferred verbs:

- `inspect`
- `follow`
- `compare`
- `record`
- `label`
- `downgrade`
- `hold`
- `hand off`
- `escalate`

## Unsafe Copy Patterns

Reject these as unsupported public claims unless they appear only inside an
explicit rejection or non-claim context:

- `Route-flow proves runtime behavior.`
- `Route-flow proves production traffic or endpoint performance.`
- `Route-flow identifies outage root cause.`
- `Route-flow proves the release is safe.`
- `Route-flow proves complete coverage.`
- `Route-flow proves runtime dependency-injection target selection.`
- `Route-flow is AI or LLM impact analysis.`
- `This story approves a release or authorizes autonomous approval.`
- `This replaces tests, source review, runtime observability, service-owner judgment, or human review.`
- `Raw source, SQL, config, facts, SQLite, analyzer logs, local paths, raw remotes, generated outputs, private samples, command output, hidden validation detail, or credentials should be pasted into public copy.`

Unsafe examples are rejected patterns, not live claims. Do not include real
private paths, private sample names, credentials, command output, hidden
validation details, raw route values, or raw snippets.

Each sentence above must appear in the rendered page under a clearly labeled
`rejected pattern`, `do not use`, or `non-claim example` heading or callout. It
must not appear as a standalone sentence, pull quote, or summary item without
that framing.

## Adjacent Links

The future implementation should link to current public-safe equivalents for:

- `/proof-paths/`
- `/proof-paths/tour/`
- `/evidence/`
- `/limitations/`
- `/static-vs-runtime/`
- `/review-claim-checklist/`
- `/glossary/`

If a route does not exist, is renamed, or is replaced, record the substitution
or deferral in `implementation-state.md`. Do not add dead links to
concept-stage routes.

## Validation Design

Focused validation should parse generated HTML and metadata with structured
tools where practical. It should verify:

- visible concept claim label and shared principle;
- required placement and adjacent-link decisions;
- proof-path field coverage;
- allowed row/status vocabulary and stop states;
- static classification and coverage language;
- current-branch evidence wording is cited, illustrative, deferred, or
  downgraded;
- authored illustrative diagrams, rows, or examples are labeled illustrative
  and are not presented as real TraceMap findings;
- standalone route metadata/discovery or host-section metadata reconciliation;
- no forbidden runtime, production, performance, outage, release, operational,
  complete-coverage, business-impact, AI/LLM, approval, or replacement claims
  outside bounded rejection/non-claim contexts;
- no forbidden private/raw material;
- no blame language;
- static HTML contains the full text needed for validation.

Implementation validation should run `npm test`, `npm run validate`,
`npm run build`, `git diff --check`, `./scripts/check-private-paths.sh`, and
desktop/mobile browser sanity checks when layout or interaction changes.

## Non-Goals

- No additional site implementation beyond the checked-in static route,
  metadata, and validation.
- No route-flow product implementation.
- No scanner, reducer, adapter, or generated artifact changes.
- No runtime probes, production telemetry, endpoint performance claims,
  incident-cause claims, release approval, operational approval, or AI/LLM
  impact analysis.
- No publication of private/raw artifacts.
