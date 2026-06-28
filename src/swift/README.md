# TraceMap Swift Adapter

This is the v0 Swift adapter. It provides a deterministic scan command, output
contract, checked-in inventory/project metadata discovery, SwiftSyntax-backed
declarations, call/construction candidates, source-local direct symbol
relationships, package/dependency surfaces, HTTP/API client surfaces,
SwiftUI/UIKit UI surfaces, storage/data surfaces, and reduced-coverage
diagnostics. SourceKit/compiler semantic enrichment and runtime behavior remain
future evidence-backed slices.

The scanner emits static evidence only. It does not build the app, resolve
packages over the network, launch a simulator or device, inspect runtime state,
or prove UI navigation, storage access, network reachability, deployment,
production use, or impact.

## Build

```bash
swift build --package-path src/swift
```

## Test

```bash
swift run --package-path src/swift tracemap-swift-smoke-tests
```

## Scan

```bash
swift run --package-path src/swift tracemap-swift scan --repo <repo> --out <out>
```

The scanner writes:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

The fixed default `--max-file-byte-size` is `1048576` bytes. Raw source snippets,
raw remotes, local absolute paths, connection strings, provisioning details, and
private labels are not stored by default.

Useful checked-in smoke fixtures:

```bash
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-dependency-surfaces --out /tmp/tracemap-swift-dependency-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-http-api-client-surfaces --out /tmp/tracemap-swift-http-api-client-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-ui-surfaces --out /tmp/tracemap-swift-ui-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-storage-data-surfaces --out /tmp/tracemap-swift-storage-data-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-diagnostics-reduced --out /tmp/tracemap-swift-diagnostics-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-reduced --out /tmp/tracemap-swift-metadata-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-unsupported --out /tmp/tracemap-swift-metadata-unsupported
swift run --package-path src/swift tracemap-swift scan --repo samples/no-swift --out /tmp/tracemap-no-swift
```

## Coverage

This adapter reports non-semantic inventory coverage:

- `SwiftInventoryFileBasedSucceeded`: file-based inventory succeeded for the
  selected scope and supported metadata files were handled according to v0
  policy.
- `SwiftInventoryReduced`: useful inventory exists, but at least one metadata,
  project, lockfile, plist, exclusion, or toolchain diagnostic is partial or
  unavailable.
- `SwiftInventoryNotDetected`: no Swift files or supported Swift metadata were
  found in the selected scope.

These labels do not prove Swift compiler semantic coverage, build success,
package compatibility, Xcode scheme behavior, runtime behavior, or impact.
Manifest analysis remains `Level3SyntaxAnalysis` with `NotRun` for
SwiftSyntax-only v0 scans.

## Evidence Boundaries

Swift v0 is static evidence only. Supported rows may describe visible source,
metadata, dependency, HTTP, UI, and storage/data surfaces, but they do not prove
runtime call targets, protocol witness selection, Objective-C dispatch, UI
rendering/navigation, network reachability, SQL execution, persisted values,
Keychain item existence, Realm live schema, build success, production usage, or
impact.
