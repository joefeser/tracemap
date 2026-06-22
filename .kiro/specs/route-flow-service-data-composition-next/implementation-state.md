# Route Flow Service/Data Composition Next Implementation State

Status: reviewed-ready-for-implementation
Readiness: ready-for-implementation
Spec branch: `codex/spec-route-flow-service-data-composition-next`
Target base: `dev`
Primary issue: `#179`
Public claim level: hidden

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

## Validation

Spec-only validation completed:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- No dedicated spec/docs validation script beyond the private path guard was
  found for this spec-only packet.
- Product code, site files, generated outputs, private paths, private names,
  raw SQL, config values, snippets, secrets, raw remotes, and local-only
  artifacts were not intentionally committed.

No `dotnet` validation was run because this is a spec-only PR and product code
did not change.

## PR State

Pending.
