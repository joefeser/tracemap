# Route-Centered Endpoint Trace Completeness Tasks

Status: implemented-first-slice-with-follow-ups

Readiness: follow-up-slices-available

Post-promotion note: PR #241 implemented the touched-file/touched-symbol first
slice. Remaining unchecked items are follow-up slices.

## Spec Delivery Tasks

- [x] 1. Draft public-safe Kiro spec files.
  - [x] Add `requirements.md`.
  - [x] Add `design.md`.
  - [x] Add `tasks.md`.
  - [x] Add `implementation-state.md`.
  - [x] Add `review-packet.md`.
  - [x] Keep examples synthetic and generic.
  - [x] Avoid private repo names, private local paths, raw private routes, raw
        SQL/config values, snippets, hostnames, secrets, raw remotes, and
        private sample labels.

- [x] 2. Review the spec before opening the PR.
  - [x] Run Opus Kiro spec review when available, or record the exact blocker.
  - [x] Run Sonnet Kiro spec review when available, or record the exact blocker.
  - [x] Patch all Medium+ findings.
  - [x] Patch Low findings only when narrow and safe.
  - [x] Run at most two re-review cycles, preferring Sonnet for the final
        re-review unless Opus is clearly needed.
  - [x] Record commands, artifacts, coverage, findings, and dispositions in
        `implementation-state.md`.

- [x] 3. Validate the spec-only change.
  - [x] Run `git diff --check`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run any existing spec lint/check if present, or record that none exists.
  - [x] Confirm the diff is limited to
        `.kiro/specs/route-centered-endpoint-trace-completeness/`.
  - [x] Record validation results in `implementation-state.md`.

## Implementation Tasks

- [x] 4. Confirm route-flow contract and rule catalog coverage. Requirements:
      1, 5, 6.
  - [x] Preserve `tracemap route-flow`, `reportType = "route-flow"`, and
        `version = "1.0"` unless a later breaking-schema spec supersedes this
        one.
  - [x] Confirm all emitted rows use existing `combined.route-flow.*` rule IDs
        or add narrowly documented rule-catalog entries before emitting new
        row kinds.
  - [x] Add or update catalog assertions so every emitted route-flow rule ID
        resolves and no parallel route-flow namespace is introduced.
  - [x] Confirm limitations cover static evidence, runtime execution,
        production traffic, runtime DI, dynamic dispatch, branch feasibility,
        query execution, release safety, outage cause, and business impact.

- [x] 5. Implement safe selector trace metadata. Requirements: 1, 6, 7.
  - [x] Reuse existing route/client/from-* selector normalization.
  - [x] Record selector kind, safe normalized key, match mode, and redaction
        state.
  - [x] Prevent unsafe selector values from appearing in Markdown, JSON, logs,
        or committed fixtures.
  - [x] Add selector miss, unsafe selector, dynamic URL, and reduced-coverage
        tests.

- [x] 6. Add touched-file summaries from existing rows. Requirements: 2, 3, 6.
  - [x] Derive summaries from entry evidence, flow rows, logic rows,
        dependency surfaces, and gaps without mutating the combined index.
  - [x] Group by source label, commit SHA, repo-relative file path, and safe
        source-scoped identity.
  - [x] Preserve supporting row IDs, weakest classification, weakest coverage,
        rule IDs, evidence tiers, and line-span ranges.
  - [x] Sort deterministically and add byte-stability tests.

- [x] 7. Add touched-symbol summaries from existing rows. Requirements: 2, 3,
      6.
  - [x] Extract stable symbol IDs and safe display names from selected
        route-flow evidence.
  - [x] Include source label, file span, symbol kind where available, supporting
        row IDs, weakest classification, coverage, and limitations.
  - [x] Use explicit unavailable placeholders when symbol identity or spans are
        absent.
  - [x] Add tests for route handler symbols, service/interface symbols,
        implementation candidates, dependency symbols, and missing-symbol gaps.

