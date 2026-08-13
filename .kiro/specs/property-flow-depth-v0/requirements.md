# Property Flow Depth v0 Requirements

## Goal

Implement the remaining bounded scope of issue #517: improve Razor
action/handler model-binding identity and direct C# property-to-property mapping
evidence, then let `tracemap property-flow` compose those facts without short-
name guesses.

This runway deepens deterministic static evidence. It does not prove runtime
model binding, route selection, mapper execution, dependency-injection target,
database access, browser behavior, business intent, or impact.

## Existing Baseline

The shipped baseline already provides:

- Razor `asp-for`/`Html.*For` bindings and form-target metadata;
- syntax/convention `RazorModelBindingTarget` facts, including explicit gaps
  for cross-file and ambiguous syntax-only parameter types;
- `PropertyDeclared`, `PropertyAccessed`, `ParameterDeclared`, object creation,
  argument flow, local/field aliases, endpoint alignment, and route-flow facts;
- `tracemap property-flow` report version `1.0`, deterministic selectors,
  first-hop Razor/Angular joins, coverage gaps, and safe Markdown/JSON; and
- property-flow consumers in docs export, vault export, release review, and the
  static evidence explorer.

The current syntax model-binding extractor is useful reduced evidence, not a
compiler-resolved cross-file property identity contract. The current semantic
extractor records property access but does not state that one property is
mapped to another. Those are the two producer gaps owned here.

## Requirement 1: Exact Semantic Razor Admission

1. A Tier1 Razor model-binding target SHALL independently establish the owning
   framework surface and any binding-source attribute used for classification:
   an MVC parameter requires an admitted controller and action; a Razor Page
   handler parameter requires an admitted PageModel and handler; a PageModel
   property requires an admitted PageModel plus canonical property and
   `[BindProperty]` identity but SHALL NOT require or synthesize a handler.
   A binding attribute alone SHALL NOT establish MVC endpoint ownership.
2. Framework identity SHALL include an allowlisted metadata type and assembly
   identity. Source-declared, unsigned same-name, namespace-only, suffix-only,
   and unresolved lookalikes SHALL NOT qualify for Tier1 evidence.
3. The first implementation SHALL admit only a closed ASP.NET Core assembly-
   name allowlist, the Microsoft public-key token, metadata-only symbols, and
   exact type/member/attribute signatures. Tested package/runtime versions
   SHALL be pinned as fixture provenance, but version SHALL NOT participate in
   the admission predicate. The initial candidate assembly set is
   `Microsoft.AspNetCore.Mvc.Core`, `Microsoft.AspNetCore.Mvc.Abstractions`,
   `Microsoft.AspNetCore.Mvc.ViewFeatures`,
   `Microsoft.AspNetCore.Mvc.RazorPages`, and
   `Microsoft.AspNetCore.Http.Abstractions`; PR 1 SHALL narrow rather than
   silently expand this set after inspecting the signed fixture symbols.
4. A controller action or page handler parameter SHALL expand to properties
   only when Roslyn resolves the parameter type and property symbols. Cross-
   file and partial source types MAY qualify; error types, type parameters,
   dynamic types, inaccessible metadata-only shapes, and ambiguous candidates
   SHALL emit a categorical gap rather than a target guess.
   An expanded model property SHALL be an ordinary public instance property
   with no index parameters, no ref/ref-readonly return, and a public,
   non-init setter admitted by the binding source. Static, indexer, read-only,
   init-only, inaccessible, explicit-interface, and otherwise unsupported
   property shapes SHALL NOT emit Tier1 targets. Constructor/record binding and
   collection-element expansion remain unsupported until separately specified.
5. `[BindProperty]`, `[FromBody]`, `[FromForm]`, and any later admitted binding
   attributes SHALL be recognized only by canonical attribute symbol identity,
   not attribute text. Parameter attributes classify binding source only after
   the containing controller/action or PageModel/handler independently
   qualifies. PageModel property binding instead requires canonical PageModel,
   property, and `[BindProperty]` identity and remains handler-independent.
