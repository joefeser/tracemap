# Full Web Forms application review editing

`wits-webforms-application-review.v1` records one human decision for every
selected surface in a `webforms-modernization-packet.v1`. It is private review
metadata. It never changes the packet, `index.sqlite`, `facts.ndjson`, or the
evidence-docs corpus.

For each decision, choose exactly one `verdict`:

- `unreviewed`
- `expected-ui-only`
- `supported-backend-present`
- `backend-evidence-missing`
- `binding-or-source-mismatch`
- `needs-review`

Choose exactly one `migrationDisposition`:

- `unassigned`
- `retain`
- `replace-angular`
- `replace-dotnet`
- `replace-postgresql`
- `replace-multiple`
- `retire`
- `manual-redesign`
- `defer`

Do not comma-separate closed values. Put nuance in `comment`, the owner-provided
business name in `capabilityLabel`, and an explicit amendment in `correction`.
Do not edit `pageId`, `surfaceId`, `surfaceFactId`, or root `evidence`; validation
uses those immutable references to reject a packet mismatch.

Keep `reviewState` as `draft` while any page is unreviewed. To complete the
overlay, set every verdict, change `reviewState` to `completed`, provide a
non-empty reviewer or approved pseudonymous identifier, and use a UTC RFC 3339
timestamp such as `2026-09-11T18:00:00Z`.

Static evidence and human review still do not prove runtime execution,
correctness, business intent, migration equivalence, or successful generation.

