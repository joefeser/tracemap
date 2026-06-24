# Evidence Export Navigation Polish Review Packet

## Review Scope

Review the spec packet only:

- `.kiro/specs/evidence-export-navigation-polish/requirements.md`
- `.kiro/specs/evidence-export-navigation-polish/design.md`
- `.kiro/specs/evidence-export-navigation-polish/tasks.md`
- `.kiro/specs/evidence-export-navigation-polish/implementation-state.md`

## Context

TraceMap already ships:

- `tracemap vault export`
- `tracemap docs-export`
- vault hidden/local safety hardening
- first evidence export usability polish slice
- static HTML evidence explorer

This spec should define the next follow-up slice for human/RAG navigation
usability without duplicating shipped first-slice work.

## Review Questions

1. Does the spec avoid claiming that RAG/vector/LLM output is TraceMap evidence?
2. Are public/demo/hidden safety modes clear enough?
3. Are safe-name, slug, alias, tag, and collision requirements deterministic?
4. Does the spec preserve stable IDs as source-of-truth?
5. Are vault and docs-export compatibility requirements additive and practical?
6. Are route-flow/property-flow navigation requirements bounded to static
   evidence without runtime claims?
7. Are tests sufficient for unsafe names, hidden-only evidence locations,
   deterministic ordering, unsupported question families, and chunk boundaries?
8. Are rule-ID requirements complete without inventing unnecessary rules?

## Non-Negotiables

- No LLM calls, embeddings, vector DB writes, retrieval APIs, or prompt-based
  classification.
- No raw snippets, raw SQL, config values, secrets, private paths, private repo
  names, raw remotes, or raw URLs in public/demo-safe output.
- No runtime proof, production telemetry, release approval, vulnerability
  scanning, ownership assignment, or absence-of-impact claims.
- No conclusion without evidence.
