# Legacy Data Model Reporting Integration Review Packet

## Review Objective

Review the `legacy-data-model-reporting-integration` Kiro spec for correctness,
implementability, safety, and consistency with TraceMap's deterministic static
evidence model.

## Files To Review

- `.kiro/specs/legacy-data-model-reporting-integration/requirements.md`
- `.kiro/specs/legacy-data-model-reporting-integration/design.md`
- `.kiro/specs/legacy-data-model-reporting-integration/tasks.md`
- `.kiro/specs/legacy-data-model-reporting-integration/implementation-state.md`

## Context

Related specs:

- `.kiro/specs/legacy-data-metadata-extraction/`
- `.kiro/specs/legacy-data-model-metadata-extraction/`
- `.kiro/specs/combined-dependency-reporting/`
- `.kiro/specs/combined-dependency-paths/`
- `.kiro/specs/reverse-impact-query/`
- `.kiro/specs/route-flow-service-data-composition/`
- `.kiro/specs/release-review-report/`
- `.kiro/specs/deterministic-risk-scoring/`
- `.kiro/specs/evidence-graph-vault-export/`
- `.kiro/specs/static-html-evidence-explorer/`

Rule families:

- `legacy.data.*.v1`
- `combined.paths.*.v1`
- `combined.reverse.*.v1`
- `combined.route-flow.*.v1`
- `release.review.*.v1`
- `vault-export.*.v1`
- `explorer.*.v1`

## Questions For Reviewers

1. Does the spec preserve the boundary between static descriptor evidence and
   runtime database behavior?
2. Are route-flow, reverse, combined reports, paths, release-review, vault/RAG,
   and explorer integrations specific enough for implementation?
3. Are absent optional facts/tables and in-flight extractor fields handled
   without crashing or overclaiming?
4. Are `AnalysisGap` and unsupported ORM facts kept out of terminal surface
   projection?
5. Are stable IDs deterministic and free of private values?
6. Are public/demo privacy and claim-level rules strict enough?
7. Is the first implementation PR boundary small and reviewable?

## Findings To Prioritize

Please classify findings as High, Medium, or Low.

Patch Medium+ findings unless they require a broad scope change that should be
recorded as a follow-up. Patch Low findings when narrow and safe.

Focus areas:

- runtime database or production behavior overclaiming;
- missing rule ID, evidence tier, coverage, or limitation requirements;
- unsafe display of raw SQL, config values, connection strings, hostnames,
  remotes, local paths, snippets, private routes, or private labels;
- selector ambiguity or duplicate identity behavior;
- missing fallback behavior for current indexes without near-term model fields;
- accidental AI/LLM/vector/RAG overreach in core TraceMap behavior;
- first PR scope that is too broad for review.
