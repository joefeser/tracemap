# TraceMap Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-owner-followup-map` spec for spec-review
findings. This is a spec-only public-site phase; it should not implement site
code.

## Review Orientation

Branch: `codex/spec-site-owner-followup-map`
Base: `origin/dev`
PR target: `dev`
Review cycle: Opus/Sonnet spec review completed; Medium+ findings patched
before readiness changed to `ready-for-implementation`.

## Scope

The future surface should help readers route static-evidence questions to the
right next owner category: code owner, reviewer, test owner, service/runtime
owner, database owner, release reviewer, architect, or manager.

The surface must not claim TraceMap knows real organizational ownership. It
must not claim production ownership proof, runtime behavior proof, release
approval, operational safety, complete coverage, AI/LLM analysis, or
replacement of human judgment.

## Review Log

See `implementation-state.md` -> Review Log for Opus/Sonnet findings, patch
status, re-review status, and validation results. The review log must be
updated there before `Readiness` changes to `ready-for-implementation`.

Please inspect:

- `.kiro/specs/site-tracemap-tools-owner-followup-map/requirements.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/design.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/tasks.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/review-packet.md`

## Review Focus

- Are `Status: not-started` and `Public claim level: concept` present and
  consistent, and does `Readiness` accurately reflect the current review
  state (`spec-review` before findings are handled, `ready-for-implementation`
  only after Medium+ findings and required validations are handled)?
- Is the spec clearly future-facing and spec-only?
- Does the spec require visible `Public claim level: concept` and visible
  `No public conclusion without evidence` in the future page or section?
- Are candidate placements limited to `/owners/follow-up/`,
  `/review-room/owners/`, a section on `/team-evidence-handoff/`, or a
  section on `/questions/`, with final selection deferred to future
  implementation?
- Does the spec distinguish the map from `/team-evidence-handoff/`,
  `/incident-evidence-handoff/`, `/reviewer-quickstart/`, `/questions/`,
  `/questions/objections/`, `/packets/assembly/`, and `/manager-packet/`?
- Are all required rows present: code path question, test coverage question,
  runtime behavior question, data/schema question, config/deployment question,
  release decision question, architecture decision question, and evidence gap
  question?
- Does each required row include static evidence trigger, what TraceMap can
  show, what TraceMap cannot show, next owner, handoff wording, proof path,
  limitation, and stop condition?
- Does the spec keep next owners as categories rather than real teams,
  people, on-call rotations, approval chains, service catalogs, or org
  metadata?
- Does the spec forbid real org ownership claims, production ownership proof,
  runtime behavior proof, release approval, operational safety, complete
  coverage, AI/LLM analysis, and replacement of human judgment?
- Does the spec forbid raw facts, SQLite database content, analyzer logs,
  source snippets, SQL, configuration values, secrets, local paths, repository
  remotes, generated scan directories, private sample names, command output,
  hidden validation details, and credential-like values?
- Does the spec avoid blame language and frame missing evidence as a follow-up
  condition?
- Are validation expectations specific enough for required rows, required
  links, route metadata, discovery/sitemap metadata if standalone, forbidden
  claims, private/raw material, word count bounds, and desktop/mobile browser
  sanity checks?
- Do `tasks.md` spec-review task checkboxes reflect the review-cycle state
  recorded in `implementation-state.md`? Any completed review cycle recorded
  in the log should be marked `[x]` before readiness changes.
- Does the spec require a negative test that fails when any handoff-wording
  placeholder token, such as `[static evidence boundary]`, `[question]`,
  `[non-claim]`, `[owner category]`, `[proof path]`, `[limitation]`, or
  `[stop condition]`, remains unsubstituted in rendered copy or metadata?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
