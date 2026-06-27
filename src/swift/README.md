# TraceMap Swift Adapter

This is the v0 Swift adapter scaffold. It provides a deterministic scan command
and output contract before deeper SwiftSyntax, SourceKit, SwiftPM, Xcode, UI,
HTTP, storage, or relationship extraction is added.

The scanner emits static evidence only. It does not build the app, resolve
packages over the network, launch a simulator or device, inspect runtime state,
or prove UI navigation, storage access, network reachability, deployment, or
impact.

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

## Coverage

This scaffold always reports reduced coverage (`Level1SemanticAnalysisReduced`
with `FailedOrPartial`) or lower. Full Swift semantic coverage is reserved for a
future implementation that proves deterministic semantic evidence for the
selected scope and records toolchain gaps.
