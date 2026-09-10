# Editing a WITS modernization review overlay

The overlay records human review of the exceptional cases in one bounded Web
Forms batch inspection. It does not describe every page or every resolved call
path, and it is not a complete BRD or modernization plan.

## One decision per case

Each item in `decisions` represents one `caseId`. Set exactly one `verdict` and
exactly one `migrationDisposition` string. Do not comma-separate values and do
not turn either field into an array. Use `comment` for nuance.

```json
{
  "caseId": "case-001",
  "surfaceIds": ["retain the exported value"],
  "handlerFactId": "retain the exported value",
  "bindingFactIds": ["retain the exported value"],
  "verdict": "supported-backend-present",
  "comment": "The reviewed path reaches an existing data-access operation.",
  "correction": null,
  "migrationDisposition": "replace-multiple"
}
```

Do not edit `caseId`, `surfaceIds`, `handlerFactId`, `bindingFactIds`, or the
top-level `evidence` object. Validation binds those values to the exact source
inspection.

## Verdict values

- `unreviewed`
- `expected-ui-only`
- `supported-backend-present`
- `backend-evidence-missing`
- `binding-or-source-mismatch`
- `needs-review`

Choose the best overall verdict. Put additional observations in `comment`.

## Corrections

Use `correction` only when the reviewer needs to amend or qualify the retained
description. Otherwise leave it `null`.

```json
"correction": {
  "category": "callee",
  "statement": "The retained path reaches a data-access abstraction not classified by this inspection."
}
```

Allowed categories are `binding`, `callee`, `business-intent`, `coverage`, and
`other`. A correction is a human assertion; it does not become scanner evidence.

## Migration disposition values

- `unassigned`
- `retain`
- `replace-angular`
- `replace-dotnet`
- `replace-postgresql`
- `replace-multiple`
- `retire`
- `manual-redesign`
- `defer`

Use `replace-multiple` when more than one replacement target applies. Explain
the intended split in `comment`; do not comma-separate dispositions.

## Completing the overlay

Draft overlays may retain `unreviewed` decisions and null reviewer metadata. To
complete an overlay, set `reviewState` to `completed`, supply a nonblank
`reviewer`, set `reviewedAtUtc` to an RFC 3339 UTC timestamp ending in `Z`, and
replace every `unreviewed` verdict.

The overlay is private. Comments, corrections, identifiers, and referenced
evidence may reveal application structure.
