# Implementation state — Phase 6
Branch `codex/sqlserver-field-reads`, worktree `~/.codex/worktrees/sqlserver-field-reads/tracemap`,
created from freshly fetched `origin/dev` = `7e96bca84aed04258577f1b02b6505186863bcfa` (the #918
merge commit itself, so the Phase 5 merge and its escaped-identifier and unavailable-input
repairs are ancestors). Push with an explicit `origin codex/sqlserver-field-reads` refspec only;
never to `dev`. The user checkout and every other worktree were not touched.
## Scope decisions
- Distinct sibling state `sqlserver-field-read/1`; Phase 2-5 states unchanged (Field reads stay
  `datarow-field-extension-unsupported` there).
- The four Phase 2-5 analyses share one `fieldReads` mode; the sibling picks the single bounded
  origin and partitions sites (`field-read-absent` when no Field read remains).
- The linked runtime matrix is extended by six cases per language (design.md).
## Oddities noticed
- On .NET 10 the `System.Data.DataSetExtensions` reference assembly is a facade; Roslyn binds
  `DataRowExtensions` to `System.Data.Common` (b03f5f7f11d50a3a). The .NET Framework identity is
  `System.Data.DataSetExtensions` (b77a5c561934e089). Both are admitted explicitly for Field only.
- First direct run: lambdas (`Select(r => r.Field<T>(...))`) and `row?.Field<T>` hit the unchanged
  method-wide `unsupported-control-flow` before any row rule, in both Phase 2 and Field mode, so the
  AsEnumerable cell uses `table.AsEnumerable().First().Field<string>("Name")` (still
  `ambiguous-row-ownership`); design clarified, no expectation weakened. Nullable and tuple type
  arguments needed `ExpandNullable` / `ExpandValueTuple` to stay inside the bounded vocabulary.
## Development validation (not acceptance)
- Step 6.3: 44 new direct tests; SQL lane with Razor and the new tests 818 passed.
