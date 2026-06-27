# UI Field Property Lineage Terminal Context Coverage Design

## Overview

This spec is the follow-up coverage hardening slice after PR #400. It does not
add product code. It defines the implementation-ready work needed to protect
the new property-flow terminal-context gate before broader terminal-context
families are added.

The current shape is intentionally narrow:

```text
selected property root
  -> exact selected-property bridge in combined path nodes
  -> existing terminal surface evidence
  -> additive StaticTerminalContext note and terminalContextKind safe metadata
```

Every step must be backed by deterministic facts, rule IDs, evidence tiers,
source identity, commit SHA, file paths, line spans, and extractor versions
where those fields are available from the underlying evidence.

## Current Context

This spec was drafted from `origin/dev` after PR #400 merged. Verified baseline
commit:

```text
5e88a10486a1bf0c088ee681f140c643a2635415
```

PR #400 added the property-flow terminal context gate and current tests:

- positive selected `ProfileDto.Email` path reaching `sql-query`;
- negative endpoint/method selector proximity coverage;
- `StaticTerminalContext` note wording that says static context is not runtime
  execution, dependency execution, database execution, or impact proof;
- safe metadata carrying `terminalContextKind` only when the selected-property
  bridge is present.

## Non-Goals

- No product-code implementation in this spec PR.
- No scanner extraction changes.
- No Swift, site, public copy, generated output, or static-site work.
- No new persisted tables or generated artifacts.
- No runtime HTTP, browser, database, dependency, queue, package, WCF, ASMX,
  remoting, or production execution.
- No impact proof, reducer conclusion, release safety claim, business impact,
  or complete coverage claim.
- No AI/LLM calls, embeddings, vector databases, or prompt-based
  classification.

## Vocabulary Coverage Plan

Implementation should add table-driven or equivalent deterministic tests that
enumerate `CombinedTerminalSurfaceKinds.All` and assert the property-flow
presentation decision for every current value.

### Test Seam

Closed-vocabulary enumeration should be a direct table test over the
property-flow terminal-context mapping. If the current mapper remains private,
the implementation may extract a tiny internal helper or make the mapping
method internal; the reporting assembly already supports test visibility for
internal members. This table test is the fail-closed seam for every current and
future `CombinedTerminalSurfaceKinds.All` value.

Full report-generation fixtures are still required, but only for
representative buckets and exact bridge behavior. The implementation should not
create scanner-like fixtures for every surface kind solely to satisfy the
enumeration requirement.

Expected current decisions:

| Surface kind family | Property-flow terminal decision |
| --- | --- |
| `sql-query`, `sql-persistence` | `data-surface terminal context` |
| `legacy-data` | `legacy-data terminal context` |
| `package-config` | `package/config terminal context` |
| `message-*`, including `message-unknown` | `message-surface terminal context` |
| `asmx-*`, `remoting-*`, exact `wcf-operation` | `legacy-communication terminal context` |
| `http-route`, `http-client` | suppressed for property-flow terminal context |
| `dependency-surface` literal kind | `dependency-surface terminal context` |
| other closed-vocabulary non-HTTP surfaces | `dependency-surface terminal context` unless a reviewed bucket is added |

The table test should fail closed when `CombinedTerminalSurfaceKinds.All` gains
a new kind. A new kind must be assigned to a bucket, explicitly suppressed, or
deferred with a rule/version note before broader context work proceeds. The WCF
decision is intentionally exact for `wcf-operation`; a future `wcf-*` value
does not automatically inherit legacy-communication semantics. The table test
should include an out-of-vocabulary probe such as `wcf-service` or
`wcf-binding` and assert it returns `dependency-surface terminal context`, not
`legacy-communication terminal context`, until a future spec explicitly maps or
suppresses it. That WCF fallthrough is the default catch-all branch, not a
dedicated `wcf-*` arm; the exhaustion test must also prove that no new
`CombinedTerminalSurfaceKinds.All` value is implicitly absorbed by an existing
prefix arm without a mapped, suppressed, or deferred decision.

