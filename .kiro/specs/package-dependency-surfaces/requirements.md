# Package and Dependency Surfaces Requirements

## Summary

TraceMap should treat package and dependency metadata as first-class dependency surfaces across .NET, TypeScript, Python, and JVM scans. The feature extends existing `PackageReferenced`, `DependencyResolved`, and `DependencyRegistered` evidence into combined reports, diff, impact, paths, and reverse queries without claiming runtime loading or business impact.

This is GitHub issue #32. The MVP must remain deterministic, evidence-backed, and compatible with the current multi-adapter architecture.

## Goals

- Normalize package and dependency evidence emitted by language adapters into stable, reducer-compatible facts.
- Project package evidence as a terminal dependency surface named `package-config`.
- Support combined report, diff, impact, paths, reverse, and contract delta workflows using safe structured metadata.
- Record rule IDs, evidence tiers, repo identity, commit SHA, line spans, extractor IDs, extractor versions, and documented limitations for every conclusion.
- Preserve stable JSON and SQLite schemas using additive changes only.
- Mark partial evidence and analysis gaps honestly when dependency data is unresolved, dynamic, generated, or missing.

## Non-Goals

- No package vulnerability scanning, license compliance, SBOM generation, provenance attestation, package signing verification, or registry lookups in the MVP.
- No dependency solving, transitive closure, lockfile resolution, or installed environment inspection beyond statically visible files.
- No runtime loading claims. Static package metadata does not prove a dependency is restored, imported, injected, executed, reachable, or deployed.
- No LLM calls, embeddings, vector indexes, or prompt-based classification.
- No raw secrets, raw script commands, raw source snippets, developer-local absolute paths, or unredacted private registry credentials in facts, reports, logs, or JSON outputs.
- No cross-language symbol identity inference from matching package names alone.

## Users and Workflows

### Package Surface Review

As a reviewer, I want `tracemap report` over a combined index to show package dependency surfaces by source label, package name, version/range when safe, ecosystem, dependency scope, and evidence tier so I can see dependency evidence without opening every manifest file.

Acceptance criteria:

- Package surfaces are derived only from facts with rule IDs.
- Rows include source label, source repo identity where available, commit SHA, fact ID, rule ID, evidence tier, file path, line span, package name, safe version/range metadata, ecosystem, manifest kind, dependency scope, and caveats.
- Reports do not render raw package scripts, tokens, credentials, local absolute paths, or source snippets.
- Missing or malformed package manifests produce `AnalysisGap` evidence instead of silent success.

### Package Surface Diff

As a reviewer, I want `tracemap diff --scope surfaces --surface package-config` to compare package surfaces between two combined indexes so I can identify added, removed, changed, or review-tier dependency metadata.

Acceptance criteria:

- Diff keys are deterministic and based on safe normalized metadata.
- Version or scope changes produce changed surface rows when package identity is stable.
- Hash-only or weak metadata produces review-tier classifications, not strong changed/unchanged conclusions.
- Reduced coverage in either side downgrades no-diff conclusions and emits rule-backed gaps.

### Package Surface Impact

As a release owner, I want `tracemap impact` to project package surface diffs into static change-impact items so package metadata changes appear beside endpoint, SQL, config, and edge changes.

Acceptance criteria:

- Impact items cite the originating diff evidence and use a rule ID.
- Impact wording says static package surface change, dependency metadata change, or needs review. It must not say runtime impacted, vulnerable, loaded, exploitable, or affected without separate evidence.
- Impact JSON exposes machine-readable package surface fields and caveats.
- Reduced coverage, selector misses, and truncation are explicit gaps.

### Reverse Dependency Surface Query

As a maintainer, I want `tracemap reverse --surface package-config --surface-name <package>` to find static upstream roots and paths that can reach package-related evidence when such paths are supported by the combined graph.

Acceptance criteria:

- Reverse selection matches only package surface nodes and supports exact safe package name matching.
- Paths reuse existing path graph evidence and carry rule IDs and evidence tiers on nodes and edges.
- Unattached package facts remain visible as selected surfaces or review gaps rather than being linked to symbols by guesswork.
- Results are bounded and deterministic under existing depth/frontier/root/path caps.

### Contract Delta for Dependency Surfaces

As a platform maintainer, I want contract delta v2 to accept dependency-surface changes for package metadata so an external package upgrade or manifest change can be reduced against TraceMap indexes.

Acceptance criteria:

- Contract delta changes with `kind = "dependency-surface"` and `surfaceKind = "package-config"` can match package surface facts.
- The reducer supports stable package selectors such as package name, ecosystem, source label, dependency scope, manifest kind, and optional version/range.
- No-evidence results respect full versus reduced coverage.
- Findings include path and reverse context only when stable combined selectors can be derived.

