# Legacy .NET Toolchain Diagnostics Design

## Overview

This spec defines the next diagnostic layer after
`legacy-build-environment-diagnostics`. The existing implementation records
static build-environment facts such as target frameworks, toolset clues,
project formats, restore shape, generated-file gaps, and sanitized workspace
diagnostics. The new layer should turn those signals plus scan outcomes into a
stable analyzer-capability contract:

- What analyzer capability was attempted or available?
- What static old .NET project/toolchain evidence explains that state?
- Which capability gaps reduced semantic coverage?
- How should downstream reports, combined indexes, release review, evidence
  vault export, smoke catalogs, and reducers explain no-evidence outcomes?

The feature is still static, deterministic, and evidence-backed. It is not an
installer, migration assistant, runtime compatibility checker, or AI analysis
layer.

## Goals

- Emit machine-readable analyzer capability facts and gaps for legacy .NET
  scans.
- Preserve the existing `BuildEnvironmentDiagnostic` vocabulary as supporting
  evidence rather than replacing it.
- Make reduced coverage explainable in `scan-manifest.json`, `facts.ndjson`,
  `index.sqlite`, `report.md`, `logs/analyzer.log`, combined indexes, release
  review, evidence graph/vault export, smoke catalogs, and reducer reports.
- Keep all output deterministic, redacted, schema-compatible, and explicit
  about limitations.

## Non-Goals

- No SDK, framework, Visual Studio, MSBuild, NuGet, workload, Mono, or
  reference-assembly installation.
- No automatic restore retry, project migration, project-file rewriting, or
  remediation execution.
- No application build execution beyond existing scan behavior, no app startup,
  no IIS probing, no database connections, and no service calls.
- No runtime supportability, vulnerability, license, deployment, production
  usage, release approval, or clean/runnable conclusions.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  probabilistic model inference.
- No site work.

## Current State

Relevant implemented pieces on `dev`:

- `BuildEnvironmentDiagnostic` facts under:
  - `build.environment.target-framework.v1`
  - `build.environment.toolset.v1`
  - `build.environment.project-format.v1`
  - `build.environment.restore.v1`
  - `build.environment.generated-files.v1`
  - `build.environment.workspace-diagnostic.v1`
- Sanitized workspace and restore diagnostic categories before fact creation.
- `ScanManifest` coverage/build status and known gaps.
- Fallback syntax/config/SQL/WCF/WebForms/remoting/data extractors that continue
  after semantic gaps.
- Combined reports and reducers that already treat reduced coverage as weaker
  than clean full-coverage absence.

Missing pieces:

- A first-class fact/summary that says which analyzer capability was available,
  reduced, unavailable, or not requested.
- Stable coverage-effect codes that downstream consumers can use without
  parsing human report wording.
- Explicit references from no-evidence/reduced-coverage explanations back to
  legacy toolchain gaps.
- A compact schema for smoke catalogs and evidence exports to represent legacy
  toolchain capability without raw diagnostics.

## Proposed Fact Model

Add one primary fact type:

```text
AnalyzerCapabilityDiagnostic
```

Suggested rule IDs:

```text
analyzer.capability.semantic.v1
analyzer.capability.syntax-fallback.v1
analyzer.capability.project-config.v1
analyzer.capability.package-restore.v1
analyzer.capability.generated-design-time.v1
analyzer.capability.legacy-toolchain.v1
analyzer.capability.downstream-coverage.v1
```

The implementation may choose a smaller first slice, but every emitted rule ID
must be documented in `rules/rule-catalog.yml` with limitations before use.

Common properties:

