# Base44 Entity Payload Evidence Validation

Date: 2026-09-05

This receipt validates TraceMap issue #713 against the canonical ShopGenie
source. It is independent static evidence only. It does not create database
schema authority, prove runtime behavior, admit an SDK, or declare ShopGenie
compatible.

## Bound identities

- Source repository: `BigRiverMachine/ShopGenie`
- Source ref: exact `origin/main`
- Source commit: `6753cfa6264b04c0d7dbfcde942689ee32d2c06b`
- Accepted source/tree SHA-256:
  `a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24`
- TraceMap implementation commit:
  `07149774b7af8c08b57dea49036bcb3f762d074f`
- Extractor identity: `base44-evidence@base44-evidence/0.2.0`
- Evidence packet SHA-256:
  `cabdaf7f2e01501436867833574fc490a3a1d1da98a9a3955899a2a907c2c053`

The packet was generated into a disposable local output directory and was not
committed because standard scan artifacts are local-only.

## Results

| Evidence | Count |
| --- | ---: |
| Existing Base44 entity-operation facts | 1,368 |
| Linked entity payload/query shape facts | 1,368 |
| Mutation payload facts | 537 |
| Complete mutation payload facts | 402 |
| Partial mutation payload facts | 33 |
| Unresolved mutation payload facts | 102 |
| Query shape facts | 831 |
| Unique statically observed payload field names | 111 |
| Decimal-number payload field observations | 84 |
| Numeric-coercion payload field observations | 22 |
| ToolingItem decimal `job_cost` observations | 3 |

Every operation fact has one distinct matching `operationEvidenceId` in a
payload or query fact. This binds the new evidence to the existing TraceMap
operation census without replacing it.

The result independently demonstrates incompatible rigid-required-field
assumptions. Among complete executable-source create shapes:

- one ToolingItem call contains `cost` without `job_cost`;
- three ToolingItem calls contain `job_cost` without `cost`;
- ten of fourteen MaterialItem calls omit `vendor`; and
- twelve of fourteen MaterialItem calls omit `qty`.

These observations are callsite facts, not a declaration that any field is
globally optional. Partial and unresolved sibling callsites preserve their own
typed gaps.

## Controlled acceptance checks

The focused TypeScript suite proves:

- field presence classes for unconditional, conditional, spread-derived, and
  dynamic-computed properties;
- integer, decimal, numeric-expression, numeric-coercion, literal, call,
  property, and binding expression classes without storing values;
- bounded intrafile object binding, spread, post-initialization assignment, and
  lexical-shadow handling;
- deterministic fact identities for an unchanged executable source snapshot;
- zero Base44 fact delta after adding a false `database-schema.md` document;
- an exact added/removed payload-shape delta after changing executable payload
  code; and
- absence of seeded private literal values from the packet.

## Commands

```bash
cd src/typescript
npm run check
node dist/src/cli.js base44-evidence \
  --repo <canonical-shopgenie-origin-main-checkout> \
  --out <disposable-output> \
  --accepted-source-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --accepted-tree-sha256 a5a313b180ef0cdfd2e837779f251cc754978fcd474b6a32646c133b058b6e24 \
  --coverage-label canonical-shopgenie-origin-main-syntax \
  --no-semantic
python3 ../../scripts/validate-adapter-artifacts.py <disposable-output>
../../scripts/check-private-paths.sh
git diff --check
```

Results: 51/51 TypeScript tests passed; the canonical ShopGenie adapter
artifacts passed the shared artifact validator; the private-path guard passed.

## Remaining boundary

This is 1,368/1,368 shape coverage for TraceMap's current source-proven static
entity-operation facts, not whole-app compatibility. The hardened independent
reverse-engineer currently expands additional closed dynamic selector
candidates. The 88mph reconciliation stage must compare both inventories,
preserve every discrepancy as a blocker, and must not use either tool's missing
row as clean absence. The 33 partial and 102 unresolved payload facts also
remain explicit analysis gaps until stronger executable-source evidence closes
them.
