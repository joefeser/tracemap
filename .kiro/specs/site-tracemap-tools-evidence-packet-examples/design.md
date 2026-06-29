# Site TraceMap Tools Evidence Packet Examples Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

This design defines a future public-site concept surface for evidence packet
examples. The examples teach how a public-safe claim label, proof path, rule
family, evidence tier, coverage label, limitation, non-claim, validation
evidence, and next owner travel together.

The design center is a compact example gallery. Each card or row is a
synthetic public-safe packet shape unless a later implementation links it to a
checked-in public demo artifact. The surface must teach packet anatomy without
publishing raw artifacts or implying real customer, private repository, or
production evidence. See the full example schema below for the required fields
that travel with each example.

Shared principle: No public conclusion without evidence.

## Claim Level Decision

The surface uses `Public claim level: concept` because the required examples
are teaching shapes. Even the "demo-backed packet" category remains
concept-level unless implementation verifies a checked-in public demo artifact
and records the proof path in `implementation-state.md`.

The page may give a single example the `demo-backed` coverage label only when
a public-safe demo artifact exists. The example-level and page-level public
claim level remain concept because the page teaches how to read packet shapes
rather than claiming a shipped packet catalog or real repository result. Any
claim level stronger than concept is out of scope for this spec.

## Information Architecture

Candidate placements:

- `/packets/examples/`
- `/examples/evidence-packets/`
- section on `/packets/`
- section on `/packets/assembly/`

Recommended default: `/packets/examples/`. It keeps the examples under the
packet family while avoiding overlap with the assembly workflow. The future
implementation must still record the final placement and rejected alternatives
before changing site source.

Rejected-alternative guidance:

- Reject `/examples/evidence-packets/` if it reads like a broad examples hub
  or competes with `/examples/scan-packet/`.
- Reject a section on `/packets/` if the examples make the artifact model page
  too long or blur concept explanation with example details.
- Reject a section on `/packets/assembly/` if it makes the workflow page feel
  like a packet gallery instead of an assembly checklist.

## Page Structure

The future surface should use this structure:

1. Header with `Public claim level: concept`.
2. Shared principle: `No public conclusion without evidence`.
3. One-sentence synthetic boundary.
4. Four example packet shapes.
5. Relationship block for neighboring pages.
6. Public-safe boundaries and non-claims.

The content should be compact. Prefer a table, accordion, or repeated simple
sections using existing site patterns. Do not add an interactive generator,
upload flow, raw artifact viewer, client-side state, agent integration, or
autonomous approval flow.

## Example Schema

Each example must include these fields:

| Field | Required meaning |
| --- | --- |
| Claim label | The bounded sentence or packet state under review. |
| Public claim level | The visible public claim label, normally `concept`. |
| Proof path | A public-safe link or named proof trail; private-only support stays labeled. |
| Rule ID or family | The deterministic rule basis and documented limitation. |
| Evidence tier | `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`. |
| Coverage label | The exact coverage state, such as demo-backed, reduced, gap-labeled, or stopped. |
| Synthetic path/span | A public-safe example path and line span, not raw source. |
| Commit/extractor placeholder | A fake placeholder unless checked-in demo evidence supports a real public value. |
| Limitation | What the evidence cannot prove. |
| Non-claim | The stronger claim readers must not infer. |
| Next owner | The public-safe role or review process that owns the remaining question, not a named person or private team. |
| Validation evidence | The public-safe validation summary without raw command output. |

## Required Example Shapes

### Demo-backed packet

Purpose: show the strongest public-safe example shape the site may publish.

Required notes:

- Label as demo-backed only when a checked-in public demo artifact or
  public-safe demo summary supports it.
- Otherwise label it `synthetic public-safe example` and describe it as a
  demo-backed shape, not a demo-backed result.
- Keep the claim bounded to demo evidence, not real customer or private repo
  evidence.

### Reduced-coverage packet

Purpose: teach that partial evidence is still useful when the reduced coverage
label travels with the claim.

Required notes:

- Make reduced coverage visible in claim label, coverage label, limitation,
  validation evidence, and next owner.
- Do not normalize reduced, partial, syntax-only, or private-only evidence into
  complete or clean wording.

### Gap-labeled packet

Purpose: show how an analysis gap is represented instead of hidden.

Required notes:

- Use `Tier4Unknown` when the example demonstrates an inability to prove or
  disprove the claim.
- Name the gap as a limitation and route the next question to a human owner or
  future validation owner.

