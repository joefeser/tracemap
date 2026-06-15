# Legacy Build Environment Diagnostics Requirements

## Introduction

TraceMap already labels failed or partial project load as reduced coverage and
continues with syntax/config fallback. Very old .NET repositories need a more
actionable diagnostic layer that explains why semantic coverage was reduced:
declared target frameworks, project style, required SDK/runtime/MSBuild/toolset
clues, restore blockers, unsupported project formats, Web Application project
quirks, and generated/designer-file gaps.

The feature is deterministic scan diagnostics only. It must emit
machine-readable facts and report sections backed by rule IDs, evidence tiers,
file paths, line spans, commit SHA, and extractor versions. It must not install
tooling, mutate repositories, fetch packages by default, or make runtime
compatibility claims.

Public claim level: hidden until implemented and validated against safe fixtures
or redacted local summaries.

## Scope

In scope:

- Static inspection of solution, project, config, package, lock, restore, and
  generated-file indicators that explain build/project-load coverage.
- Classification of environment diagnostics into deterministic gap codes and
  evidence-backed guidance.
- Report and schema additions that make diagnostics visible in `facts.ndjson`,
  `index.sqlite`, `scan-manifest.json`, `report.md`, and `logs/analyzer.log`.
- Tests using synthetic or checked-in public fixtures only.
- Conservative guidance that states what appears necessary, not what users must
  install.

Out of scope:

- Installing SDKs, runtimes, Visual Studio, MSBuild, NuGet, Mono, or workload
  components.
- Fetching remote packages or remote build metadata except through existing
  explicit restore behavior.
- Rewriting project files or package references.
- Claiming a repository is clean when project load/build failed.
- Runtime compatibility, deployment, package vulnerability, or production usage
  conclusions.
- LLM calls, embeddings, vector databases, prompt-based classification, or
  probabilistic model inference in TraceMap core.
- Site pages, site specs, or public marketing copy.

## Requirements

### Requirement 1: Environment Diagnostic Facts

**User Story:** As a maintainer scanning a legacy .NET repository, I want
TraceMap to emit structured diagnostics that explain reduced coverage instead of
only saying project load failed.

#### Acceptance Criteria

1. WHEN `tracemap scan` inventories a .NET solution or project THEN it SHALL
   emit deterministic environment diagnostic facts for supported indicators
   found in solution, project, package, config, and generated-file metadata.
2. WHEN a diagnostic is emitted THEN it SHALL include a stable rule ID, evidence
   tier, file path, line span where available, commit SHA, extractor ID, and
   extractor version.
3. WHEN evidence comes from declared files or known project structure THEN the
   diagnostic SHALL be no stronger than `Tier2Structural`.
4. WHEN evidence comes from syntax/text fallback or diagnostic message parsing
   THEN the diagnostic SHALL be no stronger than `Tier3SyntaxOrTextual`.
5. WHEN TraceMap cannot prove a cause or supported remediation THEN it SHALL
   emit an `AnalysisGap` with `Tier4Unknown`, not a guessed diagnosis.
6. WHEN multiple diagnostics apply THEN TraceMap SHALL preserve each diagnostic
   as a separate fact and SHALL NOT collapse them into a single untraceable
   summary string.
7. WHEN diagnostics are stored in SQLite THEN they SHALL be queryable through
   existing fact tables or a documented additive table/view without breaking
   existing readers.
8. WHEN existing workspace or restore gap paths currently emit raw diagnostic
   message properties THEN the implementation SHALL migrate those emissions to
   sanitized categories at gap construction or restore-output capture time,
   before `ScanEngine.GetGapMessage`, manifest `KnownGaps`, fact creation, or
   fact ID hashing can observe raw text.

### Requirement 2: Framework, SDK, Runtime, and Toolset Clues

**User Story:** As a user with an old codebase, I want TraceMap to tell me which
target frameworks and build tooling appear relevant when the current machine
cannot load the project.

#### Acceptance Criteria

1. WHEN project files declare `TargetFramework`, `TargetFrameworks`,
   `TargetFrameworkVersion`, or equivalent legacy framework metadata THEN
   diagnostics SHALL preserve normalized target framework values and the source
   file span.
2. WHEN project files declare old-style MSBuild metadata such as
   `ToolsVersion`, `VisualStudioVersion`, `ProjectTypeGuids`,
   `Import` targets, `Sdk`, or workload-like imports THEN diagnostics SHALL
   record the indicator without evaluating arbitrary imported build logic.
3. WHEN solution or project metadata indicates a Visual Studio-era project type
   or toolset requirement THEN guidance SHALL use language such as "appears to
   require" or "likely needs compatible tooling" and SHALL cite the supporting
   indicator.
4. WHEN a diagnostic maps a target framework to a known SDK/runtime family THEN
   it SHALL document that the mapping is static guidance and not proof that the
   runtime is installed, supported, secure, or sufficient.
