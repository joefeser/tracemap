# TraceMap Kiro Review Packet

Status: implemented
Readiness: implemented
Public claim level: concept

Review the `site-tracemap-tools-guided-proof-path-tour` spec for spec-review
findings first. This is a spec-only site phase; it should not implement site
code.

## Review Orientation

Branch: `codex/spec-site-guided-proof-path-tour`
Base: `origin/main`
Target PR base: `main`

Local review artifacts are not committed and should be saved under
`.tmp/kiro-reviews/site-tracemap-tools-guided-proof-path-tour/`.

Remaining open questions after review: the future route or placement remains
deferred to implementation. The future implementer must evaluate
`/proof-paths/tour/`, `/demo/proof-path-tour/`, and a folded section on
`/proof-paths/`, then record the final choice and rejected alternatives in
`implementation-state.md`.

## Scope

The future page or section would give public-site readers a guided
proof-path tour. It starts from a public claim label, follows the public-safe
proof path, checks rule/evidence/coverage/source/limitation fields, and shows
where the reader should stop.

The tour is a guided reading experience for existing public-safe evidence
surfaces. It is not a proof engine, not a runtime trace, not AI analysis, not
release approval, and not operational approval. This phase creates only the
spec packet.

Please inspect:

- `.kiro/specs/site-tracemap-tools-guided-proof-path-tour/requirements.md`
- `.kiro/specs/site-tracemap-tools-guided-proof-path-tour/design.md`
- `.kiro/specs/site-tracemap-tools-guided-proof-path-tour/tasks.md`
- `.kiro/specs/site-tracemap-tools-guided-proof-path-tour/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-guided-proof-path-tour/review-packet.md`

Review focus:

- Are `Status: not-started`, `Readiness: ready-for-implementation`, and
  `Public claim level: concept` present and consistent after review patches?
- Does the packet remain spec-only and restrict writes to the new spec
  directory?
- Are future implementation tasks unchecked?
- Does the spec require visible `Public claim level: concept` and
  `No public conclusion without evidence` copy?
- Does it frame the future surface as a guided tour, not a proof engine,
  runtime trace, AI analysis, release approval, or operational approval flow?
- Does it require evaluation of `/proof-paths/tour/`,
  `/demo/proof-path-tour/`, and a folded section on `/proof-paths/`, with the
  final choice and rejected alternatives recorded?
- Does it distinguish the tour from `/proof-paths/`,
  `/proof-source-catalog/`, `/demo/evidence-trail/`, `/review-room/`,
  `/packets/`, `/validation/`, `/limitations/`, `/demo/runbook/`,
  `/review-claim-checklist/`, and `/glossary/`?
- Does it require proof steps for claim label, public claim level, proof path,
  rule ID/family, evidence tier, coverage label, commit SHA/source context,
  extractor version, supporting public route/artifact, limitation, non-claim,
  and next owner, plus at least one complete illustrative worked example?
- Does it clearly tell readers where to stop when evidence fields are absent
  or incomplete?
- Does it forbid runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, complete coverage,
  AI/LLM impact analysis, embeddings, vector databases, and prompt
  classification claims?
- Does it forbid raw facts, raw SQLite, analyzer logs, raw source snippets,
  raw SQL, config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, and hidden validation details?
- Does it require public-safe links, route-status recording, metadata,
  discovery metadata including `hintCategory` selection and recording plus
  confirmation that `concept` is accepted by tooling before use, sitemap
  metadata if standalone, word-count bounds, forbidden-claim validation,
  private/raw-material validation, sanctioned-section markup decisions,
  illustrative example validation, and desktop/mobile browser sanity when
  implemented?
- Does it include the non-binding `hintCategory` vocabulary reference
  (`start`, `evidence`, `limitations`, `demo`, `repo-doc`, `roadmap`,
  `use-case`) and the non-binding recommendation of `evidence`?
- Do both requirements and design validation sections require that at least
  one worked example traverses all required proof-step fields to a bounded
  non-claim conclusion, not only that an example is present and labeled
  illustrative?
- Does it record guidance for folded-section word count when the minimum
  content cannot fit the suggested 350 to 900 word bound?
- Is `implementation-state.md` sufficient for a future agent to resume without
  guessing?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
