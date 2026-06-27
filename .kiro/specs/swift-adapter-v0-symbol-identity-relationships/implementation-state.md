# Swift Adapter v0 Symbol Identity And Relationships Implementation State

Status: ready-for-implementation

Issue: [#381](https://github.com/joefeser/tracemap/issues/381)

Parent: [#377](https://github.com/joefeser/tracemap/issues/377)

Current branch: `codex/spec-swift-symbol-identity-relationships`

## Current Scope

This is a spec-only state note. No Swift analyzer/runtime code has been
implemented. The spec defines the intended Swift v0 symbol identity and direct
relationship model for a future implementation slice.

## Public Claim Level

Current PR: implementation-ready design only.

Future implementation may claim deterministic static Swift symbol identity and
direct relationship evidence only after fixtures, storage validation, reports,
and reduced-coverage gaps pass review. It must not claim runtime proof, protocol
witness resolution, Objective-C bridging, macro expansion, Xcode build success,
or AI impact analysis.

## Source Material Paths

- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- `.kiro/specs/jvm-indexer/requirements.md`
- `.kiro/specs/jvm-indexer/design.md`
- `.kiro/specs/python-indexer/requirements.md`
- `.kiro/specs/python-indexer/design.md`
- Companion Swift v0 specs in this implementation series, when present:
  - `.kiro/specs/swift-adapter-v0-scaffold-cli-output-contract/`
  - `.kiro/specs/swift-adapter-v0-inventory-project-discovery/`
  - `.kiro/specs/swift-adapter-v0-swiftsyntax-declarations-calls/`
- GitHub issue #377 Swift adapter v0 runway
- GitHub issue #381 Swift adapter v0 symbol identity and relationships

## Scope Decisions

- Start with SwiftSyntax-backed static declaration and relationship evidence.
- Reserve `Tier1Semantic` for future deterministic compiler/SourceKit evidence;
  SwiftSyntax-only symbol and relationship facts are not semantic proof.
- Use shared role-symbol properties and shared SQLite tables so combine/report
  and future path/reverse/route workflows can consume Swift rows.
- Use existing canonical relationship kinds for traversable rows:
  `InheritsFrom`, `ImplementsInterface`, `ExtendsInterface`, and `Overrides`.
  Swift-specific protocol/conformance language may appear in display metadata,
  not as a competing persisted relationship kind.
- Treat ambiguous identity, unresolved imports, conditional compilation, macros,
  generated code, typealias uncertainty, Objective-C bridging, generic
  specialization, protocol witness selection, and runtime dispatch as explicit
  gaps or lower-tier candidate evidence.
- Keep implementation tasks unchecked until a future implementation PR lands.

## Out Of Scope For This Spec PR

- Creating `src/swift` or any Swift adapter code.
- Updating shared storage schemas or rule catalog files.
- Adding fixtures or test projects.
- Running app code, Xcode builds, simulators, devices, SourceKit, or Swift
  package tests.
- Making public site/product claims.

## Exact Spec Validation Commands

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-symbol-identity-relationships --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

## Future Implementation Validation Commands

Do not run these for this spec-only PR. `src/swift` does not exist yet.

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
swift test --package-path src/swift
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <swift-scan>/index.sqlite --label swift --out <tmp>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/combined.sqlite --out <tmp>/combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index <tmp>/combined.sqlite --out <tmp>/combined-paths
./scripts/check-private-paths.sh
git diff --check
```

## Safe / No-Overclaim Boundaries

Safe language:

- deterministic static Swift symbol evidence;
- syntax-backed direct relationship evidence;
- package/module structural evidence;
- reduced coverage;
- candidate protocol implementation evidence;
- explicit identity or relationship gap.

Unsafe language:

- runtime call target;
- protocol witness proven;
- Objective-C selector resolved;
- macro-generated member indexed;
- Xcode build succeeded;
- app behavior impacted;
- production usage detected;
- AI impact analysis.

## Follow-Up Items

- Future implementation should decide the concrete Swift package path and CLI
  name.
- Future implementation should add rule catalog entries before emitting any new
  Swift facts.
- Future implementation should add local Swift fixtures for full supported paths
  and reduced/unsupported paths.
- Future implementation should update validation docs only when commands exist.
