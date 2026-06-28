# Swift Adapter v0 Scaffold CLI and Output Contract Review Prompts

Use these prompts to review the spec before implementation starts.

## Opus Product and Evidence Review

```text
You are reviewing a Kiro spec for TraceMap issue #378, "Swift adapter v0: scaffold CLI and output contract."

TraceMap context:
- TraceMap is a deterministic repository indexer and contract-change impact reducer.
- Core principles: no conclusion without evidence, no evidence without a rule ID, no rule without documented limitations, no scan without repo and commit SHA, and partial analysis must be labeled partial.
- The Swift adapter must not add LLM calls, embeddings, vector databases, runtime app execution, simulator/device inspection, or prompt-based classification.
- This issue is spec-only for a future scaffold/output-contract implementation. It must not implement Swift analyzer/runtime code.
- Public claims must stay bounded to deterministic static evidence, rule IDs, evidence tiers, coverage labels, limitations, and generated artifacts.

Files to review:
- .kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/requirements.md
- .kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/design.md
- .kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/tasks.md
- .kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/implementation-state.md

Please review for:
- Any wording that overclaims Swift support, runtime behavior, app reachability, complete semantic coverage, production safety, or AI impact analysis.
- Whether the future scaffold scope is small enough for a first implementation PR.
- Whether reduced-coverage behavior is explicit when Swift toolchain, SwiftPM, or Xcode project load is missing/failing.
- Whether required artifacts match the TraceMap language adapter contract.
- Whether repo/commit SHA, extractor versions, rule IDs, evidence tiers, file paths, line spans, coverage labels, and limitations are required everywhere they should be.
- Whether the public claim level and safe/no-overclaim boundaries are strong enough.
- Whether out-of-scope items prevent accidental Swift analyzer/runtime work in this issue.

Return:
- Blocking issues.
- Important non-blocking issues.
- Wording changes.
- Scope cuts.
- Missing implementation decisions.
```

## Sonnet Implementation Review

```text
You are reviewing the TraceMap Swift adapter v0 scaffold/output-contract spec for implementation feasibility.

Context:
- The future implementation should live under src/swift unless discovery records a better reason.
- The first implementation should scaffold CLI/project layout and output writers, not implement broad Swift analysis.
- It must emit TraceMap-compatible scan-manifest.json, facts.ndjson, index.sqlite, report.md, and logs/analyzer.log.
- Existing .NET combine/report/export/reduce readers should be able to open the scaffold index.
- Missing Swift toolchain, failed SwiftPM load, and failed Xcode project load must produce reduced coverage or fail before success artifacts, never clean coverage.
- Raw snippets and unsafe raw values must not be stored by default.

Files to review:
- .kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/requirements.md
- .kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/design.md
- .kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/tasks.md

Please review for:
- CLI ambiguity.
- Adapter host/tooling ambiguity that should be resolved before coding.
- Output contract ambiguity.
- SQLite compatibility risks.
- Manifest coverage/build-status ambiguity.
- Deterministic scanId/factId ambiguity.
- Missing tests for failure paths, reduced coverage, and downstream .NET reader compatibility.
- Missing tests for manifest timestamp carve-outs, scanId invariance across output paths/checkouts, NDJSON byte stability, default excludes, and report/log privacy leakage.
- Missing tests for non-file-backed span convention, fact ID stability, `tracemap export`, and combine/report field preservation.
- Tasks that are too broad for the first implementation PR.
- Places where docs or rule catalog updates are missing.
- Any accidental requirement to use Xcode or run a successful build for basic scan usefulness.

Return:
- Implementation blockers.
- Proposed task reordering.
- Specific spec edits.
- Test gaps.
- Questions that must be answered before coding.
```

## Safety Bug Hunt

```text
Act as a skeptical reviewer for the Swift adapter v0 scaffold/output-contract spec.

Find likely bugs before implementation:
- Clean coverage emitted when Swift toolchain is missing.
- Clean coverage emitted when SwiftPM or Xcode project load fails.
- Scans that write artifacts without a concrete commit SHA.
- Non-deterministic scan IDs or fact IDs caused by timestamps, UUIDs, output paths, or local absolute paths.
- Facts without rule IDs, evidence tiers, extractor versions, file spans, or limitations.
- Raw source snippets, raw URLs, config values, entitlements/provisioning details, local absolute paths, remotes, secrets, or private labels leaking into facts/reports/logs.
- SQLite schema drift from existing TraceMap readers.
- Scaffold facts that imply runtime UI navigation, storage access, network reachability, protocol dispatch, Objective-C selector resolution, or app behavior.
- Tasks that accidentally implement Swift analyzer depth instead of only scaffold/output contract.
- Missing tests for reduced coverage and downstream reader compatibility.

Return issues ordered by severity with suggested spec edits.
```

## Self-Review Checklist

- [ ] Is `implementation-state.md` status `ready-for-implementation`, not implemented?
- [ ] Are all implementation tasks unchecked?
- [ ] Does the spec link issue #378 and parent #377?
- [ ] Does it name intended branch and future implementation branch?
- [ ] Does it keep files and changes inside the spec folder?
- [ ] Does it require repo and commit SHA?
- [ ] Does it require `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`?
- [ ] Does every fact require rule ID, evidence tier, path/span where applicable, commit SHA, and extractor version?
- [ ] Does it define reduced coverage for missing Swift toolchain, SwiftPM load failure, and Xcode load failure?
- [ ] Does it avoid promising perfect Swift analysis?
- [ ] Does it avoid runtime proof and AI/LLM/vector claims?
- [ ] Does it list source material paths?
- [ ] Does it list exact validation commands?
- [ ] Are out-of-scope items explicit enough to prevent implementing Swift analyzer/runtime code in issue #378?