All current `asmx-*` variants, including `asmx-client`, `asmx-config`, and
`asmx-metadata`, `asmx-service`, and `asmx-operation`, intentionally map to
`legacy-communication terminal context`; `asmx-config` does not redirect to
package/config context. The literal `dependency-surface` value has no
dedicated mapping arm in the current implementation; it reaches
`dependency-surface terminal context` through the default catch-all branch. Do
not add a named `dependency-surface` arm unless a spec decision records why the
catch-all behavior should change. `message-unknown` maps to
`message-surface terminal context` because its presence as a selected path
terminal is static evidence of a message-surface terminus, not proof of
message semantics or complete classification.

## Exact Bridge Contract

Current bridge coverage should be documented and tested as exact selected
property identity, not proximity. The current implementation uses
ordinal-ignore-case symbol/display comparison for these identities. PR 1 must
record whether that is the intended exact-bridge contract, such as to tolerate
mixed-case symbol canonicalization, or a known boundary. If it is intentional,
tests should assert the accepted case-insensitive behavior; if not, tests
should include a case-only negative fixture before tightening the bridge. The
implementation may use existing `PropertyFlowReporter` behavior as the
baseline:

- selected combined fact ID present in the path;
- selected root symbol ID present in the path;
- selected root target symbol present in the path;
- selected `symbolId`, `memberSymbolId`, or `targetSymbolId` metadata present
  in the path;
- selected type-qualified model property present in the path;
- selected type-qualified DTO property present in the path;
- selected type-qualified display/model binding property present in the path.

The exact bridge contract intentionally excludes:

- method selector roots;
- endpoint selector roots;
- class, file, namespace, package, route, or dependency selector roots;
- same method, endpoint, class, file, namespace, project, or folder proximity;
- same short property, symbol, or method name;
- route-flow context groups, touched files, touched symbols, and dependency
  surfaces without selected-property identity.

If an implementation adds another bridge family, it must add tests proving both
the positive exact bridge and at least one negative proximity-only fixture that
looks similar but lacks the selected-property identity.

If PR 1 defers a final case-boundary decision, that deferral must be resolved
before any PR 2 or later bridge-family expansion proceeds.

## Negative Fixture Matrix

The implementation should create focused fixtures with tiny combined indexes.
Each negative fixture should contain a real terminal surface and a plausible
nearby selected root, but no exact selected-property bridge.

Required matrix:

| Negative case | Expected assertion |
| --- | --- |
| same method only | no `StaticTerminalContext`, no `terminalContextKind` |
| same endpoint or route only | no `StaticTerminalContext`, no `terminalContextKind` |
| same class only | no `StaticTerminalContext`, no `terminalContextKind` |
| same file only | no `StaticTerminalContext`, no `terminalContextKind` |
| same namespace/folder/project only when represented | no `StaticTerminalContext`, no `terminalContextKind` |
| same short property name only | no `StaticTerminalContext`, no `terminalContextKind`; use an unqualified node symbol/display such as `Email` against a root whose qualified target is `ProfileDto.Email`, not the qualified form in both places |
| same short symbol or method name only | no `StaticTerminalContext`, no `terminalContextKind` |
| current property-flow generic selected names without exact identity (`id`, `name`, `type`, `value`, `state`, `status`) | no terminal context and no stronger-than-supported classification |
| broader route-flow/high-fan-out generic examples such as `result` or `response` when consulted by a future implementation | explicit compatibility decision plus no terminal context without exact identity |
| broad endpoint dependency or route-flow context only | no property-flow terminal context |

These tests are not proof of complete false-positive coverage. They are
regression guards for known tempting inference paths.

Vocabulary tests should exercise the `SurfaceKind` mapping path directly. The
implementation also has node-kind terminal heuristics, but those are separate
from terminal-context vocabulary: a terminal-looking node kind with no
`SurfaceKind` must not create `terminalContextKind` metadata.

