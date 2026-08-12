# Property Flow Depth v0 Review Prompts

Issue: #517

## Exact-Head Kiro Advisory Review

Run a fresh, read-only Kiro CLI review on the exact branch head using explicit
model `claude-opus-5`. Treat it as advisory unless ACK verifies and admits an
exact-head `kiro-local` receipt.

Review `.kiro/specs/property-flow-depth-v0/` against:

- `.kiro/specs/ui-field-property-lineage-composition/`;
- current `RazorBindingExtractor`, syntax model-binding extraction,
  `CSharpSemanticExtractor`, and `PropertyFlowReport`;
- `rules/rule-catalog.yml`, generic storage/combine/reducer consumers, and
  `docs/VALIDATION.md`; and
- TraceMap evidence, privacy, partial-analysis, and non-claim principles.

Check specifically:

1. Does Tier1 Razor admission require enough compiler/framework identity to
   reject source and unsigned metadata lookalikes?
2. Can cross-file/partial action and handler model properties be represented
   without replacing useful syntax fallback?
3. Is direct property mapping narrow enough to avoid turning arbitrary
   expressions or same-name properties into evidence?
4. Is assignment direction explicit and preserved through storage/composition?
5. Are constructor forwarding and mapper packages deferred unless both sides
   have exact independently proven contracts?
6. Can property-flow join by canonical IDs without silently upgrading syntax,
   convention, alias, or broad endpoint evidence?
7. Are gaps bounded, categorical, rule-backed, deterministic, and free of raw
   expressions/protected digests?
8. Are consumer/versioning and reducer-exclusion audits complete enough for
   staged implementation PRs?
9. Are the four PR slices independently reviewable?

Return blocking findings, important non-blocking findings, missing adversarial
fixtures, recommended spec edits, and readiness. Do not mutate the branch.

## Hosted Review Focus

Reviewers should prioritize false-positive identity, direction reversal,
evidence-tier inflation, silent fallback replacement, unsafe expression
retention, consumer compatibility, and claims that outrun static evidence.
