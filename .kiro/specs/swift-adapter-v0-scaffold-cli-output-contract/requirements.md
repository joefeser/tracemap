# Swift Adapter v0 Scaffold CLI and Output Contract Requirements

Issue: [#378 Swift adapter v0: scaffold CLI and output contract](https://github.com/joefeser/tracemap/issues/378)

Parent: [#377 Swift adapter v0 runway](https://github.com/joefeser/tracemap/issues/377)

Intended branch: `codex/spec-swift-scaffold-output-contract`

Public claim level: planning/specification only. This spec may say TraceMap is planning a deterministic Swift static evidence adapter. It must not say Swift analysis is implemented, production-ready, runtime-proven, complete, AI-powered, or able to prove app behavior.

## Introduction

TraceMap needs a first Swift adapter implementation slice that scaffolds a Swift lane and locks the scan output contract before deeper Swift analysis begins. This slice should create the future CLI/project layout, deterministic artifact writers, manifest/report behavior, and reduced-coverage conventions needed by later SwiftSyntax, SwiftPM, Xcode, UI, HTTP, storage, and dependency extractors.

This is not a Swift analyzer/runtime implementation spec. It must not add runtime execution, simulator/device inspection, app startup, Xcode build requirements for basic output, LLM calls, embeddings, vector databases, or prompt-based classification to the scanner or reducer. The only implementation described here is a future implementation plan.

The future Swift adapter must follow TraceMap's existing language adapter contract:

- no conclusion without evidence;
- no evidence without a rule ID;
- no rule without documented limitations;
- no scan without repo and commit SHA;
- failed or unavailable toolchain/project load is not clean coverage;
- partial analysis is useful, but must be labeled partial;
- deterministic, testable extractors are preferred.

## Source Material

The implementation plan should use these repository sources as the local source of truth:

- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/ACCEPTANCE.md`
- `docs/VALIDATION.md`
- `docs/DECISIONS.md`
- `docs/ADAPTER_RUNWAY.md`
- `src/python/` and `.kiro/specs/python-indexer/` for sibling adapter packaging/output-contract precedent
- `src/typescript/` for Node-based standalone adapter precedent
- `src/jvm/` for non-.NET standalone adapter precedent
- `src/dotnet/TraceMap.Storage/` for shared SQLite schema behavior
- `src/dotnet/TraceMap.Cli/`, `src/dotnet/TraceMap.Combine/`, and `src/dotnet/TraceMap.Reporting/` for downstream index consumers
- `rules/rule-catalog.yml` for rule ID and limitation requirements
- GitHub issue #378 and parent issue #377

## Requirements

### Requirement 1: Swift Adapter Scaffold

**User Story:** As a TraceMap maintainer, I want a Swift adapter lane with a CLI and project structure so future Swift extraction work can be added without changing the output contract.

#### Acceptance Criteria

1. WHEN the implementation slice begins THEN it SHALL create a sibling adapter lane under `src/swift` unless implementation discovery records a documented reason for a different adapter root.
2. WHEN the Swift adapter is scaffolded THEN it SHALL include a package/build layout, CLI entry point, tests, README or local usage notes, and deterministic version reporting.
3. WHEN the user runs the future CLI help command THEN it SHALL print deterministic usage for `scan`, `--repo`, `--out`, `--project`, `--include`, `--exclude`, `--max-file-byte-size`, and `--version`.
4. WHEN `--max-file-byte-size` is omitted THEN the scanner SHALL use a documented fixed default of `1048576` bytes so inventory, skipped-file gaps, and `scanId` inputs are reproducible across machines.
5. WHEN the scanner command is not yet able to extract Swift facts beyond scaffold-level inventory/gaps THEN it SHALL still write only evidence-backed scaffold facts and explicit gaps; it SHALL NOT invent analysis conclusions.
6. WHEN the implementation chooses Swift-native tooling, Node, .NET, or another runtime for the adapter host THEN the choice SHALL be documented with local install/build/test commands and deterministic-output implications.
7. WHEN future SwiftSyntax or SourceKit enrichment is deferred THEN the scaffold SHALL keep interfaces narrow enough that later extractors can be added without changing required artifact names.

### Requirement 2: Scan CLI Contract

**User Story:** As a reviewer, I want to run a Swift scan command that behaves like other TraceMap adapters and produces the same artifact set.

#### Acceptance Criteria

1. WHEN the user runs the future Swift scanner as `tracemap-swift scan --repo <path> --out <path>` or the selected equivalent documented command THEN it SHALL require a valid repository path and output path.
2. WHEN the repo path does not exist THEN the scanner SHALL exit non-zero and SHALL NOT write a partial success manifest.
3. WHEN the repo is inside a Git checkout THEN the scanner SHALL resolve repo name, remote URL when available, branch when available, and a concrete commit SHA.
4. WHEN Git commit SHA cannot be resolved THEN the scanner SHALL fail before writing a successful scan artifact set.
5. WHEN the output path already exists THEN the scanner SHALL rebuild only the requested output contents and SHALL NOT delete files outside that path.
6. WHEN the scan succeeds, even with reduced coverage, THEN it SHALL write:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

7. WHEN the scanner is invoked repeatedly against the same repo, commit, scan options, and file inventory THEN deterministic fields, fact IDs, SQLite rows, and report ordering SHALL be stable.
8. WHEN the user requests raw snippets through no option or default behavior THEN raw snippets SHALL NOT be stored. Any future raw snippet option must be explicit, documented, and excluded from public-safe output by default.

### Requirement 3: Manifest and Coverage Contract

**User Story:** As a downstream reducer/report user, I want Swift manifests to be honest about commit identity, extractor version, and reduced coverage.

#### Acceptance Criteria

1. WHEN a Swift scan writes `scan-manifest.json` THEN it SHALL include `scanId`, repo identity, branch when available, commit SHA, scanner version, extractor versions, scan timestamp, analysis level, build status, project/workspace identifiers when available, known gaps, and scan-root metadata.
2. WHEN computing `scanId` THEN the adapter SHALL derive it from stable repo identity, commit SHA, normalized scan options, and sorted selected file inventory or another documented stable signature; it SHALL NOT use timestamps, UUIDs, output paths, process IDs, or absolute local paths.
3. WHEN writing scan timestamp fields such as manifest `scannedAt` or SQLite `scanned_at` THEN the timestamp SHALL be documented as the only expected non-stable local scan field; it SHALL be excluded from `scanId`, fact ID, `facts.ndjson`, and byte-stability assertions.
4. WHEN only scaffold/inventory/syntax/project metadata is available THEN the manifest SHALL use reduced or syntax coverage, not full semantic coverage.
5. WHEN the Swift toolchain is missing THEN the scan SHALL either produce a reduced artifact set from file/project metadata with `AnalysisGap` facts, or fail before writing success artifacts if no useful evidence can be produced. It SHALL NOT mark coverage clean.
6. WHEN SwiftPM package loading fails THEN the scanner SHALL continue with syntax/project-file fallback where possible, emit `AnalysisGap` facts, and mark coverage reduced.
7. WHEN Xcode workspace/project loading fails or Xcode is unavailable THEN the scanner SHALL continue with supported SwiftPM/file/config fallback where possible, emit `AnalysisGap` facts, and mark coverage reduced.
8. WHEN any selected files are skipped because of size, unsupported encoding, unsupported syntax, inaccessible paths, or parser/toolchain errors THEN known gaps SHALL include the reason and coverage SHALL be reduced.
9. WHEN no supported Swift files or project metadata are found THEN the scan SHALL produce a valid empty/reduced inventory result only if commit SHA is known and the report makes the absence of supported inputs explicit.

Conservative v0 coverage mapping:

| Condition | `analysisLevel` | `buildStatus` |
| --- | --- | --- |
| Future full deterministic semantic selected scope with no known gaps and concrete commit SHA | `Level1SemanticAnalysis` | `Succeeded` |
| SwiftSyntax/project/package/config evidence with useful facts but no full semantic proof | `Level1SemanticAnalysisReduced` | `FailedOrPartial` |
| SwiftPM or Xcode project load fails but syntax/file/config fallback emits useful facts | `Level1SemanticAnalysisReduced` or `Level3SyntaxAnalysis` | `FailedOrPartial` |
| Swift toolchain missing and only textual/project metadata inventory is available | `Level3SyntaxAnalysis` | `NotRun` or `FailedOrPartial` |
| No commit SHA or no credible evidence source | no successful artifact set | non-zero exit |

`Level1SemanticAnalysis` / `Succeeded` is listed only as a future-slice reserved state. The issue #378 scaffold implementation SHALL always report `Level1SemanticAnalysisReduced` or lower because scaffold output cannot prove full Swift semantic coverage. `Level1SemanticAnalysisReduced` SHALL always pair with `FailedOrPartial`; `Succeeded` is reserved for `Level1SemanticAnalysis` only.

### Requirement 4: Fact and Evidence Contract

**User Story:** As a reducer maintainer, I want every Swift fact to preserve TraceMap evidence fields so findings remain rule-backed and auditable.

#### Acceptance Criteria

1. WHEN the Swift scanner emits any fact THEN the fact SHALL include deterministic fact ID, scan ID, repo, commit SHA, fact type, rule ID, evidence tier, file path, line span when file-backed, extractor ID, extractor version, and sorted safe properties.
2. WHEN a fact has no file-backed span, such as a repo-level metadata gap or toolchain-unavailable gap, THEN it SHALL use `scan-manifest.json` as the file path with `startLine = 1` and `endLine = 1`, plus safe metadata instead of fake source locations.
3. WHEN v0 emits scaffold-level facts THEN they SHALL be limited to inventory, project/package metadata, manifest/report support, and explicit `AnalysisGap` facts unless the implementation slice intentionally adds a documented extractor.
4. WHEN the adapter emits facts intended for existing reducer/report behavior THEN it SHALL reuse shared fact types and property keys from `docs/LANGUAGE_ADAPTER_CONTRACT.md` where possible.
5. WHEN a new Swift-specific rule ID is introduced THEN it SHALL be added to `rules/rule-catalog.yml` with fact type, evidence tier expectations, required properties, limitations, and known false positives/false negatives before the scanner emits it.
6. WHEN evidence depends on syntax only THEN it SHALL use `Tier3SyntaxOrTextual`.
7. WHEN evidence depends on known project/package structure, file roles, or framework metadata without compiler-resolved symbols THEN it SHALL use `Tier2Structural`.
8. WHEN evidence depends on SourceKit, SwiftPM, or compiler-resolved symbols in a future slice THEN it MAY use `Tier1Semantic` only when the implementation proves the symbol resolution source and records toolchain gaps.
9. WHEN evidence is missing, unsupported, dynamic, ambiguous, or toolchain-blocked THEN the scanner SHALL emit `AnalysisGap` with `Tier4Unknown` and safe gap metadata.
10. WHEN Swift facts include source text, URLs, config values, SQL, endpoints, bundle identifiers, entitlements, provisioning details, secrets, local absolute paths, raw remotes, or private labels THEN unsafe raw values SHALL be omitted or hashed according to the shared safe metadata conventions.

### Requirement 5: SQLite, NDJSON, and Report Compatibility

**User Story:** As a cross-language TraceMap user, I want Swift indexes to be readable by existing combine, report, export, and reducer commands.

#### Acceptance Criteria

1. WHEN `facts.ndjson` is written THEN it SHALL contain one deterministic JSON fact object per line and SHALL be byte-stable for identical inputs except for explicitly documented non-stable local-only fields.
2. WHEN `index.sqlite` is written THEN it SHALL include at minimum the shared `scan_manifest` and `facts` tables compatible with existing `.NET` readers.
3. WHEN the Swift adapter emits symbols or relationship facts THEN `index.sqlite` SHALL populate shared symbol, occurrence, fact-symbol, call-edge, object-creation, argument-flow, alias, or relationship tables using the shared schema rather than a divergent Swift-only schema.
4. WHEN the adapter has no facts for an optional table THEN it SHALL leave the table empty or omit only where the shared schema allows; downstream compatibility SHALL be verified by running `tracemap report`, `tracemap export --format json`, and `tracemap combine` against both minimal and reduced-coverage Swift fixture outputs without schema errors or non-zero exits.
5. WHEN `report.md` is written THEN it SHALL include scan metadata, coverage, output artifact list, fact counts by type/rule/tier, known gaps, and Swift limitations without raw snippets or unsafe values.
6. WHEN the scan produces reduced coverage THEN `report.md` SHALL visibly label reduced coverage and SHALL NOT present absence of evidence as clean absence.
7. WHEN the Swift index is consumed by existing `tracemap combine`, `tracemap report`, `tracemap export`, `tracemap reduce`, or future path/reverse commands THEN those commands SHALL preserve Swift source labels, commit SHA, analysis level, build status, extractor versions, rule IDs, evidence tiers, gaps, and limitations.

### Requirement 6: Project and File Discovery Boundaries

**User Story:** As a Swift maintainer, I want v0 scaffold behavior to recognize common Swift project metadata while avoiding claims about builds or runtime app behavior.

#### Acceptance Criteria

1. WHEN SwiftPM files such as `Package.swift` and `Package.resolved` are present THEN the scanner SHALL inventory them and may emit structural package/dependency facts only from statically visible metadata.
2. WHEN Xcode project/workspace bundles such as `.xcodeproj` or `.xcworkspace` are present THEN the scanner SHALL inventory the bundle presence as structural metadata using the bundle directory path with `startLine = 1` and `endLine = 1`, and may parse contained deterministic metadata files such as `project.pbxproj` or `contents.xcworkspacedata`; it SHALL NOT treat the bundle path itself as a normal source file.
3. WHEN CocoaPods files such as `Podfile` or `Podfile.lock` are present THEN the scanner SHALL inventory them and may emit dependency facts only from safe literal metadata.
4. WHEN Carthage files such as `Cartfile` or `Cartfile.resolved` are present THEN the scanner SHALL inventory them and may emit dependency facts only from safe literal metadata.
5. WHEN app configuration files such as `Info.plist`, entitlements, asset catalogs, storyboards, xibs, CoreData model files, or privacy manifests are discovered THEN v0 scaffold MAY inventory them as structural evidence, but it SHALL NOT claim runtime UI navigation, permission use, storage behavior, or deployed app state.
6. WHEN files are under `.git`, build outputs, DerivedData, `.build`, Pods build outputs, Carthage checkouts/build outputs, SwiftPM checkouts, dependency caches, generated TraceMap output, or hidden tool caches THEN they SHALL be excluded by default unless explicitly included. The implementation SHALL normalize repository-relative paths, split them into path segments, and compare individual segment names or documented segment sequences instead of using slash-delimited substring containment.
7. WHEN project metadata uses executable Swift code, scripts, environment variables, generated files, plugins, or network/dependency resolution THEN the scanner SHALL treat those pieces as dynamic boundaries unless a deterministic parser extracts safe literal metadata.

### Requirement 7: Reduced-Coverage and Safe/No-Overclaim Boundaries

**User Story:** As a public user, I want Swift v0 output to be useful but never oversold as runtime proof.

#### Acceptance Criteria

1. WHEN SwiftSyntax, SourceKit, SwiftPM, Xcode, CocoaPods, Carthage, storyboard, plist, or CoreData parsing is unavailable or fails THEN the scanner SHALL emit reduced coverage/gap evidence where useful evidence remains.
2. WHEN protocol dispatch, dynamic dispatch, Objective-C selectors, responder chain behavior, dependency injection, property wrappers, result builders, macros, reflection, `@objc` bridging, storyboards/xibs, SwiftUI runtime navigation, async scheduling, Combine pipelines, notifications, app lifecycle, feature flags, build configurations, or environment values are relevant but not proven THEN the scanner SHALL emit gaps or limitations instead of inferred facts.
3. WHEN a future extractor sees HTTP, storage, UI, package, config, or serializer-like evidence THEN it SHALL treat it as static evidence only and SHALL NOT claim network reachability, database existence, permissions, runtime execution, auth behavior, payload compatibility, app-store deployment, or production use.
4. WHEN public copy or reports describe Swift support THEN they SHALL use bounded language: deterministic static evidence, rule IDs, evidence tiers, coverage labels, limitations, and generated artifacts.
5. WHEN public copy or reports describe unsupported areas THEN they SHALL avoid "impacted", "safe", "complete", "runtime", "AI impact analysis", "LLM analysis", or "perfect Swift analysis" claims unless a reducer/report rule and evidence tier support the exact statement.

### Requirement 8: Tests, Fixtures, and Validation Commands

**User Story:** As a reviewer, I want the scaffold implementation to prove output compatibility and reduced-coverage behavior before deeper Swift extraction is attempted.

#### Acceptance Criteria

1. WHEN the scaffold implementation is complete THEN tests SHALL prove CLI help/version behavior, missing repo failure, missing Git SHA failure, deterministic scan ID/fact ID behavior, required output artifacts, manifest coverage labels, analyzer log creation, NDJSON validity, SQLite reader compatibility, and reduced coverage for missing toolchain or project-load failure paths.
2. WHEN fixtures are added THEN they SHALL include at least one minimal SwiftPM-style repo, one broken or toolchain-unavailable reduced-coverage path, and one metadata-only or unsupported-project path.
3. WHEN the scaffold creates rule IDs THEN tests SHALL fail if emitted rule IDs are absent from `rules/rule-catalog.yml`.
4. WHEN the scaffold writes `index.sqlite` THEN existing `.NET` reader tests or smoke commands SHALL verify that downstream commands can open the index without schema errors.
5. WHEN determinism tests run THEN they SHALL prove `scanId` is invariant for the same repo, commit, options, and inventory across different `--out` paths and different local absolute checkout paths.
6. WHEN byte-stability tests run THEN they SHALL prove repeated runs produce byte-identical `facts.ndjson` and fact rows for identical inputs.
7. WHEN manifest determinism tests run THEN they SHALL prove repeated runs differ only in documented timestamp fields such as `scannedAt`/`scanned_at`.
8. WHEN default-exclude tests run THEN they SHALL prove `.git`, `.build`, DerivedData, Pods/Carthage build outputs, dependency caches, hidden tool caches, and generated TraceMap outputs are excluded by default.
9. WHEN privacy/safety tests run THEN they SHALL assert `facts.ndjson`, SQLite `properties_json`, `report.md`, and `logs/analyzer.log` do not contain local absolute paths, raw remotes, source snippets, or unsafe raw values.
10. WHEN fact ID stability tests run THEN they SHALL prove fact IDs are stable across repeated scans of the same commit when only `--out` changes.
11. WHEN downstream combine/report tests run THEN they SHALL assert commit SHA, analysis level, build status, rule IDs, evidence tiers, and source labels survive combine and report/export round-trips.
12. WHEN default-exclude fixtures are added THEN at least one fixture SHALL include `.build` or `DerivedData` with a `.swift` file that the scanner must not inventory by default.
13. WHEN report coverage tests run against a reduced-coverage fixture THEN they SHALL assert `report.md` contains the exact reduced coverage label.
14. WHEN manifest coverage tests run THEN they SHALL assert `Level1SemanticAnalysisReduced` is never paired with `Succeeded`.
15. WHEN report command-hint tests run THEN they SHALL assert command hints do not contain the literal `--out` value or local absolute paths.
16. WHEN toolchain or project-load gap tests run THEN they SHALL assert every `AnalysisGap` fact has non-empty `extractorId` and `extractorVersion`.
17. WHEN SQLite schema tests run THEN they SHALL assert required shared tables exist even when empty, including `scan_manifest`, `facts`, `symbols`, `symbol_occurrences`, `fact_symbols`, and `symbol_relationships` where the shared schema requires them.
18. WHEN validation runs locally for this implementation slice THEN these commands SHALL be run or explicitly deferred with reason:

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

19. WHEN the implementation changes shared language adapter behavior, combined indexes, report, export, reduce, path, reverse, or release-review behavior THEN it SHALL follow `docs/VALIDATION.md` and run or explicitly defer the relevant pinned smoke checks.

## Out of Scope for Issue #378

- Implementing SwiftSyntax AST extraction beyond scaffold/inventory/gap proof.
- Implementing SourceKit, sourcekit-lsp, SwiftPM semantic load, or Xcode semantic load.
- Implementing SwiftUI/UIKit UI analysis, storyboard/xib navigation, CoreData/Realm/SQLite analysis, HTTP client extraction, package impact, serializer/schema extraction, Combine/async flow, protocol conformance resolution, or Objective-C bridging.
- Updating public site copy.
- Adding LLM, embedding, vector, prompt-based, runtime, simulator, or device analysis.
- Claiming perfect Swift analysis or runtime proof.
- Rewriting shared SQLite schema except for additive changes required by the language adapter contract and covered by tests.