## Rule Catalog And Version Decisions

PR #400 reuses existing combined path evidence; it does not add a new
`property-flow.terminal-context.v1` rule. The follow-up implementation should
preserve that decision unless new emitted behavior requires a rule.

`StaticTerminalContext` notes and `terminalContextKind` safe metadata are
additive presentation. They do not carry a dedicated terminal-context rule ID
in PR #400. Rule resolution tests should assert the existing path edge and
source terminal fact rules resolve in `rules/rule-catalog.yml`, such as
`combined.paths.surface-evidence.v1`, rather than expecting a terminal-context
node rule that does not exist. Verify any named rule ID against the current
catalog in the implementation branch.

Reuse is acceptable when:

- terminal context remains additive note/safe metadata on an existing
  property-flow path;
- the path edge still carries a catalogued rule such as
  `combined.paths.surface-evidence.v1`;
- the source terminal fact keeps its source rule ID and evidence tier;
- no new gap code, edge kind, row family, top-level JSON section, or
  consumer-visible conclusion is emitted.

A rule/catalog update is required when:

- property-flow emits a new terminal-context rule ID;
- property-flow emits a new terminal-context gap code;
- property-flow adds a new context row family or top-level JSON section;
- bucket semantics change the meaning of an existing path, node, edge,
  inventory, gap, summary, or Markdown section;
- a broader terminal family introduces a new evidence bridge not already
  catalogued.

Report version `1.0` may remain only if metadata is additive and safely
ignorable. A version bump is required if terminal context becomes required,
changes existing semantics, or alters consumer expectations.

The version pin test should assert `report.Version == "1.0"` for the
terminal-context fixture when the implementation chooses additive metadata
without a version bump.

Path notes are currently sorted with ordinal string ordering, so
`StaticRouteFlowContext` precedes `StaticTerminalContext` when both appear.
Future note prefixes that would change consumer-visible ordering need an
explicit compatibility/version decision. Tests should assert the ordinal order
explicitly.

## PR Slices

### PR 1: Coverage Harness

Recommended first implementation PR:

1. Add vocabulary bucket tests for every current
   `CombinedTerminalSurfaceKinds.All` value.
   Include the literal `dependency-surface` value and all current `asmx-*`
   variants in the table.
2. Add exact selected-property bridge positive tests for current bridge
   identities where fixtures can represent them without scanner changes.
   Isolate the selected combined fact ID bridge in at least one fixture when
   practical so it is not silently covered by a simultaneous symbol bridge.
3. Add the required proximity-only negative fixtures.
4. Add rule-catalog resolution assertions for emitted path/surface rule IDs.
5. Add a report-version pin or compatibility assertion for the current
   property-flow report version decision.
6. Record report-version and rule-catalog decisions in the predecessor or this
   spec's implementation state before product edits.

PR 1 should not add new terminal-context families or scanner facts.

### PR 2: Broader Terminal Family, If Still Needed

Only after PR 1 passes, a later implementation PR may add one broader terminal
family. It must choose exactly one family, document its bridge, update the rule
catalog if needed, and add positive/negative tests for that family.

Candidate families remain bounded to existing deterministic evidence:

- legacy communication surfaces;
- message surfaces;
- package/config surfaces;
- legacy data surfaces;
- generic dependency surfaces;
- selected validation/read/write/mapping/service context only where exact
  property-specific facts already exist.

## Validation Strategy

Spec-only PR validation:

- Kiro Opus review when available.
- Kiro Sonnet review when available.
- Patch Medium+ actionable findings.
- `git diff --check`.
- `./scripts/check-private-paths.sh`.
- Confirm diff is limited to this spec folder.

Implementation PR validation:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PropertyFlowTests`
- focused route-flow/path/reverse/export tests if touched;
- `dotnet test src/dotnet/TraceMap.sln`;
- `./scripts/check-private-paths.sh`;
- `git diff --check`;
- adapter validation from `docs/VALIDATION.md` only if scanner or language
  adapter behavior changes.
