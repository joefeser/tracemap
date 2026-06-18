# Interface, Override, and DI Approximation Tasks

## Spec Authoring Tasks

- [x] 0.1 Create the spec folder under `.kiro/specs/interface-override-di-approximation/`.
- [x] 0.2 Draft requirements for relationship extraction, DI registration evidence, dispatch candidates, combined schema, consumers, classifications, safety, tests, and non-goals.
- [x] 0.3 Draft design notes covering implementation slices, rule/fact model, candidate derivation, gaps, schema direction, safety, and validation.
- [x] 0.4 Draft implementation tasks while leaving future product-code work unchecked.
- [x] 0.5 Add spec-local implementation state for branch, scope decisions, validation, and follow-up items.
- [x] 0.6 Run Kiro Opus spec review and patch Medium+ findings when available.
- [x] 0.7 Run Kiro Sonnet spec review and patch Medium+ findings when available.
- [x] 0.8 Run at most two Kiro re-review cycles, choosing Sonnet for final re-review unless Opus is clearly needed.
- [x] 0.9 Run `git diff --check`.
- [x] 0.10 Run `./scripts/check-private-paths.sh` when present.

## Implementation Plan

### Phase 1: Baseline Audit and Tests

- [ ] 1.1 Audit current C# relationship extraction, rule catalog entries, SQLite rows, combine import, export, paths, reverse, route-flow, impact/include-paths, and report consumers.
- [ ] 1.2 Add fixture tests that lock current behavior for direct interface implementation, explicit interface implementation, inheritance, overrides, and current relationship export/combine behavior.
- [ ] 1.3 Add byte-stability tests for relationship facts, symbol relationship rows, combined relationship rows, and exported relationship evidence.
- [ ] 1.4 Document any existing behavior gaps before changing scanner or graph code.

### Phase 2: Relationship Evidence Hardening

- [ ] 2.1 Ensure C# semantic relationship facts include source/target symbol IDs, relationship kind, rule ID, evidence tier, file span, extractor name, and extractor version.
- [ ] 2.2 Cover direct class inheritance, interface implementation, interface inheritance via `ExtendsInterface`, member overrides, and interface member implementations.
- [ ] 2.3 Add or update tests for explicit interface implementation, generic interface implementation, default interface methods, abstract overrides, virtual overrides, and partial types.
- [ ] 2.4 Emit `AnalysisGap` facts when semantic relationship extraction cannot run because project load, references, generated sources, or semantic model binding is reduced.
- [ ] 2.5 Decide whether Tier3 syntax-only relationship candidates are needed; if yes, add a separate rule catalog entry and tests proving review-tier behavior.

### Phase 3: DI Registration Extraction

- [ ] 3.1 Audit existing `DependencyRegistered` emission under `csharp.semantic.runtimeevidence.v1`, then define any missing registration method families and shapes for generic and `typeof(...)` service/implementation registrations.
- [ ] 3.2 Extend the existing `DependencyRegistered` fact contract only where needed for supported semantic registration shapes with service symbol, implementation symbol when available, lifetime, registration shape, file span, rule ID, tier, extractor name, and extractor version.
- [ ] 3.3 Emit review/gap evidence for implementation-only, self, instance, and open-generic registrations without overclaiming runtime binding.
- [ ] 3.4 Emit gaps or flow-boundary facts for factories, lambdas, scanning, conditional branches, keyed/named services, decorators, service locators, custom containers, configuration, and reflection-built types.
- [ ] 3.5 Add tests for multiple registrations, high fan-out, unsupported shapes, reduced semantic coverage, deterministic ordering, and byte-stable DI registration rows.
- [ ] 3.6 Add safety tests proving raw expressions, config values, URLs, hostnames, local paths, remotes, snippets, and secrets are omitted or hashed.

### Phase 4: SQLite, Export, and Combine Contract

- [ ] 4.1 Preserve relationship and registration evidence in single-language SQLite indexes with stable nullable columns and sorted properties; for relationship rows, audit and test whether `relationship_id = fact_id` plus the backing fact row satisfies extractor metadata and supporting fact ID requirements.
- [ ] 4.2 Add an additive precise DI registration table only if fact properties and fact-symbol attachments are not sufficient.
- [ ] 4.3 Update combined index import to preserve registration evidence and source/language namespacing; if a precise registration table is added, define its combined table columns per Requirement 5 AC 2 before implementation.
- [ ] 4.4 Ensure older indexes remain readable with schema gaps and fact fallback where possible.
- [ ] 4.5 Update export models to include safe relationship, registration, candidate, and gap evidence.
- [ ] 4.6 Add tests for schema compatibility, combined source namespacing, source-local symbol matching, and deterministic export ordering.
- [ ] 4.7 Add tests proving relationship kind values remain in the documented closed set or are mapped through an explicit compatibility layer.

