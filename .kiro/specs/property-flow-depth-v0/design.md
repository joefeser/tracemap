# Property Flow Depth v0 Design

## Boundary

This spec extends the C# evidence producers and the existing property-flow
reporter. It does not replace the shipped syntax Razor extractor or the
property-flow traversal engine.

```text
Razor markup root
  -> existing structural binding/form target
  -> new semantic action/handler property target
  -> new direct property mapping edge(s)
  -> existing property-specific route/service/data context
```

Every arrow requires its own rule-backed fact. Missing arrows remain gaps.

## Current Contract Audit

At base `8eb8a72d85878f69f1a2d2851089e269d1f26a78`:

- `RazorBindingExtractor` emits structural `RazorBinding`, `RazorFormTarget`,
  and `RazorBindingGap` facts from `.cshtml`.
- `CSharpSyntaxExtractor.AddRazorModelBindingTargetFacts` emits Tier3
  `RazorModelBindingTarget` rows. It expands only unique same-file parameter
  types, uses safe convention metadata, and emits explicit cross-file or
  ambiguous-type gaps.
- `CSharpSemanticExtractor` emits declarations, property access, argument
  flow, parameter forwarding, object creation, local aliases, and field aliases
  with canonical Roslyn identities. It does not emit a direct property-mapping
  relationship.
- `PropertyFlowReport` can join Razor roots/form targets to model-binding facts,
  but several joins compare model/property/action metadata. These remain
  probable or review-tier and can encounter same-name collisions.
- Report version `1.0` and existing docs/vault consumers already tolerate
  deterministic property-flow paths and gaps, but new source fact families and
  metadata require an explicit consumer audit.

The older `ui-field-property-lineage-composition` spec is superseded as an
active queue. Its task 4 Razor depth and tasks 9-10 compatibility work are
owned here; its tasks 2-3 and 5-8 were either shipped by the recorded successor
slices or remain outside #517's narrowed producer scope. Its implementation
state is updated in this PR so two specs do not claim active ownership. This
spec does not reopen completed Angular, route-flow, export, or terminal-context
work.

## Slice A: Semantic Razor Model-Binding Producer

Add a semantic pass adjacent to the current C# semantic extraction. The pass
examines compiler symbols rather than text:

1. Admit an owner only through one of three closed surfaces: controller plus
   action for MVC parameters; PageModel plus handler for handler parameters; or
   PageModel plus canonical property and `[BindProperty]` for PageModel-bound
   properties. A property-bound PageModel target has the property as its owner
   and does not require or synthesize a handler. All framework roles resolve to
   metadata-backed semantics in the closed ASP.NET Core assembly-name/public-
   key-token/signature allowlist. Runtime/package version is fixture provenance,
   not an admission key. A real binding attribute on a helper/service method
   cannot establish endpoint ownership.
2. Admit an action/handler only through exact method/attribute/base semantics
   documented by the implementation fixture. Method-name conventions alone
   may remain syntax fallback but cannot yield Tier1.
   After parameter ownership qualifies, canonical `[FromBody]` or `[FromForm]`
   identity may classify its binding source; it cannot admit the owner or
   method. Canonical `[BindProperty]` participates only in the separate
   PageModel-property admission contract.
3. Resolve parameter/property types through Roslyn, including cross-file and
   partial source types.
4. Apply a binding-source-aware eligibility predicate before expansion:
   ordinary public instance property, no index parameters or ref return, and a
   public non-init setter. Unsupported/static/indexer/read-only/inaccessible/
   constructor-bound shapes produce no Tier1 target and a bounded categorical
   gap when the source boundary was otherwise admitted.
5. Emit one target per eligible source property symbol and owner/parameter binding
   source with canonical owner, parameter, type, and property roles. Preserve
   supported HTTP methods as one sorted bounded property, not row multiplication.
6. Emit categorical gaps for error/dynamic/type-parameter/external-unavailable
   shapes without guessing a property.

