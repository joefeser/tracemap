# Route-Centered Endpoint Trace Completeness Implementation State

## Snapshot

- Spec: `route-centered-endpoint-trace-completeness`
- Branch: `codex/implement-route-centered-endpoint-trace-completeness`
- Base: `dev`
- Scope: first implementation slice for touched-file and touched-symbol route-flow summaries.
- Product code touched: `src/dotnet/TraceMap.Reporting/CombinedRouteFlowReport.cs`.
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
- Touched-file summaries are derived from existing route-flow entry evidence,
  flow rows, logic rows, dependency surfaces, and gaps. They group by safe
  source label, commit SHA, and repo-relative file path, and inherit supporting
  row IDs, route-flow rule IDs, evidence tiers, weakest classification,
  weakest coverage, and line-span ranges.
- Touched-symbol summaries are derived from existing safe row display
  identities and additive report-envelope evidence. They do not join back to
  scanner tables or infer runtime dispatch targets.
- New JSON fields are additive: `touchedFiles` and `touchedSymbols`.
- Markdown adds narrow `Touched Files` and `Touched Symbols` sections while
  preserving existing route-flow sections.
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

### Sonnet implementation review

- Command:
  `node scripts/kiro-review.mjs --phase route-centered-endpoint-trace-completeness --kind implementation --model claude-sonnet-4.6 --fresh --save-review-text`
- Result: completed with wrapper exit code 0 but reduced coverage because Kiro
  reported denied tool access and drifted to older route-flow specs.
- Artifact:
  `.tmp/kiro-reviews/route-centered-endpoint-trace-completeness/2026-06-20T220425-006Z-implementation-claude-sonnet-4.6.meta.json`
- Session: `88f99416-924f-44a6-ad1f-9c17600cddde`.
- Disposition: treated as inconclusive for this implementation slice; Opus
  fallback was run.

### Opus implementation review

- Command:
  `node scripts/kiro-review.mjs --phase route-centered-endpoint-trace-completeness --kind implementation --model claude-opus-4.8 --fresh --save-review-text`
- Result: completed with wrapper exit code 0 and full coverage.
- Artifact:
  `.tmp/kiro-reviews/route-centered-endpoint-trace-completeness/2026-06-20T220559-845Z-implementation-claude-opus-4.8.meta.json`
- Session: `194b9b06-3fff-400e-a922-fb04cb643761`.
- Review result: no Medium+ findings.
- Low findings patched:
  - Updated `review-packet.md` so the packet describes the first
    implementation slice rather than the prior spec-only PR.
  - Updated Requirement 4 to name `FactSymbolProjectionUnavailable` alongside
    `ArgumentProjectionUnavailable`.
  - Scoped the deterministic ordering requirement to newly added arrays/maps
    while preserving existing route-flow row orderings.

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

- Implementation slice focused validation:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`
  passed after adding touched-file/touched-symbol summaries and focused
  assertions.
- Solution build:
  `dotnet build src/dotnet/TraceMap.sln` passed.
- Solution tests:
  `dotnet test src/dotnet/TraceMap.sln` passed with 554 tests.
- Route-flow/reporting validation followed `docs/VALIDATION.md` reporting-change
  guidance with a fresh public-demo combine and route-flow smoke:
  `dotnet run --project src/dotnet/TraceMap.Cli -- combine --index .tracemap-demo/scans/typescript-endpoint-client/index.sqlite --label public-ts-client --index .tracemap-demo/scans/dotnet-endpoint-server/index.sqlite --label public-dotnet-server --out /tmp/tracemap-route-flow-smoke/combined.sqlite`
  passed, and
  `dotnet run --project src/dotnet/TraceMap.Cli -- route-flow --index /tmp/tracemap-route-flow-smoke/combined.sqlite --route "GET /api/admin/runner/get-by-id/{runnerId}" --out /tmp/tracemap-route-flow-smoke/route-flow`
  passed. Generated Markdown and JSON contain `Touched Files` /
  `Touched Symbols` and `touchedFiles` / `touchedSymbols`.
- Smoke-output sentinel:
  `rg -n '/Users|/tmp/tracemap-route-flow-smoke|select \* from|Server=private|Password=secret|https?://' /tmp/tracemap-route-flow-smoke/route-flow/route-flow-report.md /tmp/tracemap-route-flow-smoke/route-flow/route-flow-report.json`
  returned no matches.
- `./scripts/check-private-paths.sh`: passed for the implementation diff.
- `git diff --check`: passed for the implementation diff.
- Post-rebase validation on `origin/dev`: `dotnet build
  src/dotnet/TraceMap.sln`, `dotnet test
  src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter
  CombinedRouteFlowTests`, `dotnet test src/dotnet/TraceMap.sln`, the
  public-demo combine/route-flow smoke, `./scripts/check-private-paths.sh`, and
  `git diff --check origin/dev...HEAD` passed. A parallel focused test/build
  run emitted one transient MSBuild copy retry warning; the subsequent
  sequential full solution test passed cleanly.
- `git diff --cached --check`: passed after staging the intended spec files.
- `./scripts/check-private-paths.sh`: passed after staging the intended spec
  files.
- Spec lint/check discovery: no dedicated spec lint script found. The only
  matching script under `scripts/` was `scripts/check-private-paths.sh`; search
  hits in docs/specs described Kiro review, `git diff --check`, and the private
  path guard rather than a separate repo spec lint command.
- Diff scope: staged files are limited to
  `.kiro/specs/route-centered-endpoint-trace-completeness/`.
- Follow-up PR-loop patch validation: `git diff --cached --check` passed and
  `./scripts/check-private-paths.sh` passed after staging the review-thread
  fixes.

## PR Loop Log

- Implementation PR:
  `https://github.com/joefeser/tracemap/pull/241`
