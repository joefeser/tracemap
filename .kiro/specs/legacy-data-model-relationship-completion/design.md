# Legacy Data Model Relationship Completion Design

## Overview

This spec defines the next relationship-focused legacy data model follow-up. The
current scanner already emits deterministic relationship evidence for common
DBML, EDMX, typed DataSet, and NHibernate XML shapes. The remaining problem is
the boundary around shapes that look like relationships but cannot be proven
deterministically from checked-in static metadata.

The intended evidence chain is:

```text
DBML / EDMX / typed DataSet XSD / NHibernate hbm.xml relationship descriptor
  -> deterministic relationship fact when endpoints are safe and scoped
  -> cataloged AnalysisGap or reduced relationship evidence when ambiguous
  -> needs-review labels in touched downstream projections
  -> no runtime ORM/database/impact claim
```

The first implementation slice should be intentionally small. The preferred PR 1
is a shared relationship gap classifier and focused test harness, wired to one
relationship family if necessary. A one-family PR is acceptable if it leaves the
classifier reusable for the remaining families.

## Goals

- Keep deterministic existing relationship facts stable.
- Add a catalog-gated path for relationship ambiguity and unsupported shapes.
- Require `AnalysisGap` or needs-review evidence for unsupported DBML, EDMX,
  typed DataSet, and NHibernate relationship shapes.
- Prevent relationship gaps from becoming terminal `legacy-data` surfaces.
- Preserve privacy and deterministic output.
- Create a small PR 1 that is reviewable and does not duplicate merged slice-1
  regression work.

## Non-Goals

- No product-code implementation in this spec PR.
- No complete relationship coverage claim.
- No runtime ORM model loading, database connection, SQL execution, schema
  introspection, query evaluation, service activation, config transform
  execution, or designer execution.
- No proof of referential integrity, lazy loading, cascade behavior, table
  existence, provider compatibility, production usage, business impact, or
  reducer impact.
- No arbitrary Fluent mapping execution, project-local DSL execution, or
  dynamic mapping evaluation.
- No LLM calls, embeddings, vector databases, prompt classification, fuzzy
  matching, or probabilistic inference in TraceMap core.
- No raw SQL/config/connection/source snippets/local paths/remotes/URLs/private
  labels/secrets in default outputs.

## Existing Foundation

Live `origin/dev` already contains:

- Rule catalog entries for `legacy.data.model.relationship.v1`,
  `legacy.data.orm.nhibernate.v1`, `legacy.data.orm.unsupported.v1`, and
  `legacy.data.model.surface.v1`.
- `LegacyDataMappingDeclared` relationship evidence with source rule IDs for
  deterministic DBML associations, EDMX CSDL/MSL associations, typed DataSet
  relationships and constraints, and NHibernate XML relationship descriptors.
- Existing gaps such as `AmbiguousLegacyDataModelIdentity`,
  `UnsupportedLegacyOrmMappingShape`, `UnsupportedLegacyOrmDescriptor`, and
  `AmbiguousLegacyDataModelSelector`.
- Reverse-query selector downgrade behavior for ambiguous legacy-data model
  surfaces.
- Slice-1 regression proof that source relationship facts and normalized
  relationship projection do not double-count terminal `legacy-data` surfaces.

This spec should not repeat that regression-only slice unless a new change
touches those code paths.

## Rule And Vocabulary Gate

The rule catalog is the first implementation gate. Before emitting a new string,
the implementation must confirm or add catalog coverage for:

