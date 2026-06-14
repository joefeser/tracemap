# Parameter and Value-Origin Flow Design

## Overview

Parameter and value-origin flow is a static explanation layer over existing TraceMap evidence. It connects direct argument-passing, alias, field, constructor, call, and dependency-surface facts into bounded paths while preserving every limitation.

The implementation direction is:

```text
language scanner facts
  -> symbols / fact_symbols / call_edges / object_creations
  -> argument_flows / local_aliases / field_aliases / parameter_forward_edges
  -> combine
  -> report / paths / reverse / diff / impact
```

The design intentionally avoids full taint analysis. It answers "what static evidence connects this value-like origin to that call or surface?" rather than "what runtime value reaches production?"

## Goals

- Normalize direct argument-to-parameter evidence across adapters.
- Make simple parameter forwarding useful in combined path and reverse reports.
- Capture same-method local aliases and deterministic constructor-to-member origins without arbitrary alias analysis.
- Make callback, async, mutation, dynamic dispatch, DI, reflection, serializer, branch, and collection boundaries visible as gaps.
- Support endpoint/request parameter roots and dependency-surface terminals where existing facts are credible.
- Preserve deterministic outputs and public-report safety.

## Non-Goals

- No full taint engine.
- No symbolic execution.
- No runtime request/value inference.
- No object identity, lifetime, collection contents, or mutation-order analysis.
- No runtime dependency injection, reflection, dynamic dispatch, serializer, scheduler, or branch feasibility claims.
- No graph database or vector database.
- No LLM/prompt-based classification.
- No raw source snippets by default.

## Current State

TraceMap already has useful primitives:

- C# semantic `ArgumentPassed`, `LocalAlias`, `FieldAlias`, `ParameterForwardEdge`, symbol identity, call edges, object creation, symbol relationships, flow boundaries, runtime-adjacent evidence, and combined path notes.
- Python reduced-coverage `ArgumentPassed`, `FieldAlias`, and derived `parameter_forward_edges` for direct syntax-visible forwarding.
- TypeScript and JVM argument/call evidence where compiler or syntax analysis supports it.
- Combined report/path/reverse layers that understand dependency surfaces, source provenance, rule IDs, evidence tiers, and reduced coverage.

The gap is a coherent contract and next implementation slice that makes these primitives consistently queryable as value-origin evidence without implying full taint.

## Shared Concepts

### Value Origin

A value origin is a static source of a value-like entity:

- endpoint/request parameter;
- method/function parameter;
- constructor parameter;
- local variable;
- field/member;
- literal/hash-only boundary;
- callback parameter;
- dependency-surface input such as SQL text hash, HTTP request argument, package/config key, or event/message name.

Origins are only credible when backed by a fact with rule ID, evidence tier, span, and role metadata.

### Value Hop

A value hop is one direct static step that can move value-origin evidence:

| Hop | Evidence |
| --- | --- |
| argument to parameter | `ArgumentPassed` |
| parameter to parameter | `ParameterForwardEdge` |
| local alias | `LocalAlias` |
| field/member alias | `FieldAlias` |
| constructor argument to member | constructor `ArgumentPassed` plus `FieldAlias` or dedicated derived edge |
| endpoint/source root | endpoint facts with containing symbol and parameter evidence |
| dependency terminal | SQL, HTTP, config/package, package, event/message, or persistence surface facts |

`CallEdge` and `ObjectCreated` are structural reachability context, not value-moving hops by themselves. They may appear in a value-origin path only when paired with direct argument, alias, field/member, constructor-member, or terminal evidence that explains which value is moving.

Hops are not runtime proof. They are static evidence rows.

### Boundary

A boundary is where TraceMap intentionally stops or downgrades:

- mutation boundary;
- collection boundary;
- branch feasibility boundary;
- alias ambiguity;
- dynamic dispatch;
- dependency injection runtime state;
- reflection;
- serializer/model binding;
- callback/delegate/event boundary;
- async scheduling boundary;
- generated code;
- reduced analysis coverage.

Boundaries should be represented as explicit gaps or review-tier notes, not hidden.

## Fact and Property Contract

Value-origin facts should use the shared role convention:

| Role | Meaning |
| --- | --- |
| `source` | caller/root/source-side symbol |
| `target` | callee/target-side symbol |
| `argument` | argument expression symbol or display origin |
| `parameter` | callee parameter symbol |
| `origin` | alias/member origin symbol |
| `constructor` | constructor symbol for object creation/member origin |

Each role should use:

```text
{role}SymbolId
{role}SymbolLanguage
{role}SymbolKind
{role}SymbolDisplayName
```

Useful safe metadata:

- `argumentOrdinal`;
- `parameterOrdinal`;
- `parameterName`;
- `parameterType`;
- `argumentExpressionKind`;
- `argumentExpressionHash`;
- `originExpressionKind`;
- `originExpressionHash`;
- `aliasDepth`;
- `boundaryKind`;
- `flowClassification`;
- `supportingFactIds`;
- `supportingEdgeIds`.

`supportingFactIds` and `supportingEdgeIds` are optional on source adapter facts and required on derived combined rows when those rows are constructed from existing facts or edges.

Do not store raw expressions, source snippets, literal values, config values, raw URLs, connection strings, or local absolute paths.

## Adapter Plans

### .NET

.NET has the strongest semantic base. The implementation should harden and extend existing behavior:

- keep `csharp.semantic.valueflow.v1` for resolved call-site arguments;
- keep `csharp.semantic.localalias.v1` and `csharp.semantic.fieldalias.v1` for direct aliases;
- keep `csharp.semantic.parameterforwarding.v1` for derived forwarding;
- add focused gaps or notes where callback, async, mutation, collection, dynamic, DI, reflection, branch, or serializer boundaries block stronger flow;
- ensure constructor-member flow remains unique-constructor and deterministic only;
- attach endpoint/dependency surface facts to containing symbols where credible.

Do not infer runtime DI activation, serializer-created values, reflection calls, or dynamic dispatch targets.

### TypeScript

TypeScript should align with the shared contract while respecting compiler limitations:

- use TypeScript compiler symbols for function/method parameters and calls when available;
- emit lower-tier syntax evidence for unresolved call shapes;
- handle direct parameter forwarding, simple local aliasing, and callback body forwarding when deterministic;
- include class/property `FieldAlias` support for direct `this.field = parameter` or equivalent member assignments only if the compiler can identify the member and origin symbol; otherwise emit a gap or leave it unsupported for the slice;
- represent Promise/async callback boundaries as gaps unless direct source call evidence is enough;
- avoid claiming object property/member flow through structural types unless symbol evidence and assignment shape are direct.

### JVM

JVM should support Java and Kotlin through shared JVM concepts where available:

- use compiler/parser evidence for method parameters, constructor calls, and method invocation;
- derive direct parameter forwarding and constructor field assignment only when source syntax and symbol identity are credible;
- treat lambdas, method references, reflection, DI frameworks, and generated bytecode as boundaries unless a dedicated rule proves a deterministic hop;
- preserve package/class/member descriptors and module identity.

### Python

Python remains reduced-coverage without a type checker:

- keep direct AST-visible `ArgumentPassed` and `FieldAlias` evidence at Tier3 unless stronger analysis is added later;
- use ordinal placeholder parameters only when target parameter names are not known;
- support same-function direct forwarding and `self.field = parameter` evidence;
- label dynamic dispatch, monkey patching, decorator side effects, async scheduling, and runtime imports as gaps.

## Derived Edges

Derived edges should be built from facts, not independent conclusions.

### ParameterForwardEdge

Inputs:

- `ArgumentPassed`;
- optional `LocalAlias` chain;
- optional deterministic `FieldAlias`;
- symbol identity/fact-symbol rows where present.

Derivation rules:

1. Direct parameter argument to callee parameter is strongest.
2. Same-method local alias chain may be followed to a bounded depth.
3. Field/member alias may be used when assigned in the same method or from exactly one constructor parameter in the containing type. Constructor uniqueness is scoped to constructors visible in the current adapter's analyzed declaring-project source scope; partial, generated, or unloaded constructor bodies make the origin ambiguous.
4. Ambiguous constructors, multiple assignments, or mutation boundaries stop derivation.
5. The derived row keeps every supporting fact ID and a derived rule ID.
6. Same-method alias chains are bounded by a documented constant, default `3` hops, and cycles stop derivation.

### ConstructorMemberOrigin

The implementation may either emit a dedicated fact/edge or reuse `FieldAlias` plus constructor role properties. The design preference is to avoid a new table until a concrete reader needs it. If a new fact is introduced, it must be cataloged and additive.

## Combined Flow Tables

The implementation should audit existing combined tables before adding schema. Expected optional precise flow tables are:

| Source table | Combined table | Purpose |
| --- | --- | --- |
| `argument_flows` | `combined_argument_flows` | direct argument-to-parameter rows |
| `local_aliases` | `combined_local_aliases` | local alias evidence |
| `field_aliases` | `combined_field_aliases` | field/member origin evidence |
| `parameter_forward_edges` | `combined_parameter_forward_edges` | derived parameter-forwarding edges |