Prefer reusing `RazorModelBindingTarget` so downstream selectors retain their
family. Use a new semantic rule and extractor version so Tier1 rows are
distinguishable from syntax fallback. The positive fixture SHALL obtain real
signed framework metadata through a pinned test dependency or shared-framework
metadata reference available under locked restore, following existing metadata-
reference fixture precedent. A source-declared synthetic framework is retained
only as a forged-framework negative case. The exact property schema is closed
in PR 1 after that fixture confirms available symbols.

Minimum safe fields:

| Role | Required evidence |
| --- | --- |
| owner | canonical action/handler method ID, or PageModel property ID for property binding, plus assembly identity |
| parameter | canonical parameter ID/ordinal/source classification when parameter-bound; absent for PageModel-property binding |
| model type | canonical named-type ID and assembly identity |
| target | canonical property ID, type, containing-type ID |
| binding | closed binding kind/model family/HTTP evidence |
| provenance | repo-relative span, commit, extractor, rule, tier, coverage, limitations |

For a handler-independent PageModel property, do not manufacture handler or
HTTP-method rows. Read the canonical `[BindProperty]` attribute data and retain
only closed `supportsGet: true|false` evidence (`false` when the named argument
is absent). Later composition must use that property-owned semantic rather than
inherit the syntax producer's POST convention or treat missing `httpMethods` as
a negative GET claim.

Syntax fallback remains independently attributable. If the same logical target
has both Tier1 and Tier3 rows, composition selects by exact canonical target ID
and preserves both supporting IDs; it does not delete historical facts or
pretend syntax evidence became semantic.

### Review-tier reconciliation boundary

PR 1 does not claim that the existing Tier3 syntax row can form the Tier1
canonical reconciliation identity. The tiers share only contextual fields:
binding kind, controller/action or page/handler label, parameter name/source,
simple model label, property name/path, and compatible source location. Tier3
does not carry a canonical owner ID, parameter ordinal, or canonical model-type
display identity. Therefore it remains contextual supporting evidence only;
it cannot select, replace, or upgrade a Tier1 target. The Tier1
`reconciliationProfileVersion` names this staged profile and records
`tier3ReconciliationState = contextual-support-only-missing-canonical-identity`.
PR 3 may preserve a compatible Tier3 supporting fact only after an exact Tier1
target is independently selected; ambiguity must remain a gap.

PR 1 is independently safe to merge: the existing property-flow reporter must
exclude semantic rows by the new producer rule ID because its current
`RazorModelBindingTarget` path still joins display metadata. The facts remain
available in NDJSON/SQLite, but cannot create name-based paths. PR 3 removes
this temporary reader exclusion only alongside exact canonical-ID admission,
collision fixtures, and unchanged-v1 compatibility evidence.

## Slice B: Direct Property Mapping Producer

Add a small Roslyn operation/syntax projector for direct mappings. The producer
uses `IPropertySymbol` identities for both ends and never infers by labels.

Supported shapes:

| Shape | Source | Target | Direction |
| --- | --- | --- | --- |
| simple assignment | resolved RHS property | resolved LHS property | RHS -> LHS |
| object initializer | resolved RHS property | initialized member property | RHS -> initialized member |
| LINQ projection | same object-initializer rule inside lambda | initialized member property | RHS -> initialized member |

The implementation starts with assignment/object-initializer shapes.
Constructor forwarding is a named later slice and is not required for v0.

Unwrap only parentheses, null-forgiving syntax, and Roslyn identity conversions.
Do not classify method calls or expression transformations as direct mappings.
A recognized mapping context with unsupported shape may emit one aggregated gap
per containing method/shape. Ordinary assignments unrelated to two resolved
properties produce neither mapping facts nor migration-style global noise.

Proposed fact type: `PropertyMappingDeclared`. Its producer applies documented
per-method/per-file bounds and emits `PropertyMappingTruncated` rather than
silently dropping eligible facts. Generated and excluded sources follow the
scanner's existing recorded scope decision.

Proposed closed fields:

- `mappingShape`: `assignment`, `object-initializer`, `projection`, or a later
  versioned value;
