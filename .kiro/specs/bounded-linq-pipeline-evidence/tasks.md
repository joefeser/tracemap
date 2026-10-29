# Bounded LINQ pipeline evidence

- [x] Add one shared C#/VB source-semantic rule for object identity grouping, anonymous key/count projection and scalar captured-count equality.
- [x] Add DataTable/DataSet-table AsEnumerable -> Select(object initializer) -> ToList column dependency evidence with bounded conversions.
- [x] Require signed framework declaration profiles and supported overloads; reject comparers, indexed selectors, dynamic columns and arbitrary projection helpers.
- [x] Preserve existing SQL Fill/consumer/lifecycle contracts and the compiled delegate refusal; document no Fill association or runtime effect proof.
- [x] Author independent public C#/VB fixtures and positive/negative/determinism test definitions.
- [x] Compile fixtures, CLI and test project with -warnaserror; update catalog and handoff.
- [ ] Execute focused tests and broader shared-core Debug suite (explicitly deferred by Joe).
- [ ] Add compiled delegate/LINQ interpretation under a separately reviewed bounded contract (not implemented here).
- [ ] Add dynamic DataTable.Select predicate support (explicitly outside this task).
