# Legacy .NET Toolchain Diagnostics Tasks

## Spec Authoring Tasks

- [x] 1. Draft requirements for the legacy .NET toolchain capability contract.
  Requirements: all.
  - [x] Define scope boundaries for deterministic diagnostics only.
  - [x] Capture static project/toolchain signals and downstream coverage needs.
  - [x] Keep public/private safety, evidence tiers, rule IDs, and partial
    analysis requirements explicit.

- [x] 2. Draft design for facts, derivation, reporting, and downstream
  integration.
  Requirements: all.
  - [x] Align with existing `legacy-build-environment-diagnostics` facts.
  - [x] Propose a reviewable MVP that starts with project/config metadata and
    coverage/report integration.
  - [x] Defer installers, deep remediation tooling, Visual Studio-specific
    probing, and runtime claims.

## Implementation Tasks

- [ ] IMPL-1. Add analyzer capability vocabulary and rule catalog entries.
  Requirements: 1, 3, 6.
  - [ ] Add `AnalyzerCapabilityDiagnostic` or equivalent fact type.
  - [ ] Add rule IDs for semantic, syntax fallback, project/config,
    package/restore, generated/design-time, legacy toolchain, and downstream
    coverage capability diagnostics.
  - [ ] Define the `legacy-dotnet-toolchain-diagnostics.v1` fact schema version
    constant for MVP facts.
  - [ ] Define `DownstreamNoEvidenceCoverage` under
    `analyzer.capability.downstream-coverage.v1` with
    `coverage-context-only` limitation and guidance that it is coverage context
    only, not absence, impact, no-impact, or runtime proof.
  - [ ] Define the inherited restore-failure category vocabulary from
    `build.environment.restore.v1` for capability derivation, including
    `NuGetRestoreFailed`, `CredentialRequired`,
    `PackageSourceUnavailable`, `UnsupportedPackageFormat`, and
    `PackageVersionUnavailable`.
  - [ ] Document limitations for every new rule ID in `rules/rule-catalog.yml`
    before emitting scanner or report facts.
  - [ ] Define closed vocabularies for capability code, kind, state, coverage
    effect, guidance code, and limitation code.
  - [ ] Ensure fact IDs and gap fingerprints use sanitized fields only.

- [ ] IMPL-2. Derive capability facts from existing scan evidence.
  Requirements: 1, 2, 3, 5, 6.
  - [ ] Read existing `BuildEnvironmentDiagnostic`, `AnalysisGap`,
    `BuildStatus`, scan manifest, and semantic-result state without parsing
    human report text.
  - [ ] Emit semantic and project-load capability states for successful,
    reduced, unavailable, and unknown coverage.
  - [ ] Emit syntax fallback and project/config inspection capability states.
  - [ ] Emit legacy framework, MSBuild/toolset, WebForms/WCF/ASMX/remoting
    shape, package/restore, and generated/design-time capability states.
  - [ ] Keep static toolchain signals informational unless observed scan
    behavior actually reduces coverage.
  - [ ] Preserve supporting fact IDs or safe gap/category codes with bounded
    deterministic ordering.

- [ ] IMPL-3. Integrate capability diagnostics with scan artifacts.
  Requirements: 1, 3, 5, 6.
  - [ ] Write facts to `facts.ndjson` through existing stable serialization.
  - [ ] Store/query capability facts in `index.sqlite` through existing fact
    storage or an additive documented view/table.
  - [ ] Add a deterministic `report.md` analyzer capability diagnostics
    section with conservative guidance and limitations.
  - [ ] Keep `scan-manifest.json` compatible while adding a stable capability
    summary only if needed for downstream consumers.
  - [ ] Ensure `logs/analyzer.log` and all artifacts use sanitized categories,
    not raw tool output.

- [ ] IMPL-4a. Preserve capability facts through combined indexes and source
  summaries.
  Requirements: 3, 4, 6.
  - [ ] Preserve capability facts and per-source summaries through combined
    indexes.
  - [ ] Surface capability gaps in combined source coverage summaries.
  - [ ] Add a combined-index/source-summary propagation test using a legacy
    fixture that asserts `LegacyProjectConfigInspection` and
    `LegacyFrameworkSignalDetected` facts remain queryable in the combined
    index with original rule IDs, evidence tiers, source labels, coverage
    labels, and commit SHAs.

- [ ] IMPL-4b. Add release-review reduced-coverage explanation.
  Requirements: 3, 4, 6.
  - [ ] Update release review so reduced toolchain capability remains
    coverage-relative in source/coverage and no-evidence explanations.
  - [ ] Assert release review does not upgrade to definite impact, definite
    no-impact, or clean absence because a toolchain hint exists.
  - [ ] Assert release review emits a schema/capability compatibility gap
    rather than silently dropping a capability fact with an unrecognized
    `schemaVersion`.
  - [ ] Preserve existing coverage-relative behavior in other downstream
    consumers that are not directly updated in the MVP.

- [ ] IMPL-5. Add focused fixtures and tests.
  Requirements: 2, 5, 6, 7.
  - [ ] Test SDK-style semantic success with no noisy legacy capability gap.
  - [ ] Test non-SDK-style .NET Framework project metadata and reduced semantic
    coverage.
  - [ ] Test `ToolsVersion`, `VisualStudioVersion`, import basename, and
    project type GUID capability mapping.
  - [ ] Test `packages.config`, `PackageReference`, lock file, `nuget.config`,
    and restore-not-requested behavior without claiming missing packages.
  - [ ] Test at least one sanitized explicit restore-failure category and
    assert raw source URLs, credentials, and command output do not leak.
  - [ ] Test Web Application, WebForms, WCF, ASMX, remoting, and generated/
    designer-file capability limits where deterministic.
  - [ ] Test project/build load failure where no precise capability fact can be
    derived and assert an explicit unknown capability gap is emitted.
  - [ ] Assert no capability fact has an evidence tier higher than its
    documented cap from the design table for every capability code;
    specifically assert `CSharpSemanticCompilation` and
    `ReferenceAssemblyResolution` are not emitted as `Tier1Semantic`.
  - [ ] Assert every `sourceScope` value is a safe relative scope or
    `workspace`, never a local absolute path.
  - [ ] Test clean SDK-style semantic success does not render an empty or noisy
    legacy capability diagnostics section in `report.md`.
  - [ ] Test unsafe values do not appear in facts, manifest, report, SQLite, or
    analyzer log artifacts.
  - [ ] Test deterministic ordering and fact ID stability by scanning the
    non-SDK-style legacy fixture twice at the same fixture commit.

- [ ] IMPL-6. Validate implementation.
  Requirements: 7.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run the CLI against at least one checked-in or generated legacy fixture
    and verify `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
    `report.md`, and `logs/analyzer.log`.
  - [ ] Run relevant pinned smoke checks from `docs/VALIDATION.md` or record an
    explicit deferral rationale.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.

## Deferred Follow-Ups

- Visual Studio installation probing, registry inspection, COM/design-time
  automation, or private local tool inventory.
- Automatic SDK/framework/reference-assembly installation or project migration.
- Deep MSBuild condition evaluation and imported target execution.
- Direct evidence graph/vault export, legacy smoke catalog, and evidence-pack
  schema-version presentation for `AnalyzerCapabilityDiagnostic`; when that
  work is selected, add closed-code consumption or schema compatibility-gap
  tests before claiming support.
- Runtime hosting, IIS, database, service, package registry, deployment, or
  production compatibility checks.
- Public site copy or capability-matrix promotion before public-safe validation
  artifacts exist.
