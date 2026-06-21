# UI Field and Property Lineage Next Slice Tasks

## Spec-Only PR Scope

- [x] Create `.kiro/specs/ui-field-property-lineage-next-slice/` as a focused
  continuation spec.
- [x] Inspect the existing `ui-field-property-lineage` spec and implementation
  state before drafting this continuation.
- [x] Inspect current `property-flow` implementation and tests enough to avoid
  duplicating completed baseline work.
- [x] Capture public issue #165 and related issue #159 context without private
  sample names, private paths, raw routes, raw SQL, hostnames, remotes, or
  snippets.
- [x] Run Kiro spec review with Opus if available, or record the exact
  unavailable-model/tool error in `implementation-state.md`.
- [x] Run Kiro spec review with Sonnet if available, or record the exact
  unavailable-model/tool error in `implementation-state.md`.
- [x] Patch all Medium+ review findings; patch Low findings only when narrow
  and safe.
- [x] Run at most two re-review cycles, preferring Sonnet for final re-review.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Run any existing spec lint/check if present; otherwise record that no
  spec lint/check exists.
- [x] Open a ready PR into `dev` for the spec-only changes.
- [x] Run the PR review loop and record the final decision.

## Recommended Implementation PR Slices

### PR 1: Model-Binding And Property Identity Join

- [x] 1. Add or strengthen model-binding target facts.
  Requirements: 3, 4, 8.
  - [x] Emit rule-backed MVC action parameter binding facts where static.
  - [x] Emit rule-backed Razor Page handler parameter binding facts where
    static.
  - [x] Emit `[BindProperty]`, page model, and view-model property target facts
    where static.
  - [x] Use the existing `csharp.razor.model-binding.v1` catalog rule for these
    sub-families, or add and review separate rule catalog entries before
    emitting facts under any new rule IDs.
  - [x] Preserve model/DTO family, containing type, property name, parameter
    source, rule ID, tier, file span, commit SHA, extractor ID/version, and
    safe metadata.

- [x] 2. Add UI binding/control to property identity joins.
  Requirements: 2, 3, 4.
  - [x] Verify every new TypeScript scanner fact type used in PR 1 fixtures has
    a corresponding rule catalog entry and limitations before any test exercises
    that fact type; if no new scanner fact is needed, record that explicitly.
  - [x] Connect Razor binding/form roots to model/view-model properties through
    static model metadata or documented syntax fallback.
  - [x] Downgrade same-name-only joins to `NeedsReviewLineage` when containing
    type identity, symbol ID, exact fact identity, alias evidence, or
    binding/value-origin evidence is absent.
  - [x] Emit `PropertyIdentityUnavailable`, `SameNameOnlyPropertyMatch`, or
    equivalent gaps when evidence is insufficient.

- [x] 3. Add payload/form to endpoint and DTO/model joins.
  Requirements: 3, 4, 5.
  - [x] Connect payload fields to HTTP calls through object-shape, body-field,
    query-field, route-parameter, or value-origin facts.
  - [x] Connect static Razor form targets to action/handler facts through
    normalized action, controller, page, handler, and method metadata.
  - [x] Connect endpoint/action/handler facts to DTO/model binding targets where
    rule-backed facts support the hop.
  - [x] Emit ambiguity and fan-out gaps instead of selecting hidden winners.

- [x] 4. Add PR 1 fixtures and tests.
  Requirements: 2, 3, 4, 5, 6, 8.
  - [x] Add synthetic Angular fixture coverage for event handler to payload to
    HTTP property hops.
  - [x] Add synthetic Razor/MVC/Pages fixture coverage for form target to
    action/handler/model-binding property hops.
  - [x] Test direct static hops, same-name review-tier hops, unsafe selector
    sanitized diagnostics, generic property-name downgrade, high fan-out at the
    v1 threshold, ambiguous DTO/model overlap, family exclusion, and missing
    property evidence gaps.
  - [x] Test `control:<name>` with `--framework razor`, proving Razor form
    control candidates are matched and Angular candidates are excluded.
  - [x] Test `dto:<type>.<property>` where the type exists only in the model
    family, proving DTO family exclusion.
  - [x] Test `model:<type>.<property>` where the type belongs to both model and
    DTO families, proving ambiguity metadata is visible in JSON.
  - [x] Test `fact:<combinedFactId>` as a strong disambiguator for a generic
    property name.
  - [x] Test a Razor form target with no matching action/handler, proving
    `EndpointAlignmentUnavailable` or equivalent gap output.
  - [x] Test a `[FromBody]` or `[FromForm]` parameter fact without a Razor form
    target referencing the action, proving UI selectors produce
    `SelectorNoMatch` and do not treat server-only model-binding target facts as
    UI roots.
  - [x] Test a combined index where `combined_route_flow_edges` is present but
    empty, proving route-flow-specific traversal remains unavailable.
  - [x] Test `model:<type>.<property>` where the type is present but no property
    facts exist, proving `SelectorNoMatch` or equivalent gap output.
  - [x] Test byte stability for a combined index containing model-binding target
    facts and the new node/edge shapes.
  - [x] Test high fan-out at 9 candidates and 10 candidates, proving the v1
    threshold is inclusive at 10.
  - [x] Test cross-source hops without endpoint-alignment evidence and older
    combined indexes missing optional route/path/property schema.
  - [x] Test deterministic root/path/gap ordering and byte-stable Markdown/JSON.
  - [x] Test safe rendering and private-path guard compatibility.