| Property | Meaning |
| --- | --- |
| `capabilityCode` | Stable code such as `CSharpSemanticCompilation`, `SyntaxFallbackAvailable`, `LegacyProjectConfigInspection`, `LegacyNuGetRestoreAwareness`, or `GeneratedDesignerLinkage` |
| `capabilityKind` | `semantic`, `syntax-fallback`, `project-config`, `package-restore`, `generated-design-time`, `legacy-toolchain`, or `downstream-coverage` |
| `capabilityState` | `available`, `reduced`, `unavailable`, `not-requested`, `unknown`, or `not-applicable` |
| `coverageEffect` | `full-semantic`, `reduced-semantic`, `syntax-only`, `structural-only`, `config-only`, `unknown-gap`, or `informational` |
| `sourceScope` | Safe repo-relative project/config path, source label, or `workspace` |
| `projectStyle` | `sdk-style`, `non-sdk-style`, `legacy-web-application`, `non-csharp-project`, `unknown`, or omitted |
| `frameworkFamily` | Safe normalized family such as `.NET Framework`, `.NET`, `.NET Standard`, `ASP.NET Web Application`, `unknown`, or omitted |
| `supportingFactIds` | Bounded, sorted IDs for supporting `BuildEnvironmentDiagnostic`, `BuildStatus`, or `AnalysisGap` facts, encoded as a single deterministic string property |
| `supportingGapKinds` | Bounded, sorted safe gap/category codes when fact IDs are unavailable, encoded as a single deterministic string property |
| `strongestSupportingEvidenceTier` | Optional strongest tier among cited support facts, used so derived capability facts do not inflate their own tier |
| `limitationCode` | Stable limitation code such as `semantic-status-derived`, `project-config-static-only`, `syntax-fallback-only`, `restore-not-attempted`, `design-time-linkage-gap`, or `unknown-toolchain-gap` |
| `guidanceCode` | Conservative guidance code for reports, not an install command |
| `schemaVersion` | `legacy-dotnet-toolchain-diagnostics.v1` |

`schemaVersion` on each `AnalyzerCapabilityDiagnostic` fact is part of the MVP.
Define it as a constant alongside the fact vocabulary so downstream consumers
can branch safely without requiring manifest schema expansion.

TraceMap fact properties are string-valued. Any list-like capability property
must be serialized as a bounded, sorted, delimiter-safe string or count/hash
summary before fact creation, never as a JSON array value inside
`CodeFact.Properties`.

Initial guidance codes:

| Guidance code | Intended wording family |
| --- | --- |
| `UseSemanticEvidenceWhenAvailable` | Semantic evidence was available for the selected scope. |
| `TreatAsReducedCoverage` | Known gaps reduce confidence; no-evidence rows remain coverage-relative. |
| `UseSyntaxFallbackEvidence` | Syntax fallback evidence is available but not semantic proof. |
| `ReviewProjectConfigSignals` | Project/config metadata explains static capability only. |
| `RestoreNotAttemptedNoAbsenceClaim` | Explicit restore was not requested; dependency absence was not inferred. |
| `ReviewSanitizedRestoreFailure` | Explicit restore failed with a sanitized category. |
| `ReviewGeneratedDesignTimeCoverage` | Generated or design-time linkage is incomplete or unknown. |
| `ReviewLegacyToolchainSignals` | Legacy framework/toolset signals appear relevant for full semantic analysis. |
| `ReviewUnknownCapabilityGap` | TraceMap could not classify the capability state precisely. |

Initial limitation codes:

| Limitation code | Meaning |
| --- | --- |
| `semantic-status-derived` | Capability fact summarizes semantic status and is not a primary symbol observation. |
| `syntax-fallback-only` | Evidence is syntax/text fallback and cannot prove compiler-resolved behavior. |
| `project-config-static-only` | Project/config metadata was inspected without evaluating arbitrary build logic. |
| `restore-not-attempted` | Explicit restore was not requested and package absence is not inferred. |
| `restore-category-only` | Restore failure is represented by sanitized category only. |
| `design-time-linkage-gap` | Generated or design-time artifacts are missing, malformed, unlinked, or unknown. |
| `legacy-toolchain-static-signal` | Framework/toolset signal is static guidance, not local installation proof. |
| `unknown-toolchain-gap` | TraceMap could not prove the toolchain capability or cause. |
| `coverage-context-only` | Downstream coverage context only; does not prove absence, impact, no-impact, or runtime behavior. |

For workspace-wide capability facts without a precise file span, use `.` line
`1` or the existing workspace-scope convention, and set `sourceScope` to
`workspace`. Per-project capability facts should prefer the project/config file
span that supports the state.

Fact IDs must use the existing TraceMap `FactFactory` convention rather than a
bespoke ID algorithm. Capability facts should sanitize every field and property
before fact creation so raw native messages, local paths, usernames, remotes,
package sources, connection strings, config values, and snippets never feed fact
IDs or gap fingerprints. The deterministic `scanId` carries repository and
commit identity according to the shared adapter contract.