### Stop-condition packet

Purpose: show when a packet should not become public copy.

Required notes:

- Stop conditions include missing proof path, private-only support, raw
  artifact leakage, unknown or reduced coverage without label, unsupported
  runtime, release, or safety wording, no next owner, and no validation
  evidence.
- The stop example must show the blocked field with an explicit marker such as
  `proof path: blocked: missing public-safe proof trail`, plus the smallest
  public-safe next step.

## Public-Safe Placeholder Policy

Use visibly synthetic placeholders such as:

- `examples/public-demo/Controllers/OrdersController.cs:42-58`
- `examples/public-demo/Contracts/OrderDto.cs:12-24`
- `commit: demo-sha-placeholder`
- `extractor: tracemap-demo-extractor@x.y.z`
- `validation: public example schema check passed`

Do not use raw facts, raw SQLite, analyzer logs, raw source snippets, raw SQL,
configuration values, secrets, local absolute paths, raw remotes, generated
scan directories, private sample names, raw command output, hidden validation
details, credential-like values, private branch names, or customer-like names.

## Neighboring Surface Differentiation

The examples surface should include a concise comparison:

- Use `/packets/` for the general evidence packet artifact model.
- Use `/packets/assembly/` for the human workflow that builds a review packet.
- Use `/examples/scan-packet/` for scan-oriented public example material.
- Use `/demo/result/` for checked-in public demo result material.
- Use `/proof-source-catalog/` for proof-source lookup.
- Use `/review-claim-checklist/` to decide whether a sentence may be repeated.
- Use evidence packet examples to inspect synthetic packet shapes and stop
  states.

The comparison must not imply any neighboring page proves runtime behavior,
production traffic, endpoint performance, outage cause, release safety,
operational safety, AI or LLM analysis, autonomous approval, or complete
coverage.

## Metadata and Discovery

If standalone, metadata should describe the route as a concept guide for
synthetic, public-safe evidence packet examples. Titles and descriptions should
avoid runtime, production, endpoint-performance, outage-cause, release-safety,
operational-safety, AI/LLM, autonomous-approval, human-review-replacement, and
complete-coverage claims.

Discovery metadata should use:

- `publicClaimLevel: concept`
- `sourceType`: site page or the closest existing value
- `hintCategory`: `example` if the existing discovery vocabulary supports it,
  otherwise `use-case` with a note in `implementation-state.md`
- `preferredProofPath`: `/packets/` or `/proof-source-catalog/`, depending on
  the selected placement and current site routes
- `limitations`: synthetic example and deterministic static evidence
  boundaries
- `nonClaims`: runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, AI/LLM, autonomous-approval,
  human-review-replacement, and complete-coverage claims

If section placement is chosen, discovery metadata should follow the existing
page pattern and add a stable anchor when the site supports section-level
discovery.

## Validation Design

Future implementation validation should check:

- rendered route or section exists
- `Public claim level: concept`
- `No public conclusion without evidence`
- four required example categories
- required example schema fields
- `synthetic public-safe example` labels for non-demo-backed examples
- adjacent links when routes exist
- metadata and discovery claim level if standalone
- sitemap metadata if standalone
- internal link resolution in generated site output
- rendered word count between 450 and 1300 words unless
  `implementation-state.md` records a justified tighter or higher bound; any
  higher bound must explain why complete examples cannot fit without
  truncating required fields, and the default assumes compact table,
  accordion, or short-card rendering instead of repeated long-form prose for
  every field
- forbidden claims and unsupported upgrades
- forbidden private, raw, customer-like, credential-like, local-path, and raw
  command-output material
- desktop and mobile browser sanity checks when layout or interaction changes
  are made

Spec-only validation should include:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- focused text checks over
  `.kiro/specs/site-tracemap-tools-evidence-packet-examples/` for required
  status, readiness, claim level, required fields, required examples,
  neighboring routes, forbidden claims, and private/raw boundaries

Future site implementation validation should also include the standard site
checks used by adjacent public-site work: `npm test`, `npm run validate`, and
`npm run build` from `site/`.

## Accessibility and Layout

The future implementation should reuse existing static site components and
semantic HTML patterns. Headings should be hierarchical. Tables or cards should
remain readable on mobile, and link text should identify the target page or
decision clearly.

No interactive form, upload flow, raw artifact viewer, generated packet
builder, runtime monitor, production telemetry view, AI/LLM analysis, agent
approval flow, or autonomous review flow is part of this spec.
