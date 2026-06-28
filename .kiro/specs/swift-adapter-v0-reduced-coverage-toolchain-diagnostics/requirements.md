# Swift Adapter v0 Reduced Coverage And Toolchain Diagnostics Requirements

## Introduction

TraceMap's Swift adapter already emits useful static evidence without requiring
Xcode builds, simulator access, package restore, or runtime execution. The next
Swift v0 slice should make reduced-coverage diagnostics more complete and more
queryable so users can understand what the adapter saw, what it intentionally
skipped, and why conclusions remain bounded.

This spec covers GitHub issue
[#386](https://github.com/joefeser/tracemap/issues/386), a child of the Swift
v0 runway issue [#377](https://github.com/joefeser/tracemap/issues/377).

The implementation must not execute app code, connect to devices/simulators,
run package managers, require a successful Xcode build, call LLMs, use
embeddings, or infer runtime behavior.

## Source Material

- GitHub issue #386: Swift adapter v0 reduced coverage gaps and toolchain
  diagnostics.
- GitHub issue #377: Swift adapter v0 runway.
- Existing Swift adapter specs under `.kiro/specs/swift-adapter-v0-*`.
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `rules/rule-catalog.yml`
- `src/swift/`

## Public Claim Level

Public copy MAY say Swift v0 reports deterministic static analysis gaps,
toolchain availability diagnostics, and reduced-coverage labels so users can
judge evidence quality.

Public copy MUST NOT say TraceMap proves buildability, compiler semantics,
runtime UI wiring, network behavior, Objective-C bridging, storyboard behavior,
macro expansion, package resolution, or production impact.

## Requirements

### Requirement 1: Toolchain Diagnostics

**User Story:** As a reviewer scanning Swift repos on different machines, I
want TraceMap to record bounded Swift/Xcode/tool availability diagnostics so I
can distinguish analyzer gaps from environment gaps.

#### Acceptance Criteria

1. WHEN the Swift adapter probes local tool availability THEN probes SHALL be
   bounded, non-mutating, and optional.
2. WHEN Swift, SwiftSyntax capability, SourceKit/sourcekit-lsp, Xcode,
   xcodebuild, CocoaPods, Carthage, or related optional tools are absent,
   unavailable, or time out THEN the adapter SHALL emit explicit diagnostic
   evidence and mark coverage reduced when the missing tool affects precision.
3. Tool diagnostics SHALL store safe tool identity/status only and SHALL NOT
   store local absolute tool paths, usernames, machine names, environment
   variables, shell output, prompts, credentials, or private repository data.
4. Tool diagnostics SHALL be deterministic for the same tool states and scan
   options except for safe status categories such as `available`,
   `not-found`, `timeout`, `unsupported`, or `not-probed`.
5. Failed or unavailable tool diagnostics SHALL NOT prevent basic file-based
   Swift scan output.

### Requirement 2: Unsupported Swift Feature Gaps

**User Story:** As an engineer reading a Swift scan, I want unsupported language
features labeled explicitly so I do not mistake absence of evidence for
complete analysis.

#### Acceptance Criteria

1. WHEN source contains macros, conditional compilation blocks, Objective-C
   bridging markers, dynamic selectors, storyboard/nib references, generated
   code markers, reflection-like APIs, protocol dispatch uncertainty, or
   unavailable module/import context THEN the adapter SHALL emit closed
   vocabulary `AnalysisGap` facts.
2. Gap facts SHALL include rule ID, evidence tier, file path, one-based line
   span, extractor ID/version, commit SHA provenance through the scan, and a
   public-safe message.
3. Gap facts SHALL avoid raw source snippets and raw unsafe values by default.
4. Gaps SHALL be specific enough for reports and downstream tools to group by
   feature boundary without parsing prose.
5. Dynamic or unsupported features SHALL NOT be converted into route, data,
   storage, UI, dependency, or call-flow evidence unless another rule has
   direct supporting evidence.

### Requirement 3: Reduced Coverage Model

**User Story:** As a downstream user, I want Swift coverage labels to explain
the kind of partial analysis performed, so reports stay honest.

#### Acceptance Criteria

1. WHEN any important Swift precision boundary is encountered THEN
   `scan-manifest.json`, `report.md`, and `index.sqlite` SHALL preserve a
   reduced-coverage signal.
2. Coverage labels SHALL distinguish successful syntax-only evidence from
   environment-reduced, unsupported-feature-reduced, and metadata-reduced
   analysis where practical.
3. Reports SHALL explain that partial analysis is useful but incomplete and
   that no runtime behavior is proven.
4. Existing Swift facts SHALL keep their current evidence tiers unless this
   slice adds only diagnostic companion facts.
5. The adapter SHALL keep producing deterministic artifacts even when all
   optional diagnostics fail or are disabled.

### Requirement 4: Query And Report Compatibility

**User Story:** As a TraceMap user, I want Swift diagnostics to survive export,
combine, report, and future query workflows so I can filter by evidence quality.

#### Acceptance Criteria

1. Diagnostic facts and gaps SHALL be written to `facts.ndjson` and
   `index.sqlite` using existing shared schemas where possible.
2. `tracemap export`, `tracemap combine`, and `tracemap report` SHALL continue
   to read Swift indexes containing the new diagnostics.
3. Local Swift `report.md` SHALL include a concise Swift diagnostics section
   grouped by category and status.
4. Combined reports SHALL not inflate diagnostic facts into dependencies,
   endpoints, paths, impacts, or reachability claims.
5. Rule IDs and limitations SHALL be documented before emitted facts use them.

### Requirement 5: Validation

**User Story:** As a reviewer, I want focused validation proving reduced
coverage and toolchain diagnostics are deterministic, safe, and compatible with
existing readers.

#### Acceptance Criteria

1. `swift build --package-path src/swift` SHALL pass.
2. `swift run --package-path src/swift tracemap-swift-smoke-tests` SHALL pass.
3. At least one checked-in fixture SHALL exercise unsupported Swift feature
   gaps.
4. At least one test SHALL exercise missing/unavailable/timeout diagnostic
   behavior without depending on the host machine actually missing tools.
5. Shared reader validation over a generated Swift index SHALL pass.
6. `dotnet build src/dotnet/TraceMap.sln` and `dotnet test
   src/dotnet/TraceMap.sln` SHALL pass or be explicitly deferred with evidence.
7. `./scripts/check-private-paths.sh` and `git diff --check` SHALL pass.

## Non-Goals

- SourceKit semantic enrichment.
- Xcode project build graph resolution.
- Storyboard/nib wiring extraction beyond reduced-coverage gaps.
- Runtime UI, network, storage, dependency, or call-flow proof.
- Swift macro expansion or generated-code execution.
- Objective-C runtime bridging resolution.
- Package manager restore/build/test execution.
- LLM calls, embeddings, vector databases, or prompt-based classification.