## Capability Codes

Initial code set:

| Code | Rule ID | Kind | State examples | Capability tier cap | Notes |
| --- | --- | --- | --- | --- | --- |
| `CSharpSemanticCompilation` | `analyzer.capability.semantic.v1` | semantic | `available`, `reduced`, `unavailable`, `unknown` | Tier2 or Tier4 | Derived status fact; cite Tier1 support via `strongestSupportingEvidenceTier`/supporting IDs rather than inflating the capability fact |
| `MSBuildProjectLoad` | `analyzer.capability.semantic.v1` | semantic | `available`, `reduced`, `unavailable`, `unknown` | Tier2 or Tier4 | References sanitized workspace/load gaps |
| `ReferenceAssemblyResolution` | `analyzer.capability.semantic.v1` | semantic | `available`, `unavailable`, `unknown` | Tier2 for available status, Tier4 for gaps | Does not prove local install inventory beyond observed scan behavior |
| `SyntaxFallbackAvailable` | `analyzer.capability.syntax-fallback.v1` | syntax-fallback | `available`, `not-applicable`, `unknown` | Tier3 or Tier4 | Indicates fallback evidence was produced or could run |
| `LegacyProjectConfigInspection` | `analyzer.capability.project-config.v1` | project-config | `available`, `reduced`, `unknown` | Tier2 or Tier4 | Static XML/project/config metadata only |
| `LegacyFrameworkSignalDetected` | `analyzer.capability.legacy-toolchain.v1` | legacy-toolchain | `available`, `unknown`, `not-applicable` | Tier2 or Tier4 | Normalized target framework/config hints; static signals may use `coverageEffect = informational` |
| `LegacyMSBuildToolsetSignalDetected` | `analyzer.capability.legacy-toolchain.v1` | legacy-toolchain | `available`, `unknown`, `not-applicable` | Tier2 or Tier4 | ToolsVersion, VS version, imports, project type categories; static signals may use `coverageEffect = informational` |
| `LegacyNuGetRestoreAwareness` | `analyzer.capability.package-restore.v1` | package-restore | `available`, `not-requested`, `reduced`, `unknown` | Tier2/Tier3/Tier4 | Shape is structural; explicit restore failure categories may be Tier3 |
| `GeneratedDesignerLinkage` | `analyzer.capability.generated-design-time.v1` | generated-design-time | `available`, `reduced`, `unknown` | Tier2/Tier3/Tier4 | Present/missing/unlinked/malformed design-time evidence |
| `LegacyWebStackShape` | `analyzer.capability.legacy-toolchain.v1` | legacy-toolchain | `available`, `reduced`, `unknown` | Tier2/Tier4 | WebForms/WCF/ASMX web app markers without runtime claims |
| `LegacyRemotingShape` | `analyzer.capability.legacy-toolchain.v1` | legacy-toolchain | `available`, `reduced`, `unknown` | Tier2/Tier4 | Remoting/config/project-shape markers without runtime claims |
| `DownstreamNoEvidenceCoverage` | `analyzer.capability.downstream-coverage.v1` | downstream-coverage | `reduced`, `unknown`, `not-applicable` | Tier4 | Uses `coverage-context-only`; allows release review to cite reduced coverage in no-evidence rows without proving absence or impact |

The implementation should start with table-driven derivation from existing scan
manifest state, `BuildEnvironmentDiagnostic` facts, and `AnalysisGap` facts. It
should avoid parsing human report text.

## Derivation Rules

Suggested first-slice derivation:

1. Group existing facts and gaps by safe project/config scope where possible.
2. Emit `LegacyProjectConfigInspection` when project/config inventory and
   project-file reader evidence exists.
3. Emit `LegacyFrameworkSignalDetected` from target-framework diagnostics and
   safe config framework hints.
4. Emit `LegacyMSBuildToolsetSignalDetected` from toolset/project-format
   diagnostics.