- `direction`: `source-to-target`;
- canonical `sourcePropertySymbolId` and `targetPropertySymbolId`;
- source/target containing-type symbol and assembly identities;
- containing method symbol/assembly identity;
- `coverageLabel`: `bounded-static-property-mapping`; and
- one fixed limitation string stating that mapping execution, object creation,
  runtime values, persistence, and business meaning are not proven.

The top-level fact source is the source property display symbol, target is the
target property display symbol, and contract element is the closed mapping
shape. Canonical IDs—not these display fields—govern joins.

## Slice C: Exact-ID Property-Flow Composition

Extend property-flow read models to load the new canonical fields. Add paths
only when IDs agree:

- structural Razor binding with a compatible model type/property target;
- form target with compatible endpoint/semantic binding owner;
- semantic target property to direct property mapping source;
- mapping target to existing property-specific value/call/query evidence.

New mapping edges use existing `property-flow.edge.v1` unless the catalog
review proves a new rule is required. Suggested edge kinds are
`semantic-model-binding-target` and `model-property-mapped`.

Classification:

- Tier1 canonical binding plus Tier1 direct mapping adds exact support and
  directed hops in v0; it does not by itself promote an existing path
  classification. A later promotion requires a separate compatibility decision.
- Syntax/convention fallback, type-family matching, aliases, or names remain
  `NeedsReviewLineage`.
- No exact bridge means a gap; the reporter does not search globally for the
  closest property name.

Report version `1.0` remains only if new rows/metadata are additive and current
consumers are verified. The implementation-state audit owns that decision.
Candidate selection orders exact Tier1 evidence ahead of weaker candidates
before applying bounds. Displaced candidates and suppressed fallback paths
must be represented by deterministic truncation/coverage gaps.

## Gap Contract

Producer gaps are `AnalysisGap` facts with PascalCase closed `gapKind` values,
source rules, and Tier4. Existing kebab-case `RazorBindingGap` values remain a
separate markup-parser contract. Reporter gaps reuse
`property-flow.edge.v1`, `property-flow.coverage.v1`, or
`property-flow.schema.v1` when their limitations fit.

Gap rows contain only closed classifications, existing canonical symbol or
scan/scope identities already admitted by consumer allowlists, occurrence
count, provenance, coverage, and limitations. They do not store raw
expressions, arbitrary exception messages, private-label digests, or hashes of
protected content. Both new fact families use the property key `limitations`.

## Compatibility and Safety

- NDJSON/SQLite/combine must preserve new facts generically and deterministically.
- Generic reducers must explicitly exclude both new fact families and their
  producer-gap rule IDs until a reducer contract admits them. Existing reducer
  output and fan-out classification are regression-locked.
- Markdown may count facts but must not render unsafe properties.
- Property-flow, explorer, docs/vault, evidence packs, release review, snapshot
  diff, rule-catalog validators, demo summaries, site refresh fixtures, and
  `docs/VALIDATION.md` must preserve the new evidence contract or emit
  omission/compatibility gaps.
- Property-flow report metadata is allowlisted: `mappingShape`, `direction`,
  `bindingKind`, and assembly identity require an explicit safe-output decision
  rather than implicit passthrough.
- Each staged PR is consumer-safe on its own. PR 1 isolates its semantic rule
  from the current name-based property-flow reader; PR 3 owns removal of that
  isolation and exact-ID consumption in one reviewed change.
- Existing raw-snippet defaults remain unchanged.

## Adversarial Fixture Matrix

- same property/type labels across namespaces, projects, and assemblies;
- partial source types and cross-file action parameters;
- source/unsigned framework lookalikes;
- overloads, attributes, binding-source variants, and ambiguous handlers;
- lexical shadowing and local variables sharing property names;
- reversed assignment direction;
- object initializer and LINQ projection;
- identity vs transforming conversions;
- error types and partial compilation;
- generated/source mixtures and excluded files; and
- repeated scans/file-order permutations with byte-stable output.

## Non-Claims

The reports describe checked-in static relationships only. They do not prove
that a page rendered, a request bound, an action ran, a mapper executed, an
object was saved, a query ran, a database changed, or a release is safe.
