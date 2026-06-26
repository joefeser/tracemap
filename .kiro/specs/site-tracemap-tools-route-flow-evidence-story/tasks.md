# Site TraceMap Tools Route-Flow Evidence Story Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks track a spec-only public-site packet. Future implementation tasks
remain unchecked until a later site implementation phase edits site source and
validation.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8`, or record the exact
      unavailable-tool/model blocker in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6`, or record the exact
      unavailable-tool/model blocker in `implementation-state.md`.
- [x] Record Sonnet review findings and dispositions in
      `implementation-state.md` before marking the readiness gate task
      complete.
- [ ] If both requested models are unavailable, record the exact errors, run
      the best available Kiro spec review, and record the substitution and
      rationale in `implementation-state.md`.
- [x] Patch Medium or higher actionable spec findings and run one bounded
      re-review when findings were patched. Record re-review outcome in
      `implementation-state.md` before marking complete.
- [x] Patch Low findings only when narrow and safe, or record why they are
      deferred.
- [x] Move `Readiness` beyond `spec-review` only after the bounded re-review
      is recorded in `implementation-state.md`, local validation passes, and
      all Medium or higher findings from both prior reviews are patched or
      explicitly dispositioned.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Confirm the diff is limited to
      `.kiro/specs/site-tracemap-tools-route-flow-evidence-story/`.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
      route choice, scope decisions, review results, validation plan, and
      initial implementation status before changing site code.
- [ ] Audit current `origin/dev` route-flow specs, implementation-state notes,
      rule catalog entries, tests, and public site pages before writing public
      route-flow copy.
- [ ] Record which route-flow statements are backed by current-branch
      public-safe evidence and which remain illustrative, concept-level, or
      deferred.
- [ ] Choose final placement: `/proof-paths/route-flow/`, `/route-flow/`,
      section on `/proof-paths/`, section on `/evidence/`, section on
      `/capabilities/`, or a recorded equivalent.
- [ ] Record selected placement and rejected alternatives in
      `implementation-state.md`.
- [ ] Explain how the selected placement differs from proof paths, tour,
      evidence, limitations, static-versus-runtime, review checklist, and
      glossary surfaces.
- [ ] Add the concept-level route-flow evidence story page or section using
      existing static-site layout, typography, accessibility, metadata, and
      validation patterns.
- [ ] Include visible `Public claim level: concept`.
- [ ] Include visible `No public conclusion without evidence`.
- [ ] Keep the page or section out of primary navigation unless
      implementation-state records a matching information-architecture
      decision.
- [ ] If standalone, add title, description, canonical URL, Open Graph
      metadata, sitemap metadata, and discovery metadata with concept-level
      wording.
- [ ] If a host-route section, record host metadata reconciliation and add
      stable namespaced anchors.
- [ ] Present the route-flow proof path from selector/root to route/root
      evidence, bridge state, selected rows, context rows, gaps, limitations,
      and owner handoff.
- [ ] Preserve required proof fields: public claim level, selector/root,
      route/root evidence, bridge state, row/context kind, rule ID or rule
      family, evidence tier, coverage label, classification, supporting IDs,
      public-safe source context, commit/source context, extractor version or
      schema family, limitation, stop condition, and next owner.
- [ ] Explain supporting IDs as public-safe row, fact, edge, symbol, source, or
      artifact identifiers without exposing raw private identifiers or local
      paths.
- [ ] Explain selected context versus adjacent unjoined context.
- [ ] Use bounded row labels such as `selector`, `endpoint/root`,
      `bridge state`, `static flow row`, `context group`, `service/helper`,
      `repository/data`, `query or SQL shape`, `dependency surface`,
      `value origin`, `implementation candidate`, `gap`, `limitation`, and
      `owner follow-up`.
- [ ] Use only static route-flow classifications:
      `StrongStaticRouteFlow`, `ProbableStaticRouteFlow`,
      `NeedsReviewStaticRouteFlow`, `NoRouteFlowEvidence`, and
      `UnknownAnalysisGap`.
- [ ] State that every classification remains static evidence and does not
      prove runtime request execution, runtime binding, production traffic,
      endpoint performance, release safety, outage cause, business impact, or
      complete coverage.
- [ ] Explain full, partial, reduced, unknown, unavailable, future-only, and
      gap-labeled coverage states as boundaries.
- [ ] Include stop conditions for missing proof path, missing rule ID or rule
      family, missing tier, missing coverage label, missing limitation, missing
      public-safe source context, private-only evidence, hidden detail,
      unjoined adjacent context, ambiguous endpoint/root, runtime-only
      binding, reduced coverage affecting the selected path, schema/extractor
      gap, unsupported demo claim, and forbidden runtime/release/AI wording.
- [ ] Use bounded review/action labels such as `show as static evidence`,
      `show as context`, `label the gap`, `downgrade`, `keep internal`,
      `owner follow-up`, and `do not repeat`.
- [ ] Include safe wording patterns using `inspect`, `follow`, `compare`,
      `record`, `label`, `downgrade`, `hold`, `hand off`, and `escalate`.
- [ ] Include unsafe wording patterns that reject `proves`, `guarantees`,
      `certifies`, `approves`, `replaces`, `resolves`,
      `production-proven`, `safe to release`, `root cause`,
      `complete coverage`, `AI impact analysis`, `LLM impact analysis`, and
      unqualified `impacted`.
- [ ] Frame unsafe examples as rejected patterns, not live claims.
- [ ] Do not publish raw source, SQL, config, secrets, local paths, raw
      remotes, private sample names, private route values, generated output
      directories, raw facts, raw SQLite, analyzer logs, command output,
      hidden validation detail, or credential-like values.
- [ ] Link to `/proof-paths/` when it exists and explain that it remains the
      broader proof-path overview.
- [ ] Link to `/proof-paths/tour/` when it exists and explain that it remains
      the guided reading flow.
- [ ] Link to `/evidence/` when it exists and explain that it remains the
      broader evidence vocabulary and artifact-shape surface.
- [ ] Link to `/limitations/` when it exists and explain that it remains the
      canonical boundary and non-claim surface.
- [ ] Link to `/static-vs-runtime/` when it exists and explain that it remains
      the static evidence versus runtime telemetry boundary.
- [ ] Link to `/review-claim-checklist/` when it exists and explain that it
      remains the claim-repeat/downgrade ritual.
- [ ] Link to `/glossary/` when it exists and explain that it remains the
      canonical term index.
- [ ] Record substitutions, omissions, or deferrals for adjacent routes that
      do not exist at implementation time.
- [ ] Verify every public link resolves in generated site output before
      publishing it.
- [ ] Add focused validation for visible concept label and shared principle.
- [ ] Add focused validation for proof-path fields, row/status vocabulary,
      stop states, metadata/discovery, adjacent links, forbidden claims,
      forbidden private material, no blame language, and static HTML content.
- [ ] Wire focused validation into the existing aggregate site validation
      workflow.
- [ ] Run `npm test` from `site/` after site source is added.
- [ ] Run `npm run validate` from `site/` after site source is added.
- [ ] Run `npm run build` from `site/` after site source is added.
- [ ] Run desktop and mobile browser sanity checks if route, layout, or
      interaction changes are made.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Update `implementation-state.md` with route decisions, substitutions,
      validation results, review findings, claim-boundary decisions, oddities,
      and follow-up items.