- [ ] 8. Clarify method/service row grouping. Requirements: 3, 5, 6.
  - Coordination note: the route-flow service/data composition subset of this
        task is owned by
        `.kiro/specs/route-flow-service-data-composition-next`. This task keeps
        broader endpoint-trace completeness work that is not covered there.
  - [ ] Reuse existing `flowRows` where possible instead of duplicating path
        graph rows.
  - [ ] Add safe grouping labels for method rows, service rows, interface
        candidate rows, direct concrete rows, and bridge gaps.
  - [ ] Preserve candidate wording and `NeedsReviewStaticRouteFlow` caps for
        interface/override/DI boundaries.
  - [ ] Add direct call, interface single candidate, multiple candidate, no
        candidate, syntax-only, and high-fan-out tests.

- [ ] 9. Complete data/query/dependency and value-origin rows. Requirements:
      4, 5, 6.
  - Coordination note: grouped data/query/dependency/value-origin context that
        is already joined to selected route-flow evidence is owned by
        `.kiro/specs/route-flow-service-data-composition-next`. This task keeps
        broader endpoint-trace completeness work that is not covered there.
  - [ ] Reuse `logicRows` and `dependencySurfaces` for object-shape,
        query-shape, repository/data, package/config, HTTP, queue/event,
        storage, WCF, remoting, and legacy-data evidence.
  - [ ] Project `combined_argument_flows` only when joined to selected static
        route-flow evidence.
  - [ ] Project `combined_fact_symbols` only when the selected source-local
        symbol identity is rule-backed.
  - [ ] Emit projection-unavailable gaps for unjoinable or unsafe rows.
  - [ ] Add tests for safe metadata, unjoinable projection, raw SQL/config/URL
        redaction, and attached-versus-context row labeling.

- [ ] 10. Enforce coverage, gap, and classification downgrades. Requirements:
      1, 3, 4, 5.
  - Coordination note: downgrade tests and gaps for service/data grouping rows
        are owned by `.kiro/specs/route-flow-service-data-composition-next`.
        This task keeps broader endpoint-trace completeness downgrade coverage.
  - [ ] Keep classification values limited to the existing
        `RouteFlowClassifications` vocabulary.
  - [ ] Require full route-flow coverage before strong or clean no-evidence
        conclusions.
  - [ ] Cap weak, fallback, ambiguous, candidate, dynamic, high-fan-out,
        reduced, missing-extractor, unknown-commit, and truncated evidence.
  - [ ] Ensure touched-file and touched-symbol summaries inherit the weakest
        classification and coverage from contributing rows.
  - [ ] Add reduced coverage, unknown commit SHA, missing schema, missing
        extractor, traversal bound, and clean no-evidence tests.

- [x] 11. Extend Markdown and JSON output safely. Requirements: 6, 7.
  - [x] Add backward-compatible JSON fields only.
  - [x] Keep existing Markdown sections and add only narrow sections that help
        trace completeness.
  - [x] Use deterministic IDs, ordering, nulls, empty arrays, and closed-set
        placeholders.
  - [x] Add forbidden-wording tests for runtime and business-impact claims.
  - [x] Add byte-stability tests for Markdown and JSON.

- [x] 12. Validate implementation. Requirements: 7.
  - [x] Run focused route-flow tests.
  - [x] Run `dotnet test`.
  - [x] Run a public-safe CLI smoke over a synthetic or checked-in fixture.
  - [x] Follow `docs/VALIDATION.md` for route-flow/reporting changes, or
        record an explicit deferral with rationale.
  - [x] Run `git diff --check`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Confirm generated outputs do not contain unsafe values.

## Suggested PR Boundaries

- PR 1: Add touched-file and touched-symbol summaries over existing route-flow
  rows, deterministic rendering, rule/catalog assertions, and privacy tests.
- PR 2: Improve method/service grouping and candidate boundary presentation
  without changing scanner evidence.
- PR 3: Complete value-origin and fact-symbol projection presentation,
  projection-unavailable gaps, and data/query/dependency row grouping.
- PR 4: Add public-safe CLI smoke fixtures and broader downgrade/redaction
  coverage.
