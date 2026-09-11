# WITS modernization review overlay implementation state

- Integrated branch: `dev`
- Parent integration PR: #723
- Tracking issue: #724
- Scope: deterministic private export and validation only

The completed public slice defines a private immutable overlay for one exact
batch inspection. Human decisions remain separate from scanner evidence. WITS
hosting, anonymous review projection, business-requirements synthesis, planning,
and generation are downstream concerns outside this spec rather than unchecked
TraceMap implementation tasks.

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

The current-head review follow-up also rejects non-string review states,
verdicts, migration dispositions, and correction categories before exact closed
code comparison, preventing PowerShell boolean coercion from bypassing the JSON
Schema contract. `Export` and `Validate` now dispatch case-insensitively to match
their `ValidateSet` admission behavior. Every reviewer occurrence in the schema
requires at least one non-whitespace character, matching the bundled validator.
The focused regression covers lowercase accepted modes, all four non-string
closed-code fields, whitespace-only reviewer metadata, and all three schema
reviewer branches.

The operator guide now explicitly identifies the batch cases as an exception
review queue rather than complete modernization evidence. A dedicated editing
guide documents that each decision accepts one verdict and one migration
disposition, with comments for nuance and corrections for explicit human
amendments. The exporter points operators to that guide without adding mutable
instructions to the strict overlay contract.