6. Each target SHALL preserve canonical owner method/property/type IDs,
   assembly identity, binding kind, model family, parameter source, controller/
   action or page/handler identity, HTTP method evidence where available,
   repository-relative span, rule ID, tier, commit SHA, extractor version,
   coverage label, supporting fact IDs where materialized, and limitations.
   One fact SHALL represent one resolved target property for one owner and
   parameter/property binding source. HTTP methods SHALL be a sorted,
   deduplicated bounded property on that fact rather than multiplying facts.
   Handler-independent PageModel property targets SHALL NOT synthesize handler
   HTTP methods. They SHALL record a closed `supportsGet` boolean from the
   canonical `[BindProperty]` named argument (default `false`); composition
   SHALL treat absent HTTP-method rows as property-owned evidence, not as proof
   that GET binding is unavailable.
7. Semantic evidence SHALL coexist with syntax fallback deterministically. A
   compatible Tier1 target SHALL be preferred for strong joins; a syntax row
   SHALL not create a second hidden winner or upgrade an incompatible semantic
   gap.

## Requirement 2: Direct Property Mapping Evidence

1. A property mapping fact SHALL require Roslyn to resolve both source and
   target as properties with canonical symbol and assembly identities.
2. v0 SHALL support only these direct shapes:
   - simple assignment: `target.Property = source.Property`;
   - object initializer member assignment, including inside a LINQ projection:
     `new Target { Property = source.Property }`.
   Constructor forwarding MAY be added in a later bounded slice only when an
   exact constructor parameter-to-target-property assignment is independently
   visible and both hops are preserved.
3. Parentheses, null-forgiving syntax, and identity conversions MAY be unwrapped
   only when Roslyn preserves the same source property symbol. Arbitrary method
   calls, conditional expressions, arithmetic, interpolation, reflection,
   dynamic access, collection transforms, custom resolvers, and runtime mapping
   configuration SHALL not become direct mapping facts.
4. Each mapping SHALL preserve source and target property IDs, containing type
   IDs, assembly identities, containing method ID, closed mapping shape,
   direction, source span, rule ID, tier, commit SHA, extractor version,
   coverage label, and limitations.
5. Same-name properties without an admitted mapping shape SHALL not produce a
   mapping fact. Multiple candidate symbols or incomplete semantic binding
   SHALL fail closed.
6. Recognized mapper/projection boundaries that cannot expose one exact direct
   source and target MAY emit an aggregated categorical gap. Gaps SHALL not
   retain source expressions, snippets, constant values, configuration values,
   or hashes derived from protected content.
7. The producer SHALL define a deterministic per-method and per-file emission
   bound before implementation. Exceeding a bound SHALL emit an aggregated,
   rule-backed truncation gap; generated or excluded sources SHALL follow the
   scanner's recorded scope policy and SHALL NOT be silently analyzed.

## Requirement 3: Property-Flow Composition

1. `tracemap property-flow` SHALL prefer exact canonical property IDs when
   joining Razor binding, model-binding, declared-property, and property-mapping
   facts.
2. A Tier1 semantic Razor target MAY strengthen a property-specific hop only
   when its exact property identity agrees with the selected root/model
   identity and any form/endpoint evidence used by the path.
3. A direct property mapping MAY add a `model-property-mapped` hop only from its
   recorded source property to its recorded target property. Reverse direction
   SHALL not be inferred unless a separate fact proves it.
4. Same-name, convention-only, syntax-only, alias-only, or family-only matches
   SHALL remain `NeedsReviewLineage` or an explicit gap. They SHALL not be
   upgraded merely because a stronger unrelated fact exists in the index.
5. Ambiguous model targets, mapping targets, endpoint matches, or property IDs
   SHALL preserve bounded candidates and emit gaps rather than select a hidden
   winner.
6. Backend route/service/query/data/dependency context MAY be reused only after
   the selected trail reaches it through exact property-specific evidence.
   Broad endpoint reachability remains insufficient.
