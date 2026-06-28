# UI Field Property Lineage Terminal Context Design

## Overview

This spec narrows the next implementation step after PR #376. The work is not
a new scanner and not a broad route-flow rewrite. It is a conservative
report-layer composition slice for `tracemap property-flow`: attach backend
terminal context only when existing facts expose a selected-property trail.

Expected evidence shape:

```text
selected property root
  -> existing property-specific bridge
  -> backend DTO/model/property/parameter/payload evidence
  -> validation/read/write/mapping/service/query/data/dependency terminal fact
  -> terminal context row or explicit gap
```

Every arrow is optional and evidence-gated. Missing evidence is a useful gap,
not permission to infer through route reachability or source proximity.

## Current Context

`main` and `dev` were recently aligned at `4b5844ff`. This spec was drafted
after fetching `origin/dev` and verifying the PR #376 baseline SHA:

```text
4b5844ff07199969eacd040e9383037d0b266d49
```

Before re-review, `origin/dev` was fetched again and the spec branch was
fast-forwarded to:

```text
6bec000244340311cc385e4ebdeee4655a7251d4
```

PR #376 merged into `dev` on 2026-06-26 as merge commit
`21343d88e795d0f0348dae361036241c128a343b`. It added
`UnsupportedRouteFlowSchema` and route-schema gap evidence for
property-flow. The next slice should build on that baseline rather than
reopening missing/empty/unsupported route-flow schema handling.

## Relationship To Existing Specs

| Existing spec | Relationship |
| --- | --- |
| `ui-field-property-lineage` | Owns the broad property-flow command, selectors, roots, report shape, and safety model. |
| `ui-field-property-lineage-next-slice` | Documents model-binding and property identity joins that form upstream selected-property bridge families. |
| `ui-field-property-lineage-continuation` | Documents the current PropertyFlowReporter baseline for `RouteFlowNoPropertyContext`, `RouteFlowUnavailable`, route-flow context notes, and `property-flow.edge.v1` gaps. |
| `ui-field-property-lineage-composition` | Owns UI/backend property composition strategy and says broad endpoint context is not property lineage. |
| `route-flow-service-data-composition` and successors | Own route-centered service/data/dependency rows that property-flow may reuse only as terminal context after a selected-property bridge exists. |
| PR #376 implementation notes | Establish current route-flow schema/gap baseline, including `UnsupportedRouteFlowSchema`. |

This spec does not replace the prior composition spec. It chooses the next
small backend-only implementation slice from that work queue.

## Non-Goals

- No new public command.
- No scanner rewrite.
- No new persisted derived property-flow table.
- No runtime request execution, production execution, live HTTP, browser
  automation, DOM visibility, credentials, telemetry, or production login.
- No authorization, role, feature-flag, dependency-injection runtime target,
  serializer runtime, branch feasibility, mutation, persistence outcome,
  database execution, traffic, release safety, or impact proof.
- No LLM calls, embeddings, vector databases, or prompt-based classification
  in TraceMap core.
- No raw source snippets, raw SQL, raw config values, raw URLs, raw remotes,
  hostnames, local absolute paths, connection strings, secrets, credentials,
  form values, or private sample names in default reports.

## Proposed PR 1 Slice

PR 1 should be intentionally small:

1. Audit current `PropertyFlowReport.cs`, `PropertyFlowTests.cs`,
   route-flow/path/reverse row shapes, and `rules/rule-catalog.yml`.
2. Record in this spec's `implementation-state.md` which existing facts can
   support terminal context without scanner changes.
3. Add catalog-first rule/gap coverage if existing `property-flow.edge.v1`,
   `property-flow.schema.v1`, `property-flow.coverage.v1`, route-flow, path,
   reverse, query, data, dependency, or redaction rules are insufficient.
   Record in `implementation-state.md` which existing rules are reused and why
   `property-flow.terminal-context.v1` is or is not needed before writing
   implementation code.
4. Attach one narrow family of backend terminal context only when a selected
   property trail reaches it through existing facts.
5. Add negative tests proving broad endpoint reachability, route reachability,
   same method, same class, same file, and same property name do not attach
   terminal context.
6. Add one positive fixture proving property-specific evidence can attach the
   selected terminal context family.
7. Keep report version `1.0` if the metadata is additive and consumers safely
   ignore it; otherwise bump/report the compatibility reason.

Good PR 1 candidates are validation/read/write terminal context or
fact-symbol-backed query/data/dependency context, whichever live code can
support with the smallest rule-backed bridge.

## Deferred Follow-Ups

- Additional terminal context families after PR 1 proves the gate.
- Angular/Razor bridge expansion that requires new scanner facts.
- Persisted property-flow rows or write-mode storage.
- Vault, docs-export, evidence-pack, explorer, or evidence graph changes unless
  PR 1 changes row shape in a way those consumers must handle.
- Advanced serializer contract expansion.
- Runtime DI container solving.
- Branch feasibility or symbolic execution.
- Mutation or taint analysis beyond existing deterministic facts.
- Runtime browser/live HTTP/production validation.
- Public site copy or public claims.
- AI/LLM classification, embeddings, vector databases, or prompt-based
  analysis.

## Evidence Gate

Terminal context may attach only when one of these property-specific bridge
families exists and carries rule-backed supporting IDs:

- exact selected fact or symbol identity;
- model-binding or DTO/model property facts tied to the selected property;
- payload/body/query/route field evidence tied to the selected property;
- value-origin, argument-flow, parameter-forward, alias, or assignment evidence;
- object-shape, projection, mapper, constructor, or initializer evidence tied
  to the selected property;