## Evidence Requirements

Every package or dependency surface fact used by this feature must include:

- `factId`
- `scanId`
- repo and commit SHA
- `factType`
- `ruleId`
- `evidenceTier`
- file path and line span
- extractor ID and extractor version
- sorted string properties
- safe package identity fields when available

Required safe properties for `PackageReferenced` facts:

| Property | Meaning |
| --- | --- |
| `surfaceKind` | Must be `package-config` for terminal package surfaces. |
| `packageName` | Normalized package name or ecosystem-native package coordinate. |
| `ecosystem` | One of `nuget`, `npm`, `python`, `maven`, `gradle`, or a documented adapter value. |
| `manifestKind` | Source manifest kind, such as `csproj`, `packages.config`, `package.json`, `pyproject.toml`, `requirements.txt`, `pom.xml`, or `gradle`. |
| `dependencyScope` | Safe dependency bucket when visible, such as `runtime`, `development`, `test`, `build`, `optional`, `peer`, `dependencyManagement`, or `unknown`. |
| `version` | Raw version/range only when it is already public manifest metadata and contains no local path, URL credential, token, or environment expansion. |
| `versionHash` | Stable hash of the exact static version/range string when the raw value is unsafe or policy chooses hash-only output. |

Recommended properties:

| Property | Meaning |
| --- | --- |
| `packageManager` | `nuget`, `npm`, `pip`, `poetry`, `setuptools`, `maven`, `gradle`, or similar. |
| `dependencyGroup` | Ecosystem-native group such as `dependencies`, `devDependencies`, `PackageReference`, `project.dependencies`, or `testImplementation`. |
| `targetFramework` | Safe target framework when statically visible. |
| `groupId` | JVM group ID when visible. |
| `artifactId` | JVM artifact ID when visible. |
| `sourceKind` | `manifest`, `lockfile`, `build-file`, or `literal-config`. |
| `metadataHash` | Hash over normalized safe metadata used for stable diff identity. |
| `redactionReason` | Reason a raw version/path/value was omitted. |

## Evidence Tiers

- `Tier2Structural`: package manifest, build file, or lockfile metadata parsed from statically visible files.
- `Tier3SyntaxOrTextual`: literal-only build script or setup script evidence where structure is partial.
- `Tier4Unknown`: parser failure, dynamic dependency declaration, unresolved interpolation, unsupported manifest, skipped file, missing lockfile, or scanner limitation.

`Tier1Semantic` is not expected for MVP package manifest facts. Runtime-adjacent facts such as `DependencyResolved` or `DependencyRegistered` may remain `Tier1Semantic` in C# when Roslyn proves the code construct, but they must not be reclassified as package manifest evidence.

## Language Adapter MVP Scope

### .NET

- Existing `ProjectFileReader` and `ConfigExtractor` package facts from `.csproj` and `packages.config`.
- Add or confirm safe properties: `surfaceKind`, `ecosystem=nuget`, `manifestKind`, `dependencyScope`, and version redaction behavior.
- Do not evaluate conditional MSBuild properties beyond current scanner capability unless explicitly implemented with gaps.

### TypeScript

- Existing `package.json` facts for package identity, dependency groups, and scripts.
- Package scripts remain hashed key evidence only.
- Add or confirm safe properties: `surfaceKind`, `ecosystem=npm`, `manifestKind=package.json`, dependency group/scope, and version redaction behavior.
- Do not execute scripts, resolve workspaces, query registries, or install dependencies.

### Python

- Existing metadata from `pyproject.toml`, `setup.cfg`, literal-only `setup.py`, and requirements files.
- Add or confirm safe properties: `surfaceKind`, `ecosystem=python`, manifest kind, dependency scope, package manager/source kind, and version redaction behavior.
- Never execute setup scripts or import target modules.
- Treat editable installs, URL dependencies, environment markers, extras, and interpolation as structural evidence or gaps according to parser confidence.

### JVM

- Existing Maven and literal Gradle metadata from `pom.xml` and Gradle files.
- Add or confirm safe properties: `surfaceKind`, `ecosystem=maven` or `gradle`, manifest kind, group/artifact/version, scope/configuration, and version redaction behavior.
- Do not execute Gradle, resolve version catalogs, fetch parent POMs remotely, evaluate plugins, or inspect dependency caches.

## Output Expectations

### `facts.ndjson`

- Emits package facts as normal TraceMap facts.
- Does not store raw source snippets or raw script commands.
- Uses deterministic fact IDs and sorted properties.

### `index.sqlite`

