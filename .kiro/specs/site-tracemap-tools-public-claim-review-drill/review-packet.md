# TraceMap Kiro Review Packet

Status: implemented
Readiness: implemented
Public claim level: concept

Review the `site-tracemap-tools-public-claim-review-drill` spec for
spec-review findings first.

This is a spec-only public-site phase. It should define a future public-safe
claim review drill page or section that gives readers a short exercise for
checking whether a public claim is backed by a proof path, limitation,
evidence tier, coverage label, and non-claim. It is a learning/checklist
surface, not an automated grader.

Please inspect:

- `.kiro/specs/site-tracemap-tools-public-claim-review-drill/requirements.md`
- `.kiro/specs/site-tracemap-tools-public-claim-review-drill/design.md`
- `.kiro/specs/site-tracemap-tools-public-claim-review-drill/tasks.md`
- `.kiro/specs/site-tracemap-tools-public-claim-review-drill/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-public-claim-review-drill/review-packet.md`

Review focus:

- Does the spec stay implementation-free and leave future implementation tasks
  unchecked?
- Are `Status: not-started`, `Readiness: ready-for-implementation`, and
  `Public claim level: concept` present, with the
  `implementation-state.md` review record showing the initial spec-review gate
  was cleared only after review findings were handled?
- Does the future page or section visibly require
  `Public claim level: concept` and `No public conclusion without evidence`?
- Does the spec evaluate the required candidate placements:
  `/claims/review-drill/`, `/review-claim-checklist/drill/`, section on
  `/review-claim-checklist/`, and section on `/proof-paths/tour/`?
- Does the spec distinguish the drill from `/review-claim-checklist/`,
  `/proof-paths/tour/`, `/proof-paths/faq/`, `/questions/objections/`,
  `/packets/examples/`, and `/language/change-risk/` when present?
- Does the spec require sections for drill setup, sample public-safe claims,
  evidence checklist, answer key, unsafe answer examples, stop conditions, and
  non-claims?
- Does the spec require rows for supported demo-level claim, concept-only
  claim, reduced-coverage claim, unsafe runtime claim, unsafe release claim,
  private-evidence-only claim, and missing-proof claim?
- Does each row require claim text, expected claim level, proof path needed,
  evidence fields to check, limitation or non-claim, correct outcome, and next
  action?
- Does `evidence fields to check` require discrete proof path, rule ID or rule
  family, evidence tier, coverage label, limitation, non-claim, source context,
  and public/private status rather than a vague combined field?
- Does validation require rows with `repeat with proof` to expose discrete
  rule ID or rule family, evidence tier, and coverage label?
- Does the answer key stay bounded to `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`?
- Does the spec forbid automated grading claims, runtime proof, release
  approval, release safety, operational safety, absence-of-impact proof,
  complete coverage, AI/LLM analysis, and replacement of human review?
- Does the spec prohibit raw facts, SQLite, analyzer logs, source snippets,
  SQL, config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation
  details, and credential-like values?
- Does the spec avoid blame language and treat missing, reduced, or
  private-only evidence as a gap, limitation, downgrade, internal-only state,
  stop condition, or owner handoff?
- Does the spec provide enough validation guidance for required drill rows,
  answer-key outcomes, required links, metadata, discovery or sitemap metadata
  if standalone, forbidden claims, private/raw material, word count bounds, and
  desktop/mobile browser sanity?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.

Record review outcomes and dispositions in this spec's
`implementation-state.md`.
