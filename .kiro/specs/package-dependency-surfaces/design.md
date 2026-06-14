# Package and Dependency Surfaces Design

## Overview

Package and dependency surfaces are a shared projection over existing adapter facts, primarily `PackageReferenced`, plus selected runtime-adjacent dependency facts when they are already rule-backed. The design keeps extraction local to each adapter and keeps cross-index behavior in the existing combined reporting layer.

The MVP should make package metadata visible and queryable without performing dependency resolution. A package surface is static evidence that a dependency declaration exists in a repo at a commit. It is not evidence that the package is installed, loaded, executed, vulnerable, or reachable.

## Current Architecture Fit

TraceMap already has the core pieces this feature needs:

- Language adapters emit facts into `facts.ndjson` and `index.sqlite`.
- Facts carry rule IDs, evidence tiers, file spans, extractor identity, repo identity, and commit SHA.
- `tracemap combine` imports multiple indexes into a combined SQLite database.
- Combined report, diff, impact, paths, reverse, and contract delta v2 already understand terminal surface kinds, including `package-config` in parts of the .NET reporting code.
- Rule catalog entries already exist for package-related adapter rules and combined surface rules.

The implementation should therefore be mostly normalization, projection, tests, and report polish, not a new scanner subsystem.

## Data Model

### Fact Types

Use existing fact types where possible:

| Fact type | Role |
| --- | --- |
| `PackageReferenced` | Primary package manifest/build dependency declaration. |
| `DependencyRegistered` | Runtime-adjacent dependency registration evidence when statically visible. Not a package manifest surface unless explicitly projected with caveats. |
| `DependencyResolved` | Runtime-adjacent dependency resolution evidence when statically visible. Not a package manifest surface unless explicitly projected with caveats. |
| `AnalysisGap` | Parser, coverage, dynamic metadata, unsupported manifest, or redaction gap. |
| `ConfigKeyDeclared` | Package-manager config/script key evidence when safe and already emitted. |

MVP package terminal surfaces should be sourced from `PackageReferenced` only. `DependencyRegistered` and `DependencyResolved` can appear in reports as dependency evidence, but should not be merged with package manifests unless a later rule defines the relationship.

### Required Package Properties

Adapters should emit or backfill these properties on `PackageReferenced`:

```json
{
  "surfaceKind": "package-config",
  "packageName": "example",
  "ecosystem": "npm",
  "manifestKind": "package.json",
  "dependencyScope": "runtime",
  "version": "^1.2.3",
  "versionHash": "",
  "packageManager": "npm",
  "dependencyGroup": "dependencies",
  "sourceKind": "manifest"
}
```

When a raw value is unsafe or unstable:

- omit `version` or set it to an empty string according to existing adapter conventions
- include `versionHash`
- include `redactionReason`
- preserve line span and manifest kind

Properties must be sorted before serialization.

### Ecosystem Normalization

| Adapter | Ecosystem | Package name |
| --- | --- | --- |
| .NET | `nuget` | NuGet package ID using manifest casing or documented normalized casing. |
| TypeScript | `npm` | npm package name, including scope when present. |
| Python | `python` | normalized Python distribution name when safe; preserve display name separately if needed. |
| JVM Maven | `maven` | `groupId:artifactId` as `packageName`, with separate `groupId` and `artifactId`. |
| JVM Gradle | `gradle` or `maven` | Prefer `groupId:artifactId` when literal coordinates are visible. Use `gradle` for plugin/configuration-only evidence. |

Do not compare package names across ecosystems unless the query explicitly requests cross-ecosystem review-tier matching.

### Stable Surface Identity

Combined package surface identity should be deterministic and safe:

```text
surfaceKind
sourceLabel
ecosystem
packageName
manifestKind
dependencyScope
dependencyGroup
targetFramework or module coordinate when available
```

Version is compared as changed metadata, not part of the stable identity, unless duplicate rows require a disambiguator. If identity is still ambiguous, add a safe line/file discriminator and downgrade the row with a caveat such as `VolatileIdentity`.

