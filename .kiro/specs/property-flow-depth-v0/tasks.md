# Property Flow Depth v0 Tasks

Issue: #517

## Specification Slice

- [x] Reconcile #517 against the shipped Razor, semantic, property-flow,
      route-flow, storage, and consumer contracts.
- [x] Narrow ownership to compiler-resolved Razor model-binding targets,
      direct property mapping, and exact-ID property-flow composition.
- [x] Document provenance, privacy, determinism, gap, compatibility, and
      non-claim boundaries.
- [x] Separate direct mapping from deferred AutoMapper/Mapster/runtime mapping.
- [x] Record implementation ordering and validation requirements.
- [x] Complete an exact-head read-only Kiro advisory review and disposition its
      blocking/important findings in the specification.
- [ ] Complete hosted review and ACK for this spec-only PR.

## PR 1: Semantic Razor Model-Binding Producer

- [ ] Add a pinned test dependency/shared-framework metadata reference that
      supplies real signed ASP.NET Core symbols under locked restore; record
      its version as fixture provenance only.
- [ ] Pin the closed assembly-name/public-key-token/type/member/attribute
      allowlist; keep source-declared and unsigned framework shapes negative.
- [ ] Require controller/page-model plus action/handler ownership independently
      of binding-source attributes; test real attributes on non-endpoint helper
      methods produce zero Tier1 targets.
- [ ] Emit Tier1 semantic `RazorModelBindingTarget` evidence for admitted MVC
      action and Razor Page handler parameter properties across files/partials.
- [ ] Emit canonical owner, parameter, model type, and property roles with a
      closed property schema and fixed limitations.
- [ ] Add categorical gaps for unresolved/error/dynamic/type-parameter,
      external-unavailable, ambiguous, and forged framework shapes.
- [ ] Reconcile semantic rows with syntax fallback without duplicate hidden
      winners or evidence-tier inflation.
- [ ] Emit one row per owner/parameter/property binding source with sorted,
      bounded HTTP-method metadata; document the review-tier Tier1/Tier3
      reconciliation tuple.
- [ ] Add same-name cross-assembly, partial, overload/attribute, source-
      lookalike, semantic-unavailable, span, property-schema, catalog, storage,
      and file-order regressions.

## PR 2: Direct Property Mapping Producer

- [ ] Add a versioned mapping fact/rule and closed v0 property/gap schemas.
- [ ] Emit exact RHS-property -> LHS-property facts for simple assignments.
- [ ] Emit exact source-property -> initialized-target-property facts for
      object initializers and LINQ projection initializers.
- [ ] Unwrap only bounded identity-preserving syntax/conversions.
- [ ] Fail closed for transforming/dynamic/ambiguous/unsupported shapes without
      retaining expressions or protected digests.
- [ ] Add direction, collision, shadowing, projection, partial-build,
      generated/excluded-source, determinism, catalog, storage, and generic-
      reducer exclusion regressions.
- [ ] Define per-method/per-file bounds and emit an aggregated truncation gap.
- [ ] Prove the generic reducer explicitly excludes both new fact families and
      gap rules, preserves existing output, and cannot cross its fan-out
      threshold because these facts are present.

## PR 3: Exact-ID Property-Flow Composition

- [ ] Audit the current property-flow reader/report version and every consumer
      before changing row semantics.
- [ ] Prefer exact semantic Razor property identity over syntax/convention
      matching while preserving all supporting fact IDs and limitations.
- [ ] Add directed `model-property-mapped` hops only from admitted mapping
      facts; never reverse or infer them by name.
- [ ] Keep syntax/convention/same-name/alias-only evidence review-tier and emit
      explicit ambiguity/identity gaps.
- [ ] Order Tier1 exact candidates before bounded selection; emit truncation or
      fallback-suppression gaps instead of silently displacing existing rows.
- [ ] Keep v0 classifications unchanged unless a separately recorded report-
      compatibility decision and golden regression authorizes promotion.
- [ ] Attach existing route/service/query/data context only after a selected
      exact property trail reaches it.
- [ ] Test cross-assembly collisions, ambiguous endpoints/models, broad route
      reachability, direction, reduced coverage, bounds, byte stability, and
      exact provenance.

## PR 4: Consumer and Public-Safe Validation

- [ ] Audit NDJSON, SQLite, combine, Markdown, snapshot diff, reducer, property-
      flow, docs/vault, evidence-pack, explorer, release review, rule-catalog
      validators, demo summaries/site refresh fixtures, and
      `docs/VALIDATION.md` behavior.
- [ ] Preserve rule/tier/span/commit/extractor/coverage/support IDs/limitations
      or emit documented omission/compatibility gaps.
- [ ] Add a small public-safe MVC/Razor fixture backed by real signed test
      metadata, plus source-lookalike, collision, dynamic, and direction
      negatives.
- [ ] Run focused Razor/semantic/property-flow/storage/combine/consumer tests.
- [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
- [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] Run applicable pinned smoke checks from `docs/VALIDATION.md` or record a
      bounded deferral.
- [ ] Run `./scripts/check-private-paths.sh` and `git diff --check`.
- [ ] Update this task file and `implementation-state.md` only as work lands.

## Deferred

- AutoMapper, Mapster, custom mapping packages/configuration/resolvers.
- Arbitrary interprocedural transformations and business-rule inference.
- Runtime Razor/model binding, serialization, validation, browser, HTTP, DI,
  mapper execution, persistence, database, and telemetry evidence.
- Persisted derived property-flow rows and whole-application inventory UI.
- Constructor forwarding and legacy `System.Web.Mvc` until each has a separate
  exact identity/flow contract.
