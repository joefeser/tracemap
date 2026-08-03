# Implementation State

- Branch: `codex/access-form-lineage-remainder-reduction`
- Base: `origin/dev` at `7f6cc9382694b5cf772d41a7945798b4206ff840`
- Scope: deterministic Access form-lineage remainder reduction plus report-remainder classification; no execution or extraction-boundary widening.
- Baseline: the prior same-snapshot register contained 122 deterministic binding remainders, including 11 prior owner dispositions.
- Primary cause: one resolved saved query has no DAO output-field records, preventing 17 DLookup expressions from resolving selected/criteria output names.
- Decision: admit a unique statically declared output name as a catalog identity while leaving its source lineage partial. Static binding lineage and runtime values are independent coverage dimensions.
- Regeneration: product head `bd6de084e3ccfe4ab77920168609cf78207967f7`; local snapshot commit `4a94843a612e9dcdaaea902998231d140a3a9c4a`; database SHA-256 `48453117d8cd42e4803ffcc5dfe270e3e5b313fe8a096108252d4e2d95551a8e`; 3,609 base facts and 8,773 enriched facts.
- Result: 108 deterministic binding remainders. Nine current cases have exact owner `ignore / do not port` dispositions, leaving 99 active: six form cases and 93 report cases.
- DLookup result: the seven active form domain-catalog gaps were removed. They now expose a more precise selected-output mismatch where the requested identifier does not equal a declared saved-query output; the implementation does not force the mismatch closed.
- Scoped-filter result: the targeted form-filter ambiguity is removed.
- Report pass: begun and classified in `report-specific-pass.md`; crosstab generated-column candidates are the largest remaining family.
- Validation: locked restore, 247 Access-focused tests, full build, full solution test command, private-path check, and diff check passed for the first implementation commit. Final-head validation remains pending.