7. Existing `property-flow` version `1.0` MAY be retained only if additions are
   backward-compatible rows/metadata and every current consumer safely ignores
   or preserves them. Otherwise the implementation SHALL version the report or
   emit a compatibility gap.
8. Exact semantic rows SHALL not automatically promote an existing path's
   classification in v0. They add exact supporting identity and directed hops;
   any classification promotion requires a separately documented compatibility
   decision and regression baseline.
9. Candidate ordering SHALL prefer stronger exact evidence before applying
   existing row/path limits. If eligible candidates are displaced by a bound,
   the report SHALL emit a deterministic truncation gap. Existing same-name
   fallback SHALL remain review-tier or be represented by an explicit gap; it
   SHALL not silently disappear because a different strong path exists.

## Requirement 4: Evidence, Gaps, and Determinism

1. New evidence SHALL use versioned rule IDs with catalogued limitations before
   product output emits it.
2. Proposed producer rules are
   `csharp.razor.semantic-model-binding.v1` and
   `csharp.semantic.propertymapping.v1`; implementation MAY reuse an existing
   rule only if its documented semantics and property schema are sufficient.
   The existing `csharp.razor.model-binding.v1` catalog entry SHALL be narrowed
   to its actual syntax semantics if it no longer truthfully describes all
   `RazorModelBindingTarget` rows.
3. Producer failures SHALL be `AnalysisGap` facts with PascalCase `gapKind`
   values. Existing `RazorBindingGap` facts retain their kebab-case markup-
   parser vocabulary; the two fact types SHALL NOT share an undocumented gap
   namespace. New `AnalysisGap` kinds SHALL be a closed vocabulary. Initial
   candidates are:
   `RazorFrameworkIdentityUnavailable`, `RazorBindingTypeUnavailable`,
   `RazorBindingPropertyUnavailable`, `RazorBindingExternalBaseUnavailable`,
   `RazorEndpointOwnerUnavailable`, `RazorBindingTargetTruncated`,
   `RazorBindingGapTruncated`, `AmbiguousRazorBindingTarget`,
   `PropertyMappingSemanticUnavailable`, `PropertyMappingShapeUnsupported`,
   `PropertyMappingTargetAmbiguous`, `PropertyMappingCoverageReduced`, and
   `PropertyMappingTruncated`.
4. Gaps SHALL use `Tier4Unknown`, a rule ID, coverage label, safe scope
   identity, deterministic occurrence count where aggregation applies, and
   fixed limitations.
5. Fact IDs and ordering SHALL remain byte-stable across repeated scans and
   file enumeration order. Identity inputs SHALL use canonical symbols,
   mapping shape, direction, source coordinates, and manifest scan identity.
6. Same-name cross-assembly types/properties, overloads, partial types,
   generated/source mixtures, aliases, shadowing, and semantic-unavailable
   fixtures SHALL not collide or silently upgrade evidence.
7. Tier1/Tier3 reconciliation SHALL use only the contextual fields both tiers
   actually carry: owner/action or handler labels, parameter/source labels,
   model/property labels, and compatible repository-relative spans. Tier3
   lacks canonical owner identity, parameter ordinal, and canonical model-type
   display identity, so it cannot form or select an exact reconciliation key.
   It remains contextual support only; only an independently selected Tier1
   fact governs an exact join, and reconciliation SHALL never discard either
   source fact.
8. The generic contract-delta reducer SHALL explicitly exclude the new fact
   types and their producer-gap rule IDs until a reducer contract admits them.
   Existing reduce outputs and high-fan-out classifications SHALL remain
   byte-identical when these unrelated facts are present.
9. Until the exact-ID reader in Requirement 3 is implemented, the existing
   property-flow reporter SHALL explicitly exclude semantic
   `RazorModelBindingTarget` rows by their producer rule ID. PR 1 SHALL prove
   existing property-flow rows, paths, classifications, and bounds remain
   unchanged when semantic rows are present. PR 3 SHALL remove that temporary
   exclusion only in the same change that adds canonical-ID admission and
   collision regressions.

## Requirement 5: Safety and Non-Claims

