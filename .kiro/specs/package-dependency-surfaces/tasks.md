# Package and Dependency Surfaces Tasks

## 1. Inventory Current Behavior

- [ ] Inspect existing `PackageReferenced`, `DependencyResolved`, `DependencyRegistered`, and package-related `AnalysisGap` facts across .NET, TypeScript, Python, and JVM sample scans.
- [ ] Record current property keys used by each adapter, including legacy aliases such as `package`, `name`, `packageVersion`, `metadataSource`, `groupId`, and `artifactId`.
- [ ] Verify where `package-config` is already accepted in combined report, diff, paths, reverse, and impact code.
- [ ] Identify schema or rule catalog gaps before changing emitters.

Exit criteria:

- A short implementation note or PR description lists existing keys, missing required keys, and compatibility decisions.

## 2. Rule Catalog and Contract Updates

- [ ] Update `rules/rule-catalog.yml` for changed package metadata expectations.
- [ ] Add limitations for package surfaces, redaction, hash-only metadata, unresolved dynamic declarations, and no runtime loading claims.
- [ ] Add or update docs only if implementation changes the shared adapter contract.

Exit criteria:

- No package fact or report row can be emitted without a documented rule ID and limitations.

## 3. Shared Package Metadata Normalization

- [ ] Implement or reuse a package surface normalization helper in the combined reporting layer.
- [ ] Accept current adapter keys and normalize to `surfaceKind`, `packageName`, `version`, `versionHash`, `ecosystem`, `manifestKind`, `dependencyScope`, `dependencyGroup`, and `packageManager`.
- [ ] Add an allowlist for safe metadata rendered to Markdown and JSON.
- [ ] Add caveats for hash-only evidence, redaction, duplicate/weak identity, and reduced coverage.
- [ ] Ensure deterministic ordering of normalized metadata and surfaces.

Tests:

- [ ] Unit tests for aliases, safe metadata, redaction, weak identity, duplicate rows, and deterministic sort order.
- [ ] Unit test proving the same package display name in different ecosystems remains distinct unless an explicit cross-ecosystem review-tier query is requested.

## 4. Adapter Fact Shape

### .NET

- [ ] Add `surfaceKind=package-config` and `ecosystem=nuget` on `.csproj` package facts.
- [ ] Address `packages.config` explicitly: either emit `PackageReferenced` facts or project existing `ConfigKeyDeclared` package rows into `package-config` surfaces with rule-backed metadata.
- [ ] Add manifest kind, dependency scope/group, target framework when safe, version or version hash, and redaction reason.
- [ ] Emit version-absent caveats or `AnalysisGap` evidence for central package management or unresolved externally declared versions when local static parsing is not implemented.
- [ ] Emit `AnalysisGap` for unsupported package metadata rather than silently dropping it when the scanner can detect the gap.
- [ ] Keep failed MSBuild/project load behavior partial, not clean.

Tests:

- [ ] .NET unit or scan tests for `PackageReference` and `packages.config` fact properties.
- [ ] .NET malformed package manifest or unsupported metadata test emits `AnalysisGap` rather than silent success.
- [ ] Redaction test for unsafe path or credential-like version/source metadata if supported input exists.

### TypeScript

- [ ] Add `surfaceKind=package-config`, `ecosystem=npm`, `manifestKind=package.json`, dependency group/scope, package manager, and safe version metadata.
- [ ] Map dependency groups to normalized `dependencyScope` values instead of relying only on raw group names such as `dependencySection`.
- [ ] Confirm raw package scripts are never emitted; only script keys, hashes, lengths, and redaction reasons are allowed.
- [ ] Emit parser gaps for malformed package JSON.

Tests:

- [ ] TypeScript package extractor tests for dependency groups and script redaction.
- [ ] TypeScript scan/report test for package surface facts in `facts.ndjson` and `index.sqlite`.

### Python

- [ ] Add `surfaceKind=package-config`, `ecosystem=python`, manifest kind, package manager/source kind, dependency scope, version or version hash, and redaction reason.
- [ ] Preserve per-source-file evidence spans for dependencies from pyproject, setup.cfg, setup.py, and requirements files; avoid last-write-wins batch rows that lose file/line evidence.
- [ ] Keep `setup.py` literal-only and never execute it.
- [ ] Represent direct URLs, editable installs, extras, markers, and local paths as safe metadata, hash-only metadata, or gaps.

Tests:

- [ ] Python metadata tests for pyproject/setup.cfg/requirements.
- [ ] Python redaction tests for direct URL or local path dependency forms.
- [ ] Python malformed or unsupported manifest test emits `AnalysisGap` with reduced coverage when appropriate.

### JVM

- [ ] Add `surfaceKind=package-config`, ecosystem, manifest kind, package manager/build tool, group ID, artifact ID, scope/configuration, version or version hash, and redaction reason.
- [ ] Extract Maven `<scope>` and Gradle configuration names into normalized `dependencyScope`/`dependencyGroup` fields when statically visible.
- [ ] Emit gaps for non-literal Gradle declarations, missing local parent POMs, version catalogs, and unresolved interpolation when detected.

Tests:

- [ ] JVM build file extractor tests for Maven and literal Gradle package facts.
- [ ] JVM reduced-coverage tests for dynamic or unsupported build metadata.

## 5. Combined Dependency Report