- fact-symbol attachment from a selected property path to the terminal fact;
- route-flow/path/reverse evidence that is already selected by a
  property-specific bridge.

The following are never sufficient by themselves:

- same endpoint;
- route-flow row for the same normalized route key;
- same controller, handler, service method, class, file, or namespace;
- same property or parameter short name;
- broad dependency edge from the endpoint;
- route-flow context group, touched file, touched symbol, or dependency surface
  with no selected-property bridge;
- generic property names such as `id`, `name`, `value`, `state`, `status`,
  `type`, `result`, or `response`.

Generic property names are insufficient as the primary bridge for any terminal
context kind, even when the name appears in a path, route-flow row, or terminal
fact. They may attach terminal context only when narrowed by exact fact/symbol
identity, type-qualified model/DTO identity, or Tier2-or-stronger structural
property identity.

## Terminal Context Kinds

Initial context kinds should stay closed and additive:

| Context kind | Examples | Required bridge |
| --- | --- | --- |
| `validation` | attributes, guard reads, fluent validation-like facts, branch checks | selected property read or model-binding/property fact |
| `read-write` | read, write, assignment, local alias, field/member origin | selected property fact plus value-origin/assignment evidence |
| `mapping` | manual mapping, object initializer, constructor mapping, projection, mapper config | selected property source and target properties with mapping evidence |
| `service` | service/repository method context | selected call/value-origin/fact-symbol bridge |
| `query` | query pattern or SQL shape metadata | selected property reaches query fact through path/fact-symbol/value evidence |
| `data` | entity/data surface/persistence metadata | selected property reaches data surface through mapping/query/path evidence |
| `dependency` | package/config/http/event/storage/legacy dependency surface | selected property reaches terminal surface through existing path/reverse/route-flow evidence |

PR 1 may implement only one context kind. The chosen kind must be recorded in
`implementation-state.md`.

## Report Model

Prefer additive metadata on existing property-flow paths, edges, gaps, and
inventory rows before adding a new top-level JSON section. Suggested safe keys:

- `terminalContextKind`
- `terminalEvidenceKind`
- `propertyTrailKind`
- `bridgeKind`
- `terminalRuleId`
- `terminalFactId`
- `terminalEdgeId`
- `routeFlowContextKind`
- `coverageLabel`
- `redactionState`

If a new top-level `terminalContext` array is needed, it must be versioned,
deterministically sorted, and proven safe for current consumers or accompanied
by a report version bump. A version bump is required if terminal context
becomes a required top-level key or changes the meaning of existing path, gap,
inventory, root, or summary rows for current consumers. Keeping report version
`1.0` requires at least one existing property-flow consumer compatibility test,
such as docs-export or another touched evidence-export path.

## Rule ID Plan

Reuse first:

- `property-flow.edge.v1`
- `property-flow.path.v1`
- `property-flow.coverage.v1`
- `property-flow.schema.v1`
- `property-flow.truncation.v1`
- `combined.route-flow.*.v1`
- existing path/reverse/query/data/dependency/mapping/value-origin/redaction
  rules in `rules/rule-catalog.yml`

Candidate new rule, only if reuse is insufficient:

- `property-flow.terminal-context.v1`

Candidate gap codes, only after catalog or implementation-state closure:

- `TerminalContextUnavailable`
- `TerminalContextNoPropertyTrail`
- `TerminalContextAmbiguous`
- `TerminalContextTruncated`
- `TerminalContextUnsupportedSchema`

No implementation may emit these candidates until the rule catalog documents
the rule behavior and limitations, or the gap code is mapped to an existing
catalogued rule with tests proving the mapping. `implementation-state.md` may
record the mapping decision, but it is not an emission authority by itself.
The implementation PR that introduces any candidate gap code or rule SHALL add
the catalog entry, emitted-artifact documentation, and at least one test in the
same PR before the code path can reach production.

## Classification

Terminal context inherits the weakest required evidence in the selected
property trail.

- `StrongStaticLineage` requires a complete semantic or equivalently strong
  selected-property trail, compatible schema, known commit SHA, known extractor
  identity, full relevant coverage, and no blocking gaps.
- `ProbableStaticLineage` requires Tier2 or stronger property-specific
  structural evidence for each non-semantic hop.
- `NeedsReviewLineage` covers syntax-only, same-name plus weak supporting
  bridge, alias-only, convention-only, high fan-out, generated-code uncertain,
  ambiguous, or partial terminal evidence.
- `UnknownAnalysisGap` covers missing schema, unsupported schema, reduced
  coverage, unknown commit SHA, missing extractor identity, absent
  property-specific bridge, dynamic behavior, or truncation that prevents a
  conclusion.

Endpoint, route, method, class, file, or property-name proximity cannot produce
`ProbableStaticLineage` or attach terminal context by itself.

## Validation Strategy

Spec-only PR validation:

- Kiro Opus review when available.
- Kiro Sonnet review when available.
- Patch Medium+ actionable findings.
- One bounded re-review when feasible.
- `git diff --check`.
- `./scripts/check-private-paths.sh`.
- Confirm diff is limited to this spec folder.

Implementation PR validation:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PropertyFlowTests`
- focused route-flow/path/reverse/export tests when touched;
- `dotnet test src/dotnet/TraceMap.sln`;
- `./scripts/check-private-paths.sh`;
- `git diff --check`;
- additional adapter validation from `docs/VALIDATION.md` if any scanner or
  language adapter changes are made.
