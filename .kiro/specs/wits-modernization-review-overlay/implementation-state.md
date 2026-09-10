# WITS modernization review overlay implementation state

- Branch: `codex/wits-modernization-review-overlay`
- Parent integration PR: #723
- Tracking issue: #724
- Scope: deterministic private export and validation only

The initial slice defines a private immutable overlay for one exact batch
inspection. Human decisions remain separate from scanner evidence. WITS hosting,
anonymous review projection, planning, and generation remain unchecked follow-up
tasks.

Validation on 2026-09-10:

- `Test-WitsModernizationReview.ps1`: passed deterministic export, draft and
  completed validation, provenance/reference/duplicate/code/completion/privacy
  failure cases, and source-inspection immutability.
- JSON Schema parses with `jq`.
- `git diff --check`: passed.
- `scripts/check-private-paths.sh`: passed.

PR #725 review follow-up tightened required-field enforcement, ordinal
case-sensitive identifiers and closed codes, atomic create-new output, and the
UTC/draft-metadata agreement between the schema and validator. The review-only
decision boundary is now explicit in `design.md`; per-fact tier and location
remain in the digest-bound inspection rather than being copied into mutable
human decisions.