- [ ] Project `PackageReferenced` into `package-config` terminal surfaces.
- [ ] Tighten package surface projection to exact package fact types or `surfaceKind=package-config`; do not rely on broad fact-type substring matching for new conclusions.
- [ ] Add package fields and caveats to `dependency-report.json`.
- [ ] Extend combined surface JSON additively for ecosystem, manifest kind, dependency scope, dependency group, version hash, metadata hash, and caveats if those fields are not already present.
- [ ] Add or polish package rows in `dependency-report.md`.
- [ ] Ensure Markdown does not expose raw scripts, snippets, credentials, or absolute paths.

Tests:

- [ ] Combined dependency report tests for package surfaces.
- [ ] Combine import fidelity test proving source package fact IDs survive combine without being rewritten.
- [ ] Private-path and secret guard coverage for report output.

## 6. Combined Diff

- [ ] Support `diff --scope surfaces --surface package-config`.
- [ ] Compare package identity separately from version/scope metadata; stable package keys must include a safe repository-relative manifest/project path and exclude version, dependency scope, and dependency group.
- [ ] Classify added, removed, changed, needs-review, no-evidence, selector no-match, and truncation cases.
- [ ] Add package-config caveats for hash-only evidence and volatile identity.

Tests:

- [ ] Added package.
- [ ] Removed package.
- [ ] Version/range changed.
- [ ] Scope/group changed.
- [ ] Hash-only version changed.
- [ ] Duplicate package identity downgrades to needs review.
- [ ] Same package declared in two different manifests under one source label remains two distinct surface identities.
- [ ] Hash-only and volatile-identity package caveats are present when package version evidence or identity is weak.
- [ ] Reduced coverage downgrades no-diff confidence.

## 7. Combined Impact

- [ ] Project package diff rows into `combined.impact.surface.v1` items.
- [ ] Use static package-surface wording only.
- [ ] Include package metadata and caveats in `impact-report.json`.
- [ ] Include compact package rows in `impact-report.md`.

Tests:

- [ ] Impact tests for added, removed, changed, and needs-review package surface rows.
- [ ] Assertion that impact text does not claim runtime loading, loaded packages, vulnerabilities, exploitability, security affected, or business impact.

## 8. Paths and Reverse Queries

- [ ] Support `paths --to-surface package-config`.
- [ ] Support `reverse --surface package-config` and exact `--surface-name` package selection.
- [ ] Keep unattached package facts as selected surfaces or explicit gaps.
- [ ] Reuse existing path edge rules and evidence tiers.
- [ ] Ensure selectors reject invalid edge kinds or unsupported package selector shapes with rule-backed gaps/errors consistent with existing commands.

Tests:

- [ ] Path query finds package terminal surfaces when graph attachment is credible.
- [ ] Path query emits gaps for unattached package surfaces.
- [ ] Reverse query selects package surfaces by name.
- [ ] Reverse query returns deterministic zero-path output for bogus package selector.

## 9. Contract Delta v2

- [ ] Accept dependency-surface changes with `surfaceKind=package-config`.
- [ ] Make `package-config` the canonical reducer surface kind for package facts, with any existing `package` helper updated or treated as an alias.
- [ ] Match by `surfaceName`/`packageName` and ecosystem, with optional source label, manifest kind, scope, and version fields.
- [ ] Update dependency-surface validation, specificity scoring, and matching to recognize the additive `packageName` alias for `surfaceKind=package-config`.
- [ ] Reject version-only selectors without package identity as invalid or selector gaps; treat `oldVersion`/`newVersion` as comparison metadata, not sufficient selectors.
- [ ] Reconcile `kind=package` and `kind=dependency-surface` package matching so one reduction does not double-report the same fact.
- [ ] Add optional path and reverse context only when stable selectors are derivable.

Tests:

- [ ] Contract delta package version change produces evidence-backed findings.
- [ ] Contract delta package-config selector works with `surfaceName=<packageName>` and with the additive `packageName` alias.
- [ ] Missing package under full coverage and reduced coverage produces distinct no-evidence classifications.
- [ ] Invalid selector produces rule-backed input gap.

## 10. Smoke and Determinism Validation

- [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
- [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] Run `npm run check --prefix src/typescript`.
- [ ] Run JVM tests with pinned Java command from `docs/VALIDATION.md`.
- [ ] Run Python tests in a temporary virtual environment.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run `./scripts/smoke-combined-paths.sh`.
- [ ] Run package-focused combine/report/paths/reverse/diff/impact smoke from `requirements.md`.
- [ ] Include a package diff/impact fixture or sample pair with a real version or scope change; the same-index smoke is not enough to prove changed rows.
- [ ] Compare repeated package report JSON output byte-for-byte for a stable fixture.

Exit criteria:

- All required outputs exist.
- Package surfaces appear in JSON/Markdown with rule IDs and evidence tiers.
- Reduced coverage and gaps are explicit.
- Generated artifacts contain no raw snippets, raw scripts, secrets, or developer-local absolute paths.

## 11. Final Review Checklist

- [ ] No source or report conclusion lacks evidence.
- [ ] No evidence lacks a rule ID.
- [ ] No rule lacks limitations.
- [ ] Every scan still requires repo and commit SHA.
- [ ] Partial analysis is labeled partial.
- [ ] Facts are deterministic and evidence-backed.
- [ ] JSON schemas are stable or additively extended.
- [ ] Docs/rule catalog changed if behavior changed.
- [ ] No LLM/vector/prompt classification was added.
- [ ] No push or PR creation unless explicitly requested.
