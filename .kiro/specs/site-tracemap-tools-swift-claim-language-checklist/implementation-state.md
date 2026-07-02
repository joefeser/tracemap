# Implementation State

Status: implemented
Public claim level: shipped

Branch: `codex/site-swift-claim-language-checklist`

Scope:

- Implement issue #442.
- Publish `/swift/claim-language/` as a public-safe reviewer checklist.
- Keep it as copy guardrail content, not a new scanner or Swift adapter feature.

Claim boundary:

- Safe: Swift v0 is available for deterministic static evidence discovery, and
  public Swift wording should carry proof path, claim level, evidence tier,
  coverage label, limitation, and non-claim boundary.
- Not safe: runtime/API correctness, build proof, simulator/device behavior,
  app execution, endpoint reachability, backend compatibility, production use,
  package compatibility, release safety, stored-value proof, query execution,
  live schema proof, complete Swift semantic analysis, or AI impact analysis.

Validation:

- Focused validator: `site/scripts/swift-claim-language.mjs`
- Focused tests: `site/scripts/swift-claim-language.test.mjs`
