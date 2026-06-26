# Route Flow Service/Data Composition Next Implementation State

Status: implemented-pr1-merged-followups-superseded
Readiness: no-new-work-from-this-spec
Spec branch: `codex/spec-route-flow-service-data-composition-next`
Implementation branch: `codex/implement-route-flow-service-data-composition`
Target base: `dev`
Primary issue: `#179`
Public claim level: hidden

## Reconciliation State

Reconciled against `origin/dev` on 2026-06-25 at `87fe78a3`.

Evidence on `dev`:

- PR #287 merged this continuation spec (`77ced02d`).
- PR #292 merged the PR 1 implementation slice for route-flow service/data
  context groups (`050a9f84`).
- Current code contains additive `RouteFlowReport.contextGroups`, Markdown
  Context Groups rendering, route-flow rule-catalog documentation, and focused
  route-flow tests for those emitted rule IDs and context rows.

Do not use this spec as the next implementation queue. Its remaining unchecked
follow-ups were intentionally deferred beyond PR 1 and are now superseded by
`.kiro/specs/route-flow-service-data-composition-final/`, which is the current
authority for remaining route-flow service/data composition work.

## Implementation Slice

Selected PR boundary: PR 1 from the task list.

This implementation adds additive route-flow `contextGroups` to
`route-flow-report.json` and a `## Context Groups` section to
`route-flow-report.md`. The groups are presentation context over already
selected route-flow rows only; they do not create a new traversal engine or a
new rule namespace.

JSON placement:

- `RouteFlowReport.contextGroups`
- `reportType` remains `route-flow`.
- JSON `version` remains `1.0`.
- Missing `contextGroups` in older JSON renders as an empty list.

Group kinds emitted by the selected slice:

- `method`
- `service`
- `interface-candidate`
- `repository`
- `query`
- `data-surface`
- `dependency`
- `legacy-data`
- `value-origin`
- `gap`

Closed metadata:

- `groupKind`
- `matchKind`
- `valueSafety`
- `rowCount`
- `sourceLabel`
- selected safe row metadata such as `surfaceKind`, `logicKind`,
  `attachmentKind`, `tableNameHash`, `columnNamesHash`, `shapeHash`, and
  `textHash`

The group evidence reuses `combined.route-flow.report.v1` and includes sorted
supporting row IDs, rule IDs, evidence tiers, fact IDs, edge IDs, coverage,
file spans where available, commit SHA where known, extractor identity, and
limitations from contributing rows. Group classification, evidence tier, and
coverage are the weakest values from contributing rows.

No new rule IDs were added. Existing route-flow rules carry the underlying
evidence; `combined.route-flow.redaction.v1` remains present when unsafe values
are hashed or omitted.

Deferred from this slice:

- narrower gaps for adjacent but unjoinable data/query/dependency evidence;
- new downgrade rules beyond the shipped route-flow gaps;
- runtime DI proof, branch feasibility, SQL execution, database/schema proof,
  mutation semantics, collection contents, or production traffic claims.

## Summary

This is a spec-only continuation packet for issue #179, "Compose route-flow
evidence through service and data facts." The prior
`route-flow-service-data-composition` slices are already implemented and
promoted. They added route-flow argument/fact-symbol projection,
parameter-forward bridging, terminal data-surface rows, and specific gaps.

The remaining useful next slice is narrower: service/data grouping, data/query/
dependency/value-origin context polish, downgrade hardening, deterministic
output, safety tests, and broader public-safe route-flow fixtures.

No product code, site files, generated outputs, or rule catalog entries are
changed by this spec-only branch.

## Live State Reviewed

- `AGENTS.md`
- `docs/NEXT_EXECUTION_REPORT.md`
- GitHub issue #179
- `.kiro/specs/route-centered-static-flow-report/`
- `.kiro/specs/route-flow-service-data-composition/`
- `.kiro/specs/route-centered-endpoint-trace-completeness/`
- `.kiro/specs/legacy-data-model-metadata-extraction/`
- `.kiro/specs/query-pattern-reporting-v2/`
- `.kiro/specs/sql-dependency-surfaces/`
- `.kiro/specs/ui-field-property-lineage/`
- Route-flow reporting, CLI, rule, and test references found by `rg`

## Scope Decisions

- This spec is a continuation, not a rewrite.
- The implementation target remains `tracemap route-flow`.
- The report type remains `route-flow`.
- The route-flow classification vocabulary remains unchanged.
- This spec owns only the service/data composition subset of
  `route-centered-endpoint-trace-completeness` tasks 8-10:
  - task 8 only for grouping existing selected route-flow method/service rows;
  - task 9 only for joined data/query/dependency/value-origin context already
    present in route-flow row families;
  - task 10 only for downgrade tests and gaps affecting those grouped rows.
  It does not own touched-file or touched-symbol summaries, selector trace
  metadata, broad endpoint-trace completeness, or unrelated route-flow
  presentation backlog.
- Existing `combined.route-flow.*` rule IDs should be reused unless an
  implementation proves a new documented rule is required.
- Grouping is presentation context and must inherit the weakest evidence tier,
  weakest classification, and weakest coverage from supporting rows.
- Data/query/dependency/value-origin context can render only when joined to
  selected static route-flow evidence.
- Adjacent but unjoined facts become gaps, not inferred flows.
- Private legacy smoke validation remains local-only and generic in committed
  docs.

## Safety Boundaries

The spec forbids committed private local paths, private sample names, private
route strings, raw SQL, raw config values, connection strings, secrets, raw
endpoint URLs, raw remotes, source snippets, and generated local artifacts.

The implementation must not claim runtime request execution, endpoint
reachability, dependency-injection binding, branch feasibility, SQL execution,
database existence, row counts, production traffic, release approval, AI impact
analysis, LLM analysis, embeddings, vector search, or prompt classification.