| Vocabulary | Preferred owner | Notes |
| --- | --- | --- |
| `AmbiguousLegacyDataModelIdentity` | Source legacy data rule or `legacy.data.model.relationship.v1` | Emitted in code today, but not yet literally documented in `rules/rule-catalog.yml`; catalog entry/update is required before relationship follow-up code reuses or expands it. |
| `UnsupportedLegacyOrmMappingShape` | Source legacy data rule | Emitted in code today, but not yet literally documented in `rules/rule-catalog.yml`; catalog entry/update is required before relationship follow-up code reuses or expands it. |
| `UnsupportedLegacyOrmDescriptor` | `legacy.data.orm.unsupported.v1` | Emitted in code today, but not yet literally documented in `rules/rule-catalog.yml`; catalog entry/update is required before relationship follow-up code reuses or expands it. |
| `AmbiguousLegacyDataModelSelector` | `legacy.data.model.surface.v1` | Literally documented in catalog for reverse selector behavior; do not reuse for extractor-local ambiguity. |
| `DuplicateIdentity` with reason `duplicate-surface` | `legacy.data.model.surface.v1` | Actual projection/reporting vocabulary described in catalog; not an extractor-local relationship gap. Do not emit a new `DuplicateLegacyDataModelSurface` string unless cataloged first. |
| New relationship-specific strings | Catalog update required before code emits them | Include limitations and tests first. |

If a generic relationship classifier introduces closed reason labels, those
labels must be documented as safe machine-readable values under
`legacy.data.model.relationship.v1` before emission. Avoid free-form strings
that contain endpoint names, SQL fragments, config values, paths, remotes, URLs,
or private labels.

Recommended initial `safeReasonCode` closed set for PR 1:

| Reason code | Meaning |
| --- | --- |
| `missing-endpoint` | One or both endpoint identities are absent from static metadata. |
| `duplicate-relationship-identity` | Multiple relationship descriptors share the same safe local identity in scope. |
| `ambiguous-endpoint-candidates` | Multiple endpoint candidates remain after safe scoping. |
| `unsupported-relationship-shape` | A supported metadata family exposes a relationship construct outside deterministic MVP handling. |
| `reduced-parser-coverage` | Parser bounds, caps, malformed metadata, or reduced extraction coverage prevents complete relationship analysis. |
| `unsafe-redacted-endpoint-identity` | Endpoint identity exists but cannot be rendered cleartext and requires hash-only or omitted output. |
| `not-in-scope` | The descriptor is not a relationship descriptor for this classifier and should not emit a gap. |

The catalog update may choose different names, but the final implementation
must keep the set closed, documented, and tested before code emits values.

## Relationship Gap Classifier

PR 1 should prefer a small internal classifier. Its exact C# shape can follow
the codebase, but the implementation should preserve this normative schema:

| Input field | Required | Expected type/shape |
| --- | --- | --- |
| `relationshipFamily` | Yes | Closed family label: `dbml`, `edmx`, `typed-dataset`, `nhibernate-hbm`, or future cataloged value. |
| `sourceRuleId` | Yes | Existing source rule ID. |
| `descriptorKind` | Yes | Safe closed descriptor label such as `association`, `relation`, `keyref`, `many-to-one`, or `collection`. |
| `sourceEndpointState` | Yes | Closed endpoint state: deterministic, missing, ambiguous, unsafe-redacted, or not-applicable. |
| `targetEndpointState` | Yes | Same closed endpoint state set. |
| `joinOrKeyState` | Yes | Closed state: deterministic, missing, ambiguous, unsupported, unsafe-redacted, or not-applicable. |
| `parserCoverageState` | Yes | Closed state: full, reduced, too-large, malformed, security-rejected, or not-applicable. |
| `unsupportedShapeFlags` | No | Closed labels only; no raw metadata values. |
| `existingFamilyAllowsUnidirectional` | Yes | Boolean or equivalent policy flag for preserving existing reduced relationship facts. |

| Output field | Required | Expected type/shape |
| --- | --- | --- |
| `decision` | Yes | Closed decision: emit relationship, emit reduced relationship, emit analysis gap, or emit nothing. |
| `classification` | Conditional | Cataloged gap classification when `decision` emits a gap. |
| `coverageLabel` | Yes | Existing coverage label vocabulary. |
| `relationshipEndpointCoverage` | Conditional | Existing endpoint coverage vocabulary for emitted relationship facts. |
| `limitations` | Yes | Closed limitation labels only. |
| `evidenceTier` | Yes | Tier ceiling from owning source rule. |
| `ruleId` | Yes | Owning rule for the emitted fact or gap. |
| `safeReasonCode` | Yes | Closed, cataloged reason code. |