- First implementation PR-loop run:
  `agent-control pr-loop --repo joefeser/tracemap --pr 241 --base dev --require-codex-review --quiet --json`
- First implementation PR-loop result: `actionable_findings`, stop reason
  `UNRESOLVED_REVIEW_THREADS`, canMerge `false`.
- Findings:
  - Codex P2: gap file-span evidence with known source was grouped under
    commit `unknown`, splitting touched-file summaries from the real-commit
    file row and hiding weakest classification/coverage.
  - Codex P2: touched-symbol summaries synthesized a new display/file hash
    instead of preserving available row/node identities.
- Disposition:
  - Patched gap evidence projection to reuse the known source/file commit when
    available so gap rows merge into the real touched-file summary.
  - Patched touched-symbol aggregation to carry stable candidate identities,
    using route-flow row node IDs when available and a deterministic
    unavailable placeholder only as fallback.
  - Added regression assertions for known-commit gap merging and non-hashed
    symbol identities.
- Follow-up validation after patch: `dotnet test
  src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter
  CombinedRouteFlowTests`, `dotnet test src/dotnet/TraceMap.sln`, the
  public-demo combine/route-flow smoke, `./scripts/check-private-paths.sh`, and
  `git diff --check` passed.
- Second implementation PR-loop run result: `actionable_findings`, stop reason
  `UNRESOLVED_REVIEW_THREADS`, canMerge `false`.
- Findings:
  - Qodo: touched-symbol summaries lacked a top-level `commitSha`.
  - Qodo: additive `touchedFiles`/`touchedSymbols` fields could be `null` when
    deserializing older `route-flow-report.json` artifacts, and Markdown
    rendering dereferenced them directly.
- Disposition:
  - Added top-level `CommitSha` to `RouteFlowTouchedSymbol` while continuing to
    emit evidence-level commit SHA.
  - Made Markdown rendering coalesce missing additive touched collections to
    empty lists for older JSON-derived report objects.
  - Added a regression test that removes the additive fields from serialized
    route-flow JSON and verifies Markdown rendering treats them as empty.
- Follow-up validation after Qodo patch: focused route-flow tests passed with
  23 tests, full solution tests passed with 555 tests, public-demo
  combine/route-flow smoke passed, smoke-output sentinel passed,
  `./scripts/check-private-paths.sh` passed, and `git diff --check` passed.
- Third implementation PR-loop run requested and received a fresh Codex review
  for commit `35e6ff250dd005398fd0d33401f9507b1b27a754`. Result:
  `actionable_findings`, stop reason `UNRESOLVED_REVIEW_THREADS`, canMerge
  `false`.
- Finding:
  - Codex P2: target touched symbols for cross-source rows could inherit the
    edge/source-side evidence rather than target/source-local evidence.
- Disposition:
  - Reworked path method/service touched-symbol candidates to derive from the
    selected route-flow path nodes with node evidence, rather than deriving
    target symbols from edge evidence.
  - Added a client-call cross-source regression assertion that the server
    controller touched symbol is attributed to the server source label, commit,
    and file rather than the client.
- Follow-up validation after cross-source evidence patch: focused route-flow
  tests passed with 23 tests, full solution tests passed with 555 tests,
  public-demo combine/route-flow smoke passed, smoke-output sentinel passed,
  `./scripts/check-private-paths.sh` passed, and `git diff --check` passed.