5. WHEN the repository targets frameworks that may not be installable on the
   current platform THEN TraceMap SHALL label the scan partial/reduced and keep
   syntax/config scanning available.
6. WHEN old and modern projects coexist THEN diagnostics SHALL be per-project
   where evidence permits and SHALL NOT imply the entire repository has one
   toolchain requirement.

### Requirement 3: NuGet and Restore Blockers

**User Story:** As a maintainer, I want restore-related blockers to be visible
as evidence-backed gaps without leaking package sources or config values.

#### Acceptance Criteria

1. WHEN `packages.config`, `PackageReference`, `packages.lock.json`,
   `nuget.config`, `packages` directories, or legacy restore markers are
   present THEN diagnostics SHALL summarize safe metadata such as manifest kind,
   package manager shape, and target framework where available.
2. WHEN restore is not requested THEN diagnostics SHALL state that package
   resolution was not attempted only as scan-option state in manifest/report
   coverage context and SHALL NOT emit a standalone diagnostic fact or claim
   packages are missing.
3. WHEN explicit restore is requested and fails THEN diagnostics SHALL capture a
   sanitized category such as `NuGetRestoreFailed`, `PackageSourceUnavailable`,
   `CredentialRequired`, `PackageVersionUnavailable`, or
   `UnsupportedPackageFormat` when the category is deterministically supported.
4. WHEN restore diagnostics include source URLs, credentials, paths, package
   source names, config values, or literal command output THEN reports and facts
   SHALL hash or omit unsafe values and keep only safe categories/counts.
5. WHEN package metadata is conditional, imported, or unresolved THEN TraceMap
   SHALL emit reduced precision guidance or an `AnalysisGap`, not a resolved
   dependency claim.

### Requirement 4: Unsupported Project Formats and Web Application Quirks

**User Story:** As a user scanning old ASP.NET, WebForms, WCF, or desktop
projects, I want TraceMap to identify project-format blockers while preserving
fallback evidence.

#### Acceptance Criteria

1. WHEN project metadata indicates unsupported or legacy formats such as old
   non-SDK-style C# projects, ASP.NET Web Application projects, setup/deployment
   projects, database projects, service-reference-heavy projects, or other
   unsupported project type GUIDs THEN diagnostics SHALL emit a structural
   project-format fact or gap.
2. WHEN Web Application project imports, project type GUIDs, or web.config
   markers suggest Visual Studio or Web targets are required THEN diagnostics
   SHALL record a conservative guidance row and SHALL NOT claim runtime IIS
   deployment behavior.
3. WHEN `.designer.cs`, `.resx`, `.settings`, `.svcmap`, `.wsdl`, `.xsd`,
   `.aspx`, `.ascx`, `.master`, or other generated/design-time files are
   missing, malformed, excluded, or present without semantic linkage THEN
   diagnostics SHALL label the generated/designer evidence gap.
4. WHEN generated/designer gaps reduce confidence in WebForms/WCF/event evidence
   THEN report sections SHALL connect the gap to coverage limitations without
   suppressing syntax/config facts that were found.
5. WHEN a project format is unknown THEN TraceMap SHALL emit
   `UnknownLegacyProjectFormat` or equivalent gap evidence instead of selecting
   an arbitrary known format.

### Requirement 5: Sanitized Workspace and Tool Diagnostics

**User Story:** As a reviewer, I want Roslyn/MSBuild load failures to be useful
without exposing local machine details.

#### Acceptance Criteria

1. WHEN MSBuildWorkspace, MSBuild registration, SDK resolution, runtime
   resolution, reference assembly resolution, or compilation creation fails THEN
   TraceMap SHALL capture deterministic sanitized diagnostic categories.
2. WHEN native tool output includes absolute paths, usernames, private
   repository names, remotes, package source URLs, connection strings, config
   values, or source snippets THEN TraceMap SHALL omit or hash them before
   writing facts, reports, logs intended for artifacts, or manifest gaps.
3. WHEN a raw diagnostic cannot be safely categorized THEN TraceMap SHALL emit a
   generic category and keep detailed raw output out of committed/shareable
   artifacts.
4. WHEN logs contain raw tool output needed for local debugging THEN the spec
   implementation SHALL either sanitize it before artifact writing or gate it
   behind an explicit local-only option documented as unsafe for sharing.
5. WHEN sanitization removes details THEN reports SHALL say details were
   redacted and keep the rule ID/gap category visible.

### Requirement 6: Report and Manifest Presentation

**User Story:** As a user reading `report.md`, I want a clear explanation of
coverage reduction and possible environment requirements with evidence links.

#### Acceptance Criteria

1. WHEN environment diagnostics exist THEN `report.md` SHALL include a
   deterministic "Build Environment Diagnostics" or equivalent section.
