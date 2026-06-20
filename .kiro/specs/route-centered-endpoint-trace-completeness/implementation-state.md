# Route-Centered Endpoint Trace Completeness Implementation State

## Snapshot

- Spec: `route-centered-endpoint-trace-completeness`
- Branch: `codex/spec-route-centered-endpoint-trace-completeness`
- Base: `dev`
- Scope: spec-only PR for completing route-centered endpoint trace reports.
- Product code touched: none.
- Public-safety posture: examples are synthetic; no private repo names, private
  local paths, private route values, raw SQL/config values, snippets, hostnames,
  secrets, raw remotes, or private sample labels are intentionally included.

## Context Reviewed

- `.kiro/specs/route-centered-static-flow-report/`
- `.kiro/specs/route-flow-endpoint-composition/`
- `.kiro/specs/route-flow-service-data-composition/`
- `src/dotnet/TraceMap.Reporting/CombinedRouteFlowReport.cs`
- `rules/rule-catalog.yml` route-flow entries
- Public issue #159, "Add route-centered static call flow report"

## Scope Decisions

- Keep `tracemap route-flow` as the public command.
- Preserve `reportType = "route-flow"` and `version = "1.0"` unless a future
  breaking-schema spec supersedes this one.
- Treat this spec as a completion layer over current route-flow output, not a
  scanner rewrite.
- First implementation slice should summarize touched files and touched symbols
  from existing rows before adding deeper presentation changes.
- Interface, override, and DI-related rows remain static candidates and cannot
  prove runtime target selection.
- Argument/value-origin evidence is included only when existing
  `combined_argument_flows` evidence joins to selected static route-flow rows.

## Oddities And Follow-Ups

- Existing route-flow specs already implemented much of the composition
  vocabulary; this spec focuses on completeness and review ergonomics rather
  than inventing new classifications.
- Some existing implementation task lists still contain follow-up calibration
  items. This spec references those boundaries but does not mark them complete.
- No spec lint script has been identified yet; validation will record the final
  discovery result.

## Kiro Review Log

### Opus spec review

- Command:
  `node scripts/kiro-review.mjs --phase route-centered-endpoint-trace-completeness --kind spec --model claude-opus-4.8 --fresh`
- Result: completed with wrapper exit code 0.
- Artifact:
  `.tmp/kiro-reviews/route-centered-endpoint-trace-completeness/2026-06-20T202142-571Z-spec-claude-opus-4.8.meta.json`
- Coverage reported by wrapper: Full.
- Session: `92519e5b-dfec-4811-87ce-4702fb4d859c`.
- Review result: ready for implementation; no Medium+ findings.

### Sonnet spec review

- Command:
  `node scripts/kiro-review.mjs --phase route-centered-endpoint-trace-completeness --kind spec --model claude-sonnet-4.6 --fresh`
- Result: wrapper exited with code 1 after model output.
- Artifact:
  `.tmp/kiro-reviews/route-centered-endpoint-trace-completeness/2026-06-20T202354-446Z-spec-claude-sonnet-4.6.meta.json`
- Coverage reported by wrapper: Full.
- Session: `7f308025-761e-40ee-be41-db61c70fde2b`.
- Coverage caveat: Sonnet selected and reviewed
  `.kiro/specs/route-flow-endpoint-composition/` instead of this spec despite
  the phase-specific wrapper command. Findings were treated as reduced coverage
  for this spec.

### Sonnet re-review

- Command:
  `node scripts/kiro-review.mjs --phase route-centered-endpoint-trace-completeness --kind re-review --model claude-sonnet-4.6 --fresh`
- Result: completed with wrapper exit code 0.
- Artifact:
  `.tmp/kiro-reviews/route-centered-endpoint-trace-completeness/2026-06-20T202521-033Z-re-review-claude-sonnet-4.6.meta.json`
- Coverage reported by wrapper: Full.
- Session: `fe7f2921-6168-49fd-92f0-7a6f2973a1c4`.
- Coverage caveat: Sonnet again selected the older endpoint-composition spec
  and related route-flow specs instead of this new spec. The run is recorded as
  required Sonnet execution evidence but not treated as clean review coverage
  for this spec.

## Review Findings And Dispositions

- Opus LOW-1: the suggested `RouteFlowValueOriginRow` could invite a parallel
  structure when current argument projection uses logic rows.
  Disposition: patched `design.md` to remove the standalone example row and
  state that argument/value-origin evidence should follow
  `combined.route-flow.argument-projection.v1` logic-row projection unless a
  future implementation review proves a separate additive shape is necessary.
- Opus LOW-2: touched-file and touched-symbol summaries needed explicit rule
  ID stamping semantics.
  Disposition: patched `design.md` to state summaries are report-envelope
  aggregations under `combined.route-flow.report.v1` and inherit supporting row
  rule IDs, tiers, classifications, coverage, and limitations.
- Sonnet LOW-1 from drifted review: clarify `UnknownAnalysisGap` as a possible
  top-level summary classification.
  Disposition: patched `design.md`; the clarification also applies to this
  spec.
- Sonnet LOW-2 from drifted review: clarify `SelectorNoMatch` versus
  `MissingRouteRoot` boundaries.
  Disposition: patched `requirements.md`; selector misses, including indexes
  with no endpoint route evidence, remain `SelectorNoMatch`, while
  `MissingRouteRoot` describes matched selector context that cannot produce
  route-root evidence for composition.
- Sonnet wording clarification from drifted review: clarify the stricter
  route-root fallback cap compared with ordinary downstream structural
  evidence.
  Disposition: patched `design.md`.
- Medium+ findings: none from the usable Opus review. The Sonnet runs did not
  provide reliable current-spec coverage.

## Validation Log

- `git diff --cached --check`: passed after staging the intended spec files.
- `./scripts/check-private-paths.sh`: passed after staging the intended spec
  files.
- Spec lint/check discovery: no dedicated spec lint script found. The only
  matching script under `scripts/` was `scripts/check-private-paths.sh`; search
  hits in docs/specs described Kiro review, `git diff --check`, and the private
  path guard rather than a separate repo spec lint command.
- Diff scope: staged files are limited to
  `.kiro/specs/route-centered-endpoint-trace-completeness/`.

## PR Loop Log

Pending.
