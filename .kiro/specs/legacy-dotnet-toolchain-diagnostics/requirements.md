# Legacy .NET Toolchain Diagnostics Requirements

## Introduction

TraceMap now emits deterministic build-environment diagnostics for many legacy
.NET project signals, but realistic old repositories still need a clearer
contract for analyzer capability: what the scanner could analyze, which static
toolchain signals were observed, which capability was missing or downgraded,
and how that reduced coverage for reports, combined indexes, legacy smoke
catalogs, release review, evidence graph/vault export, and reducer
explanations.

This feature is a deterministic diagnostics and coverage-contract layer. It
does not install SDKs, migrate projects, execute application builds beyond
existing scan behavior, connect to databases, fetch design-time metadata, or
claim runtime success. It must preserve TraceMap's evidence model: no
conclusion without rule-backed evidence, no partial analysis hidden as clean
coverage, and no raw private values in shareable artifacts.

Public claim level: hidden until implemented and validated against safe
fixtures, redacted legacy smoke summaries, and downstream report consumers.

## Scope

In scope:

- Static analyzer-capability facts and gaps derived from existing scan behavior
  plus old .NET solution, project, package, and config artifacts.
- Project/config metadata signals such as `TargetFrameworkVersion`,
  `TargetFramework`, `TargetFrameworks`, `ToolsVersion`,
  `VisualStudioVersion`, MSBuild imports/targets, SDK-style versus legacy
  project shape, `packages.config`, `PackageReference`, binding redirects,
  `web.config`/`app.config` framework hints, and legacy WebForms/WCF/ASMX/
  remoting project markers.
- Deterministic capability states for semantic analysis, syntax fallback,
  config/project inspection, restore awareness, generated/design-time evidence,
  and legacy framework/project-shape support.
- Explicit reduced-coverage notes consumable by scan reports, combined indexes,
  legacy smoke catalog expectations, release-review sections, evidence
  graph/vault export, and reducer no-evidence explanations.
- Rule catalog additions with documented limitations for every new rule ID.
- Tests using synthetic or checked-in public-safe fixtures only.

Out of scope:

- Installing .NET Framework, SDKs, Visual Studio, MSBuild, NuGet, Mono,
  workloads, or reference assemblies.
- Project/package migrations, code fixes, project-file rewrites, or automatic
  remediation commands.
- Remote package registry queries, database connections, service calls, live
  application startup, IIS/ASP.NET hosting checks, or runtime compatibility
  certification.
- Visual Studio-specific probing that requires private local installation
  layout, COM automation, registry crawling, or design-time execution.
- Vulnerability, license, production usage, deployment, supportability, or
  "repo is clean/runnable" conclusions.
- LLM calls, embeddings, vector databases, prompt-based classification, or
  probabilistic inference in TraceMap core.
- Site pages, site specs, public marketing copy, or static-site files.

## Requirements

### Requirement 1: Analyzer Capability Facts

**User Story:** As a maintainer scanning an old .NET repository, I want
TraceMap to report which analyzer capabilities were available so that partial
results are understandable and machine-readable.

#### Acceptance Criteria

1. WHEN `tracemap scan` completes for a .NET repository THEN it SHALL emit
   deterministic analyzer capability facts describing semantic analysis,
   syntax fallback, project/config inspection, package/restore awareness,
   generated/design-time inspection, and legacy framework/project-shape
   support where those capabilities were attempted, unavailable, reduced, or
   intentionally not requested.
2. WHEN a capability fact is emitted THEN it SHALL include a stable rule ID,
   evidence tier, safe relative file path or workspace scope, line span where
   available, commit SHA, extractor ID, extractor version, capability code,
   capability state, coverage effect, and documented limitation code.
3. WHEN capability diagnostics summarize compiler-resolved Roslyn evidence THEN
   they SHALL preserve the strongest supporting evidence tier separately where
   useful, but the derived capability fact itself SHOULD remain capped at
   `Tier2Structural` unless a future rule documents why the capability fact is a
   primary semantic observation.
4. WHEN capability evidence is derived from project/config/package structure
   THEN the strongest allowed capability fact tier is `Tier2Structural`.
5. WHEN capability evidence is derived from syntax-only extraction or sanitized
   diagnostic text THEN the strongest allowed tier is `Tier3SyntaxOrTextual`.
6. WHEN TraceMap cannot prove whether a capability was available THEN it SHALL
   emit an `AnalysisGap` or capability fact capped at `Tier4Unknown` rather
   than inventing a confident state.
7. WHEN existing `BuildEnvironmentDiagnostic` facts already describe a static
   signal THEN capability facts SHALL reference or summarize that evidence by
   safe supporting fact IDs or codes instead of duplicating raw values.
8. WHEN no capability facts can be produced but project load/build failed THEN
   TraceMap SHALL emit an explicit unknown capability gap so the failure is not
   hidden.

