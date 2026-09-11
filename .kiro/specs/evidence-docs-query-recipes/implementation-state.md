# Implementation state

- Branch: `codex/evidence-docs-query-recipes`
- Base: PR #731 head `f34cc7c8`; stacked until #731 merges into `dev`.
- Tracking: PR #733 closes #732; part of #651.
- Scope: deterministic public retrieval metadata and read-only TraceMap-index query recipes; no private workflow prompts, company vocabulary, business interpretation, target design, planner, or generator.
- Implemented: versioned 10-recipe catalog for single and combined indexes; typed bounded retrieval hints on generic and Web Forms chunks; generated JSON/Markdown artifacts with manifest integrity and collision handling; documented rules and limitations.
- Safety: recipe validation restricts SQL to one parameterized read-only `SELECT`, a closed table allowlist, and `$limit`; hint validation requires an existing recipe, exact typed parameter names, positive integers, limits no greater than 1,000, and supporting IDs.
- Validation: focused EvidenceDocs/WebForms suite passed 40/40; full `TraceMap.sln` suite passed 1,806/1,806 on 2026-09-10; `git diff --check` passed. The existing nullable warning in `PropertyMappingTests.cs:560` remains unchanged.
- Current: implementation complete in stacked PR #733; retarget to `dev` after PR #731 merges.
