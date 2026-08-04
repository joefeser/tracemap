# Implementation State

- Branch: `codex/access-domain-lookup-completion`
- Base: `origin/dev` at `e6546f34b77edcd809a4c50013bbacbb6c34eff6`
- Scope: static domain-lookup completion only; no Access COM, Windows, row reads, query execution, or runtime claims.
- Inventory: 19 remaining cases comprise 12 numeric return identifiers whose domains instead declare uniquely matching `W`-prefixed crosstab headings/outputs, three return identifiers absent from the output catalog but present ambiguously in the bounded dependency scope, and four criteria identifiers with multiple bounded candidates.
- Decision: preserve partial evidence. Exact crosstab pivot headings become explicit candidates, while the representative numeric-vs-`W` shapes become explicit prefix mismatches rather than invented aliases. Dependency-only return identifiers remain outside the output catalog and receive precise unique/ambiguous classifications.
- Validation: 127 focused projector/composer/UI tests and 278 Access-filtered tests pass. Full solution build succeeds with zero warnings/errors; full solution tests exit successfully. Changed-file formatting, adapter artifact validation, private-path guard, and diff validation pass.
- Private regeneration: two independent runs each emitted 8,984 facts; `facts.ndjson` and `scan-manifest.json` are byte-identical. The 19 prior generic cases now comprise 12 `AccessBindingDomainCrosstabPivotPrefixMismatch`, three `AccessBindingDomainSelectedFieldDependencyAmbiguous`, and four `AccessBindingDomainCriteriaFieldAmbiguous` gaps.
- Workbook: refreshed private remainder register preserves seven owner dispositions and 27 active cases; all sheets render and contain no formula errors.
- Deferred: runtime lookup results, dynamic domain/criteria construction, and any alias equivalence not proven by existing source-to-output lineage.
- PR: #587 targets `dev`; no merge is authorized by this goal.

## Full-register follow-up

- Scope decision: extend the same PR with two deterministic fixes found while reconciling all 34 populated remainder cases. A single-table wildcard record source can be statically complete only when the table field catalog has no acquisition gaps. Access `IN (...)` predicate syntax is an operator and must not be classified as a custom function.
- Owner decisions: the seven `qlkpActionTabs` cases and one `WeekAtAGlanceJohnAcceleratedwk1` case correspond to objects already identified as not required for the rebuild. Preserve those as owner dispositions in the private register; do not alter or delete the source database.
- Preserved gaps: four domain-criteria and three domain-return ambiguities lack source-to-output candidates in the immutable evidence and cannot be resolved honestly. Twelve numeric-versus-`W` crosstab cases are source-design contradictions, not aliases.
- Private regeneration checkpoint: two independent runs emitted 8,982 facts with byte-identical `facts.ndjson` and `scan-manifest.json`. The generic remainder dropped by two: the complete `Users.*` table wildcard and the report `IN (...)` predicate no longer emit gaps.
- Validation: 131 focused projector/composer/UI tests and all 281 Access-filtered tests pass. The full solution builds with zero warnings/errors and all 1,159 tests pass. Changed-file formatting, adapter-artifact validation, private-path guard, diff validation, workbook formula scan, and visual rendering of all three workbook sheets pass.
- Refreshed private register: 32 deterministic remainder cases; 15 carry confirmed owner do-not-port dispositions and 17 remain active. The register contains no formulas or formula errors and preserves the deterministic fact ID for every row.

## Review hardening

- ACK authorized patching 15 unresolved exact-head review threads at `aa299b41a532d6b726e47f3dd61149626b3aa0fb`.
- Correctness fixes cover exact crosstab headings that also have declared output facts, output-only proof for numeric-versus-`W` mismatch candidates, per-lookup aggregation and gap precedence, fail-closed wildcard completion on truncated or malformed field catalogs, bracketed `In`/`Exists` field identities, malformed predicate operands, and release-review preservation of expression gap classifications.
- Hash-only candidate construction is centralized. Predicate terminology and the redundant set-ordering step were cleaned up without changing evidence claims.
- Validation: 109 focused projector/composer/reporting tests and all 1,161 solution tests pass. The solution builds with zero warnings/errors; changed-file whitespace formatting, private-path guard, and diff validation pass. Repository-wide `dotnet format --verify-no-changes` still reports unrelated baseline whitespace findings outside this PR's changed files.
- Exact-head Baz follow-up: four additional threads were valid in three correction clusters. Delimiter validation now rejects stray brackets/parentheses and unsupported literal-only `EXISTS` operands while preserving valid literal `IN` lists. Release review treats any explicit expression gap as review-recommended. Static crosstab candidates retain partial coverage and deterministic supporting-fact provenance rewritten to the composed scan's fact IDs.
- Final exact-head follow-up: malformed domain expressions are now excluded from both projection-time and reconciliation-time candidate discovery. Crosstab pivot hashes and provenance derive from one validated, deterministically ordered evidence traversal to prevent future drift.
- Qodo disposition follow-up: the edited Qodo summary exposed one still-current fail-open case. Criteria-only crosstab pivot or prefix-mismatch identities now force partial coverage and the matching candidate gap instead of being treated as resolved criteria.