1. No output SHALL retain raw source snippets, raw Razor/HTML, submitted values,
   validation values, secrets, connection material, raw URLs, local absolute
   paths, private infrastructure identity, arbitrary mapping expressions, or
   protected-value digests.
   Hashing is limited to repository-defined scan/scope identity inputs and
   existing canonical symbol IDs; arbitrary source expressions, values, and
   private labels SHALL NOT be newly hashed. Canonical symbol IDs may appear
   only in consumers whose established public-safe allowlist already admits
   them.
2. No output SHALL claim runtime binding success, serializer behavior,
   validation success, handler execution, route reachability, mapper execution,
   object persistence, database execution, business intent, correctness,
   completeness, impact, safety, or release approval.
3. No implementation SHALL add LLM calls, embeddings, vector databases, or
   prompt-based classification to scanner, reducer, report, or export paths.
4. Partial analysis remains useful but SHALL be labeled reduced/partial and
   SHALL cap absence conclusions.

## Requirement 6: Validation

1. Producer fixtures SHALL cover exact cross-file MVC and Razor Pages targets,
   partial models, same-name cross-assembly lookalikes, source framework
   lookalikes, binding attributes, error types, and deterministic fallback.
   Positive Tier1 tests SHALL reference real signed ASP.NET Core metadata from
   a pinned test dependency/shared-framework reference available during locked
   restore. Source-declared framework lookalikes remain negative fixtures.
   Negative eligibility fixtures SHALL include static, indexer, read-only,
   init-only, non-public-setter, explicit-interface, ref-return, constructor/
   record, and collection-element shapes. PageModel fixtures SHALL cover
   default and constant `SupportsGet = true` without synthesized handlers.
2. Mapping fixtures SHALL cover direct assignment, object initializer, LINQ
   projection, same-name non-mapping, reversed
   direction, conversion wrappers, dynamic/unsupported expressions, ambiguity,
   and file-order determinism.
3. Composition fixtures SHALL prove exact-ID joins, downgrade syntax/convention
   evidence, reject short-name/cross-assembly collisions, preserve direction,
   and attach no broad endpoint context without a property bridge.
4. Consumer tests SHALL cover report JSON/Markdown, storage/combine, docs/vault,
   release review, explorer, evidence packs, snapshot diff, rule-catalog
   validators, demo summary generation, and site demo-summary refresh behavior
   for any changed fact or row contract. Reporter metadata allowlists SHALL
   explicitly decide whether `mappingShape`, `direction`, `bindingKind`, and
   assembly identity are safe row fields or fact-only fields.
5. Validation SHALL run focused Razor, semantic extraction, property-flow,
   combine/storage, and touched consumer tests; full .NET build/test; applicable
   pinned smokes from `docs/VALIDATION.md`; private-path guard; and diff check.
6. Reducer regressions SHALL prove explicit exclusion of both new fact
   families/gap rules, unchanged existing reduce output, and no fan-out-driven
   `DefiniteImpact` downgrade after more than ten unrelated new facts.
7. Property-flow regressions SHALL cover unchanged v1.0 golden rows/counts,
   tier-aware bounded selection, preserved-or-gapped same-name fallback,
   directed and reversed assignments, no synthesized reverse edge, source-
   index scoping across combined inputs, and persisted new-property round trip.
8. Staged-delivery regressions SHALL prove PR 1 semantic targets cannot enter
   the current name-joining property-flow reader; removing that isolation and
   admitting the rule is part of PR 3's exact-ID composition change.

## Deferred

- AutoMapper, Mapster, custom mapper packages, custom resolvers, profiles, and
  runtime configuration until exact package/signature contracts are pinned.
- Interprocedural mapping through arbitrary helpers or repository abstractions.
- Runtime model-binding, serializer, validation, browser, HTTP, DI, or database
  observation.
- Constructor forwarding until both exact hops fit one documented identity
  contract, and legacy ASP.NET MVC (`System.Web.Mvc`) until its distinct
  framework identity, binding semantics, and fixtures are specified.
- Whole-application property inventory UI and persisted derived flow rows.
