# TraceMap Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-test-planning-handoff` spec for spec-review
findings. This is a spec-only site phase; it should not implement site code.

## Review Orientation

Branch: `codex/spec-site-test-planning-handoff`
Base: `origin/dev`
Target PR base: `dev`
Last verified: 2026-06-22
Review cycle: Opus and Sonnet spec review completed with full coverage;
Medium+ findings were patched and Sonnet re-review found no remaining Medium+
findings before readiness became `ready-for-implementation`.

## Scope

The future public-site surface should help readers turn TraceMap static
evidence into targeted test-planning questions. It should show how proof paths,
rule IDs or rule families, evidence tiers, coverage labels, changed surfaces,
and limitations can guide conversations with test owners without claiming
TraceMap generates tests, proves runtime behavior, establishes test
sufficiency, approves releases, or replaces QA.

Candidate placements are `/test-planning/`,
`/reviewer-quickstart/test-planning/`, a section on `/reviewer-quickstart/`,
or a section on `/packets/assembly/`. The implementation spec must preserve
the ability to select the least confusing placement after checking the live
neighboring routes.

Please inspect:

- `.kiro/specs/site-tracemap-tools-test-planning-handoff/requirements.md`
- `.kiro/specs/site-tracemap-tools-test-planning-handoff/design.md`
- `.kiro/specs/site-tracemap-tools-test-planning-handoff/tasks.md`
- `.kiro/specs/site-tracemap-tools-test-planning-handoff/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-test-planning-handoff/review-packet.md`

Review focus:

- Are `Status`, `Readiness`, and `Public claim level` consistent across all
  packet files, and was `Readiness` advanced to `ready-for-implementation`
  only after Medium+ findings were handled or dispositioned?
- Does the spec keep this branch spec-only and limited to the new spec
  directory?
- Does the spec require visible `Public claim level: concept` and `No public
  conclusion without evidence`?
- Does the spec include all requested candidate placements and a future
  placement decision record?
- Does the spec distinguish the future surface from `/reviewer-quickstart/`,
  `/packets/assembly/`, `/review-claim-checklist/`, `/validation/`,
  `/proof-paths/tour/`, and `/questions/objections/`?
- Does the spec require sections for static evidence input, test-planning
  questions, coverage caveats, safe handoff language, stop conditions, test
  owner handoff, and non-claims?
- Does the spec include the required fields: claim label, proof path, rule
  ID/family, evidence tier, coverage label, changed surface, limitation,
  suggested test question, next owner, validation evidence, and non-claim?
- Does the spec forbid generated tests, test sufficiency, runtime behavior
  proof, production traffic, endpoint performance, release safety/approval,
  complete coverage, AI/LLM analysis, and replacement of QA/tests/source
  review/runtime observability/human judgment?
- Does the spec forbid raw facts, SQLite content, analyzer logs, source
  snippets, SQL, config values, secrets, local paths, remotes, generated scan
  dirs, private sample names, command output, hidden validation details, and
  credential-like values?
- Are validation expectations specific enough for required copy, required
  links, route metadata, discovery/sitemap metadata when standalone,
  forbidden claims, private/raw material, word-count bounds, and
  desktop/mobile browser sanity?
- Is there any wording that could be read as blame toward test owners, QA,
  service owners, reviewers, or previous authors?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
