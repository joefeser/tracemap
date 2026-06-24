# TraceMap Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-site-claim-guardrails` spec for spec-review
findings first.

This is a spec-only public-site or contributor-facing phase. It should define
a future site claim guardrails page or section that tells contributors,
agents, reviewers, and maintainers how public copy may describe TraceMap
without overclaiming. The surface is a copy-governance rulebook, not a product
capability claim, runtime proof, release gate, AI/LLM analysis feature, or
replacement for human review.

Please inspect:

- `.kiro/specs/site-tracemap-tools-site-claim-guardrails/requirements.md`
- `.kiro/specs/site-tracemap-tools-site-claim-guardrails/design.md`
- `.kiro/specs/site-tracemap-tools-site-claim-guardrails/tasks.md`
- `.kiro/specs/site-tracemap-tools-site-claim-guardrails/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-site-claim-guardrails/review-packet.md`

Review focus:

- Does the spec stay implementation-free and leave future implementation tasks
  unchecked?
- Are `Status: not-started`, `Readiness: ready-for-implementation`, and
  `Public claim level: concept` present, with `implementation-state.md`
  showing that the packet started at `spec-review` and moved to
  `ready-for-implementation` only after review findings were handled?
- Is `Public claim level: concept` justified for public-facing output, with a
  bounded `hidden` alternative only for strictly contributor-only placement?
- Does the future public-facing page or section visibly require
  `Public claim level: concept` and
  `No public conclusion without evidence`?
- Does the spec evaluate the required candidate placements:
  `/site-claim-guardrails/`, `/docs/site-claim-guardrails/`, section on
  `/review-claim-checklist/`, and contributor-facing docs page linked from
  public `/docs/`?
- Does the spec distinguish the guardrails from `/review-claim-checklist/`,
  `/proof-source-catalog/`, `/roadmap/`, `/limitations/`,
  `/questions/objections/`, and `/language/change-risk/` when present?
- Does the spec require sections for public claim levels, proof-path
  requirements, allowed evidence references, forbidden raw material,
  non-claim patterns, downgrade and hidden rules, validation expectations, and
  review handoff?
- Does the spec require guardrail rows for shipped, demo, concept, hidden, raw
  artifact reference, dev-only feature, reduced coverage, runtime/release
  wording, AI/LLM wording, and private-only support?
- Does each guardrail row require condition, allowed public wording or action,
  required proof path, downgrade or hidden trigger, forbidden implication, and
  review handoff?
- Does the spec keep public claim levels bounded to `shipped`, `demo`,
  `concept`, and `hidden`?
- Does the spec require proof-path fields: public-safe proof path, rule ID or
  rule family, evidence tier where applicable, coverage label, limitation, and
  source context?
- Does the spec forbid new product capability claims, runtime proof, release
  approval, release safety, operational safety, complete coverage, AI/LLM
  analysis, and replacement of human review?
- Does the spec prohibit raw facts, SQLite, analyzer logs, source snippets,
  SQL, config values, secrets, local paths, remotes, generated scan
  directories, private sample names, command output, hidden validation details,
  and credential-like values?
- Does the spec avoid blame language and treat missing, reduced, private-only,
  or hidden evidence as a gap, limitation, downgrade, hidden state, stop
  condition, or owner handoff?
- Does the spec provide enough validation guidance for required guardrail
  rows, required links, metadata, discovery or sitemap metadata if public
  standalone, forbidden claims, private/raw material, word count bounds, and
  desktop/mobile browser sanity?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.

Record review outcomes and dispositions in this spec's
`implementation-state.md`.