Hash-only version evidence should use a caveat such as `HashOnlyEvidence`.

### SQLite

No new table is required for MVP. Store package facts in existing tables:

- `facts`
- `combined_facts`
- optional symbol tables only when a source-local symbol relationship exists

If later work adds a package-specific table, it must be additive, shared across adapters, tested in schema tests, and documented in `docs/LANGUAGE_ADAPTER_CONTRACT.md`.

## Adapter Extraction

### .NET

Sources:

- `.csproj`
- `Directory.Packages.props` if implemented
- `packages.config`

MVP behavior:

- Parse XML only; do not run MSBuild evaluation unless a future rule documents it.
- Emit `PackageReferenced` with `surfaceKind=package-config`, `ecosystem=nuget`, `manifestKind`, `dependencyGroup`, `dependencyScope`, `version`, and safe target framework metadata when visible.
- For conditional properties or unresolved property references, emit structural evidence with a caveat or `AnalysisGap`.

### TypeScript

Sources:

- `package.json`

MVP behavior:

- Emit package identity as project evidence and dependencies as `PackageReferenced`.
- Map dependency groups:
  - `dependencies` to `runtime`
  - `devDependencies` to `development`
  - `peerDependencies` to `peer`
  - `optionalDependencies` to `optional`
  - other groups to `unknown`
- Hash package script commands. Never store raw script text.
- Do not inspect npm/yarn/pnpm lockfiles in MVP unless a rule is added.

### Python

Sources:

- `pyproject.toml`
- `setup.cfg`
- literal-only `setup.py`
- requirements files

MVP behavior:

- Parse static dependency declarations only.
- Normalize distribution names conservatively.
- Represent extras, environment markers, editable installs, direct URLs, and local paths as safe metadata, hash-only metadata, or gaps depending on certainty.
- Never execute setup scripts or import target code.

### JVM

Sources:

- `pom.xml`
- literal Gradle build files

MVP behavior:

- Parse local Maven dependency declarations and dependency management blocks.
- Parse literal Gradle dependency coordinates only.
- Emit group/artifact/version/scope/configuration when visible.
- Emit gaps for version catalogs, plugin conventions, interpolation, parent POMs not available locally, buildSrc, and remote resolution.

## Surface Projection

The combined dependency surface projector should recognize `PackageReferenced` when:

- `surfaceKind` is `package-config`, or
- `factType` is `PackageReferenced` and adapter-specific safe package metadata exists.

Projection fields:

| Field | Source |
| --- | --- |
| `surfaceKind` | Fact property or inferred as `package-config` for `PackageReferenced`. |
| `displayName` | Package name plus version/range when safe. |
| `packageName` | `packageName`, `package`, `dependencyName`, `moduleName`, or `name`. |
| `version` | `version` or `packageVersion` when safe. |
| `versionHash` | `versionHash` when raw version is omitted. |
| `ecosystem` | `ecosystem` or adapter/language fallback. |
| `manifestKind` | `manifestKind`, `metadataSource`, or file kind fallback. |
| `dependencyScope` | `dependencyScope`, `scope`, or dependency group mapping. |
| `safeMetadata` | Allowlist only. |
| `caveats` | Redaction, hash-only, weak identity, reduced coverage, parser limitations. |

The allowlist should exclude raw command values, URLs with credentials, local absolute paths, source snippets, and arbitrary unknown keys that might contain secrets.

## Reporting

### Combined Dependency Report

Add or verify a package row section with:

- source label
- package name
- version or version hash label
- ecosystem
- manifest kind
- scope/group
- rule ID
- evidence tier
- relative file span
- caveats

Markdown should be compact. JSON should be complete and stable.

### Diff

For `diff --scope surfaces --surface package-config`:

- `Added`: package identity exists only after and after coverage is adequate.
- `Removed`: package identity exists only before and before coverage is adequate.
- `ChangedEvidence`: package identity exists on both sides and safe metadata differs.
- `NeedsReviewDiff`: identity or metadata is weak, hash-only, duplicate, or coverage-reduced.
- Gap rows: selector no-match, missing precision table, reduced coverage, truncation, or invalid selector.

