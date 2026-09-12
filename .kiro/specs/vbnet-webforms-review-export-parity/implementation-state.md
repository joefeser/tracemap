# Implementation state

- Issue: #750
- Branch: `codex/issue-750-vb-webforms-review`
- Base: `origin/dev` at `3666356d`
- Status: implementation and validation complete; ready for PR review

## Scope decisions

- Reuse the existing versioned Web Forms packet, inspection, handoff, and review contracts.
- Treat syntax-discovered definitions only as navigation candidates, never evidence.
- Keep raw source opt-in and private; shareable artifacts remain structural aliases only.
- Do not add BRD or modernization-generation behavior.

## Known starting gaps

- `WebFormsCodePathReview` only searches retained `.cs` files for unique-name navigation candidates.
- The anonymous rule allowlist accepts `csharp.semantic.*` but not the documented VB semantic rule family.
- The focused review tests do not exercise `.vb` source or VB rule provenance.

## Validation

- Focused `WebFormsCodePathReviewTests`: 10/10 passed.
- Focused VB Web Forms plus code-path review suites: 31/31 passed.
- Synthetic VB scan -> packet -> batch inspection -> private/anonymous review: passed.
- Full solution after review fixes: 1,928/1,928 passed with no compiler/analyzer warnings.
- PowerShell review-set, application-workbench, and configuration workflows: passed.
- `check-private-paths.sh` and `git diff --check`: passed.
- Pinned OSS scan not rerun: this slice changes only report consumption and HTML/source navigation; it does not change inventory, extraction, facts, coverage, or adapter artifacts. The synthetic VB scan exercises the changed consumer path end to end.

## Remaining boundaries

- Syntax-discovered unique-name definitions remain navigation candidates, not evidence.
- The supplied working tree is not asserted to equal the scanned commit; Git remains optional.
- Raw source is opt-in/private and never enters shareable artifacts.
- Runtime behavior, cross-language identity joins, BRD generation, and modernization generation remain out of scope.

## Review fixes

- Anonymous VB rule provenance is bound to the finite call-evidence rule set; unknown well-formed IDs are withheld.
- Same-span invocation/call-graph witnesses retain every distinct rule and tier while the physical call-site count remains one.
- Structural-only reports do not read source when there is no unresolved lookup work and raw source is disabled.
- VB candidate navigation normalizes bracket-escaped identifiers and covers `Declare Sub`/`Declare Function` statements.
