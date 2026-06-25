# UI Field Property Lineage Composition Tasks

Status: ready-for-implementation-after-route-flow-contract
Readiness: ready-after-current-route-flow-worker

Reconciliation note: this is the active property-flow implementation spec after
the older continuation packet. Start here only after any in-flight route-flow
branch is merged or explicitly closed, `origin/dev` is fetched, and the
route-flow contract is re-audited against that commit.

## Spec-Only PR Scope

- [x] Create `.kiro/specs/ui-field-property-lineage-composition/`.
- [x] Inspect predecessor UI lineage specs and route-flow composition specs.
- [x] Capture issue #165 scope without private sample names, private paths,
  raw URLs, raw SQL, hostnames, raw remotes, source snippets, secrets, or
  generated local artifacts.
- [x] Run Kiro spec review with Opus if available, or record the exact
  unavailable-model/tool/timeout evidence in `implementation-state.md`.
- [x] Run Kiro spec review with Sonnet if available, or record the exact
  unavailable-model/tool/timeout evidence in `implementation-state.md`.
- [x] Patch Medium+ actionable findings; patch Low findings only when narrow
  and safe.
- [x] Run one final bounded Sonnet or Opus re-review after patches.
- [x] Update `implementation-state.md` status and readiness to
  `ready-for-implementation` only after review fixes are applied.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run any existing spec lint/check if present, or record that none exists.
- [x] Confirm the committed diff is limited to this spec folder.
- [x] Open a ready PR into `dev`.
- [x] Run ACK PR loop and record the final decision/stop reason.

## Recommended Implementation Slices

### PR 1: Composition Contract Audit And Gap Hardening

- [ ] 1. Inventory current property-flow and route-flow contracts.
  Requirements: 1, 3, 5, 6.
  - [ ] Confirm current property-flow report version, row shapes, selectors,
    classifications, and rule IDs.
  - [ ] Confirm current route-flow schema/report signals available to
    property-flow, including context groups, touched files, touched symbols,
    logic rows, dependency surfaces, argument projection, and fact-symbol
    projection.
  - [ ] Record which property-specific bridge kinds already exist and which
    remain gaps.
  - [ ] Decide whether new output is report version `1.0` compatible or needs a
    documented version bump, using the existing continuation spec's version
    `1.0` compatibility decision as the starting point.
  - [ ] Commit the audit result in this spec's `implementation-state.md` or a
    spec-local contract audit note before marking this task complete.

- [ ] 2. Harden gaps and rule-catalog coverage.
  Requirements: 3, 4, 5.
  - [ ] Reuse existing `property-flow.*.v1` and source rule IDs where possible.
  - [ ] Add catalog entries and limitations before emitting any new rule IDs.
  - [ ] Emit route-flow unavailable/no-property-context gaps with coverage
    labels and supporting IDs where available.
  - [ ] Emit explicit property identity, endpoint alignment, mapper evidence,
    terminal context, schema, coverage, unsafe input, and truncation gaps.
  - [ ] Add tests proving broad endpoint reachability does not become property
    lineage.

### PR 2: Angular And Razor To Backend Property Bridges

- [ ] 3. Compose Angular UI roots to HTTP payload and endpoint evidence.
  Requirements: 1, 2, 3, 5.
  - [ ] Connect event bindings to handlers only through existing static handler
    evidence.
  - [ ] Connect component/control values to payload fields through direct
    assignment, value-origin, alias, argument-flow, parameter-forwarding, or
    object-shape evidence.
  - [ ] Connect payload/body/query/route fields to HTTP calls and endpoint
    alignment evidence.
  - [ ] Downgrade same-name-only, alias-only, and generic/fan-out matches.
  - [ ] Emit gaps for dynamic expressions, callbacks, unresolved templates,
    custom directives, and unsupported form semantics.

- [ ] 4. Compose Razor/cshtml roots to actions, handlers, and model-binding.
  Requirements: 1, 2, 3, 5.
  - [ ] Connect `asp-for` and `Html.*For` roots to model/view-model properties
    through static `@model`, page model, action/handler parameter,
    model-binding target, fact, or symbol evidence.
  - [ ] Connect form targets to MVC actions and Razor Page handlers through
    normalized action/controller/page/handler/method metadata.
  - [ ] Connect endpoint/action/handler evidence to DTO/model properties only
    through rule-backed model-binding or equivalent property facts.
  - [ ] Emit gaps for `ViewBag`, `ViewData`, dynamic models, partial/editor
    template ambiguity, generated Razor uncertainty, custom tag helpers, and
    unsupported conventions.

