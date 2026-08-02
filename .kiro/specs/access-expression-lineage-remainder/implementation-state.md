# Implementation State

- Branch: `codex/access-expression-lineage-remainder`
- Base: `origin/dev` at `a38202f2be516f5e771af3a4efe28c2d6e1afaa4`
- Scope: deterministic classification and resolution of the representative post-#578 expression/binding remainder plus a private human-review register.
- Current census: 32 `AccessBindingExpressionPartial`; 98 `AccessBindingTargetUnresolved`.
- Proven concentration: 55 unresolved controls under partial crosstab sources without output catalogs; 19 partial domain expressions with resolved domain objects but incomplete field catalogs.
- Boundary: no execution, row reads, COM widening, raw private material in standard facts, or forced zero-gap result.
- Representative result: generic `AccessBindingExpressionPartial` 32 -> 0 and generic `AccessBindingTargetUnresolved` 98 -> 0. Eight targets resolve through dependency-scoped inline SQL fields; 122 remaining cases have precise classifications.
- Precise remainder: 55 crosstab output candidates; 20 dynamic expressions; 19 incomplete domain-field catalogs; eight partial inline SQL projections; six unmatched inline outputs; five ambiguous record sources; three missing owning record sources; three unresolved custom functions; one incomplete query-output catalog; and two explicit ambiguity cases.
- Private review projection: 122-row workbook with exact private surface/control context and separate human annotation columns; stored outside the repository and independently deletable.
- Downstream rerun: 100 bounded flow paths and 32 copy/clone candidates, both correctly labeled partial.
- Tracking: issue #579.
- Pull request: #580 targeting `dev`; ACK review is pending and merge is not authorized by this slice.
- Determinism: two final representative enrichments emitted byte-identical 7,548-fact streams and manifests.
- Downstream validation: 100 bounded flow paths and 32 copy/clone candidates, both correctly labeled partial.
- Validation: 96 focused tests passed; changed-file format verification passed; full solution build passed with zero warnings/errors; full solution test command exited successfully; private-path guard and `git diff --check` passed.
- Repository-wide format note: the unscoped formatter reports pre-existing whitespace findings in unrelated files, so this slice uses the changed-file format gate and does not rewrite unrelated code.
