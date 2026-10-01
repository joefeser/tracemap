# Implementation tasks

- [x] Shared bounded path/form selection validation and normalization (10 focused cases; coordinator-level filesystem race/resume tests remain in the full matrix).
- [x] Versioned root/project state, per-file atomic persistence, exclusive locking and generator/bounded-input hashes (crash-between-files fails closed; explicit recovery is part of repair below).
- [ ] Input target classification, web-root selection and toolchain/build validation.
  - [x] Bounded static folder/project/solution classification and explicit solution-root membership; tool-family candidates, no build/evaluation proof.
  - [x] Toolchain executable hash/version validation, explicit consent and build-result handling (real process version probe covered; actual fixture-build/Windows acceptance remains in full matrix).
- [ ] Resumable terminal prompts with --continue, explicit add-project and subset pause.
  - [x] Shared forms transition generates missing/blank selection, pauses, resumes after restart and revalidates retained selected paths.
  - [x] Terminal setup prompt/command adapter and explicit add-project flow (later stages remain below).
- [ ] Publication/DLL inventory and existing native configuration/proof adapter.
  - [x] Publication-root/bin validation, explicit managed primary/dependency selection, bounded input snapshots and resume change detection.
  - [x] Native configuration generation, preflight and owned hash-verified staging for external dependencies.
  - [x] Explicit source-commit attestation and native preparation/start execution integration.
- [ ] Explicit repair previews, confirmation and per-project invalidation.
- [ ] Scan/combine/report execution with immutable output roots and revalidation.
  - [x] Pinned native start/resume, unique attempts and retained report-status verification; real local fixture execution.
  - [ ] Complete fresh/repair/multi-project acceptance matrix and compiled-chain fixture report assertions.
- [ ] Full regression matrix, CLI replay, documentation and rule limitations.
- [ ] Scoped stacked PR and exact-head validation, without modifying #798.
