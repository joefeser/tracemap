# Tasks — Phase 2 (SqlDataAdapter.Fill → DataTable/DataRow)
- [x] 1. Verify base (Phase 1 `c28dbd3df` ancestor of fresh origin/dev `4f664daf5`), dedicated worktree/branch, read AGENTS/VALIDATION/SQL docs/specs/code/smokes; #864/#865/#870/#908 context only.
- [x] 2. Inspect signed SqlClient/System.Data reference metadata; author the accepted syntax matrix, contracts and independent expected matrix (design.md) before implementation.
- [x] 3. Shared command classification extracted from the reader walker without changing reader output; new `sqlServerDataTableState` walker on Fill facts; extractor version bumps.
- [x] 4. DataTable bindings, three-origin comparison route, Markdown, `sqlserver-datatable-result` CLI, rule catalog entry.
- [x] 5. Public synthetic C#/VB fixtures; tests and a new native smoke carrying the authored expectations (retained smokes unchanged).
- [x] 6. Focused tests, Python guards, docs.
- [ ] 7. Commit implementation; frozen full Debug `-warnaserror`.
- [ ] 8. Sequential committed-head retained smokes, new DataTable smoke, pinned OSS C#/VB smoke; privacy/whitespace guards.
- [ ] 9. Record results (docs-only commit), next-slice prompt, push branch. No PR.
