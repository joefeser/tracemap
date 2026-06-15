# Legacy Flow Composition Reporting Implementation State

Status: implemented
Branch: codex/legacy-flow-composition-reporting
Public claim level: hidden

## Why This Spec Exists

TraceMap has implemented WCF metadata normalization and WebForms event flow, and
legacy data metadata extraction is queued as a ready implementation spec. Those
pieces give older .NET codebases useful static facts, but users still need a
conservative composition/reporting layer that explains possible WebForms or API
entry-point paths to service, HTTP, SQL, and data metadata surfaces.

The goal is an implementation-ready spec for making old-codebase demos
understandable without claiming runtime proof.

## Scope Decisions

- This branch is spec-only. It does not implement scanner, reducer, reporting,
  or CLI code.
- Composition reads existing facts and edges from `facts.ndjson`/`index.sqlite`
  outputs and combined indexes where available.
- The feature should work when some extractor families are absent by emitting
  availability gaps.
- The queued legacy data metadata facts are optional inputs, not prerequisites.
- Output classifications are conservative: `StrongStaticPath`,
  `ProbableStaticPath`, `NeedsReviewStaticPath`, `NoBackendEvidence`,
  `ReducedCoverage`, and `AnalysisGap`.
- Every conclusion requires rule IDs, evidence tiers, supporting fact/edge IDs,
  coverage labels, limitations, commit SHA, and extractor identity.
- Public/demo artifacts must be redacted and use neutral labels only.
- No LLM calls, embeddings, vector databases, prompt-based classification,
  runtime tracing, service calls, database connections, or browser execution are
  in scope.

## Review State

Initial spec drafted for Kiro Opus and Sonnet review. Medium+ and blocking
findings from the review loop are resolved; this spec is ready for
implementation.

Review outcomes:

- Opus spec review completed with full coverage. Blocking findings patched:
  reconciliation with existing `legacy.webforms.event-flow.v1`, concrete reuse
  targets, deterministic `nodeId`/`edgeId`/`flowId` derivation, optional
  parameter-forward evidence, cross-source alignment limits, and missing tests.
- Sonnet spec review completed with full coverage. Blocking findings patched:
  command ownership by extending `tracemap paths` instead of adding a separate
  command, display/source label privacy, legacy data metadata availability
  semantics, WCF terminal behavior, and byte-stability wording.
- Important review findings also patched: selector grammar, cycle detection,
  high fan-out threshold, frontier semantics, redaction rule scope, task order,
  forbidden-wording validation, read-only enforcement, and cross-source negative
  tests.
- Sonnet re-review completed with full coverage. Remaining blockers patched:
  traversal-level parameter-forward unavailability handling and high fan-out
  threshold rationale. Important re-review refinements patched: lifecycle root
  node kind, WCF client/operation node representation, classification-filter
  no-match gap kind, byte-stability input definition, auto-wireup rule
  cross-reference, same-source wording, parameter-forward tests, and SOAP/WCF
  log privacy.
- Final Sonnet re-review completed with full coverage. Remaining blockers
  patched: WCF operation traversal terminal wording and dedicated
  `legacy.flow.parameter-forward-unavailable.v1` rule ID. Additional clarity
  added for cross-source gap rule ownership and `SchemaVersion =
  "legacy-flow.v1"`.
- Additional final re-review clarification patched: WCF operation terminals now
  explicitly have no outbound traversal in v1, parameter-forward availability
  gaps have global/per-path placement rules, `NoRootsFound` is defined, and
  high fan-out calibration guidance is more explicit.
- Last Sonnet re-review completed with full coverage and found one remaining
  documentation blocker: WCF operation terminal behavior needed to be reflected
  in classifier rules. Patched by adding classifier-level WCF terminal guidance,
  schema version evolution rules, and expanded high fan-out calibration metrics.
  No known Medium+ review findings remain.
- PR review loop addressed Qodo/Codex actionable comments: requirements now use
  only canonical classifications (`AnalysisGap`, `NeedsReviewStaticPath`,
  `ReducedCoverage`) for unresolved or ambiguous roots, and design/tasks now
  preserve the existing `tracemap paths` output contract (`--format
  markdown|json`, directory output writes both) plus existing selectors such as
  `--from-source`, `--surface-name`, and `--source-pair`.

## Validation Commands For Spec Delivery

```bash
node scripts/kiro-review.mjs --phase legacy-flow-composition-reporting --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase legacy-flow-composition-reporting --kind spec --model claude-sonnet-4.5 --fresh --timeout-ms 600000
node scripts/kiro-review.mjs --phase legacy-flow-composition-reporting --kind re-review --model claude-sonnet-4.5 --fresh --timeout-ms 600000
./scripts/check-private-paths.sh
git diff --check
```

No .NET implementation validation is required for this spec-only branch unless
review patches touch source code, docs outside the spec, or validation scripts.

## Implementation Notes

Implemented as an extension of the existing `tracemap paths` reporter rather
than a parallel graph engine. The legacy flow mode adds `--include-legacy-roots`,
`--view legacy-flows`, `--from-webforms-event`, and `--classification` selectors,
plus `legacy-flow.v1` schema metadata on the shared path report contract.

Single-index support is enabled for `tracemap paths` report building only.
Shared graph inventory callers such as reverse query still require combined
indexes to preserve existing behavior. Combined-index paths keep their current
default start-node semantics unless legacy roots are explicitly requested.

Legacy roots are composed from existing WebForms handler/event facts, API route
facts, WCF service-reference mappings, SQL/query surfaces, dependency surfaces,
and optional legacy data metadata facts. WCF operations are v1 terminals and do
not traverse through service-side implementation evidence. Projection facts from
`legacy.webforms.event-flow.v1` are accepted as review-tier fallback/corroborating
edges and cannot upgrade classification.

Final legacy output guards neutralize source labels, display names, paths, URLs,
raw SQL-like values, remotes, connection strings, config-looking values, and
secret-looking tokens before Markdown/JSON serialization.

## Implementation Validation

Completed:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln --filter LegacyFlowCompositionTests
dotnet test src/dotnet/TraceMap.sln
./scripts/smoke-combined-paths.sh
./scripts/check-private-paths.sh
git diff --check
```

The pinned combined-path smoke is relevant because this slice extends
`tracemap paths` and shared path rendering behavior. No language-adapter pinned
smoke is required beyond the .NET suite because scanner extraction behavior was
not changed.

## Oddities

- Existing `CombinedDependencyPathReport` is reused and extended with optional
  legacy schema/view/query fields. This avoids a split output model but means
  non-legacy JSON now contains additional null/default fields.
- Availability gaps are emitted conservatively when optional parameter-forward
  or legacy data metadata evidence is unavailable or empty; this prevents clean
  absence claims from old indexes.
- Source-label redaction intentionally favors neutral labels over preserving
  private combined labels in legacy-flow output.

## Follow-Ups To Keep Out Of This Slice

- Scanner implementation.
- Site copy or public site claims.
- Runtime flow proof.
- Visual graph UI.
- Persisted flow tables by default.
- Committed local sample names, private paths, raw SQL, config values, remotes,
  snippets, secrets, or generated smoke artifacts.
