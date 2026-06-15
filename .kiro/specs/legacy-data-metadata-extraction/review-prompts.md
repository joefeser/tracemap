# Legacy Data Metadata Extraction Review Prompts

Branch:

```text
codex/legacy-data-metadata-extraction-spec
```

Spec files:

- `.kiro/specs/legacy-data-metadata-extraction/requirements.md`
- `.kiro/specs/legacy-data-metadata-extraction/design.md`
- `.kiro/specs/legacy-data-metadata-extraction/tasks.md`
- `.kiro/specs/legacy-data-metadata-extraction/implementation-state.md`

## Opus Review Prompt

Review the TraceMap `legacy-data-metadata-extraction` spec on branch
`codex/legacy-data-metadata-extraction-spec` for merge readiness.

This is a spec-only branch. It should make implementation ready for extracting
legacy .NET data/ORM metadata from checked-in DBML, EDMX, typed DataSet/XSD,
TableAdapter metadata, app.config/web.config provider and connection metadata,
generated data code, and old ORM/service data descriptors. It must not implement
scanner code.

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Failed build is not a clean repo.
- No LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw SQL, connection strings, config values, source snippets,
  raw remotes, local absolute paths, private sample names, secrets, URLs with
  credentials, or generated local smoke artifacts.

Please inspect:

- `.kiro/specs/legacy-data-metadata-extraction/requirements.md`
- `.kiro/specs/legacy-data-metadata-extraction/design.md`
- `.kiro/specs/legacy-data-metadata-extraction/tasks.md`
- `.kiro/specs/legacy-data-metadata-extraction/implementation-state.md`
- `.kiro/specs/legacy-wcf-metadata-normalization/*`
- `.kiro/specs/legacy-webforms-event-flow/*`
- `rules/rule-catalog.yml`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`

Review questions:

1. Is the scope implementable as a first legacy data metadata slice, or should
   DBML, EDMX, typed DataSet/TableAdapter, and config provider metadata be split?
2. Are the proposed fact types distinct enough from existing SQL/query/config and
   reducer facts, without overloading runtime access semantics?
3. Are rule IDs complete and are their limitations specific enough?
4. Does the typed DataSet `.xsd` gate prevent unrelated schemas, WCF schemas,
   vendor specs, docs, and fixtures from becoming data facts?
5. Are DBML entity/table/column/routine extraction boundaries safe?
6. Are EDMX CSDL/SSDL/MSL mapping rules conservative enough around inheritance,
   complex types, conditional mappings, many-to-many shapes, and provider
   extensions?
7. Does TableAdapter SQL handling reuse hash/shape evidence without leaking raw
   SQL or claiming execution?
8. Are config provider and connection metadata redaction rules sufficient for
   connection strings, server/catalog names, usernames, passwords, encrypted
   sections, transforms, and external config?
9. Is generated-code linkage scoped tightly enough to avoid global short-name
   false positives?
10. Are coverage labels and `AnalysisGap` expectations clear when old codebases
    do not build locally?
11. Does the spec avoid runtime data flow, SQL execution, database existence,
    provider compatibility, branch feasibility, and production usage overclaims?
12. Are tasks implementation-ready, reviewable, and traceable to requirements?
13. What tests are missing before implementation starts?

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `legacy-data-metadata-extraction` spec on branch
`codex/legacy-data-metadata-extraction-spec` as an implementation planner.

Focus on:

- Existing code seams in the .NET scanner, fact model, SQL/query extraction,
  config extraction, reports, and legacy validation harness.
- Whether DBML, EDMX, typed DataSet/TableAdapter, and config provider metadata
  should ship in one implementation or staged PRs.
- Whether the proposed `LegacyData*` fact types are right, too broad, or should
  reuse existing fact types differently.
- Safe XML/config parsing and XXE/entity-expansion handling.
- Safe identifier policy and redaction for raw SQL, connection strings,
  provider metadata, namespaces, paths, URLs, remotes, and secrets.
- Typed DataSet `.xsd` gating.
- EDMX CSDL/SSDL/MSL mapping ambiguity.
- Generated-code linkage without global short-name matching.
- How legacy data facts should feed existing SQL/table/entity surfaces, paths,
  reverse, impact, and release-review without changing reducer semantics.
- Minimal first PR boundary.
- Tests most likely to catch false positives and privacy leaks.
- Validation commands and likely failure points.

Return a concrete implementation plan, risky assumptions, recommended first PR
boundary, and any spec edits needed before implementation.

## Re-Review Prompt

Re-review the `legacy-data-metadata-extraction` spec after first-pass review
patches.

Confirm:

- Medium+ and blocking findings from the first review pass were addressed.
- Remaining issues are low-risk, clearly documented, or intentionally deferred.
- `tasks.md` is implementation-ready and unchecked.
- `implementation-state.md` says `ready-for-implementation` only if the spec is
  genuinely ready.
- No scanner code, site pages, private paths, raw sample names, raw SQL,
  connection strings, config values, snippets, remotes, or secrets were added.
- The spec still avoids runtime data flow, SQL execution, database existence,
  provider compatibility, branch feasibility, and LLM/vector/prompt-based
  claims.

Return remaining blocking issues, if any, and implementation readiness.

