# Swift Adapter v0 Inventory And Project Discovery Implementation State

## Branch

- Spec branch: `codex/spec-swift-inventory-project-discovery`
- Intended implementation branch:
  `codex/implement-swift-inventory-project-discovery`
- Base: `origin/dev`
- Issue: `Refs #379`
- Parent issue: `Refs #377`
- Adjacent follow-up: `Refs #382`
- PR: pending

## Current Status

Status: `ready-for-implementation`

This is a spec-only packet for Swift adapter v0 repository inventory and
project/package discovery. No Swift analyzer/runtime code has been implemented
in this branch. Implementation tasks in `tasks.md` are intentionally unchecked.

## Source Material

- GitHub issue #379:
  https://github.com/joefeser/tracemap/issues/379
- Swift v0 runway issue #377:
  https://github.com/joefeser/tracemap/issues/377
- Package/dependency surface follow-up issue #382:
  https://github.com/joefeser/tracemap/issues/382
- Adapter contract: `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- Validation guide: `docs/VALIDATION.md`
- Repository instructions: `AGENTS.md`

## Public Claim Level

Allowed claim:

- Swift v0 inventory/project discovery is deterministic static evidence over
  checked-in files, with rule IDs, evidence tiers, repo/commit SHA provenance,
  extractor versions, coverage labels, and public-safe paths.

Forbidden claims:

- No runtime proof.
- No build success proof.
- No simulator or device proof.
- No package compatibility, license, vulnerability, freshness, or registry
  proof.
- No production usage or impact conclusion.
- No LLM, AI impact-analysis, embedding, vector database, or prompt-based
  classification claim.

## Scope Decisions

- Discover and inventory Swift repository/project metadata from checked-in
  files only.
- Include `Package.swift`, `Package.resolved`, `*.xcodeproj`,
  `*.xcworkspace`, `Info.plist`, Swift source roots, Swift test roots,
  generated-source candidates, vendor/external dependency candidates,
  `Podfile`, `Podfile.lock`, `Cartfile`, and `Cartfile.resolved`.
- Parse enough package/dependency metadata to feed issue #382, but do not
  implement dependency surface interpretation in this issue.
- Continue when Swift/Xcode/CocoaPods/Carthage tooling is missing; record
  diagnostics and reduced coverage.
- Use `SwiftInventoryFileBasedSucceeded` for the successful inventory label;
  every v0 Swift inventory label has a `syntax-or-structural` coverage ceiling
  and must not imply semantic/build/runtime proof.
- Treat `project.pbxproj` parsing as a gap by default unless implementation
  adds a documented narrow deterministic reader with tests.
- Treat binary plist parsing as a gap by default unless implementation adds a
  cross-platform deterministic parser with tests.
- Keep output artifact names aligned with the language adapter contract:
  `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
  `logs/analyzer.log`.
- Implement Swift v0 as an independent scanner package under `src/swift` with a
  `tracemap-swift` executable unless a later architecture spec explicitly
  changes adapter dispatch.
- Do not store source snippets, manifest snippets, raw URLs, hostnames, local
  absolute paths, raw remotes, credentials, secrets, or private labels by
  default.

## Files

- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/requirements.md`
- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/design.md`
- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/tasks.md`
- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/implementation-state.md`
- `.kiro/specs/swift-adapter-v0-inventory-project-discovery/review-prompts.md`

## Exact Validation Commands