### Requirement 2: Legacy Project And Toolchain Signal Coverage

**User Story:** As a user with a legacy solution, I want TraceMap to connect old
project metadata to capability limits without claiming the machine has or lacks
specific installed tools.

#### Acceptance Criteria

1. WHEN `.csproj`, `.sln`, `.props`, `.targets`, `Directory.Build.*`,
   `packages.config`, `packages.lock.json`, `nuget.config`, `web.config`, or
   `app.config` files contain supported old .NET signals THEN capability
   diagnostics SHALL cite the static source evidence and safe line spans.
2. WHEN `TargetFrameworkVersion`, legacy `TargetFramework`/`TargetFrameworks`,
   or `supportedRuntime`/framework hints are detected THEN diagnostics SHALL
   normalize safe framework family values and SHALL NOT claim the runtime or
   reference assemblies are installed unless existing scan behavior observed a
   deterministic load/resolution result.
3. WHEN `ToolsVersion`, `VisualStudioVersion`, known MSBuild import basenames,
   project type GUID categories, Web Application targets, or SDK-style markers
   are present THEN diagnostics SHALL identify project/toolchain shape using
   conservative wording such as "appears relevant" or "may be required for full
   semantic analysis."
4. WHEN both SDK-style and legacy projects exist in one repo THEN diagnostics
   SHALL remain per-project or per-scope where evidence permits and SHALL NOT
   collapse the repository into a single toolchain requirement.
5. WHEN project/config metadata is conditional, imported, unsupported, or
   unresolved THEN TraceMap SHALL emit reduced precision or an explicit gap,
   not an evaluated MSBuild/runtime conclusion.
6. WHEN old WebForms, WCF, ASMX, remoting, setup/deployment, database project,
   service-reference, or generated/designer shapes are detected THEN
   diagnostics SHALL describe analyzer capability and limitations without
   proving runtime deployment, service activation, event firing, or proxy
   freshness.

### Requirement 3: Coverage Effects And Manifest Integration

**User Story:** As a report consumer, I want capability diagnostics to explain
coverage reduction consistently across scan artifacts.

#### Acceptance Criteria

1. WHEN semantic project load, compilation, reference assembly resolution, SDK
   resolution, MSBuild registration, generated-file linkage, or explicit restore
   behavior reduces analysis confidence THEN the scan manifest SHALL preserve a
   reduced/partial coverage status and stable known-gap summary.
2. WHEN capability diagnostics are present THEN `report.md` SHALL include a
   deterministic analyzer capability or legacy toolchain diagnostics section
   with capability code, state, coverage effect, evidence tier, rule ID,
   supporting fact/gap IDs where available, safe source scope, guidance, and
   limitation text.
3. WHEN coverage is reduced by missing or unknown toolchain capability THEN
   no-evidence report rows and reducer explanations SHALL be able to cite that
   coverage state instead of implying clean absence.
4. WHEN no relevant capability diagnostics exist and semantic analysis succeeds
   for the selected scan scope THEN reports SHOULD avoid noisy empty legacy
   capability sections unless current report conventions require empty sections.
5. WHEN capability summaries are stored in SQLite THEN existing fact tables MAY
   be sufficient, but any additive view/table SHALL be documented and SHALL NOT
   break existing readers.
6. WHEN analyzer logs include capability information THEN shareable artifact
   logs SHALL use sanitized categories, not raw native tool output.

### Requirement 4: Downstream Consumer Compatibility

**User Story:** As a maintainer of TraceMap reports and exports, I want
toolchain capability gaps to flow into downstream artifacts without each
consumer reinventing coverage logic.

#### Acceptance Criteria

1. WHEN a combined index includes sources with toolchain capability diagnostics
   THEN the MVP SHALL preserve per-source capability facts and source coverage
   summaries with capability states, rule IDs, evidence tiers, coverage labels,
   commit SHAs, and supporting IDs where schema-compatible.
2. WHEN the MVP updates release review as the selected downstream explanation
   path and that path encounters reduced toolchain capability THEN it SHALL
   keep no-evidence conclusions coverage-relative and SHALL emit or surface
   rule-backed gaps instead of claiming absence.
3. WHEN downstream consumers are not yet updated for direct capability
   presentation THEN they SHALL preserve existing coverage-relative behavior and
   SHALL NOT drop raw capability facts from compatible fact/index flows.
4. WHEN combined dependency reports, path reports, reverse reports,
   snapshot/diff reports, impact reports, release-review reports, evidence
   graph/vault export, smoke catalog, or evidence-pack workflows later claim
   direct support for this capability schema THEN they SHALL represent
   capability diagnostics with original rule IDs, evidence tiers, coverage
   labels, commit SHAs, safe supporting IDs, closed capability codes, and
   limitation codes.
