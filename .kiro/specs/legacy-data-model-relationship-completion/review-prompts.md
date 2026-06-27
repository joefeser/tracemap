# Legacy Data Model Relationship Completion Review Prompts

Use these prompts when reviewing the spec or a future implementation slice.

## Spec Review Prompt

Review `.kiro/specs/legacy-data-model-relationship-completion/` as a hidden
claim-level, implementation-ready TraceMap spec.

Focus on:

- whether the scope is a true follow-up after
  `legacy-data-model-orm-mapping-completion` slice 1 and does not duplicate the
  already-merged evidence regression work;
- whether PR 1 is small enough to implement safely;
- whether relationship gap vocabulary is catalog-gated before emission;
- whether DBML, EDMX, typed DataSet, and NHibernate relationship boundaries are
  deterministic and conservative;
- whether ambiguity/unsupported shapes produce `AnalysisGap` or needs-review
  labels instead of invented relationship surfaces;
- whether the spec avoids runtime ORM/database behavior, query execution,
  impact proof, AI/LLM analysis, and complete coverage claims;
- whether privacy guardrails cover raw SQL, config values, connection strings,
  source snippets, local paths, remotes, URLs, private labels, provider values,
  and secrets;
- whether validation expectations include focused tests, full .NET validation
  when feasible, CLI smoke with commit SHA, private-path guard, and diff check.

Return findings by severity. Treat Medium+ actionable issues as required before
the spec is ready.

## Implementation Review Prompt

Review the future implementation PR for this spec.

Focus on:

- rule catalog entries and limitations before any new gap string or classifier
  is emitted;
- deterministic relationship gap decisions and stable fact IDs;
- preservation of existing deterministic relationship evidence and
  `mappingKind` values;
- no arbitrary endpoint selection for ambiguous DBML, EDMX, typed DataSet, or
  NHibernate shapes;
- `AnalysisGap` and needs-review behavior for unsupported or ambiguous
  relationship shapes;
- no double-counting of source relationship facts and derived relationship
  projections if projection/report code is touched;
- `AnalysisGap` facts staying out of terminal `legacy-data` surfaces;
- privacy in `facts.ndjson`, `index.sqlite`, `report.md`, and touched exports;
- no runtime provider loading, database connections, SQL execution, LLM calls,
  embeddings, vector stores, fuzzy matching, or prompt classification;
- focused tests and validation evidence, including CLI smoke with an actual
  scanned repository commit SHA.

Do not request broad downstream expansion unless the implementation directly
touches those workflows.
