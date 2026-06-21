# Evidence Packet Examples Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Purpose

Review the `site-tracemap-tools-evidence-packet-examples` spec for future
implementation readiness. This is a spec-only public-site phase for synthetic,
public-safe evidence packet examples. It should not implement site source,
generated output, scanner code, reducer code, or validation scripts.

Shared principle: No public conclusion without evidence.

## Review Focus

- Confirm the page-level public claim level is conservatively `concept`.
- Confirm the spec requires visible `Public claim level: concept` and
  `No public conclusion without evidence`.
- Confirm candidate placements are complete:
  `/packets/examples/`, `/examples/evidence-packets/`, a section on
  `/packets/`, or a section on `/packets/assembly/`.
- Confirm future implementation must record the final placement and rejected
  alternatives before editing site source.
- Confirm the required examples are present: demo-backed packet,
  reduced-coverage packet, gap-labeled packet, and stop-condition packet.
- Confirm every example requires claim label, public claim level, proof path,
  rule ID or family, evidence tier, coverage label, synthetic public-safe
  path/span, commit or extractor placeholder, limitation, non-claim, next
  owner, and validation evidence.
- Confirm synthetic examples are visibly labeled synthetic/public-safe unless
  backed by checked-in demo artifacts.
- Confirm the spec distinguishes this surface from `/packets/`,
  `/packets/assembly/`, `/examples/scan-packet/`, `/demo/result/`,
  `/proof-source-catalog/`, and `/review-claim-checklist/`.
- Confirm boundaries forbid raw artifacts, private material, hidden validation
  details, credential-like values, unsupported runtime/release/safety claims,
  AI/LLM analysis claims, autonomous approval, and replacement of human
  review.
- Confirm the spec avoids blame language around vendors, consultants, teams,
  maintainers, authors, or code quality.
- Confirm validation expectations cover example schema, labels, required
  links, metadata, discovery/sitemap metadata if standalone, forbidden claims,
  private/raw material, synthetic labeling, word count bounds, and
  desktop/mobile browser sanity.

## Files in Scope

- `.kiro/specs/site-tracemap-tools-evidence-packet-examples/requirements.md`
- `.kiro/specs/site-tracemap-tools-evidence-packet-examples/design.md`
- `.kiro/specs/site-tracemap-tools-evidence-packet-examples/tasks.md`
- `.kiro/specs/site-tracemap-tools-evidence-packet-examples/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-evidence-packet-examples/review-packet.md`

## Out of Scope

- `site/src`
- `site/scripts`
- generated site output
- core scanner or reducer code
- existing specs
- raw scan artifacts
- generated scan directories
- validation script implementation

## Review Commands Run

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-packet-examples --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-packet-examples --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Both requested models ran through `scripts/kiro-review.mjs` and produced saved
review artifacts. Coverage was reduced because Kiro reported denied tool
access inside the review sandbox. The exact artifact paths, findings, patches,
and residual coverage note are recorded in `implementation-state.md`.

## Required Local Validation

```bash
git diff --check
./scripts/check-private-paths.sh
```

Also run focused text checks over this spec directory for required
status/readiness/claim-level labels, required example categories, required
example fields, neighboring route distinctions, synthetic labeling, forbidden
claims, and private/raw material boundaries.

## Readiness Result

Medium or higher Kiro spec-review findings were patched or dispositioned.
Spec-only validation passed, and the spec packet is ready for a future
implementation phase with the reduced-review-coverage note recorded in
`implementation-state.md`.
