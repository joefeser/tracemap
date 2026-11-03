# Bounded LINQ pipeline evidence

- [x] Add one shared C#/VB source-semantic rule for object identity grouping, anonymous key/count projection and scalar captured-count equality.
- [x] Add DataTable/DataSet-table AsEnumerable -> Select(object initializer) -> ToList column dependency evidence with bounded conversions.
- [x] Require signed framework declaration profiles and supported overloads; reject comparers, indexed selectors, dynamic columns and arbitrary projection helpers.
- [x] Preserve existing SQL Fill/consumer/lifecycle contracts and the compiled delegate refusal; document no Fill association or runtime effect proof.
- [x] Author independent public C#/VB fixtures and positive/negative/determinism test definitions.
- [x] Compile fixtures, CLI and test project with -warnaserror; update catalog and handoff.
- [x] Execute focused source pipeline tests after Joe authorized local verification.
- [x] Complete the broader shared-core Debug suite: 5633 passed, 3 skipped, 0 failed.
- [ ] Add compiled delegate/LINQ interpretation under a separately reviewed bounded contract (not implemented here).
- [ ] Add dynamic DataTable.Select predicate support (explicitly outside this task).

## JToken expansion and verification — 2026-10-29
- [x] Add independent C#/VB JToken direct/local-pipeline/JSON-source samples and dynamic-key/helper/reassignment refusals.
- [x] Add signed Newtonsoft.Json 13 declaration checks and bounded Where -> Select -> String.Join source dependency extraction, with hashed keys/literal and upstream JSON origin unmodeled.
- [x] Fix failures exposed by executing earlier coverage: metadata Item names, VB synthesized function-value returns and reduced generic method binding; preserve user-defined conversions for inspection.
- [x] Run the focused tests and C#/VB public CLI source smokes; six candidates and seven intentional gaps per language.
- [x] Complete full Debug -warnaserror and record final results before push: 5633 passed, 3 skipped.

- [x] Repair the typed-scalar RuntimeHelpers.GetObjectValue gap exposed by executed VB controls; preserve untyped/tracked-object refusals and version the IL policy.