Spec-only validation for this branch:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-inventory-project-discovery --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-inventory-project-discovery --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Future implementation validation before implementation PR review:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
swift test --package-path src/swift
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-reduced --out /tmp/tracemap-swift-metadata-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-unsupported --out /tmp/tracemap-swift-metadata-unsupported
swift run --package-path src/swift tracemap-swift scan --repo samples/no-swift --out /tmp/tracemap-no-swift
test -f /tmp/tracemap-swift-package-basic/scan-manifest.json
test -f /tmp/tracemap-swift-package-basic/facts.ndjson
test -f /tmp/tracemap-swift-package-basic/index.sqlite
test -f /tmp/tracemap-swift-package-basic/report.md
test -f /tmp/tracemap-swift-package-basic/logs/analyzer.log
test -f /tmp/tracemap-swift-metadata-reduced/scan-manifest.json
test -f /tmp/tracemap-swift-metadata-reduced/facts.ndjson
test -f /tmp/tracemap-swift-metadata-reduced/index.sqlite
test -f /tmp/tracemap-swift-metadata-reduced/report.md
test -f /tmp/tracemap-swift-metadata-reduced/logs/analyzer.log
test -f /tmp/tracemap-swift-metadata-unsupported/scan-manifest.json
test -f /tmp/tracemap-swift-metadata-unsupported/facts.ndjson
test -f /tmp/tracemap-swift-metadata-unsupported/index.sqlite
test -f /tmp/tracemap-swift-metadata-unsupported/report.md
test -f /tmp/tracemap-swift-metadata-unsupported/logs/analyzer.log
test -f /tmp/tracemap-no-swift/scan-manifest.json
test -f /tmp/tracemap-no-swift/facts.ndjson
test -f /tmp/tracemap-no-swift/index.sqlite
test -f /tmp/tracemap-no-swift/report.md
test -f /tmp/tracemap-no-swift/logs/analyzer.log
./scripts/check-private-paths.sh
git diff --check
```

## Kiro Review State

- Opus spec review: completed with reduced coverage because Kiro reported
  denied tool access. Medium+ findings patched:
  - Specified the Swift adapter host as an independent `src/swift` package with
    a `tracemap-swift` executable.
  - Replaced invalid future unified-CLI validation commands with Swift package
    commands.
  - Softened named fact-type requirements to require evidence contracts first
    and Swift-specific fact names only after catalog/test coverage.
  - Clarified Swift inventory coverage labels do not imply
    `Level1SemanticAnalysis`/`Succeeded`.
  - Clarified artifact-safety wording versus forbidden "safe" conclusions about
    scanned subjects.
  - Added remote redaction guidance and additional coverage/ordering/plist
    tests.
- Sonnet spec review: completed with reduced coverage because Kiro reported
  denied tool access. Medium+ findings patched:
  - Replaced the original full-coverage label with a file-based inventory label
    and added the `syntax-or-structural` coverage ceiling.
  - Made `project.pbxproj` parse-gap the v0 default unless a narrow
    deterministic reader is documented and tested.
  - Clarified implementation-state exists in this spec folder; the review
    reported it missing because wrapper tool access was denied.
  - Tightened scan-ID sorting, Package.swift parsing, binary plist scope,
    toolchain probe timeouts, no-Swift artifact behavior, dependency identity
    safe-value policy, output-path redaction, and log-safety tests.
- Sonnet re-review: completed with reduced coverage because Kiro reported
  denied tool access. Medium+ findings patched:
  - Clarified scan ID inputs include all included files, including Swift source
    files and metadata files, while excluded roots do not affect scan ID.
  - Split `Package.swift` manifest presence/hash evidence from token-scanned
    manifest value evidence by tier.
  - Made `project.pbxproj` parsing opt-in beyond the v0 default parse-gap task.
  - Renamed the coverage label to `SwiftInventoryFileBasedSucceeded`.
  - Replaced unbounded lockfile identity metadata with count plus bounded
    sample guidance and added additional schema, workspace, ecosystem safety,
    excluded scan-ID, and toolchain timeout tests.
- Re-review cycles used: 1.

## Validation State

- Opus spec review:
  `.tmp/kiro-reviews/swift-adapter-v0-inventory-project-discovery/2026-06-27T162611-437Z-spec-claude-opus-4.8.clean.md`
  completed with reduced coverage because Kiro reported denied tool access.
- Sonnet spec review:
  `.tmp/kiro-reviews/swift-adapter-v0-inventory-project-discovery/2026-06-27T163140-624Z-spec-claude-sonnet-4.6.clean.md`
  completed with reduced coverage because Kiro reported denied tool access.
- Sonnet re-review:
  `.tmp/kiro-reviews/swift-adapter-v0-inventory-project-discovery/2026-06-27T163423-019Z-spec-claude-sonnet-4.6.clean.md`
  completed with reduced coverage because Kiro reported denied tool access.
- `git diff --check`: passed.
- `git diff --cached --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Full product tests are not required for this spec-only branch unless review
  findings request them.

## Out Of Scope

- Swift analyzer/runtime implementation.
- SwiftSyntax symbol/call extraction.
- SourceKit or sourcekit-lsp enrichment.
- Xcode build graph execution.
- SwiftPM/CocoaPods/Carthage restore, resolve, install, update, or build.
- Package dependency surfaces from issue #382.
- HTTP, UI, storage, serializer, or runtime surfaces.
- Reducer impact conclusions for Swift.
- Site copy or public marketing changes.

## Safe/No-Overclaim Boundaries

- Use "inventory", "metadata", "declared", "discovered", "candidate",
  "gap", and "reduced coverage" wording.
- Do not use "runtime", "builds", "reachable", "production dependency",
  "compatible", "vulnerable", "safe", or "impacted" as conclusions for this
  slice.
- Every future emitted fact needs a rule ID, evidence tier, file path/line span
  where applicable, commit SHA, extractor ID, extractor version, and documented
  limitations.
- Every unavailable, unsupported, malformed, unsafe, or partial metadata path
  should produce a gap or reduced-coverage label instead of a guessed
  conclusion.

## Follow-Up Items

- Open implementation PR from
  `codex/implement-swift-inventory-project-discovery` after this spec merges.
- Update this state file with PR number, selected implementation slice, review
  outcomes, validation results, and any scope oddities during implementation.