5. Emit `LegacyNuGetRestoreAwareness` from restore-shape diagnostics and scan
   restore option state. `not-requested` belongs in capability/manifest context,
   not as a missing-package conclusion. Sanitized explicit restore-failure
   categories are inherited from `build.environment.restore.v1` for MVP, such
   as `NuGetRestoreFailed`, `CredentialRequired`,
   `PackageSourceUnavailable`, `UnsupportedPackageFormat`, and
   `PackageVersionUnavailable`; this spec does not introduce a parallel restore
   category rule unless a later implementation needs additional values.
6. Emit `GeneratedDesignerLinkage` from generated-file diagnostics and design
   metadata extractors.
7. Emit `CSharpSemanticCompilation` and `MSBuildProjectLoad` from semantic
   extraction state, sanitized workspace diagnostics, and build status.
8. Emit `SyntaxFallbackAvailable` when syntax/config fallback ran after reduced
   semantic coverage or when the adapter explicitly reports syntax-only
   analysis for a scope.
9. Emit `DownstreamNoEvidenceCoverage` only when reduced coverage should be
   propagated to no-evidence explanations and no more specific downstream gap
   already covers the same scope.

Derivation must be deterministic and bounded. If multiple support facts exist,
sort by rule ID, safe path, line span, and fact ID; include a bounded list and
summary counts when needed. Do not choose arbitrary winners for ambiguous
projects, duplicated imported targets, or conditional metadata.

## Coverage Semantics

Capability states must mean:

| State | Meaning |
| --- | --- |
| `available` | The scanner observed successful capability evidence for the scope |
| `reduced` | The scanner observed evidence, but known gaps cap confidence |
| `unavailable` | Existing scan behavior observed a deterministic unsupported/missing capability category |
| `not-requested` | The capability was optional and was not requested, such as explicit restore |
| `unknown` | TraceMap could not prove availability or cause |
| `not-applicable` | The capability does not apply to the safe scope |

Coverage effects must be conservative:

- `full-semantic` is allowed only when selected scan scope has semantic coverage
  and no known gaps that reduce it.
- `reduced-semantic` means semantic evidence exists but is incomplete.
- `syntax-only` and `structural-only` are not clean absence evidence.
- `unknown-gap` is the default for unclassified failures.
- `informational` static toolchain signals do not by themselves reduce coverage
  unless scan behavior observed a load/resolution/build gap.

Rule catalog limitations for capability rules should explicitly state that a
capability diagnostic is derived status or coverage context. Its evidence tier
describes the diagnostic support and must not be read as a new primary symbol,
runtime, or impact observation. When Tier1 support exists, preserve that through
supporting fact IDs or `strongestSupportingEvidenceTier`.

## Report And Manifest Presentation

`report.md` should add a deterministic section such as:

```text
## Analyzer Capability Diagnostics
```

Rows should include:

- capability code
- state
- coverage effect
- evidence tier
- rule ID
- source scope
- supporting fact/gap count
- guidance text
- limitation text

Guidance wording should be conservative:

- "Compatible reference assemblies appear necessary for full semantic analysis."
- "Legacy project/config inspection was available, but semantic coverage is
  reduced."
- "Explicit restore was not requested; package absence was not inferred."
- "Generated/designer linkage is incomplete; related legacy evidence remains
  reduced."

Avoid:

- "Install .NET Framework 4.x"
- "This repository requires Visual Studio"
- "Dependencies are missing"
- "The repository is clean/runnable"
- "No impact"

`scan-manifest.json` may receive additive fields if needed, for example:

```json
{
  "capabilitySummaryVersion": "legacy-dotnet-toolchain-diagnostics.v1",
  "analyzerCapabilities": [
    {
      "capabilityCode": "CSharpSemanticCompilation",
      "capabilityState": "reduced",
      "coverageEffect": "reduced-semantic",
      "ruleId": "analyzer.capability.semantic.v1",
      "evidenceTier": "Tier4Unknown",
      "supportingFactCount": 2
    }
  ]
}
```

Manifest additions must be sorted, bounded, redacted, and compatible with
existing readers. If manifest schema churn is too risky for MVP, keep the
primary contract in facts/SQLite/report and add manifest fields in a later task.

## SQLite And Combined Indexes

MVP storage can rely on the existing `facts` table because the proposed shape is
property-based and queryable through `fact_type = 'AnalyzerCapabilityDiagnostic'`.
An additive view such as `analyzer_capability_diagnostics` is optional if it
materially simplifies downstream readers.

