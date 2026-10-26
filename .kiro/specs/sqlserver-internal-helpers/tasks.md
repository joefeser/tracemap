# Tasks — Phase 5 (same-compilation internal helpers called from other documents)
- [x] 1. Verify base (#917 merged; fresh origin/dev `7169ee6e4` is the ancestor), dedicated worktree/branch, read AGENTS/VALIDATION/SQL docs/specs/code/smokes and the linked harness.
- [x] 2. Author the accepted syntax matrix, decisions, contracts and independent native matrix (design.md) before implementation.
- [x] 3. Compilation scope registry in both adapters (documents, exclusion, generated/linked/path classes); shared Phase 4 engine with an internal mode; sibling `sqlServerInternalHelperResultState`; extractor version bumps; direct Roslyn tests (cross-document callers, scope gaps, bounds, Phase 4 unchanged).
- [x] 4. Internal bindings, three-origin comparison route, Markdown, `sqlserver-internal-helper-result` CLI, rule catalog entry; substitution tests.
- [x] 5. Public synthetic multi-document C#/VB fixtures wired into the normal test-project graph; native tests and a new native smoke with the authored expectations (retained smokes unchanged); Python guards; docs.
- [x] 6. Verify fixtures restore/build/scan through the normal test project with no pre-existing fixture `bin/`/`obj/`; focused lanes.
- [ ] 7. Commit implementation; frozen full Debug `-warnaserror`.
- [ ] 8. Sequential committed-head retained smokes, new smoke, Python guards, pinned OSS C#/VB smoke, unchanged linked live harness on the revalidated disposable context; privacy/whitespace guards.
- [ ] 9. Record results (docs-only commit), next-slice prompt, push branch. No PR.
