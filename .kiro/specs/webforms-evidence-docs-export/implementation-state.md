# Implementation state

- Branch: `codex/webforms-review-workflow-cleanup`
- Base: `origin/dev` at `06b4d7b3`
- Tracking: #730, part of #651
- Scope: deterministic public documentation projection of existing Web Forms modernization packet evidence; no BRD, prompt, LLM, embedding, vector database, business-intent inference, modernization planner, or code generator.
- Implemented: `tracemap docs-export` accepts repeatable `--webforms-packet` inputs that must match the supplied index by scan and commit identity. Compatible packets add a closed `webforms-modernization` family with packet overview, project, requested-surface coverage, surface, event-chain, downstream-boundary, identity/state, batch/data-movement, and structural-slice chunks. Packet gaps remain first-class gap chunks with retained span citations when available.
- Determinism and safety: equivalent reordered packet collections produce identical corpus hashes and JSONL bytes. Existing generated-file hashing, collision protection, unsafe-value validation, and claim-level behavior remain authoritative. Ordinary exports without a packet retain their prior default family set.
- Boundaries: the exporter emits only deterministic evidence documentation. It contains no BRD format, prompt, LLM call, embedding, vector database, inferred business intent, target architecture recommendation, migration plan, or generated application/database code.
- Validation: focused docs-export/Web Forms suite passed 39/39; full solution passed 1,805/1,805; private-path guard and `git diff --check` passed. One pre-existing nullable warning remains in `PropertyMappingTests.cs`.
- Delivery: commit `e5445f1e` opened PR #731 against `dev` and closes #730.
- Current: implementation and local validation complete; exact-head review and CI remain.
- Exact-head P1/P2 remediation: docs export now applies the explorer's recursive duplicate-property admission check, rejects arbitrary absolute Unix paths in packet-controlled prose, rejects downstream-boundary claims without retained evidence for the terminal, accepts producer-valid repeated and explicitly truncated surface selections, preserves evidence/path/traversal limitations in event-chain and boundary chunks, and includes selection aliases in packet identity.
- Remediation validation: focused Web Forms/docs/static-explorer suite passed 105/105; full solution passed 1,811/1,811; focused code-path review-set smoke passed; `git diff --check` passed. The pre-existing nullable warning in `PropertyMappingTests.cs` remains unchanged.
