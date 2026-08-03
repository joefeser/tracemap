# Implementation State

- Branch: `codex/access-dlookup-criteria-lineage`
- Base: `origin/dev` at `e5837df0a8da4aa3a0078827bd218022b810213d`
- Scope: protected static DLookup-family criteria lineage only; no Access COM, Windows, row reads, query execution, or runtime claims.
- Decision: return fields remain constrained to the domain output catalog. Criteria fields may use a separately constructed scope from direct declared query dependencies, with exact-name uniqueness required.
- Representative finding: the target domain query's immutable output catalog does not contain the criteria name as a declared output. Two direct dependencies expose that name, so TraceMap must report an exact criteria ambiguity rather than invent a unique field or ask a generic expression question.
- Private baseline: 34 binding gaps, including 19 generic `AccessBindingExpressionPartial` gaps.
- Validation: 141 focused Access expression/foundation/design/UI tests pass; full solution build passes with 0 warnings/errors; full solution tests pass; changed-file formatting, private-path guard, adapter artifact validation, and `git diff --check` pass.
- Private regeneration: 8,972 facts; the 19 generic `AccessBindingExpressionPartial` gaps became 15 precise `AccessBindingDomainSelectedFieldUnmatched` gaps and 4 precise `AccessBindingDomainCriteriaFieldAmbiguous` gaps. The total 34 binding gaps did not change because the acquired evidence does not justify a false unique match.
- Determinism: two independent regenerations produced byte-identical `facts.ndjson` and `scan-manifest.json`; adapter artifact validation passed.
- Deferred: runtime lookup validation, dynamic domain/criteria construction, transitive dependency inference, and human business-meaning confirmation.