### Impact

Package diff rows project to static impact items using `combined.impact.surface.v1`.

Impact item language should say:

- "package surface added"
- "package surface removed"
- "package metadata changed"
- "package surface needs review"

It must not say:

- "runtime impact"
- "package is loaded"
- "vulnerability"
- "security affected"
- "service impacted"

### Paths

Package surfaces can be terminal nodes for `paths --to-surface package-config`.

Attachment rules:

- Attach to a source-local symbol only when a credible source/target/origin role property points to a symbol.
- Manifest-only package facts will often be source-level terminal surfaces without a path from endpoint roots.
- Emit rule-backed gaps for unattached package facts instead of manufacturing symbol edges.

### Reverse

`reverse --surface package-config` selects package terminal surfaces and traverses upstream using the existing combined path graph.

Selector fields:

- `--source`
- `--surface package-config`
- `--surface-name <packageName>`
- future JSON selector fields for ecosystem/scope/version can be additive

Reverse results must remain bounded and include rule IDs/evidence tiers for selected surfaces, roots, paths, and gaps.

### Contract Delta v2

Dependency-surface changes can use this shape:

```json
{
  "id": "package-upgrade-serilog",
  "kind": "dependency-surface",
  "changeType": "changed",
  "reference": {
    "surfaceKind": "package-config",
    "packageName": "Serilog",
    "ecosystem": "nuget",
    "dependencyScope": "runtime",
    "oldVersion": "2.12.0",
    "newVersion": "3.1.1"
  }
}
```

Reducer matching should compare package name and ecosystem first, then optional scope, source label, manifest kind, and version fields. Version-only matches without package identity are invalid selector gaps.

## Redaction Design

Use an allowlist for report and JSON metadata:

- package name
- ecosystem
- package manager
- manifest kind
- dependency group/scope
- target framework
- group ID and artifact ID
- safe version/range string
- version hash
- metadata hash
- source kind
- redaction reason

Hash or omit:

- raw scripts
- local paths
- URLs with credentials
- registry auth lines
- environment variable expansions
- unknown keys with names containing `token`, `secret`, `password`, `apikey`, `auth`, `credential`, or similar

Line spans may point to the declaration line but snippets are not stored.

## Determinism

- Sort facts by existing adapter rules.
- Sort properties lexicographically.
- Sort package surfaces by source label, ecosystem, package name, manifest kind, scope, file path, and line span.
- Avoid timestamps, output paths, process IDs, random IDs, and filesystem absolute paths in stable IDs.
- Repeated report JSON for identical input must be byte-stable.

## Failure Modes

| Failure | Required behavior |
| --- | --- |
| Manifest parse failure | Emit `AnalysisGap`, continue scan, mark coverage reduced if applicable. |
| Dynamic build script | Emit literal facts only; emit gap for unresolved dynamic metadata. |
| Unsafe value | Hash or omit value; add redaction reason. |
| Missing package metadata | Do not invent package facts; report selector no-match or reduced coverage. |
| Duplicate package identity | Use review-tier diff/path rows with `VolatileIdentity` caveat. |
| Missing combined graph path | Emit gap; do not infer reachability. |

## Implementation Notes

- Prefer extending existing adapter extractors and combined surface projection helpers.
- Keep source code changes small and staged by adapter/reporting area.
- Update rule catalog entries before emitting new properties or new report row classes.
- Update docs only when the implemented behavior changes the adapter contract or validation commands.
- Preserve backward compatibility for existing package facts by allowing legacy keys such as `package`, `dependencyName`, `moduleName`, `name`, and `packageVersion`.

## Open Questions

- Should lockfile parsing be a follow-up issue or part of a post-MVP phase for package surfaces?
- Should direct URL dependencies expose host-only metadata when the host is public, or always hash-only?
- Should .NET central package management via `Directory.Packages.props` be MVP if the current parser does not already support it?
- Should `DependencyRegistered` and `DependencyResolved` get a separate `dependency-runtime` surface kind in a future issue?
