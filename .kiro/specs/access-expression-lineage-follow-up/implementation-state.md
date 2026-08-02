# Implementation State

- Branch: `codex/access-expression-lineage-follow-up`
- Base: `origin/dev` at `155760c0ad03fb81850827e9136f07d44e9aa9e8`
- Scope: static Access expression lineage only; no Access/COM, Windows, execution, layout, or chart work.
- Private baseline: 7,690 facts; 250 `AccessBindingExpressionPartial` gaps and 292 `AccessBindingTargetUnresolved` gaps.
- Concentration: `rptMetricsByPlanWeek` and `rptMetricsByPlanDay` account for 172 of the 250 partial expression gaps.
- Decision: model unique same-surface calculated-control references as exact stable-key candidates while preserving ambiguity as a gap. This reuses already-exported design metadata and requires no new extraction path.
- Representative discovery: Access SaveAsText splits long properties into adjacent quoted fragments. The current parser retained only the first fragment, producing 75-character pseudo-expressions and false partial gaps. The follow-up therefore reconstructs bounded fragments before expression projection.
- Representative result: 7,559 facts; `AccessBindingExpressionPartial` 250 -> 32, `AccessBindingTargetUnresolved` 292 -> 98, and `AccessDesignInputCatalogStableKeyUnmatched` 566 -> 0. Two independent runs produced identical facts and manifest SHA-256 values.
- Downstream private regeneration: hidden identities 2,905; flow 10,000 bounded paths with 1,997 gaps and partial coverage; copy/clone 32 candidates with 579 gaps and partial coverage.
- Validation: locked restore passed; focused composition/projector/UI tests 37/37; Access-focused tests 205/205; full build passed with 0 warnings/errors; full tests 1,079/1,079; changed-file formatting passed; private-path scan passed; `git diff --check` passed.
- Formatting note: solution-wide `dotnet format --verify-no-changes` reports pre-existing whitespace drift in unrelated `origin/dev` files. Changed-file formatting verification is clean; unrelated files were not rewritten.
- Deferred: runtime validation, dynamically constructed expressions, unsupported Access behavior, and chart compound properties.
