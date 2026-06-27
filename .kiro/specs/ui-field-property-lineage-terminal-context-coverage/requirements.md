# UI Field Property Lineage Terminal Context Coverage Requirements

## Introduction

PR #400 added the first backend terminal-context gate for `tracemap
property-flow`: selected-property paths may receive static terminal context
only after an exact selected-property bridge reaches an existing combined path
terminal surface. The current implementation intentionally stays narrow and
does not claim runtime behavior, database execution, dependency execution,
impact proof, AI/LLM analysis, or complete coverage.

This follow-up is a backend-only coverage and vocabulary hardening spec. It
defines the deterministic test coverage, bridge requirements, negative cases,
and rule-catalog/version decisions needed before TraceMap adds broader
terminal-context families.

Public claim level: hidden. This spec is for local deterministic evidence
behavior and does not authorize site copy, marketing copy, runtime claims, or
public product claims.

## Source Material

- PR #400: property-flow terminal context gate on `dev`.
- `.kiro/specs/ui-field-property-lineage-terminal-context/`
- `src/dotnet/TraceMap.Reporting/PropertyFlowReport.cs`
- `src/dotnet/TraceMap.Reporting/CombinedTerminalSurfaceKinds.cs`
- `src/dotnet/tests/TraceMap.Tests/PropertyFlowTests.cs`
- `rules/rule-catalog.yml`
- `docs/VALIDATION.md`

## Existing Baseline

This spec assumes the current `dev` baseline includes:

- `tracemap property-flow` report version `1.0`.
- `CombinedTerminalSurfaceKinds.All` as the closed terminal surface vocabulary
  used by combined dependency/path reporting.
- `PropertyFlowReporter.TerminalContextKind` as the property-flow presentation
  bucket mapper for selected terminal surfaces.
- A selected-property bridge check that requires a property-specific root and
  exact selected fact/symbol/display identity in the selected combined path.
- `StaticTerminalContext` path notes and `terminalContextKind` safe metadata
  only when that selected-property bridge is present.
- HTTP route/client surfaces excluded from terminal-context presentation.
- Existing positive and endpoint-proximity negative tests from PR #400.

## Requirement 1: Terminal Surface Vocabulary Buckets Are Covered

**User Story:** As a maintainer, I want every current terminal surface kind to
have deterministic property-flow vocabulary coverage before broader terminal
families are added.

### Acceptance Criteria

1. WHEN a surface kind appears in `CombinedTerminalSurfaceKinds.All` THEN tests
   SHALL prove whether property-flow maps it to a terminal-context bucket or
   intentionally suppresses terminal-context metadata. This MAY be a direct
   table test over an internal mapping helper; it does not require scanner
   fixtures for every surface kind.
2. WHEN `sql-query` or `sql-persistence` reaches a selected property through an
   exact bridge THEN property-flow SHALL render the `data-surface terminal
   context` bucket.
3. WHEN `legacy-data` reaches a selected property through an exact bridge THEN
   property-flow SHALL render the `legacy-data terminal context` bucket.
4. WHEN `package-config` reaches a selected property through an exact bridge
   THEN property-flow SHALL render the `package/config terminal context`
   bucket.
5. WHEN a `message-*` surface reaches a selected property through an exact
   bridge THEN property-flow SHALL render the `message-surface terminal
   context` bucket.
6. WHEN an `asmx-*`, `remoting-*`, or exact `wcf-operation` surface reaches a
   selected property through an exact bridge THEN property-flow SHALL render
   the `legacy-communication terminal context` bucket. The current WCF
   behavior is an exact `wcf-operation` decision, not a broad `wcf-*` prefix;
   any future WCF surface kind SHALL fail closed until mapped, suppressed, or
   deferred.
7. WHEN a non-HTTP terminal surface is in the closed combined vocabulary but
   is not mapped by a more specific property-flow bucket THEN tests SHALL prove
   the fallback `dependency-surface terminal context` behavior or require a
   documented decision to add a more specific bucket.
8. WHEN `http-route` or `http-client` reaches a selected property through an
   exact selected-property bridge THEN tests SHALL prove property-flow does
   not render `StaticTerminalContext` notes or `terminalContextKind` metadata
   for that HTTP surface under this slice.
9. WHEN the combined terminal surface vocabulary changes THEN vocabulary
   coverage tests SHALL fail until the new surface kind is mapped, suppressed,
   or explicitly deferred with a documented rule/version decision.

## Requirement 2: Exact Selected-Property Bridge Is Required

**User Story:** As a reviewer, I want terminal context to attach only when the
selected property itself is present in the selected static path evidence.

### Acceptance Criteria

1. WHEN terminal context is attached THEN the selected root SHALL be a
   property-specific root family already accepted by property-flow, such as UI
   binding, Razor binding, model/DTO property, serializer contract member, or
   declared parameter evidence.
2. WHEN terminal context is attached THEN the selected combined path SHALL
   contain one of the exact bridge identities accepted by the implementation:
   selected combined fact ID, selected symbol ID, selected target symbol,
   selected member/target symbol metadata, type-qualified model property,
   type-qualified DTO property, or equivalent documented exact property
   identity. The current implementation compares symbol/display identities
   with ordinal-ignore-case semantics; PR 1 SHALL document whether that is the
   intended exact-bridge contract or a known boundary and SHALL add a case
   boundary test accordingly.
3. WHEN the selected property bridge is exact but the terminal evidence tier is
   weaker than semantic evidence THEN the resulting path classification SHALL
   remain bounded by the weakest required evidence and SHALL NOT upgrade merely
   because terminal context is present.
4. WHEN the selected root is not property-specific, including method, endpoint,
   class, file, namespace, package, route, or dependency surface selectors,
   THEN property-flow SHALL NOT attach terminal context.
5. WHEN the selected path reaches a surface without an exact selected-property
   bridge THEN property-flow SHALL omit terminal context or emit a catalogued
   gap, not a terminal-context row.
6. WHEN exact bridge semantics are expanded THEN the implementation PR SHALL
   document the new bridge family, evidence tier, limitations, rule ID, and
   negative tests before the bridge can attach terminal context.

## Requirement 3: Proximity-Only Evidence Is Negative Coverage

**User Story:** As an implementer, I want regression tests that prevent
terminal context from being attached through nearby but unproven evidence.

### Acceptance Criteria

1. Tests SHALL prove same method proximity alone does not attach terminal
   context.
2. Tests SHALL prove same endpoint or route proximity alone does not attach
   terminal context.
3. Tests SHALL prove same class proximity alone does not attach terminal
   context.
4. Tests SHALL prove same file proximity alone does not attach terminal
   context.
5. Tests SHALL prove same namespace, folder, or project proximity alone does
   not attach terminal context when those facts are present in fixture data.
6. Tests SHALL prove same short property name alone does not attach terminal
   context.
7. Tests SHALL prove same short symbol or method name alone does not attach
   terminal context.
8. Tests SHALL prove the current property-flow generic names from
   `PropertyFlowReport.cs` (`id`, `name`, `type`, `value`, `state`, and
   `status`) do not attach terminal context without exact selected-property
   identity. The implementation PR SHALL not silently import broader
   route-flow, high-fan-out, or rule-catalog example vocabularies such as
   `result` or `response`; if it consults those broader sets, it SHALL add an
   explicit compatibility decision and matching tests.
9. Tests SHALL prove broad endpoint dependency evidence, route-flow context
   groups, touched files, touched symbols, and dependency-surface inventory do
   not attach property-flow terminal context without the selected-property
   bridge.
10. Negative tests SHALL assert both absence of `StaticTerminalContext` notes
    and absence of `terminalContextKind` safe metadata.

## Requirement 4: Rule Catalog And Report Version Decisions Precede Expansion

**User Story:** As an automation author, I want terminal-context output to
remain rule-backed and version-stable as the vocabulary grows.

### Acceptance Criteria

1. WHEN terminal-context behavior reuses existing path/surface evidence THEN
   tests SHALL assert the emitted edge and node rule IDs already resolve in
   `rules/rule-catalog.yml`.
2. WHEN a new terminal-context rule ID, gap code, context row, metadata key, or
   top-level JSON section is introduced THEN the same implementation PR SHALL
   update `rules/rule-catalog.yml`, tests, and limitations before emitting it.
3. WHEN a new terminal-context bucket is introduced THEN the implementation PR
   SHALL document whether report version `1.0` remains additive and safely
   ignorable or whether the report version must bump.
4. WHEN terminal-context metadata changes the meaning of existing path, edge,
   node, inventory, gap, summary, or Markdown sections THEN the report version
   SHALL bump and the compatibility reason SHALL be documented.
5. WHEN terminal-context metadata remains additive safe metadata or notes THEN
   tests SHALL prove existing report consumers touched by the PR can ignore or
   render the addition without unsafe forwarding.
6. WHEN terminal-context absence is reported under missing schema, unknown
   commit SHA, missing extractor identity, reduced coverage, unsupported
   schema, traversal caps, or ambiguous terminal evidence THEN the conclusion
   SHALL be no stronger than a labelled gap or omission.

## Requirement 5: Claims Stay Static, Hidden, And Bounded

**User Story:** As a reviewer, I want terminal-context coverage wording to
prevent runtime or impact interpretations.

### Acceptance Criteria

1. property-flow SHALL NOT claim runtime request execution, dependency
   execution, database execution, persistence, traffic, authorization behavior,
   feature-flag state, dependency-injection runtime target selection,
   serializer runtime behavior, branch feasibility, production behavior,
   business impact, or complete coverage.
2. property-flow SHALL NOT say a property is impacted unless a separate
   reducer with evidence emits that conclusion.
3. property-flow SHALL NOT use LLM calls, embeddings, vector databases, or
   prompt-based classification in scanner, reducer, report, export, or
   terminal-context composition logic.
4. Markdown and JSON wording SHALL describe terminal context as static evidence
   context from existing deterministic facts.
5. Terminal-context notes and `terminalContextKind` metadata SHALL NOT include
   raw SQL, snippets, literal values, connection strings, raw URLs, local
   paths, remotes, hostnames, credentials, secrets, or private sample names.
6. Public site copy, demo copy, generated site output, and public claim text
   are out of scope for this spec.

## Requirement 6: Validation

**User Story:** As a maintainer, I want implementation PRs to validate the
terminal-context gate without relying on manual inspection.

### Acceptance Criteria

1. Spec-only PR validation SHALL include Kiro Opus and Sonnet review when
   available, `git diff --check`, `./scripts/check-private-paths.sh`, and a
   diff-scope check limited to this spec folder.
2. Implementation PRs SHALL run focused `PropertyFlowTests` covering
   vocabulary buckets, exact bridge positives, and proximity negatives.
3. Implementation PRs SHALL run `dotnet test src/dotnet/TraceMap.sln` unless
   explicitly deferred with reason and risk.
4. Implementation PRs SHALL run `./scripts/check-private-paths.sh` and
   `git diff --check`.
5. If `git diff --check`, `./scripts/check-private-paths.sh`, or another
   required validation script is missing, unavailable, or non-executable, the
   implementation PR SHALL record exact tool evidence and label validation as
   partial.
6. If rule catalog, report export, docs export, route-flow, path, reverse, or
   adapter behavior changes, implementation PRs SHALL run the relevant focused
   tests or record an explicit partial-validation label.
7. Failed build or reduced validation SHALL be labelled partial, not clean.