2. WHEN diagnostics are rendered THEN rows SHALL include diagnostic code,
   evidence tier, rule ID, project or source file, line span where available,
   guidance, and limitation text.
3. WHEN scan coverage is reduced because of environment issues THEN
   `scan-manifest.json` SHALL continue to report reduced/partial build status
   and SHALL include stable known gaps or diagnostic summary fields without
   breaking existing schema consumers.
4. WHEN no environment diagnostics are found and semantic analysis succeeds THEN
   the report SHALL avoid adding noisy empty sections unless existing report
   conventions require them.
5. WHEN no diagnostics are found but build/project load fails THEN TraceMap
   SHALL emit an explicit unknown environment gap so the failure is not hidden.
6. WHEN diagnostics include guidance THEN language SHALL be actionable but
   conservative, for example "installing compatible tooling may be required" or
   "run in an environment with matching reference assemblies"; it SHALL NOT
   command users to install obsolete or unsafe runtimes.

### Requirement 7: Determinism, Privacy, and Compatibility

**User Story:** As a maintainer, I want diagnostics to be deterministic,
redacted, and safe for existing TraceMap workflows.

#### Acceptance Criteria

1. Diagnostics SHALL be sorted deterministically in facts, SQLite, reports, and
   tests by diagnostic code, safe file path, line span, and fact ID.
2. Diagnostic IDs and hashes SHALL be stable for the same repository content,
   commit SHA, scanner version, and scan scope.
3. Diagnostics SHALL NOT store source snippets by default.
4. Diagnostics SHALL NOT store raw SQL, connection strings, config values,
   secrets, package source credentials, private remotes, local absolute paths, or
   raw command output by default.
5. New schema fields SHALL be additive where possible, and existing facts,
   reducer behavior, combined reports, and portfolio reports SHALL remain
   compatible.
6. Rule catalog entries SHALL document limitations for every new rule ID before
   the implementation is considered complete.
7. Diagnostic fact IDs and diagnostic hashes SHALL be derived only from
   sanitized fields, safe relative paths, stable diagnostic codes, commit SHA,
   scanner version, and scan scope; raw native messages, temp paths, usernames,
   machine paths, package source URLs, and secrets SHALL NOT feed fact ID hashes.
8. Hashes used to replace unsafe observed values SHALL use the project's stable
   hash helper or a documented SHA-256-based equivalent with fixed truncation,
   deterministic input construction, and tests covering stability and
   distinct-value behavior.

### Requirement 8: Validation

**User Story:** As a reviewer, I want focused fixtures and checks proving the
diagnostics work without depending on private legacy repositories.

#### Acceptance Criteria

1. Tests SHALL cover SDK-style and non-SDK-style project files, legacy target
   framework metadata, old MSBuild `ToolsVersion`, project type GUIDs,
   packages.config, restore-not-requested state, sanitized restore failure
   categories, Web Application indicators, generated/designer gaps, and unknown
   project formats.
2. Tests SHALL cover reduced coverage when semantic load fails while syntax,
   config, SQL, WCF, and WebForms extractors still run where applicable.
3. Tests SHALL assert facts and report rows include rule IDs, evidence tiers,
   commit SHA, safe file paths, line spans, and extractor versions.
4. Tests SHALL assert unsafe values are omitted or hashed in facts, manifest
   gaps, report output, SQLite rows, and analyzer logs, including regression
   coverage for pre-existing `csharp.semantic.workspace.v1` workspace and
   restore gap message properties.
5. Tests SHALL assert new diagnostic fact types remain compatible with combine,
   snapshot diff, portfolio, and report readers that ingest unknown fact types.
6. Tests SHALL assert `scan-manifest.json` `KnownGaps` contains only sanitized
   categories or hashes, since it is a distinct serialization path from facts.
7. Tests SHALL exercise the real restore stdout/stderr capture path with unsafe
   output and verify the unsafe values are absent from all scan artifacts.
8. Tests SHALL assert snapshot-diff or equivalent gap fingerprinting remains
   deterministic after raw messages migrate to `messageHash`-style properties.
9. Validation SHALL run `dotnet build src/dotnet/TraceMap.sln`,
   `dotnet test src/dotnet/TraceMap.sln`, `./scripts/check-private-paths.sh`,
   `git diff --check`, and any relevant pinned smoke checks from
   `docs/VALIDATION.md` unless explicitly deferred with rationale.
10. The CLI SHALL be run against at least one checked-in sample or generated
   temporary fixture to verify the required scan artifacts still appear:
   `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
   `logs/analyzer.log`.
11. `./scripts/check-private-paths.sh` SHALL be treated as a tracked-file private
   token guard only; artifact redaction SHALL be proven by focused tests that
   inspect generated scan outputs.