The classifier must not build facts from raw XML by itself if existing
extractor structure makes that awkward. It can be an internal helper,
lightweight record, or testable function that centralizes the decision table.
The important property is that family-specific extractors do not each invent
slightly different labels for the same relationship uncertainty.

## Decision Table

Recommended initial decision table:

| Condition | Output | Notes |
| --- | --- | --- |
| both endpoints deterministic and safe | relationship fact | Preserve source rule ID and `mappingKind`. |
| one endpoint deterministic, other missing and current family already supports unidirectional evidence | reduced relationship | Keep `relationshipEndpointCoverage = unidirectional` and closed limitations. |
| one endpoint deterministic, other missing and current family has no unidirectional evidence policy | `AnalysisGap` | Do not introduce a new reduced relationship pattern without cataloged limitations. |
| duplicate relationship identity in same safe scope | gap plus reduced existing facts if already emitted | Do not choose one duplicate as authoritative. |
| multiple endpoint candidates after safe scoping | `AnalysisGap` | Prefer `AmbiguousLegacyDataModelIdentity` unless catalog adds narrower value. |
| unsupported shape inside supported family | `AnalysisGap` | Prefer `UnsupportedLegacyOrmMappingShape` until narrower catalog entry exists. |
| descriptor family unsupported | `AnalysisGap` under unsupported ORM rule | Do not emit entity/table/relationship facts. |
| endpoint names unsafe but hashable | relationship may be reduced or hash-only | Cleartext display must be omitted; stable identity may use hashes. |
| parser/bounds/caps reduced coverage | gap and reduced coverage | Do not treat skipped descriptors as clean absence. |
| descriptor is not a relationship descriptor | emit nothing | Covered by `not-in-scope` classifier tests; do not emit clean-absence gaps for unrelated descriptors. |

When conditions overlap, apply this precedence for deterministic output:

1. Parser security rejection, malformed metadata, or too-large/capped metadata.
2. Descriptor family unsupported.
3. Unsupported relationship shape inside a supported family.
4. Duplicate relationship identity in the same safe scope.
5. Ambiguous endpoint or join/key candidates.
6. Missing endpoint or join/key state.
7. Unsafe/redacted endpoint identity.
8. Deterministic relationship.
9. Not-in-scope descriptor.

Tests must include at least one overlapping-condition fixture proving the same
classification, limitations, fact order, and fact IDs across repeated scans.

## Family Boundaries

### DBML

Preserve existing deterministic association extraction. Candidate follow-ups:

- duplicate association names in identical source scope;
- missing or ambiguous `Type`, `ThisKey`, or `OtherKey`;
- multiple database/table/type scopes that make association endpoints ambiguous;
- provider extensions or unrecognized association metadata;
- unsafe endpoint identifiers that cannot be rendered cleartext.

### EDMX

Preserve existing deterministic CSDL and MSL association extraction. Candidate
follow-ups:

- association or association-set endpoint ambiguity;
- multiple conceptual/storage containers;
- inherited endpoints;
- split entity mappings;
- conditional mappings;
- complex property mappings;
- many-to-many mappings without deterministic join and endpoint evidence;
- provider extensions or missing MSL relationship mapping.

### Typed DataSet

Preserve existing `msdata:Relationship` and `xs:keyref` relationship evidence.
Candidate follow-ups:

- duplicate or ambiguous constraint names;
- key/keyref field mismatch or unsupported composite matching;
- missing parent/child selector endpoints;
- DataSet namespace indicators without real DataSet content;
- SQL-only relationship hints in TableAdapter commands, which must not become
  relationship facts.