## Review State

Initial reviews completed:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-next --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-next --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Opus completed with full coverage. It found one blocking issue: unresolved
  ownership overlap with `route-centered-endpoint-trace-completeness` tasks
  8-10. Patched by assigning this spec the service/data composition subset and
  requiring future implementation to reconcile overlap before shipping code. It
  also suggested clarifying report-envelope rule ownership, broad evidence
  wording, connection-string redaction consistency, and PR boundaries.
- Sonnet completed with reduced coverage because Kiro reported denied subagent
  tool access, but it returned concrete findings. Blocking findings patched:
  spec-delivery checklist state and explicit existing gap-code mapping for
  missing optional schema/extractor evidence. Non-blocking findings patched:
  grouping metadata placement, extractor identity behavior, product-code
  validation guard, `TruncatedByLimit`, and private-smoke deferral.

Re-review completed:

- Opus re-review completed with full coverage. Remaining blockers were
  ownership reconciliation and unfinished spec-delivery gates. Patched by
  narrowing this spec's ownership to exact service/data grouping sub-scope and
  by completing validation below. Important non-blocking findings around
  `evidenceKind`, grouping metadata placement, and broad evidence family
  wording were patched.
- Sonnet re-review completed with reduced coverage because Kiro reported denied
  subagent tool access. Blocking finding referenced the retired
  `ControllerToServiceBridgeMissing` gap code. A direct `rg` scan found that
  code was not present in this new spec folder, but the design now explicitly
  forbids emitting it and names the shipped narrower gap codes. Non-blocking
  findings around overlap ownership and closed-set metadata tests were patched.
- Sonnet second re-review completed with full coverage. Blocking findings were
  terminology consistency for generated local artifact paths and unfinished
  validation gates. Patched by aligning task wording and completing validation.
- Opus second re-review completed with reduced coverage because Kiro reported
  denied shell access. It agreed the remaining blocker was reciprocal ownership
  plus validation. Patched by adding coordination notes to
  `route-centered-endpoint-trace-completeness` tasks/state and completing
  validation. Non-blocking suggestions for schema/version, `evidenceKind`, and
  multi-extractor tests were folded into task 9.

Implementation review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-next --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Sonnet completed with full coverage.
- Blocking finding B1: `combined.route-flow.report.v1` did not document the
  new `contextGroups` JSON section in `rules/rule-catalog.yml`. Patched by
  adding the emitted section and a limitation stating that context groups are
  additive report summaries over already-selected rows.
- Blocking finding B2: task 9 lacked an emitted-rule-ID sweep test. Patched by
  adding a route-flow test that collects rule IDs from entry, flow, logic,
  dependency surface, touched file, touched symbol, context group, and gap
  rows, then asserts every emitted ID exists in `rules/rule-catalog.yml`.
- Non-blocking findings around `evidenceKind`, task ordering, reduced-coverage
  no-evidence tests, status wording, and context-group truncation remain
  follow-up guidance for later tasks 6, 7, and remaining task 9 work.

Implementation re-review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-next --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Sonnet completed with reduced coverage because Kiro reported denied tool
  access. It still returned concrete findings.
- Blocking finding B1: `tasks.md` and `implementation-state.md` lifecycle
  status labels were out of sync. Patched by setting both to
  `implemented-pr1-followups-deferred`.
- Blocking finding B2: context-group behavior was not asserted for interface
  multiple-candidate and no-candidate fixtures. Patched by adding assertions
  for `interface-candidate` context groups, candidate gap groups, and weakest
  review-tier inheritance in the existing fixtures.
- Blocking finding B3: deferred task 6/7 checkboxes looked like forgotten work.
  Patched by adding explicit "Deferred beyond PR 1" notes pointing to this
  implementation state file.
- Non-blocking closed-set `evidenceKind` and clean no-route-flow context-group
  assertions were also added while touching the test file.

## Validation

Spec-only validation completed before implementation:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- No dedicated spec/docs validation script beyond the private path guard was
  found for this spec-only packet.

Product validation completed for the implementation slice:

- `dotnet restore src/dotnet/TraceMap.sln`: passed with the repo's existing
  `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory warning.
- `dotnet test src/dotnet/TraceMap.sln --filter CombinedRouteFlowTests --no-restore`:
  passed, 29 tests after the emitted-rule-ID catalog sweep was added.
- `dotnet build src/dotnet/TraceMap.sln`: passed with the same existing
  NU1903 advisory warning.
- `dotnet test src/dotnet/TraceMap.sln --no-restore`: passed, 606 tests.
- After the implementation re-review fixes, focused route-flow tests still
  passed, 29 tests; build passed; full tests passed, 606 tests; `git diff
  --check` passed; and `./scripts/check-private-paths.sh` passed.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `./scripts/demo-public.sh .tracemap-demo`: passed. The script completed the
  public checked-in sample workflow, but this local script revision did not
  write route-flow artifacts despite `docs/VALIDATION.md` describing targeted
  route-flow as part of the public demo.
- Manual route-flow smoke over the generated public-demo endpoint combined
  index passed:
  `dotnet run --project src/dotnet/TraceMap.Cli -- route-flow --index .tracemap-demo/combined/endpoint-stack.sqlite --route "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out .tracemap-demo/reports/route-flow/endpoint-to-sql`
  produced `route-flow-report.md` and `route-flow-report.json` with `Context
  Groups` / `contextGroups`.
- Private legacy route-flow smoke was not run in this child context. That smoke
  remains local-only and should not commit private sample names, local paths,
  raw SQL, config values, snippets, secrets, raw remotes, or generated
  artifacts.

Generated `.tracemap-demo/` outputs are ignored and not committed.

## PR State

Pending.
