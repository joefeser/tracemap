# Swift Adapter v0 Scaffold CLI and Output Contract Implementation State

Status: implemented

Issue: [#378 Swift adapter v0: scaffold CLI and output contract](https://github.com/joefeser/tracemap/issues/378)

Parent: [#377 Swift adapter v0 runway](https://github.com/joefeser/tracemap/issues/377)

Spec branch: `codex/spec-swift-scaffold-output-contract`

Implementation branch: `codex/swift-scaffold-output-contract`

Public claim level: dev-only implementation. The scaffold CLI can be described as implemented only after merge to the target branch. Do not claim Swift analysis is complete, runtime-proven, production-ready, or AI-powered.

## Current Scope

This implementation adds the Swift adapter v0 scaffold and output contract. It does not implement SwiftSyntax, SourceKit, SwiftPM semantic loading, Xcode semantic loading, UI, HTTP, storage, serializer, or runtime analysis.

The implementation slice should:

- creates a native SwiftPM adapter lane under `src/swift`;
- adds `tracemap-swift scan`, `--help`, and `--version`;
- produces the required TraceMap artifacts;
- preserves repo and commit SHA;
- emits rule-backed scaffold/inventory/gap facts only;
- writes compatible `facts.ndjson`, `index.sqlite`, `scan-manifest.json`, `report.md`, and `logs/analyzer.log`;
- labels scaffold output as reduced coverage;
- proves existing `.NET` readers can export, combine, and report the Swift scaffold index.

## Implementation Decisions

- Adapter host: native SwiftPM using Apple Swift 6.3.3 available locally.
- Adapter root: `src/swift`.
- Executable: `tracemap-swift`.
- Test command: `swift run --package-path src/swift tracemap-swift-smoke-tests`.
- Test runner decision: this local Swift install did not expose XCTest or the newer Testing module to SwiftPM, so the scaffold uses a deterministic smoke-test executable rather than `swift test`.
- SQLite writer decision: the Swift adapter creates the shared schema via the system `sqlite3` command and populates `scan_manifest` plus `facts`. Shared relationship/flow tables are created empty for downstream compatibility.
- Portability decision: the scaffold avoids Apple-only `CryptoKit`; it uses a small local SHA-256 implementation and platform-gated executable imports so `swift build --package-path src/swift` is not tied to Apple SDK modules.
- Output safety decision: the scaffold refuses `--out` values that are the filesystem root, the scan/git root, or an ancestor of the scan/git root before recursive output cleanup.
- Process safety decision: local subprocess calls drain stdout/stderr concurrently and use bounded timeouts so `git`, `swift --version`, and `sqlite3` cannot hang a scan indefinitely.
- Git metadata decision: detached HEAD reports branch as unavailable instead of the literal `HEAD`.
- Evidence schema decision: Swift scaffold evidence spans include their supporting rule ID in addition to the containing fact rule ID.
- Downstream reader compatibility lives in command smoke validation for this slice; deeper Swift fixtures can add integration scripts when more fact families exist.

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

Validation for this implementation PR should run or explicitly defer with evidence:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo <fixture-repo> --out <swift-scan-output>
swift run --package-path src/swift tracemap-swift scan --repo <fixture-repo> --out <swift-reduced-output> --max-file-byte-size <small-number>
dotnet run --project src/dotnet/TraceMap.Cli -- export --index <swift-scan-output>/index.sqlite --out <tmp>/swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <swift-scan-output>/index.sqlite --label swift --out <tmp>/swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/swift-combined.sqlite --out <tmp>/swift-report
./scripts/check-private-paths.sh
git diff --check
```

If shared language adapter behavior, combined indexes, report, export, reduce, path, reverse, impact, or release-review behavior changes, follow `docs/VALIDATION.md` and run or explicitly defer the relevant pinned smoke checks.

## Open Decisions for Implementation

- Whether the fixed scaffold default `--max-file-byte-size` of `1048576` bytes should remain adapter-local or be centralized with other adapters later.
- Whether future CI should install/use a Swift toolchain with XCTest or keep the smoke-test executable pattern.
- Whether deeper `project.pbxproj` and `contents.xcworkspacedata` parsing belongs in the inventory slice or the Xcode/project metadata slice.

## Follow-Up Items

- Add deeper SwiftSyntax extraction in a follow-up issue after this output contract is stable.
- Add validation docs for pinned public Swift smoke repos after extractor behavior exists.

## Validation Notes

Completed locally during implementation:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift --help
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo <tmp>/repo --out <tmp>/swift-scan
dotnet run --project src/dotnet/TraceMap.Cli -- export --index <tmp>/swift-scan/index.sqlite --out <tmp>/swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <tmp>/swift-scan/index.sqlite --label swift --out <tmp>/swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/swift-combined.sqlite --out <tmp>/swift-report
```

Completed before PR readiness:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

`dotnet test` passed with 684 tests. The Swift scaffold downstream smoke covered scan, export, combine, and report over a generated temporary SwiftPM fixture. Broader pinned public-language smokes from `docs/VALIDATION.md` were not run because this slice adds a new adapter scaffold and does not change existing .NET, TypeScript, Python, JVM, reducer, path, reverse, impact, or release-review behavior.

Completed after PR-loop review fixes:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
dotnet test src/dotnet/TraceMap.sln --filter Combine_infers_jvm_python_and_swift_languages
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
swift run --package-path src/swift tracemap-swift scan --repo <tmp>/repo --out <tmp>/swift-scan
dotnet run --project src/dotnet/TraceMap.Cli -- export --index <tmp>/swift-scan/index.sqlite --out <tmp>/swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <tmp>/swift-scan/index.sqlite --label swift --out <tmp>/swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/swift-combined.sqlite --out <tmp>/swift-report
sqlite3 <tmp>/swift-combined.sqlite "select label || ':' || language from index_sources order by label;"
```

The post-review downstream smoke reported `swift:swift` in `index_sources`, confirming Swift scaffold indexes retain Swift language identity after combine.

Additional Qodo review cleanup added bounded subprocess timeouts, detached HEAD branch normalization, and nested evidence rule IDs. The Swift smoke executable covers dangerous output paths, bundle filtering, and detached HEAD branch behavior.
