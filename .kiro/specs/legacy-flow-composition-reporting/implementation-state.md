# Legacy Flow Composition Reporting Implementation State

Status: ready-for-implementation
Branch: codex/legacy-flow-composition-reporting-spec
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

## Implementation Validation

Not started. `tasks.md` is intentionally unchecked and implementation-ready only
after review findings are resolved.

## Follow-Ups To Keep Out Of This Slice

- Scanner implementation.
- Site copy or public site claims.
- Runtime flow proof.
- Visual graph UI.
- Persisted flow tables by default.
- Committed local sample names, private paths, raw SQL, config values, remotes,
  snippets, secrets, or generated smoke artifacts.
