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

The older `ui-field-property-lineage-composition` spec remains historical
ownership for the broad #165 composition plan. This spec narrows the still-open
#517 depth to the two missing producer contracts and their exact-ID joins. It
does not reopen completed Angular, route-flow, export, or terminal-context work.

## Slice A: Semantic Razor Model-Binding Producer

Add a semantic pass adjacent to the current C# semantic extraction. The pass
examines compiler symbols rather than text:

1. Admit an owner only when its controller/page-model base type or binding
   attribute resolves to the pinned ASP.NET Core metadata identity.
2. Admit an action/handler only through exact method/attribute/base semantics
   documented by the implementation fixture. Method-name conventions alone
   may remain syntax fallback but cannot yield Tier1.
3. Resolve parameter/property types through Roslyn, including cross-file and
   partial source types.
4. Emit one target per source property symbol with canonical owner, parameter,
   type, and property roles.
5. Emit categorical gaps for error/dynamic/type-parameter/external-unavailable
   shapes without guessing a property.

Prefer reusing `RazorModelBindingTarget` so downstream selectors retain their
family. Use a new semantic rule and extractor version so Tier1 rows are
distinguishable from syntax fallback. The exact property schema is closed in
the implementation PR after the real ASP.NET Core fixture confirms available
symbols.

Minimum safe fields:

| Role | Required evidence |
| --- | --- |
| owner | canonical action/handler method ID and assembly identity |
| parameter | canonical parameter ID, ordinal, source classification |
| model type | canonical named-type ID and assembly identity |
| target | canonical property ID, type, containing-type ID |
| binding | closed binding kind/model family/HTTP evidence |
| provenance | repo-relative span, commit, extractor, rule, tier, coverage, limitations |

Syntax fallback remains independently attributable. If the same logical target
has both Tier1 and Tier3 rows, composition selects by exact canonical target ID
and preserves both supporting IDs; it does not delete historical facts or
pretend syntax evidence became semantic.

## Slice B: Direct Property Mapping Producer

Add a small Roslyn operation/syntax projector for direct mappings. The producer
uses `IPropertySymbol` identities for both ends and never infers by labels.

Supported shapes:

| Shape | Source | Target | Direction |
| --- | --- | --- | --- |
| simple assignment | resolved RHS property | resolved LHS property | RHS -> LHS |
| object initializer | resolved RHS property | initialized member property | RHS -> initialized member |
| LINQ projection | same object-initializer rule inside lambda | initialized member property | RHS -> initialized member |
| constructor forwarding | resolved source property -> parameter, plus independently proven parameter -> assigned target property | two explicit hops | source -> parameter -> target |

The implementation should start with assignment/object-initializer shapes. Add
constructor forwarding in the same PR only if existing parameter-forwarding
facts can preserve both exact hops without a second incompatible identity
model; otherwise record it as the next bounded producer slice.

Unwrap only parentheses, null-forgiving syntax, and Roslyn identity conversions.
Do not classify method calls or expression transformations as direct mappings.
A recognized mapping context with unsupported shape may emit one aggregated gap
per containing method/shape. Ordinary assignments unrelated to two resolved
properties produce neither mapping facts nor migration-style global noise.

Proposed fact type: `PropertyMappingDeclared`.

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

- Tier1 canonical binding plus Tier1 direct mapping can support strong/probable
  paths only when every other hop meets existing requirements.
- Syntax/convention fallback, type-family matching, aliases, or names remain
  `NeedsReviewLineage`.
- No exact bridge means a gap; the reporter does not search globally for the
  closest property name.

Report version `1.0` remains only if new rows/metadata are additive and current
consumers are verified. The implementation-state audit owns that decision.

## Gap Contract

Producer gaps use source rules and Tier4. Reporter gaps reuse
`property-flow.edge.v1`, `property-flow.coverage.v1`, or
`property-flow.schema.v1` when their limitations fit.

Gap rows contain only closed classifications, safe canonical scope hashes/IDs,
occurrence count, provenance, coverage, and limitations. They do not store raw
expressions or arbitrary exception messages.

## Compatibility and Safety

- NDJSON/SQLite/combine must preserve new facts generically and deterministically.
- Generic reducers must not interpret `PropertyMappingDeclared` as impact until
  a reducer contract explicitly admits it.
- Markdown may count facts but must not render unsafe properties.
- Property-flow, explorer, docs/vault, evidence packs, release review, and
  snapshot diff must preserve the new evidence contract or emit omission/
  compatibility gaps.
- Existing raw-snippet defaults remain unchanged.

## Adversarial Fixture Matrix

- same property/type labels across namespaces, projects, and assemblies;
- partial source types and cross-file action parameters;
- source/unsigned framework lookalikes;
- overloads, attributes, binding-source variants, and ambiguous handlers;
- lexical shadowing and local variables sharing property names;
- reversed assignment direction;
- object initializer and LINQ projection;
- constructor parameter forwarding with multiple-constructor ambiguity;
- identity vs transforming conversions;
- error types and partial compilation;
- generated/source mixtures and excluded files; and
- repeated scans/file-order permutations with byte-stable output.

## Non-Claims

The reports describe checked-in static relationships only. They do not prove
that a page rendered, a request bound, an action ran, a mapper executed, an
object was saved, a query ran, a database changed, or a release is safe.
