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