- [ ] 5. Add bridge fixtures and focused tests.
  Requirements: 1, 2, 5, 8.
  - [ ] Add public-safe Angular fixtures for supported direct property trails
    and downgraded dynamic/ambiguous trails.
  - [ ] Add public-safe Razor/MVC/Pages fixtures for supported direct property
    trails and downgraded dynamic/ambiguous trails.
  - [ ] Test `model:` and `dto:` family filtering, exact `fact:` disambiguation,
    generic name fan-out, same-name-only joins, alias-only joins, ambiguous
    endpoint/handler candidates, and safe selector rejection.
  - [ ] Test `model:` and `dto:` selectors over server-only model-binding facts
    with no UI bridge, proving they are allowed as model/DTO roots but not
    presented as UI roots.
  - [ ] Test alias-like evidence without a catalogued alias rule, proving it
    remains same-name-only or convention-only and cannot upgrade above
    `NeedsReviewLineage`.
  - [ ] Test deterministic root/path/gap ordering and byte-stable JSON/Markdown.

### PR 3: Backend Terminal Context And Route-Flow Reuse

- [ ] 6. Attach route-flow context only through property-specific bridges.
  Requirements: 3, 4, 5.
  - [ ] Reuse route-flow rows, context groups, touched files, touched symbols,
    logic rows, dependency surfaces, argument projection, and fact-symbol
    projection only when the selected property trail supports the join.
  - [ ] Preserve route-flow rule IDs, supporting IDs, classifications, coverage
    labels, and limitations.
  - [ ] Do not recompute independent route traversal in property-flow.
  - [ ] Test route-flow available, route-flow empty, route-flow missing schema,
    route-flow reduced coverage, and route-flow no property context.

- [ ] 7. Attach service/data/dependency terminal context.
  Requirements: 4, 5, 6.
  - [ ] Include validation/read/write, assignment, mapping, projection,
    service/repository, query, data-surface, dependency-surface, package/config,
    event/message, storage, legacy-data, SQL, and persistence context only when
    existing facts expose a property-specific trail.
  - [ ] Treat path/reverse rows as supporting context, not recomputed semantics.
  - [ ] Emit mapper, terminal-context, schema, coverage, fan-out, ambiguity, and
    truncation gaps when evidence is insufficient.
  - [ ] Keep reducer impact claims out of property-flow output.

- [ ] 8. Validate backend composition.
  Requirements: 4, 5, 8.
  - [ ] Test terminal context present only when property-specific evidence
    exists.
  - [ ] Test broad endpoint reachability, same file/class, same method, or same
    property name does not attach unrelated service/data/dependency context.
  - [ ] Test reduced coverage and missing schema cap absence conclusions at
    `UnknownAnalysisGap`.
  - [ ] Assert every emitted rule ID resolves to the rule catalog.
  - [ ] Assert no uncatalogued candidate rule ID is emitted.
  - [ ] Assert gap codes come from the closed set documented by this spec or a
    successor spec whose gap-code additions are recorded in this spec's
    `implementation-state.md` before they are emitted.
  - [ ] Assert weak-but-present evidence produces deterministic
    `NeedsReviewLineage` behavior while absent/dynamic/unavailable evidence
    produces deterministic gap behavior.
  - [ ] Test that controller, action, route, file, or class name proximity
    alone cannot produce `ProbableStaticLineage`; assert the result is
    `NeedsReviewLineage` or a gap.

### PR 4: Export And Consumer Compatibility

- [ ] 9. Preserve report, vault, RAG/docs-export, and explorer safety.
  Requirements: 6, 7, 8.
  - [ ] Inspect `tracemap vault`, `tracemap docs-export`, RAG import/export
    chunks, `tracemap evidence-pack`, `tracemap explorer generate`, and any
    evidence graph export consumers touched by new row shapes.
  - [ ] Render additive safe metadata or emit documented compatibility gaps.
  - [ ] Introduce a report version bump if a consumer cannot safely ignore new
    rows or metadata.
  - [ ] Ensure public/demo artifacts preserve rule IDs, tiers, coverage labels,
    supporting IDs, commit SHA, extractor versions, and limitations where
    supported.
  - [ ] Reject, hash, or omit unsafe metadata and cite redaction rules.

- [ ] 10. Validate consumer compatibility.
  Requirements: 6, 8.
  - [ ] Test deterministic property-flow Markdown/JSON.
  - [ ] Test touched vault/RAG/docs-export/evidence-pack/explorer outputs are
    deterministic and public-safe.
  - [ ] Test an older or compatibility-mode consumer receiving unknown additive
    property-flow metadata safely ignores it or emits a documented
    compatibility gap rather than forwarding unsafe metadata.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.

## Deferred Follow-Ups

- Browser/computer-use capture as a separate future local/demo validation
  workflow that cannot upgrade static classifications.
- Runtime DOM visibility, live HTTP, production login, credentials, telemetry,
  feature-flag proof, authorization proof, dependency-injection runtime target
  proof, serializer runtime expansion, branch feasibility, mutation semantics,
  database execution, and production traffic claims.
- Advanced Angular custom directive, pipe, structural directive, and dynamic
  reactive-form semantics beyond deterministic static evidence.
- Cross-file Razor/model-binding expansion when semantic or deterministic
  project-wide syntax metadata can support it safely.
- Persisted derived property-flow rows behind an explicit reviewed write mode.
- Whole-application property inventory UI.
- LLM classification, embeddings, vector databases, or prompt-based analysis.
