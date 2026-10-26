# Tasks — Phase 1 (table-backed DDL result shapes)

- [x] 1. Verify base (#912 merge ancestor of fresh origin/dev), dedicated worktree/branch, read AGENTS/VALIDATION/SQL docs/#912 spec, read #864/#865/#870/#908 as context.
- [x] 2. Author the accepted table and procedure grammar (design.md) before implementation; probe ScriptDom AST shapes.
- [x] 3. Opt-in table visitor in the shared DDL admission core; existing routes keep rejecting CREATE TABLE.
- [x] 4. Table-backed shape analysis, contracts, admission, comparison wrapper, Markdown and `sqlserver-ddl-table-result` CLI.
- [x] 5. Public synthetic table/procedure templates; independently authored expected matrix in tests and a new native smoke (retained smokes unchanged).
- [x] 6. Focused tests, Python guards, rule catalog and docs.
- [ ] 7. Commit implementation; frozen full Debug `-warnaserror`.
- [ ] 8. Sequential committed-head retained JSON/DDL parameter smokes, retained result smoke, new table smoke; privacy/whitespace guards.
- [ ] 9. Record results (docs-only commit), Phase 2 continuation prompt, push branch. No PR.
