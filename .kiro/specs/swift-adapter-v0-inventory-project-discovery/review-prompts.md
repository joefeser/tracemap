# Swift Adapter v0 Inventory And Project Discovery Review Prompts

Use these prompts to review the spec before implementation starts.

## Merge-Readiness Review

Review the TraceMap `swift-adapter-v0-inventory-project-discovery` Kiro spec on
branch `codex/spec-swift-inventory-project-discovery` for merge readiness.

This is a spec-only PR for GitHub issue #379. It should only add spec artifacts
under `.kiro/specs/swift-adapter-v0-inventory-project-discovery/`. It must not
implement Swift analyzer, runtime, scanner, reducer, site, or package-surface
code.

Please inspect:

- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/requirements.md`
- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/design.md`
- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/tasks.md`
- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/implementation-state.md`

Focus on:

1. Whether the spec preserves TraceMap's deterministic static evidence model:
   rule IDs, evidence tiers, file spans, commit SHA, extractor versions, and
   documented limitations.
2. Whether the spec covers `Package.swift`, `Package.resolved`,
   `*.xcodeproj`, `*.xcworkspace`, `Info.plist`, source roots, test roots,
   generated roots, vendor roots, and ecosystem metadata inventory.
3. Whether package/dependency metadata is bounded to inventory handoff for
   issue #382 and does not duplicate package dependency surface work.
4. Whether missing Swift/Xcode/CocoaPods/Carthage tooling, malformed metadata,
   dynamic manifests, unsupported project graphs, unsafe values, and partial
   metadata produce explicit gaps or reduced coverage.
5. Whether the report and public claim level avoid build, runtime, simulator,
   device, dependency compatibility, vulnerability, license, production usage,
   or impact conclusions.
6. Whether public artifact safety forbids raw source snippets, manifest
   snippets, plist values, raw URLs, hostnames, local absolute paths, raw
   remotes, credentials, secrets, and private labels.
7. Whether implementation tasks are reviewable and remain unchecked.

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher issue and say whether the spec is ready to merge after fixes.

## Implementation-Planning Review

Review the same spec as an implementation planner for the current TraceMap
codebase.

Focus on:

1. Whether the implementation slices are ordered safely for a small first Swift
   adapter PR.
2. Whether rule IDs and fact contracts are precise enough to implement without
   overclaiming.
3. Whether SwiftPM, Xcode, plist, CocoaPods, and Carthage parsing boundaries are
   deterministic and toolchain-safe.
4. Whether the fixture and validation plan proves useful inventory and at least
   one reduced/unsupported path.
5. Whether output artifacts can stay compatible with
   `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
6. Whether the spec gives issue #382 enough handoff metadata without performing
   package-surface analysis.

Return blockers and suggested edits only. Do not recommend builds, restores,
simulator/device execution, package registry calls, LLM calls, embeddings,
vector databases, or prompt-based classification.

## Skeptical Safety Review

Act as a skeptical reviewer for the Swift inventory spec.

Find likely bugs before implementation:

- Claims that imply runtime behavior, build success, target reachability,
  package compatibility, package vulnerability, package license status, or
  production use.
- Dependency inventory fields that accidentally become package-surface
  conclusions reserved for issue #382.
- Stable IDs that depend on timestamps, output paths, local absolute paths,
  raw remotes, or unordered filesystem traversal.
- Raw Swift snippets, manifest snippets, plist values, URLs, hostnames, local
  absolute paths, raw remotes, secrets, credentials, private names, or tool
  paths leaking into artifacts.
- Project/workspace parsing that treats missing Xcode tooling as clean full
  coverage.
- Exclusion rules that hide in-scope metadata files such as lockfiles.
- Report wording that says "impacted", "reachable", "builds", "runs",
  "compatible", "vulnerable", or equivalent certainty.
- Missing tests for malformed `Package.resolved`, dynamic `Package.swift`,
  malformed `project.pbxproj`, unsupported plist format, missing toolchain,
  generated/vendor roots, and deterministic ordering.

Return issues ordered by severity with suggested spec edits.

## Self-Review Checklist

- [ ] Does the spec clearly say this is deterministic static inventory, not
  runtime proof?
- [ ] Does every proposed fact require a rule ID, evidence tier, file path/line
  span, commit SHA, extractor ID, extractor version, and limitations?
- [ ] Are `Package.swift`, `Package.resolved`, `*.xcodeproj`, `*.xcworkspace`,
  `Info.plist`, source roots, test roots, generated roots, and vendor roots in
  scope?
- [ ] Is package/dependency work bounded to inventory handoff for issue #382?
- [ ] Are missing tooling, malformed metadata, unsupported formats, unsafe
  values, and partial metadata labeled as gaps or reduced coverage?
- [ ] Are raw snippets, URLs, hostnames, local paths, remotes, credentials,
  secrets, and private labels omitted or hashed?
- [ ] Are implementation tasks unchecked?
- [ ] Are validation commands exact and listed in `implementation-state.md`?