If a table is missing from an older index, combined report/path/reverse/diff/impact commands should emit schema/coverage gaps and continue with available fallback evidence. Single-index `tracemap flow` can keep its current fail-fast behavior until explicitly changed.

Any changes to SQLite views, especially `combined_dependency_edges`, must account for view recreation. Do not rely on `CREATE VIEW IF NOT EXISTS` to update existing view definitions.

## Combined Path Behavior

Combined paths should treat value-origin edges as helper edges, not as runtime proof.

Canonical path classifications remain the existing path-level values, such as `StrongStaticPath`, `ProbableStaticPath`, `NeedsReviewPath`, `UnknownAnalysisGap`, and `NoPathFound`.

Value-origin classifications are additive metadata or notes on an existing path row. They must not replace the canonical `Classification` field unless a later compatibility spec changes that public contract.

Value-origin classifications:

- `StrongStaticValuePath`: all hops Tier1 semantic, full coverage credible, no review-only boundaries.
- `ProbableStaticValuePath`: structural surfaces or mixed strong lower-tier evidence.
- `NeedsReviewValuePath`: syntax-only, callback, constructor-member, unique-field, or symbol reconciliation evidence.
- `UnknownAnalysisGap`: coverage or boundary blocks a credible conclusion.
- `NoValuePathEvidence`: full credible coverage and no path.

`NoValuePathEvidence` is not a rename for existing `NoPathFound`; it is a value-origin-specific note/gap used when a path may exist but no credible value-origin chain exists.

## Reverse, Diff, and Impact Behavior

Reverse query should preserve value-origin supporting fact and edge IDs when walking from dependency surfaces to upstream roots.

Diff and impact should compare flow evidence conservatively:

- added/removed flow under reduced coverage is review-tier;
- unstable symbol identity downgrades conclusions;
- gaps must not be suppressed by truncation;
- absence of value-origin evidence is not absence of runtime dependency under reduced coverage.

## Rule Catalog Updates

Implementation may reuse existing rules when behavior is unchanged:

- `csharp.semantic.valueflow.v1`;
- `csharp.semantic.localalias.v1`;
- `csharp.semantic.fieldalias.v1`;
- `csharp.semantic.parameterforwarding.v1`;
- `csharp.semantic.flowboundary.v1`;
- `csharp.semantic.runtimeevidence.v1`;
- existing TypeScript/JVM/Python argument or syntax rules.

New rule IDs should only be added for genuinely new evidence behavior, for example:

- `combined.flow.valuepath.v1`;
- `combined.flow.boundary.v1`;
- adapter-specific callback or constructor-member rules if they cannot be represented with existing facts.

Boundary mapping for new callback/async behavior:

| Evidence | Preferred rule approach |
| --- | --- |
| `CallbackBoundary` | extend an existing adapter flow-boundary rule or add adapter-local callback boundary rule |
| `AsyncBoundary` | extend an existing adapter flow-boundary rule or add adapter-local async boundary rule |
| `CapturedValueFlow` | add adapter-local callback/capture flow rule before emitting |

Every new rule must document limitations, emitted fact/edge types, required safe properties, and false-positive/false-negative cases.

## Safety and Determinism

Outputs must be deterministic:

- stable IDs from fact IDs, symbol IDs, edge IDs, and sorted supporting evidence;
- sorted arrays and metadata keys;
- no timestamps in deterministic report payloads;
- no local absolute paths, raw URLs, connection strings, config values, raw snippets, or literal values.

Expression hashes should use deterministic SHA-256 truncation with documented lengths.

## Validation Strategy

Test layers:

- adapter unit tests for direct forwarding, aliases, constructor-member flow, callback boundaries, and gaps;
- storage tests for flow tables and derived edges;
- older-index compatibility tests for missing optional combined flow tables;
- combined report/path/reverse tests for value-origin notes and deterministic supporting IDs;
- reduced-coverage tests proving no-path conclusions are downgraded;
- tier-cap tests proving syntax-only evidence cannot produce `StrongStaticValuePath`;
- shared-role tests proving legacy alias properties do not override shared role properties;
- cross-source provenance tests preserving source labels, scan IDs, commit SHAs, and supporting IDs;
- safety tests proving raw snippets/values do not appear;
- smoke tests only where a sample repo already exercises endpoint-to-surface value flow.

Implementation should update validation docs only when code lands. This spec PR should not modify docs or code.