5. WHEN a downstream consumer with declared capability-schema support cannot
   read the capability schema version THEN it SHALL emit a
   schema/capability-compatibility gap rather than dropping the diagnostic
   silently.
6. WHEN reducer or release-review explanations cite capability gaps THEN they
   SHALL use language such as `UnknownAnalysisGap`,
   `NoEvidenceReducedCoverage`, or another existing coverage-relative
   classification and SHALL NOT upgrade to definite impact or definite
   no-impact because a toolchain hint exists.

### Requirement 5: Restore And Design-Time Dependency Boundaries

**User Story:** As a user, I want TraceMap to distinguish package/config shape
from actual restore or design-time execution.

#### Acceptance Criteria

1. WHEN `packages.config`, `PackageReference`, lock files, or `nuget.config`
   are present THEN capability diagnostics SHALL report package-management
   shape and restore-awareness state using safe metadata only.
2. WHEN restore is not requested THEN TraceMap SHALL describe restore as not
   attempted only in manifest/report scan-option context and SHALL NOT claim
   dependencies are missing.
3. WHEN explicit restore is requested and fails through existing scan behavior
   THEN capability diagnostics SHALL use sanitized deterministic categories
   such as restore failed, credential required, package source unavailable,
   unsupported package format, or package version unavailable only when the
   category is supported.
4. WHEN design-time artifacts such as `.svcmap`, `.wsdl`, `.xsd`,
   `.designer.cs`, `.resx`, `.settings`, `.aspx`, `.ascx`, `.master`, or
   generated proxies are missing, malformed, excluded, or unlinked THEN
   diagnostics SHALL explain the analyzer capability effect without claiming
   the generated/runtime artifact does or does not exist outside the repository.
5. WHEN package sources, credentials, local package caches, config values,
   generated files, or native tool output include unsafe values THEN facts,
   reports, SQLite rows, logs, and manifests SHALL hash, omit, or category-label
   them before fact IDs or gap fingerprints are computed.

### Requirement 6: Privacy, Redaction, And Determinism

**User Story:** As a reviewer, I want diagnostics to be safe to share and stable
across repeated scans of the same commit.

#### Acceptance Criteria

1. Capability diagnostics SHALL NOT store raw source snippets by default.
2. Capability diagnostics SHALL NOT store raw SQL, connection strings, config
   values, secrets, credentials, package source URLs, raw remotes, local
   absolute paths, usernames, hostnames, branch names, private sample names, or
   raw native diagnostic output in shareable artifacts.
3. Diagnostic fact IDs and gap fingerprints SHALL follow TraceMap's established
   fact ID conventions, using sanitized fact fields and properties only; the
   existing deterministic `scanId` carries repository identity, commit identity,
   scan scope, and adapter signature, so this spec SHALL NOT require a bespoke
   fact-ID algorithm or direct extractor-version input unless a future
   compatibility spec changes the shared convention.
4. Diagnostics SHALL be sorted deterministically in facts, SQLite, reports, and
   tests by source scope, capability code, capability state, line span, rule ID,
   and fact ID.
5. Redaction hashes, when used, SHALL use documented context-separated input
   and SHALL be treated only as redaction metadata, not stronger evidence.
6. Secret-like, credential-like, low-entropy, enumerable private, and
   source-derived unsafe values SHALL be omitted or category-only rather than
   hashed.
7. New schema fields SHALL be additive where possible, with stable version
   fields when consumers need to branch safely.
8. Rule catalog entries SHALL document limitations for every new rule ID before
   any scanner/report implementation is considered complete.

### Requirement 7: Validation And Fixture Coverage

**User Story:** As a contributor, I want focused tests proving old-toolchain
diagnostics without relying on a developer's private Visual Studio or .NET
Framework installation.

#### Acceptance Criteria

1. WHEN implementation begins THEN tests SHALL use synthetic checked-in
   fixtures or generated temporary repositories that model legacy project
   metadata without requiring obsolete SDKs, Visual Studio, IIS, databases, or
   remote package feeds.
2. WHEN fixture scans intentionally fail semantic loading THEN they SHALL still
   emit syntax/config/project evidence, capability diagnostics, `AnalysisGap`
   facts, reduced coverage labels, and all required scan artifacts.
3. WHEN downstream compatibility is implemented THEN tests SHALL cover at least
   scan report rendering, SQLite fact/query availability, combined index source
   summary propagation, and one reducer or release-review explanation that
   remains coverage-relative.
4. WHEN redaction is tested THEN fixtures SHALL plant unsafe-looking local
   paths, raw remotes, connection strings, config values, package source URLs,
   and secret-like values and SHALL assert they do not appear in generated
   artifacts.
5. WHEN language-adapter behavior changes THEN validation SHALL follow
   `docs/VALIDATION.md`, including .NET build/test checks and any relevant
   pinned smoke checks, or SHALL record an explicit deferral rationale.
