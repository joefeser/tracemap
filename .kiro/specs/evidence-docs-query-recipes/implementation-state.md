# Implementation state

- Branch: `codex/evidence-docs-query-recipes`
- Base: PR #731 head `f34cc7c8`; stacked until #731 merges into `dev`.
- Tracking: PR #733 closes #732; part of #651.
- Scope: deterministic public retrieval metadata and read-only TraceMap-index query recipes; no private workflow prompts, company vocabulary, business interpretation, target design, planner, or generator.
- Implemented: versioned 10-recipe catalog for single and combined indexes; typed bounded retrieval hints on generic and Web Forms chunks; generated JSON/Markdown artifacts with manifest integrity and collision handling; documented rules and limitations.
- Safety: recipe validation restricts SQL to one parameterized read-only `SELECT`, a closed table allowlist, and `$limit`; hint validation requires an existing recipe, exact typed parameter names, positive integers, limits no greater than 1,000, and supporting IDs.
- Validation: focused EvidenceDocs/WebForms/static-explorer suite passed 103/103; full `TraceMap.sln` suite passed 1,809/1,809 on 2026-09-11; `git diff --check` passed. The existing nullable warning in `PropertyMappingTests.cs:560` remains unchanged.
- Current: PR #733 was merged into the PR #731 feature branch; exact-head remediation continues on PR #731 before that branch merges to `dev`.
- Review remediation on PR #731 head: shared the complete packet validator with docs export; normalized supported commit identities; preserved complete surface citations and gap provenance; canonicalized nested supporting IDs; sanitized display paths; scoped gap identities and combined-index recipes to their owning source.
