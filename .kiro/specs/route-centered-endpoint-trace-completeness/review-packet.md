# Review Packet: Route-Centered Endpoint Trace Completeness

## Reviewer Focus

Please review this spec as a spec-only PR. Focus on whether it is implementable,
deterministic, public-safe, and consistent with existing route-flow contracts.

## Key Questions

- Does the spec clearly answer what files, line spans, symbols, service calls,
  data/query/dependency evidence, route-flow rows, gaps, and limitations a
  normalized route selector touches?
- Does it preserve existing `tracemap route-flow` behavior and avoid a parallel
  command or schema family?
- Are selector normalization and redaction rules strong enough for public-safe
  fixtures and reports?
- Are interface/override/DI candidate boundaries conservative and free of
  runtime claims?
- Are `NoRouteFlowEvidence`, `StrongStaticRouteFlow`, and
  `NeedsReviewStaticRouteFlow` boundaries consistent with existing rule catalog
  entries?
- Is the first implementation slice small enough for a reviewable PR?

## Explicit Non-Goals

- No product code in this PR.
- No runtime tracing, traffic observation, production call-path proof, runtime
  DI target proof, outage-cause analysis, release-safety claim, business-impact
  claim, AI/LLM analysis, embeddings, vector databases, or prompt-based
  classification.
- No private repo names, private local paths, raw private routes, raw SQL,
  config values, snippets, raw remotes, hostnames, secrets, or private sample
  labels.

## Expected Review Outcome

The spec should be ready for implementation if reviewers find no Medium+
issues. Low findings may be patched when narrow and safe; broader polish should
be left as follow-up so the spec PR stays focused.
