# Swift Adapter v0 Scaffold CLI and Output Contract Implementation State

Status: ready-for-implementation

Issue: [#378 Swift adapter v0: scaffold CLI and output contract](https://github.com/joefeser/tracemap/issues/378)

Parent: [#377 Swift adapter v0 runway](https://github.com/joefeser/tracemap/issues/377)

Spec branch: `codex/spec-swift-scaffold-output-contract`

Intended implementation branch: `codex/swift-scaffold-output-contract`

Public claim level: spec/planning only. Do not claim Swift scanning is implemented, complete, runtime-proven, production-ready, or AI-powered.

## Current Scope

This spec defines the future Swift adapter v0 scaffold and output contract. It does not implement Swift analyzer/runtime code.

The implementation slice should:

- create a future `src/swift` adapter lane unless implementation discovery records a better reason;
- add a scan CLI and deterministic help/version behavior;
- produce the required TraceMap artifacts;
- preserve repo and commit SHA;
- emit rule-backed scaffold/inventory/gap facts only;
- write compatible `facts.ndjson`, `index.sqlite`, `scan-manifest.json`, `report.md`, and `logs/analyzer.log`;
- label missing Swift toolchain, SwiftPM load failure, Xcode load failure, skipped files, parser/toolchain gaps, and metadata-only scans as reduced coverage;
- prove existing `.NET` readers can open and combine/report the Swift scaffold index.

## Source Material Paths

- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/ACCEPTANCE.md`
- `docs/VALIDATION.md`
- `docs/DECISIONS.md`
- `docs/ADAPTER_RUNWAY.md`
- `.kiro/specs/python-indexer/`
- `src/python/`
- `src/typescript/`
- `src/jvm/`
- `src/dotnet/TraceMap.Storage/`
- `src/dotnet/TraceMap.Cli/`
- `src/dotnet/TraceMap.Combine/`
- `src/dotnet/TraceMap.Reporting/`
- `rules/rule-catalog.yml`
- GitHub issue #378
- GitHub parent issue #377

## Safe Boundaries

- No LLM calls, embeddings, vector databases, or prompt-based classification.
- No simulator/device inspection, app launch, target tests, package restore, dependency network resolution, or runtime execution during scan.
- No raw snippets by default.
- No full Swift semantic coverage claim unless a future implementation proves deterministic semantic evidence and has no known gaps.
- No runtime proof for UI navigation, storyboard/xib wiring, SwiftUI navigation, storage access, protocol dispatch, Objective-C selectors, responder chain behavior, property wrapper side effects, macros, result builders, dependency injection, Combine, async scheduling, app lifecycle, permissions, deployment, auth, feature flags, branch feasibility, production use, or impact.
- No public "impacted", "safe", "complete", "runtime", "AI impact analysis", or "perfect Swift analysis" wording without rule-backed reducer/report evidence.

## Exact Spec Validation Commands

Run for this spec PR:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-scaffold-cli-output-contract --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-scaffold-cli-output-contract --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

## Exact Future Implementation Validation Commands

The future implementation PR should run or explicitly defer with evidence:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
<swift-adapter-build-command>
<swift-adapter-test-command>
<swift-scan-minimal-fixture-command>
<swift-scan-reduced-fixture-command>
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <swift-scan-output>/index.sqlite --out <tmp>/swift-report
dotnet run --project src/dotnet/TraceMap.Cli -- export --index <swift-scan-output>/index.sqlite --out <tmp>/swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <swift-scan-output>/index.sqlite --label swift --out <tmp>/swift-combined.sqlite
./scripts/check-private-paths.sh
git diff --check
```

If shared language adapter behavior, combined indexes, report, export, reduce, path, reverse, impact, or release-review behavior changes, follow `docs/VALIDATION.md` and run or explicitly defer the relevant pinned smoke checks.

## Open Decisions for Implementation

- Adapter host/tooling choice for `src/swift`.
- Exact executable name if not `tracemap-swift`.
- Whether the fixed scaffold default `--max-file-byte-size` of `1048576` bytes should remain adapter-local or be centralized with other adapters later.
- Exact v0 scaffold rule IDs and fact types added to `rules/rule-catalog.yml`.
- Exact minimal Swift fixtures and reduced-coverage fixtures.
- Whether `project.pbxproj` and `contents.xcworkspacedata` parsing is included in scaffold inventory or deferred to a later Swift/Xcode slice; bundle presence inventory itself is in scope.

## Follow-Up Items

- Implement the scaffold in a separate PR.
- Add deeper SwiftSyntax extraction in a follow-up issue after this output contract is stable.
- Add validation docs for pinned public Swift smoke repos after extractor behavior exists.