### Phase 5: Shared Candidate Graph Builder

- [ ] 5.1 Add or extract a shared reporting/query service that derives candidate dispatch edges from call edges, symbol relationships, DI registration facts, and source coverage.
- [ ] 5.2 Derive interface member implementation candidates only from supported relationship evidence.
- [ ] 5.3 Derive override candidates only from supported override relationship evidence.
- [ ] 5.4 Annotate candidates with DI registration support only when service/implementation symbols and relationship compatibility are proven.
- [ ] 5.5 Emit gaps for missing implementation candidates, missing override candidates, unsupported registration shapes, runtime binding not proven, ambiguous candidates, high fan-out, missing schema, and reduced coverage.
- [ ] 5.6 Keep candidate derivation deterministic, bounded, cycle-safe, and source-local unless a future rule defines cross-source identity.
- [ ] 5.7 Add tests for candidate ordering, fan-out caps, ambiguity, reduced coverage, and no-candidate behavior under full versus reduced coverage.
- [ ] 5.8 Add tests for registration-supported candidate labels, registration-supported-before-relationship-only ordering, and `RegistrationCompatibilityUnproven` gaps.
- [ ] 5.9 Add tests proving scanner-level `DynamicDispatchCandidate` facts are not conflated with derived combined candidate edges.

### Phase 6: Consumer Integration

- [ ] 6.1 Update `tracemap paths` to traverse candidate edges with `NeedsReviewPath` cap and candidate limitations.
- [ ] 6.2 Update `tracemap route-flow` to consume the shared candidate builder through `combined.route-flow.interface-bridge.v1` or a documented successor.
- [ ] 6.3 Update `tracemap reverse` to traverse candidate edges with `NeedsReviewReversePath` cap and runtime-binding limitations.
- [ ] 6.4 Update `tracemap impact --include-paths` so candidate path context does not become definite impact by itself.
- [ ] 6.5 Update combined report and portfolio report summaries with relationship counts, DI registration counts, candidate counts, fan-out, gaps, and limitations.
- [ ] 6.6 Update evidence graph and vault export to render candidate edges and gaps safely with rule IDs, tiers, supporting IDs, and limitations.
- [ ] 6.7 Add consumer tests proving classifications remain conservative and wording avoids runtime certainty.
- [ ] 6.8 Add forbidden-wording tests for DI-supported candidate edges across paths, route-flow, reverse, impact/include-paths, report, and export.

### Phase 7: Documentation and Rule Catalog

- [ ] 7.1 Update `rules/rule-catalog.yml` for any new or changed rules, including documented limitations.
- [ ] 7.2 Update `docs/ACCEPTANCE.md` with relationship, DI registration, candidate edge, and consumer acceptance scenarios.
- [ ] 7.3 Update `docs/LANGUAGE_ADAPTER_CONTRACT.md` if adapter fact requirements change.
- [ ] 7.4 Update `docs/VALIDATION.md` with pinned smoke guidance if shared graph, report, or adapter behavior changes.
- [ ] 7.5 Add CLI/help or README documentation only after behavior is implemented.
- [ ] 7.6 Document dispatch candidate fan-out thresholds, inherited-dispatch traversal depth, result caps, and ambiguity thresholds in the applicable rule catalog entries before product code emits the behavior.

### Phase 8: Validation

- [ ] 8.1 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 8.2 Run the CLI scan against at least one public-safe sample repo and confirm required scan artifacts are emitted.
- [ ] 8.3 Run relevant pinned smoke checks from `docs/VALIDATION.md` for language-adapter, graph, report, export, paths, route-flow, reverse, or impact changes.
- [ ] 8.4 Run `./scripts/check-private-paths.sh`.
- [ ] 8.5 Run `git diff --check`.
- [ ] 8.6 Confirm generated outputs contain no raw source snippets, raw config/SQL values, secrets, URLs, hostnames, raw remotes, private labels, or local absolute paths.

## Deferred Follow-Ups

- Runtime DI container execution or validation.
- Environment/profile-specific registration evaluation.
- Dynamic proxy expansion.
- Keyed/named service semantics beyond safe review context.
- Custom container plugin model.
- Cross-language runtime binding.
- Persisted derived candidate graph rows.
- UI or graph visualization beyond existing exports.