- Stores package facts in the existing `facts` table.
- If symbol or relationship tables are populated, they must remain source-local and must not infer cross-language identity.
- Combined indexes import package facts without rewriting source fact IDs.

### `dependency-report.json`

Package surfaces should appear in the dependency surface collection with:

- `surfaceKind = "package-config"`
- source label and source index ID
- fact ID and combined fact ID
- rule ID and evidence tier
- package name
- version or version hash
- ecosystem
- manifest kind
- dependency scope
- safe metadata
- caveats

### Markdown Reports

- Include a package dependency surface section or integrate package rows into the existing dependency-surface section.
- Prefer concise rows and explicit caveats over prose claims.
- Use safe normalized paths only, never developer-local absolute paths.

## Safety and Redaction

- Do not emit raw source snippets.
- Do not emit raw package scripts. Store script names, key paths, command hashes, lengths, and redaction reasons only.
- Do not emit URL credentials, tokens, usernames/passwords, local absolute paths, home directories, registry auth lines, `.npmrc` secrets, `pip.conf` credentials, Maven `settings.xml` credentials, Gradle properties secrets, environment-expanded values, or private filesystem paths.
- Dependency URLs may be represented by hash, host-only safe metadata when public, and redaction reason. If uncertain, hash-only.
- Reports and JSON must render redaction reason when user-visible metadata is omitted.
- Logs may include parser errors and relative file paths but must avoid raw sensitive values.

## Rules and Limitations

Implementation must update `rules/rule-catalog.yml` for every new or changed rule ID. Rule entries must document:

- emitted fact type or report row type
- evidence tier expectations
- required properties
- limitations
- known false positives and false negatives

Expected existing or extended rules:

- `project.file.v1` for .NET package references.
- `typescript.package.v1` for TypeScript package metadata.
- `python.package.metadata.v1` for Python package metadata.
- `jvm.buildfile.v1` for JVM build file metadata.
- `combined.diff.surface.v1` for package surface diff rows.
- `combined.impact.surface.v1` for package surface impact rows.
- `combined.reverse.surface.v1` and `combined.reverse.path.v1` for reverse package queries.
- `combined.paths.surface-evidence.v1` for package terminal surface edges.
- `contract.delta.impact.v2` and `contract.delta.context.v2` for dependency-surface reducer matches.

Limitations must include:

- Static package metadata does not prove runtime loading, installed versions, transitive dependencies, vulnerability, exploitability, deployment, branch feasibility, or business impact.
- Version ranges may be approximate and ecosystem-specific.
- Dynamic build files and unresolved interpolation reduce coverage.
- Package name matching is ecosystem-scoped and does not imply cross-language symbol identity.

## Test Requirements

- Unit tests for safe package metadata normalization and redaction.
- Adapter tests for .NET, TypeScript, Python, and JVM package fact shape.
- SQLite writer tests proving package properties are persisted with deterministic JSON ordering.
- Combined report tests proving package surfaces render in JSON and Markdown.
- Diff tests for added, removed, changed version/scope, weak identity, selector no-match, and reduced-coverage gaps.
- Impact tests proving package surface diff rows project into static impact items with caveats.
- Path tests proving package terminal surfaces can be selected and unattached facts become gaps instead of invented paths.
- Reverse tests for `--surface package-config` and exact package selector behavior.
- Contract delta tests for `kind=dependency-surface`, `surfaceKind=package-config`.
- Private path and secret redaction tests.
- Determinism tests comparing repeated JSON output byte-for-byte for stable fixtures.

## Smoke Validation

Run or explicitly defer the relevant pinned checks from `docs/VALIDATION.md`.

Required for implementation PRs touching this feature:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm run check --prefix src/typescript
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
./scripts/check-private-paths.sh
./scripts/smoke-combined-paths.sh
```

Also run a package-focused combined smoke over sample indexes:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index <first>/index.sqlite --label first \
  --index <second>/index.sqlite --label second \
  --out <tmp>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/combined.sqlite --out <tmp>/combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index <tmp>/combined.sqlite --to-surface package-config --out <tmp>/package-paths
dotnet run --project src/dotnet/TraceMap.Cli -- reverse --index <tmp>/combined.sqlite --surface package-config --out <tmp>/package-reverse
dotnet run --project src/dotnet/TraceMap.Cli -- diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --scope surfaces --surface package-config --out <tmp>/package-diff
dotnet run --project src/dotnet/TraceMap.Cli -- impact --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --scope surfaces --surface package-config --out <tmp>/package-impact
```

Validation should assert that generated Markdown and JSON do not contain raw snippets, raw scripts, credentials, or developer-local absolute paths.
