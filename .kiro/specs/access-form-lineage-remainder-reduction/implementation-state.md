# Implementation State

- Branch: `codex/access-form-lineage-remainder-reduction`
- Base: `origin/dev` at `7f6cc9382694b5cf772d41a7945798b4206ff840`
- Scope: deterministic Access form-lineage remainder reduction plus report-remainder classification; no execution or extraction-boundary widening.
- Baseline: the prior same-snapshot register contained 122 deterministic binding remainders, including 11 prior owner dispositions.
- Primary cause: one resolved saved query has no DAO output-field records, preventing 17 DLookup expressions from resolving selected/criteria output names.
- Decision: admit a unique statically declared output name as a catalog identity while leaving its source lineage partial. Static binding lineage and runtime values are independent coverage dimensions.
- Regeneration: product head `3d951ed6d8a09dc94c36e43ea92bac2e39050d74`; local snapshot commit `4a94843a612e9dcdaaea902998231d140a3a9c4a`; database SHA-256 `48453117d8cd42e4803ffcc5dfe270e3e5b313fe8a096108252d4e2d95551a8e`; 3,609 base facts and 8,773 enriched facts.
- Result: 108 deterministic binding remainders. Nine current cases have exact owner `ignore / do not port` dispositions, leaving 99 active: six form cases and 93 report cases.
- DLookup result: the seven active form domain-catalog gaps were removed. They now expose a more precise selected-output mismatch where the requested identifier does not equal a declared saved-query output; the implementation does not force the mismatch closed.
- Scoped-filter result: the targeted form-filter ambiguity is removed.
- Report pass: begun and classified in `report-specific-pass.md`; crosstab generated-column candidates are the largest remaining family.
- Review hardening: runtime-sensitive built-ins and SELECT-list functions retain partial runtime-value coverage; VBA expression dependencies become graph targets; the procedure catalog excludes non-value-returning declarations; static fallback outputs emit Tier3 evidence; empty fallback catalogs emit an explicit gap; and legacy public constructor signatures remain available.
- Validation: the exact product head repeated the Windows same-snapshot scan, metadata export, and VBA export with unchanged source/loaded state and clear canaries. It produced 2,899 hidden identities and the same 108 remainder cases. Post-review validation passed: 250 Access-focused tests, 1,128 full-solution tests, clean solution build with zero warnings/errors, private-path guard, and diff check.