- [x] 5. Validate PR 1.
  Requirements: 8.
  - [x] Run focused property-flow tests.
  - [x] Run focused Razor/MVC/Pages extractor tests.
  - [x] Run focused TypeScript adapter tests when TypeScript extraction changes.
  - [x] Run `dotnet build` and `dotnet test` for the relevant solution/project.
  - [x] Run relevant adapter smoke checks from `docs/VALIDATION.md`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

### PR 2: Downstream Static Composition

- [ ] 6. Compose downstream static evidence.
  Requirements: 5, 6.
  - [ ] Reuse route-flow edges only when `combined_route_flow_edges` or a
    documented successor schema signal is present.
  - [ ] Include existing path/reverse evidence as supporting context where it
    establishes part of the downstream trail.
  - [ ] Show validation, mapping, service/repository, query, data-surface, and
    dependency-surface terminal evidence only when existing combined-graph facts
    already expose the property trail.
  - [ ] Defer new surface-specific traversal when existing facts do not expose a
    property-specific trail.
  - [ ] Emit route-flow, schema, coverage, mapper, and property-identity gaps
    when required evidence is absent.
  - [ ] Keep reducer impact claims out of property-flow output.

- [ ] 7. Validate downstream composition.
  Requirements: 5, 6, 8.
  - [ ] Test route-flow available and unavailable paths.
  - [ ] Test existing path/reverse support as context, not recomputed semantics.
  - [ ] Test terminal data/dependency evidence only when existing facts support
    the property-specific trail.
  - [ ] Test reduced coverage and missing optional schema gaps.

### PR 3: Report Consumer Compatibility

- [ ] 8. Preserve report and export contracts.
  Requirements: 6, 7.
  - [ ] Keep existing Markdown section order, including Coverage Warnings, and
    JSON top-level shape stable.
  - [ ] Add new node/edge/gap safe metadata using nullable/default-compatible
    fields or bump the report version.
  - [ ] Record whether new row kinds and metadata remain version `1.0`
    compatible or require a `1.1` report version bump before product code ships.
  - [ ] Update vault/docs-export/static explorer consumers only if needed for
    safe property-flow consumption.
  - [ ] Use existing consumer gap rules for schema-incompatible,
    unsupported-schema, or unavailable-family conditions when downstream
    generated artifact consumers cannot consume the new report safely.
  - [ ] Keep optional browser/computer-use evidence deferred and demo-only.

- [ ] 9. Validate report consumer compatibility.
  Requirements: 6, 8.
  - [ ] Test property-flow report backward-compatible additive rows.
  - [ ] Test downstream generated artifact consumers emit existing gap rules
    when a report version or field set cannot be consumed safely.
  - [ ] Test generated artifact safety and byte stability for touched consumers.

## Deferred Follow-Ups

- Optional browser/computer-use observed evidence as an opt-in demo workflow.
- Advanced Angular custom directive, pipe, and structural directive semantics.
- Runtime serializer configuration expansion.
- Dynamic model-binding gap expansion for custom tag helpers, reflection,
  generated code, runtime binding uncertainty, and additional
  `ViewBag`/`ViewData`/partial/editor-template cases not covered by existing
  Razor binding gaps.
- Deeper Angular component-member identity and direct event-handler assignment
  extraction through compiler symbol, direct assignment, argument-flow, alias,
  field-alias, or parameter-forwarding evidence. This slice uses existing
  Angular event, object-shape, and HTTP-call facts at report time only.
- Alias evidence as explicit supporting metadata, including serializer aliases,
  JSON names, bind aliases, form names, constructor parameter names, and mapper
  aliases, with tests proving aliases do not select hidden winners or suppress
  source rule IDs.
- Reflection or runtime DI solving.
- Branch feasibility or symbolic execution.
- Full taint analysis.
- Persisted derived property-flow rows behind an explicit write mode.
- Whole-application default property inventory.
- LLM classification, embeddings, vector databases, or prompt-based analysis.