- First run command:
  `agent-control pr-loop --repo joefeser/tracemap --pr 233 --base dev --require-codex-review --json`
- First run result: `actionable_findings`, stop reason
  `UNRESOLVED_REVIEW_THREADS`, canMerge `false`.
- Findings:
  - Gemini Medium review thread on `requirements.md`: deterministic sorting
    keys differed from `design.md`.
  - Gemini Medium review thread on `design.md`: entry evidence preservation
    text mentioned scan IDs even though current entry/evidence-ref models do
    not carry a direct `scanId` field.
- Disposition:
  - Patched `requirements.md` to use the same ordering keys as `design.md`,
    including selector kind, sequence where available, and end line.
  - Patched `design.md` to state source scan IDs remain available through
    `RouteFlowSnapshot` source entries and entry rows should not invent a
    duplicate field unless a future schema change adds it explicitly.

## 2026-06-20 Follow-Up Slice: Safe Selector Trace Metadata

- Branch: `codex/implement-route-centered-endpoint-trace-completeness-followup`
- Selected slice: Task 5, safe selector trace metadata.
- Scope:
  - Added additive `RouteFlowSelectorTrace` metadata under `RouteFlowQuery`.
  - Reused existing route/client/from-* selector normalization and `SafeSelector` redaction helpers.
  - Recorded selector kind, safe normalized key, match mode, redaction state, coverage, rule ID, evidence tier, supporting fact IDs, and limitations.
  - Added a compact Markdown query line for selector trace metadata.
  - Preserved older JSON compatibility by making selector trace nullable and rendering missing traces as unavailable.
- Privacy and evidence notes:
  - Raw route selector values, dynamic URLs, hostnames, tokens, absolute paths, SQL/config-like values, and private local paths are not rendered by the selector trace.
  - Selector trace uses `combined.route-flow.selector.v1` and remains static, coverage-relative query metadata; it does not claim runtime execution.
- Validation:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`: passed with 23 tests.
  - `dotnet build src/dotnet/TraceMap.sln`: passed.
  - `dotnet test src/dotnet/TraceMap.sln`: passed with 570 tests.
  - `./scripts/smoke-combined-paths.sh /tmp/tracemap-selector-trace-smoke`: passed after local `npm --prefix src/typescript ci` populated ignored TypeScript dependencies.
  - Direct route-flow smoke over `/tmp/tracemap-selector-trace-smoke/combined.sqlite`: passed and verified `query.selectorTrace` rule ID, evidence tier, selector kind, and safe normalized key.
  - Smoke-output sentinel over route-flow Markdown/JSON: passed.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.
- Kiro implementation review:
  - Command: `node scripts/kiro-review.mjs --phase route-centered-endpoint-trace-completeness --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Artifact: `.tmp/kiro-reviews/route-centered-endpoint-trace-completeness/2026-06-21T024705-297Z-implementation-claude-sonnet-4.6.clean.md`
  - Coverage: Full.
  - Result: no Medium+ findings. Patched narrow Low clarifications for candidate-boundary gap names, selector trace model documentation, and probable-classification test expectations.
  - Re-review command: `node scripts/kiro-review.mjs --phase route-centered-endpoint-trace-completeness --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  - Re-review artifact: `.tmp/kiro-reviews/route-centered-endpoint-trace-completeness/2026-06-21T024939-882Z-re-review-claude-sonnet-4.6.clean.md`
  - Re-review coverage: Full.
  - Re-review result: no Medium+ findings. Patched the two narrow Low clarification requests for Strong/Probable classification wording and normalized selector ordering wording.
- Pending validation:
  - None for this slice.
- PR:
  - URL: `https://github.com/joefeser/tracemap/pull/253`
  - Initial PR-loop command: `agent-control pr-loop --repo joefeser/tracemap --pr 253 --base dev --require-codex-review --json`
  - Initial PR-loop result: `merge_ready`, stop reason `NONE`, canMerge `true`, merge state `CLEAN`, unresolved threads `0`, pending checks `0`, failed checks `0`, actionable bot findings `0`.
  - Bot lifecycle evidence: Qodo and Gemini returned; Codex was satisfied by configured trusted-code-review quorum with residual risk noted by the lane policy.
- Follow-ups remaining from this spec:
  - Task 8 method/service row grouping.
  - Task 9 data/query/dependency and value-origin row polish.
  - Task 10 broader coverage/gap/classification downgrade hardening.
