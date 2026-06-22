# Route Flow Endpoint Stitching Implementation State

Status: spec-ready
Readiness: ready-for-implementation
Branch: codex/spec-route-flow-endpoint-stitching
Base: origin/dev
Target PR base: dev
Primary issue: #201
Public claim level: hidden

## Scope

This branch creates a spec-only Kiro packet for issue #201:
route-flow endpoint stitching through endpoint method roots, call edges, and
implementation relationship evidence.

No product code, scanner code, reducer code, site files, generated outputs,
sample outputs, or rule catalog entries are changed by this spec-only branch.

## Current-State Notes

Live repo inspection before drafting found that `origin/dev` already contains
substantial route-flow behavior:

- `tracemap route-flow` command and report model.
- `CombinedRouteFlowReport` and focused `CombinedRouteFlowTests`.
- Existing `combined.route-flow.*` rule catalog family.
- Argument-flow and fact-symbol projection readers.
- Interface bridge rows and conservative bridge classification caps.
- Parameter-forward, object-creation, data-surface, selector-redaction, and
  reduced-coverage route-flow tests.

Therefore this spec is not a rewrite of route-flow. It describes the next
endpoint-root stitching slice: make bridge states explicit, ensure endpoint
method to downstream call composition is reliable, and emit precise gaps when a
route root cannot be stitched to method/call/implementation/surface evidence.

## Source Material

- GitHub issue #201: `Route-flow should stitch endpoint roots through call
  edges and implementation relationships`.
- Existing route-flow spec:
  `.kiro/specs/route-centered-static-flow-report/`.
- Existing service/data composition spec:
  `.kiro/specs/route-flow-service-data-composition/`.
- Existing candidate-dispatch spec:
  `.kiro/specs/interface-override-di-approximation/`.
- Existing value-origin spec:
  `.kiro/specs/parameter-value-origin-flow/`.
- Current implementation files inspected through `rg`:
  `CombinedRouteFlowReport`, `CombinedRouteFlowTests`, `rule-catalog.yml`, and
  related combined path/reporting rules.

## Scope Decisions

- Spec-only branch; no implementation code changes.
- Reuse existing route-flow rule namespace unless implementation proves a new
  rule is needed.
- Preserve existing `route-flow-report.json` report type and version.
- Treat implementation candidates as static review evidence only; no runtime DI
  target proof.
- Prefer explicit gap states over generic no-evidence when a bridge is missing.
- Keep all private validation generic. Do not record private sample names,
  local paths, raw route strings, raw SQL/config values, source snippets,
  secrets, raw remotes, or generated private outputs.

## Review State

Opus spec review ran with reduced coverage because Kiro reported denied shell
tool access, but it read the spec files and checked route-flow symbols/rules.
It found one Medium merge-readiness issue: the draft gap-code list proposed
aliases for existing route-flow gap names. Patched by mapping endpoint-stitching
concepts to shipped route-flow names such as `MissingMethodSymbolBridge`,
`DataSurfaceAttachmentMissing`, `IdentityGap`, `TruncatedByLimit`,
`TraversalBounds`, `SchemaMissing`, `ExtractorUnavailable`,
`ImplementationCandidateUnavailable`, and `AmbiguousImplementationCandidates`.

Sonnet spec review ran with full coverage and found the spec merge-ready with
one minor cleanup: `requirements.md` still referenced the placeholder
`EndpointMethodBridgeMissing` name. Patched by replacing that acceptance
criterion with shipped `MissingMethodSymbolBridge` wording and similarly
requiring `DataSurfaceAttachmentMissing` reuse for dependency/data attachment
gaps unless live audit proves a successor is needed.

Sonnet re-review ran with full coverage and found the spec ready to merge. It
reported one minor task precision note: `MissingCallEdge` can be emitted both
from traversal dead-end nodes with untraversable call-like edges and from the
zero-path endpoint-root fallback when the bridged method has downstream call
evidence that cannot connect. Patched Task 6 to require tests for both shapes.

Review artifacts:

- `.tmp/kiro-reviews/route-flow-endpoint-stitching/2026-06-22T053448-608Z-spec-claude-opus-4.8.clean.md`
- `.tmp/kiro-reviews/route-flow-endpoint-stitching/2026-06-22T053658-714Z-spec-claude-sonnet-4.6.clean.md`
- `.tmp/kiro-reviews/route-flow-endpoint-stitching/2026-06-22T053904-985Z-re-review-claude-sonnet-4.6.clean.md`

Required review commands:

```bash
node scripts/kiro-review.mjs --phase route-flow-endpoint-stitching --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase route-flow-endpoint-stitching --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Patch Medium+ findings and run at most two re-review cycles.

## Validation State

Completed:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- obvious spec/docs validation command discovery:
  `find scripts -maxdepth 1 -type f | sort | rg 'spec|kiro|validate|lint|check'`
  found no dedicated spec lint beyond the Kiro review wrapper and private path
  guard used above.

Full .NET tests are not required for this spec-only branch unless product code
is changed.

## Follow-Up Implementation Shape

Recommended first implementation slice:

1. Audit current route-flow endpoint root bridge behavior.
2. Add explicit endpoint method bridge state and precise missing/ambiguous root
   gaps.
3. Add one direct endpoint method to downstream call-edge stitching regression.
4. Add classification/gap rollup assertions for full versus reduced coverage.

Later slices can harden interface candidate reuse, service/data surface
attachment gaps, and output safety matrix coverage.
