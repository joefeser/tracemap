# Evidence Handoff Template Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-evidence-handoff-template` spec for future
implementation readiness. This is a spec-only site phase; it should not
implement site code.

## Review Orientation

Branch: codex/spec-site-evidence-handoff-template
Last verified: 2026-06-22
Prior review cycle: Opus and Sonnet spec reviews ran on 2026-06-22; Medium or
higher findings were patched or dispositioned before readiness moved to
`ready-for-implementation`.

Local review artifacts are not committed and should be saved under
`.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/`.

Shared principle: No public conclusion without evidence.

## Scope

The future public surface should show a reusable, public-safe handoff template
for one TraceMap evidence claim or question. It should keep handoff question,
audience, proof path, public claim level, rule ID/family, evidence tier,
coverage label, public-safe path/span, commit SHA, extractor version,
limitation, non-claim, validation evidence, owner to ask, and stop condition
together.

The future surface may be `/handoff/template/`,
`/team-evidence-handoff/template/`, a section on
`/team-evidence-handoff/`, or a section on `/packets/assembly/`. Placement is
intentionally deferred to future implementation after checking the live site.

This packet does not define generated handoff output, scanner behavior,
reducer behavior, real org ownership, runtime proof, release approval,
operational safety, complete coverage, AI or LLM analysis, or replacement of
human review.

Please inspect:

- `.kiro/specs/site-tracemap-tools-evidence-handoff-template/requirements.md`
- `.kiro/specs/site-tracemap-tools-evidence-handoff-template/design.md`
- `.kiro/specs/site-tracemap-tools-evidence-handoff-template/tasks.md`
- `.kiro/specs/site-tracemap-tools-evidence-handoff-template/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-evidence-handoff-template/review-packet.md`

Review focus:

- Are `Status: not-started`, `Readiness: spec-review`, and
  `Public claim level: concept` present and consistent before review
  readiness is granted?
- Does the spec clearly require the visible copy
  `Public claim level: concept` and
  `No public conclusion without evidence`?
- Does the spec define candidate placements and defer the final placement
  decision until implementation checks the live neighboring routes?
- Does the spec distinguish the future surface from
  `/team-evidence-handoff/`, `/incident-evidence-handoff/`,
  `/packets/assembly/`, `/reviewer-quickstart/`, `/owners/follow-up/`, and
  `/decisions/evidence-record/`?
- Are all required template fields present: handoff question, audience, proof
  path, public claim level, rule ID/family, evidence tier, coverage label,
  public-safe path/span, commit SHA, extractor version, limitation, non-claim,
  validation evidence, owner to ask, and stop condition?
- Does the spec require sections for when to use it, neighbor distinctions,
  template, filled synthetic example, unsafe example, handoff checklist, stop
  conditions, and non-claims?
- Does the spec forbid generated handoff feature claims, real org ownership
  claims, runtime proof, release approval or safety, operational safety,
  complete coverage, AI or LLM analysis, and replacement of human review?
- Does the spec forbid raw facts, SQLite content, analyzer logs, source
  snippets, SQL, config values, secrets, local paths, remotes, generated scan
  directories, private sample names, command output, hidden validation
  details, and credential-like values?
- Does the spec require synthetic labeling for examples and keep unsafe
  examples synthetic rather than copied from real raw material?
- Does future validation cover required fields, required links, metadata,
  discovery and sitemap metadata if standalone, forbidden claims,
  private/raw material, synthetic labeling, word count bounds, and
  desktop/mobile browser sanity?
- Are future implementation tasks unchecked and spec-only review tasks staged
  for completion only after the corresponding work is done?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
