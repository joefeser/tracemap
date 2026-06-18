# TraceMap Kiro Review Packet

Public claim level: demo

Review the `site-tracemap-tools-proof-source-catalog` spec for implementation
readiness.

This is a spec-only public site phase. It should define a public-safe catalog
page or section that maps existing public site routes and claim labels to their
allowed proof sources: route, required public claim level, proof path, source
artifact or source document, rule ID or rule family where available, evidence
tier or coverage label where available, limitation, and non-claims.

Please inspect:

- `.kiro/specs/site-tracemap-tools-proof-source-catalog/requirements.md`
- `.kiro/specs/site-tracemap-tools-proof-source-catalog/design.md`
- `.kiro/specs/site-tracemap-tools-proof-source-catalog/tasks.md`
- `.kiro/specs/site-tracemap-tools-proof-source-catalog/implementation-state.md`

Review focus:

- Does the spec stay implementation-free and leave all future implementation
  tasks unchecked?
- Does it require each row to include `Public claim level` with exactly one of
  `shipped`, `demo`, `concept`, or `hidden`?
- Is page-level `demo` justified without upgrading row-level concept or hidden
  claims?
- Does the spec clearly distinguish the catalog from `/proof-paths/`,
  `/roadmap/`, and `/capabilities/`?
- Does it keep SQLite, facts, reports, rule catalog entries, source docs, route
  metadata, coverage labels, and documented limitations as the source of truth?
- Does it prohibit raw facts, raw SQLite, analyzer logs, raw snippets, raw SQL,
  config values, secrets, local absolute paths, raw remotes, generated scan
  directories, private sample names, and hidden private-work details?
- Does it avoid runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, AI impact analysis, LLM
  analysis, and complete product coverage claims?
- Does it provide enough validation guidance for future humans and bots to
  verify links, row anchors, allowed claim levels, allowed evidence statuses,
  and forbidden overclaiming?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