Combined indexes should preserve these facts like any other source fact. Source
summary/report code should add optional rollups:

- source label
- commit SHA
- analysis level
- build status
- capability counts by state
- reduced/unknown capability codes
- supporting rule IDs and tiers

Downstream reports should avoid reinterpreting raw project data; they should
consume capability facts and coverage labels.

## Downstream Consumer Design

Consumer expectations:

- Combined dependency report: source coverage section lists reduced/unknown
  toolchain capability codes for each source when direct presentation is in
  scope.
- MVP downstream path: update release review and prove no-evidence remains
  coverage-relative under reduced capability.
- Other reports and exporters: preserve compatible facts and existing
  coverage-relative behavior until they explicitly implement direct capability
  presentation.
- Evidence graph/vault export, smoke catalog, and evidence-pack workflows:
  deferred for direct presentation in this MVP; when direct support is
  implemented, capability diagnostics become safe source metadata, gap nodes,
  limitation nodes, or closed-code expectations with original rule IDs and
  supporting IDs.
- Release review: no-evidence classifications under reduced capability stay
  coverage-relative and can cite `AnalyzerCapabilityDiagnostic` support in the
  MVP.

If a consumer cannot understand the schema, it should emit a compatibility gap
or preserve the raw fact as an unknown fact type rather than dropping the
diagnostic silently.

## Privacy And Redaction

Reuse existing safe relative paths and hash helpers. New diagnostic properties
must use closed vocabularies or redaction hashes. Do not render or store:

- local absolute paths
- home directory fragments
- usernames, hostnames, organization names, private sample names
- raw remotes or branch names
- package source URLs, credentials, tokens, or secrets
- raw connection strings, config values, SQL, endpoint URLs, SOAP actions, or
  snippets
- raw native MSBuild/NuGet/Roslyn diagnostic output

Hash input, when allowed, should follow the same context-separated shape used
by build-environment diagnostics: diagnostic domain, property key, diagnostic
code, then raw value. This spec intentionally uses its own domain prefix so
toolchain capability redaction hashes cannot be confused with lower-level build
environment hashes:

```text
legacy-dotnet-toolchain|<property-key>|<capability-code>|<raw-value>
```

Secret-like or low-entropy values should be category-only rather than hashed.
Redaction hashes are metadata, not proof that a value is meaningful.

## MVP Slice

Recommended first implementation slice:

1. Add rule catalog entries, schema version constant, and
   `AnalyzerCapabilityDiagnostic` fact type.
2. Derive capability facts from existing `BuildEnvironmentDiagnostic`,
   `AnalysisGap`, `BuildStatus`, and manifest coverage state.
3. Render a report section and preserve facts in SQLite.
4. Add combined-index/source-summary compatibility for capability facts.
5. Add one downstream reduced-coverage explanation path in release review,
   leaving reducer-specific presentation as a follow-up unless implementation
   work discovers release review cannot consume the capability facts safely.
6. Add focused synthetic fixtures for SDK-style success, legacy non-SDK
   reduced coverage, packages.config restore-not-requested, Web Application
   targets, generated/designer gaps, and unsafe value redaction.

Defer:

- Visual Studio installation probing.
- Automatic remediation/install guidance beyond conservative report text.
- Deep MSBuild condition evaluation.
- Full manifest schema expansion if facts/report/SQLite provide enough MVP
  value.
- Broad downstream presentation in every report until the primary contract is
  stable.

## Validation Plan

Implementation validation should include:

- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/TraceMap.sln`
- Focused tests for capability derivation and report rendering.
- CLI scan against at least one checked-in or generated legacy fixture proving
  all required artifacts exist.
- SQLite query asserting `AnalyzerCapabilityDiagnostic` rows are stored and
  redacted.
- Combined-index smoke proving source capability facts survive combine.
- At least one downstream report/reducer smoke proving no-evidence remains
  coverage-relative under reduced toolchain capability.
- `./scripts/check-private-paths.sh`
- `git diff --check`

If a pinned legacy smoke from `docs/VALIDATION.md` is not relevant to the slice,
record an explicit deferral in the implementation state note.
