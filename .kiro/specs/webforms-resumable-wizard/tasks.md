# Implementation tasks

- [x] Shared bounded path/form selection validation and normalization (10 focused cases; coordinator-level filesystem race/resume tests remain in the full matrix).
- [x] Versioned root/project state, per-file atomic persistence, exclusive locking and generator/bounded-input hashes (crash-between-files fails closed; explicit recovery is part of repair below).
- [ ] Input target classification, web-root selection and toolchain/build validation.
- [ ] Resumable terminal prompts with --continue, explicit add-project and subset pause.
- [ ] Publication/DLL inventory and existing native configuration/proof adapter.
- [ ] Explicit repair previews, confirmation and per-project invalidation.
- [ ] Scan/combine/report execution with immutable output roots and revalidation.
- [ ] Full regression matrix, CLI replay, documentation and rule limitations.
- [ ] Scoped stacked PR and exact-head validation, without modifying #798.