### NHibernate

Preserve existing XML-only relationship descriptors for deterministic
`many-to-one`, `one-to-one`, collection/key, `one-to-many`, and currently
supported `many-to-many` target shapes. Candidate follow-ups:

- composite ids or composite keys;
- formula-only joins;
- filters and custom SQL;
- dynamic components;
- inheritance/joined/union subclass relationship effects;
- custom user types or provider extensions;
- ambiguous collection children;
- missing target class;
- config-referenced runtime-loaded mapping descriptors.

NHibernate relationship evidence remains checked-in XML metadata only. It must
not claim session factory load, lazy loading, cascade, dirty tracking, provider
compatibility, schema existence, or query execution.

## Downstream Handling

Touched downstream workflows should treat reduced or ambiguous relationship
coverage as review-tier evidence. For PR 1, downstream scope should stay
minimal:

- If only extractor/test helper logic changes, do not update broad downstream
  workflows.
- If report/projection code is touched, add focused tests proving
  `AnalysisGap` facts stay out of terminal `legacy-data` surfaces and reduced
  relationship surfaces carry needs-review/reduced coverage caveats.
- Do not rerun or rewrite the merged slice-1 no-double-count regression unless
  implementation touches the relevant projection/report path.

## Privacy Model

Safe outputs may include:

- repo-relative paths;
- line spans;
- rule IDs;
- evidence tiers;
- extractor versions;
- safe closed reason codes;
- safe local identifiers that pass existing safe identifier policy;
- stable hashes for unsafe identifiers or values.

Default outputs must not include:

- raw SQL;
- raw config values;
- connection strings;
- provider/server/catalog/user values;
- formulas, filters, query text, or SQL fragments;
- URLs, remotes, local absolute paths, machine paths;
- source snippets;
- private sample labels;
- secrets or secret-looking tokens.

Privacy tests should inspect fact properties, Markdown report output, SQLite
properties, and any touched downstream JSON/Markdown exports.

## PR 1 Recommendation

Recommended PR 1: "relationship gap classifier and DBML/typed DataSet harness".

Minimum scope:

- Confirm rule catalog ownership for reused relationship gap strings.
- Add the shared classifier or equivalent deterministic helper.
- Add focused tests for the helper decision table.
- Wire the helper to one narrow family path, preferably a small DBML association
  or typed DataSet constraint ambiguity case that does not disturb existing
  deterministic evidence.
- Add privacy assertions for unsafe endpoint/key values in that touched family.
- Run focused tests, full build/test if feasible, CLI smoke, private path guard,
  and diff check.

Acceptable alternate PR 1: one complete family-specific relationship gap slice
if code inspection shows that the shared helper would add indirection without
reducing duplicated decision logic in that slice. In that case the
implementation state must explain the evidence for deferring the helper and
which families remain open. PR 2 must either add/wire the shared helper for the
remaining families or explicitly justify permanent per-family divergence with
cataloged reason-code tests.

## Deferred Follow-Ups

- Exhaustive DBML association ambiguity and provider extension handling.
- Exhaustive EDMX split/conditional/complex/many-to-many relationship gap
  detection.
- Typed DataSet composite key/keyref field matching and SQL-only relationship
  non-inference tests beyond PR 1.
- NHibernate composite-key, formula-join, filter/custom SQL, provider extension,
  inheritance, and config-loaded mapping gaps beyond PR 1.
- Broader selector downgrade behavior outside reverse query.
- Broad combined/path/reverse/diff/impact/release-review/portfolio/vault/RAG
  export/static HTML relationship expansion.
- Runtime ORM/database validation, query execution, schema introspection, or
  impact proof.
- Public site or marketing claims about relationship coverage.
- If PR 1 takes the single-family alternate path, PR 2 must either wire a shared
  classifier to the remaining families or justify permanent per-family
  divergence with cataloged reason-code tests.
