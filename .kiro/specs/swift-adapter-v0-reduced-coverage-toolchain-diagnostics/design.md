# Swift Adapter v0 Reduced Coverage And Toolchain Diagnostics Design

## Overview

This slice strengthens the Swift adapter's honesty layer. It adds structured
diagnostic facts and closed-vocabulary gaps for environment/toolchain state and
unsupported Swift feature boundaries. It should not make the adapter broader by
inference; it should make the adapter clearer about where it is precise,
reduced, or intentionally silent.

Evidence flow:

```text
checked-in repo + commit SHA
  -> bounded optional tool probes
  -> Swift source and metadata scan
  -> unsupported-feature gap detection
  -> coverage category aggregation
  -> facts.ndjson + index.sqlite + report.md diagnostics
```

## Goals

- Record optional Swift toolchain diagnostics without mutating the repo or
  requiring build success.
- Emit closed-vocabulary `AnalysisGap` facts for common Swift v0 precision
  boundaries.
- Make coverage labels and report language easier to interpret.
- Keep export/combine/report compatibility intact.
- Keep artifacts deterministic and public-safe.

## Non-Goals

- No SourceKit semantic pass, compiler model, package restore, Xcode build,
  simulator/device execution, app execution, runtime tracing, or generated-code
  execution.
- No route, UI, storage, dependency, or impact conclusions from diagnostic
  evidence alone.
- No raw snippets, shell output, local absolute paths, hostnames, environment
  variables, credentials, or private labels.

## Fact And Gap Model

### Existing Fact Types

Prefer existing fact types:

- `AnalysisGap` for unsupported feature and precision-boundary evidence.
- Existing Swift ecosystem/toolchain metadata fact types where already emitted
  by inventory discovery, if they fit without schema drift.

If a new Swift-local diagnostic fact type is needed, it must be documented in
the rule catalog and must not be projected as dependency, endpoint, path,
impact, or reachability evidence by shared readers.

### Rule IDs

Add or extend these rule IDs:

- `swift.toolchain.diagnostic.v1`
  - Emits bounded local tool availability diagnostics.
  - Evidence tier: `Tier3SyntaxOrTextual` or `Tier4Unknown` depending on the
    status source.
  - Limitations: local environment only; not repo truth; does not prove builds
    or runtime behavior.
- `swift.reduced-coverage.gap.v1`
  - Emits `AnalysisGap` for unsupported Swift precision boundaries.
  - Evidence tier: `Tier4Unknown`.
  - Limitations: conservative static detection; false positives are acceptable
    when the message says precision is reduced.

Reuse existing Swift analysis-gap rules only if they already document the
specific gap class and limitations. Do not overload a broad rule without
updating limitations.

## Toolchain Diagnostics

Probe status should be represented as safe categories:

- `available`
- `not-found`
- `timeout`
- `unsupported`
- `not-probed`
- `error-redacted`

Candidate tools/capabilities:

- Swift executable / SwiftPM basic availability.
- SwiftSyntax package capability if represented by the adapter build.
- SourceKit/sourcekit-lsp presence, if probed.
- Xcode/xcodebuild presence.
- CocoaPods presence.
- Carthage presence.

Probes must be:

- bounded by timeout;
- read-only;
- no package restore/build/test;
- no simulator/device operations;
- no raw stdout/stderr storage;
- no local absolute tool path storage.

If the current adapter already probes some of these, this slice should
normalize statuses and report grouping rather than duplicate probes.

## Unsupported Feature Gap Vocabulary

Add closed vocabulary `gapKind` values such as:

- `swift-toolchain-unavailable`
- `swift-toolchain-timeout`
- `swift-sourcekit-unavailable`
- `swift-xcode-unavailable`
- `swift-macro-expansion-unsupported`
- `swift-conditional-compilation-reduced`
- `swift-objective-c-bridging-reduced`
- `swift-selector-dynamic`
- `swift-storyboard-wiring-unresolved`
- `swift-nib-wiring-unresolved`
- `swift-generated-code-reduced`
- `swift-reflection-dynamic`
- `swift-protocol-dispatch-reduced`
- `swift-module-context-unavailable`

Each gap should include:

- rule ID;
- closed `gapKind`;
- file path when source-backed;
- line span when source-backed;
- safe message;
- optional safe counts/status properties.

## Coverage Aggregation

The Swift scan manifest and local report should preserve:

- overall analysis level;
- build status (`NotRun` remains expected);
- reduced coverage boolean/label;
- diagnostic categories and counts;
- top gap kinds by count;
- explicit limitation text.

Do not downgrade existing useful syntax facts merely because a diagnostic gap
exists elsewhere. Instead, emit companion gaps and report reduced coverage.

## Reporting

Local `report.md` should include a concise section:

```text
## Swift Diagnostics And Coverage

- Toolchain diagnostics: available N, not-found N, timeout N
- Unsupported feature gaps: macro N, storyboard N, Objective-C bridging N
- Coverage: reduced syntax-only static evidence
```

Combined reports may surface counts only where they already report gaps. They
must not interpret diagnostics as dependency, endpoint, path, or impact rows.

## Safety

Forbidden default output:

- raw shell output;
- local absolute paths;
- usernames or machine names;
- environment variables;
- credentials/secrets/tokens;
- raw URLs/hosts/remotes;
- raw source snippets;
- private repo/org/team labels.

Use role-prefixed SHA-256 hashes only when identity preservation is useful and
safe.

## Validation Strategy

Add synthetic tests that can force probe statuses without relying on the host
machine:

- unavailable tool status;
- timeout status;
- unsupported feature source fixture;
- dynamic selector/storyboard/macro gap fixture;
- deterministic fact ordering and byte stability;
- public safety grep assertions.

Run shared reader smoke on at least one Swift scan with diagnostics:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo <fixture> --out <tmp>
dotnet run --project src/dotnet/TraceMap.Cli -- export --index <tmp>/index.sqlite --out <tmp-export> --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <tmp>/index.sqlite --label swift --out <tmp>.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>.sqlite --out <tmp-report>
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

## Implementation Notes

- Prefer small helper functions over broad regex passes.
- Use existing Swift masking helpers before source-pattern gap detection where
  comments/string literals would otherwise produce false positives.
- Keep gap detection conservative and source-local.
- Preserve stable ordering by path, line, rule ID, gap kind, and discriminator.
- Update `docs/VALIDATION.md` only with commands that are actually runnable.
