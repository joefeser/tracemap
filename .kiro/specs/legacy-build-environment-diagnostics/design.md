# Legacy Build Environment Diagnostics Design

## Overview

This spec adds a deterministic diagnostics layer to `tracemap scan` so old .NET
repositories can explain reduced coverage more clearly. The scanner should keep
the current behavior of continuing with syntax/config extraction when
MSBuild/Roslyn cannot load projects, but it should emit structured facts that
answer:

- What target frameworks, project styles, and toolset clues were declared?
- What SDK/runtime/MSBuild/reference assembly/toolset appears relevant?
- Was restore skipped, attempted, or blocked by a safe category?
- Which project formats or Web Application quirks reduced semantic coverage?
- Are generated/designer/service-reference files present, missing, malformed, or
  unlinked?
- What guidance can be given without pretending TraceMap fixed the environment?

The feature must remain static and evidence-backed. It describes repository and
tool diagnostics observed during the scan; it does not prove runtime behavior or
install tooling.

## Goals

- Make legacy build-environment failures visible in `facts.ndjson`,
  `index.sqlite`, `scan-manifest.json`, `report.md`, and
  `logs/analyzer.log`.
- Preserve useful fallback scanning when build/project load fails.
- Give conservative guidance such as "compatible reference assemblies appear
  necessary" with explicit rule IDs and limitations.
- Keep output deterministic, redacted, and compatible with current TraceMap
  artifacts.

## Non-Goals

- No automatic installation, restore retries, project migration, or file
  mutation.
- No new reducer conclusion that code is impacted or safe.
- No runtime compatibility, deployment, package vulnerability, production usage,
  or test-coverage claims.
- No LLM calls, embeddings, vector databases, prompt-based classification, or
  fuzzy model inference.
- No site work.

## Current State

Current scan behavior already provides building blocks:

- `ScanManifest` records `AnalysisLevel`, `BuildStatus`, target frameworks,
  known gaps, repo identity, commit SHA, and scanner version.
- `RepoScanned`, `BuildStatus`, and `AnalysisGap` facts use
  `repo.manifest.v1`.
- `ProjectFileReader` extracts SDK-style `TargetFramework` /
  `TargetFrameworks` and package references from project files and
  `packages.config`.
- `CSharpSemanticExtractor` emits MSBuildWorkspace and compilation gaps through
  `csharp.semantic.workspace.v1`.
- Syntax/config/SQL/WCF/WebForms extractors still run after semantic gaps.

The missing piece is a first-class environment diagnostic vocabulary. Current
known gaps are mostly strings, and reports do not consistently tell users which
legacy tooling appears relevant or which generated/project-format gaps capped
coverage.

## Proposed Fact Model

Add new fact types in `TraceMap.Core`:

```text
BuildEnvironmentDiagnostic
BuildToolRequirementDetected
ProjectFormatDiagnostic
RestoreDiagnostic
GeneratedFileDiagnostic
```

The implementation may choose fewer fact types if a single
`BuildEnvironmentDiagnostic` fact with a `diagnosticKind` property is simpler,
but report and tests must still distinguish the categories above.

Suggested rule IDs:

```text
build.environment.target-framework.v1
build.environment.toolset.v1
build.environment.project-format.v1
build.environment.restore.v1
build.environment.generated-files.v1
build.environment.workspace-diagnostic.v1
```

Each rule must be added to `rules/rule-catalog.yml` with limitations before the
implementation is complete.

Common properties:

| Property | Meaning |
| --- | --- |
| `diagnosticCode` | Stable code such as `LegacyTargetFramework`, `MissingReferenceAssemblies`, or `WebApplicationProjectTargets` |
| `diagnosticKind` | `target-framework`, `toolset`, `project-format`, `restore`, `generated-file`, or `workspace` |
| `projectStyle` | `sdk-style`, `non-sdk-style`, `legacy-web-application`, `unknown`, or omitted |
| `targetFramework` | Safe normalized TFM/framework version when available |
| `toolFamily` | Safe family such as `.NET Framework`, `.NET SDK`, `MSBuild`, `Visual Studio Web targets`, `NuGet` |
| `observedValueHash` | Hash for unsafe or too-specific raw values |
| `safeObservedValue` | Safe short value only when it passes redaction rules |
| `guidanceCode` | Stable guidance code such as `UseCompatibleReferenceAssemblies` |
| `coverageEffect` | `reduces-semantic-coverage`, `caps-to-structural`, `caps-to-syntax`, or `informational` |
| `sanitization` | `none`, `hashed`, `omitted`, or `category-only` |

Evidence spans should point to the declaration line for project/config/package
metadata. For workspace diagnostics without a reliable source span, use the
nearest project path if known; otherwise use `.` line `1` with a
`workspace-scope` property.

Diagnostic fact IDs must be derived from sanitized properties only. Existing
`FactFactory` IDs hash fact properties, so the implementation must sanitize or
replace raw workspace/restore message properties before facts are created. Raw
native messages, local absolute paths, temporary directories, usernames, package
source URLs, credentials, and source-like snippets must not feed fact ID hashes.

## Hash Algorithm

Unsafe observed values should use the existing TraceMap stable hash helper where
possible. If a new helper is needed, use SHA-256 over deterministic UTF-8 input
and truncate to a documented lowercase hex length matching nearby TraceMap hash
conventions, such as 32 hex characters for redaction hashes.

For migrated workspace and restore messages, prefer the existing `messageHash`
property convention with a 32-character lowercase hex value so snapshot-diff and
gap fingerprint readers can stay stable. The core implementation can compute the
hash with the core stable hash helper and reporting code can continue honoring
`messageHash`/`messageSha256`/`messageDigest` before falling back to hashing a
message.

Hash input should include context so identical raw values in different property
roles do not become ambiguous:

```text
build-environment|<property-key>|<diagnostic-code>|<raw-value>
```

The hash is redaction metadata, not evidence that the raw value is meaningful.
Tests should prove the same sanitized diagnostic is stable across runs and that
two distinct unsafe values produce distinct hashes in ordinary cases.
Cryptographic collision handling does not need a runtime collision registry, but
documented behavior should treat collisions as indistinguishable redaction hashes
rather than stronger evidence.

## Diagnostic Codes

Initial supported diagnostic codes:

| Code | Kind | Evidence tier cap | Notes |
| --- | --- | --- | --- |
| `LegacyTargetFramework` | target-framework | Tier2Structural | `TargetFrameworkVersion`, legacy TFM, or old framework declaration |
| `SdkStyleTargetFramework` | target-framework | Tier2Structural | Modern TFM; useful when mixed with legacy projects |
| `OldMsBuildToolsVersion` | toolset | Tier2Structural | `ToolsVersion` declaration |
| `VisualStudioVersionDeclared` | toolset | Tier2Structural | explicit VS-era property |
| `ImportedLegacyTargets` | toolset | Tier2Structural | known web/desktop/service-reference targets by safe import basename |
| `UnknownImportedTargets` | toolset | Tier4Unknown | import exists but cannot be safely categorized |
| `NonSdkStyleProject` | project-format | Tier2Structural | old C# project structure |
| `WebApplicationProjectTargets` | project-format | Tier2Structural | web app imports or project type GUIDs |
| `UnsupportedProjectTypeGuid` | project-format | Tier2Structural | known unsupported GUID mapped to safe category |
| `UnknownLegacyProjectFormat` | project-format | Tier4Unknown | unsupported or unrecognized project type evidence |
| `PackagesConfigPresent` | restore | Tier2Structural | packages.config evidence |
| `NuGetConfigPresent` | restore | Tier2Structural | config presence only, no raw sources |
| `NuGetRestoreFailed` | restore | Tier3SyntaxOrTextual | sanitized native diagnostic category |
| `PackageSourceUnavailable` | restore | Tier3SyntaxOrTextual | sanitized category |
| `CredentialRequired` | restore | Tier3SyntaxOrTextual | sanitized category |
| `PackageVersionUnavailable` | restore | Tier3SyntaxOrTextual | sanitized category |
| `UnsupportedPackageFormat` | restore | Tier3SyntaxOrTextual | sanitized explicit restore category |
| `GeneratedFileMissing` | generated-file | Tier4Unknown | expected generated/designer file absent |
| `GeneratedFileMalformed` | generated-file | Tier4Unknown | parse/load failure |
| `GeneratedFileUnlinked` | generated-file | Tier3SyntaxOrTextual | present but no semantic/structural linkage |
| `MissingReferenceAssemblies` | workspace | Tier4Unknown | categorized workspace/compiler diagnostic |
| `SdkResolutionFailed` | workspace | Tier4Unknown | categorized SDK resolver diagnostic |
| `MSBuildRegistrationFailed` | workspace | Tier4Unknown | categorized MSBuild registration diagnostic |
| `CompilationCreationFailed` | workspace | Tier4Unknown | compiler setup failed |
| `UncategorizedWorkspaceFailure` | workspace | Tier4Unknown | fallback when sanitization cannot classify |

The implementation should start with deterministic exact or regex category
matching over sanitized diagnostic inputs. It must not use fuzzy matching,
prompts, or external model classification.

## Static Project Inspection

Add or extend a project-environment reader near `ProjectFileReader`.

Inputs for the first implementation slice:

- `.sln`
- `.csproj`
- `packages.config`
- `packages.lock.json`
- `nuget.config`
- `Web.config` / `App.config` presence only for environment markers already
  safe under config rules
- known generated/design-time extensions such as `.designer.cs`, `.resx`,
  `.settings`, `.svcmap`, `.wsdl`, `.xsd`, `.aspx`, `.ascx`, and `.master`

Current inventory already covers several but not all of these. The
implementation must either extend `FileInventory` for `.props`, `.targets`,
`.resx`, `.settings`, and non-C# project files used by diagnostics, or scope a
given extension out of the first implementation slice with an explicit
`UnsupportedInventoryKind` gap and follow-up. If inventory expands, tests must
cover expected `FileInventoried` baseline changes and downstream combine/report
compatibility.

Non-C# project files such as `.vbproj` or `.fsproj` are structural
project-format clues only in this spec. They should not imply language adapter
support or semantic analysis for those languages, and they must be mapped to a
distinct inventory kind or filtered out in `CSharpSemanticExtractor` so the C#
extractor does not attempt to load them standalone.

Do not evaluate arbitrary MSBuild conditions. Record the declared condition text
only as a hash or omit it. Prefer line-aware XML parsing; when parsing fails,
emit a parse gap instead of string slicing conclusions.

## Tooling Guidance Mapping

Guidance should be table-driven and documented in tests. Examples:

| Evidence | Guidance code | Report wording |
| --- | --- | --- |
| `.NETFramework,Version=v4.x` or `TargetFrameworkVersion` | `UseCompatibleReferenceAssemblies` | "Compatible .NET Framework reference assemblies appear necessary for semantic analysis." |
| old `ToolsVersion` | `UseCompatibleMSBuildToolset` | "A compatible MSBuild toolset appears necessary for this project style." |
| web app targets/GUIDs | `UseCompatibleWebApplicationTargets` | "Visual Studio Web Application targets appear necessary for full project load." |
| packages.config | `LegacyNuGetRestoreMayBeNeeded` | "Legacy NuGet restore may be required before semantic analysis can resolve references." |
| unknown imported targets | `ReviewImportedTargets` | "Imported targets could affect build behavior; TraceMap did not evaluate them." |

Wording must be conservative. Avoid:

- "Install .NET Framework X now"
- "This repository requires Visual Studio X"
- "This package is missing"
- "This code is safe/unsafe"

Prefer:

- "appears necessary"
- "may be required"
- "compatible tooling"
- "semantic coverage is reduced until this is resolved"

## Workspace Diagnostic Sanitization

Workspace diagnostics can contain machine-specific paths and raw native output.
Before writing facts, reports, manifest gaps, or artifact logs:

1. Normalize known diagnostic messages to stable categories.
2. Remove or hash absolute paths, home directories, package source URLs, remotes,
   credentials, config values, and quoted source-like fragments.
3. Keep only category, safe tool family, safe project-relative path, and counts.
4. If sanitization cannot prove safety, emit `UncategorizedWorkspaceFailure`
   with `sanitization=category-only`.

The implementation should add tests with intentionally unsafe input strings and
assert the unsafe substrings do not appear in:

- `facts.ndjson`
- `scan-manifest.json`
- `report.md`
- `index.sqlite`
- `logs/analyzer.log`

This remediation must happen at the gap source, before raw text reaches
`ScanEngine.GetGapMessage`, manifest `KnownGaps`, `FactFactory.Create`, or any
artifact writer. It must include the existing
`csharp.semantic.workspace.v1` `AnalysisGap` message path and the existing
explicit restore-failure gap path. Adding new sanitized diagnostic facts is not
sufficient while old raw message properties remain in memory long enough to feed
`facts.ndjson`, `index.sqlite`, `scan-manifest.json`, or fact ID hashing.

## Restore Diagnostics

Default scan behavior should remain no restore unless the existing CLI option
requests restore. Restore diagnostics should distinguish:

- `RestoreNotRequested`: scan-option state, not structural repository evidence.
- `RestoreAttempted`: optional informational property or diagnostic.
- `RestoreFailed`: sanitized category from explicit restore attempt.

Do not infer package absence from `packages.config` alone. Do not display raw
package source names or URLs. Package IDs and versions already have existing
safe handling; extend redaction if conditional or unsafe values are found.

`RestoreNotRequested` should be represented as manifest/report scan-option state,
not as a standalone `CodeFact`. Do not emit noisy restore-not-requested
diagnostics on clean modern projects that load semantically and have no
package/restore indicators. If package metadata plus reduced coverage makes the
skipped restore relevant, render it as coverage context tied to the package or
workspace diagnostic rather than a separate fact.

## Generated and Designer File Gaps

Generated/design-time files affect legacy WebForms, WCF, WinForms, settings,
resources, and service references. The implementation should detect:

- page/control markup with expected code-behind/designer file not inventoried;
- `.svcmap` metadata with expected generated proxy files missing or unlinked;
- `.resx` or `.settings` without generated designer evidence where a project
  references the generated file;
- generated files present but parse failures or semantic linkage failures reduce
  confidence.

These diagnostics do not replace existing WCF/WebForms facts. They explain why
coverage is partial and should be reportable alongside those facts.

## Artifact Presentation

### facts.ndjson

Facts should use normal `CodeFact` shape. The implementation may initially use
`FactTypes.AnalysisGap` for unknowns and one new diagnostic fact type for known
diagnostics, provided `diagnosticKind` and `diagnosticCode` are stable.

### index.sqlite

At minimum, diagnostics must be present in the existing `facts`/properties
storage. An additive view such as `build_environment_diagnostics` is optional if
it follows current SQLite migration patterns and is covered by tests.

### scan-manifest.json

Keep existing manifest fields stable. Additive fields are allowed only if needed
and tested for compatibility. Known gaps should include stable categories, not
raw native diagnostics.
`SemanticExtractionResult.ReducedCoverage` and existing scan-level
`AnalysisLevel`/`BuildStatus` remain the source of truth for scan-wide coverage.
Per-diagnostic `coverageEffect` explains why coverage was reduced or capped; it
does not independently mark a scan failed unless it is connected to the existing
semantic/project-load failure path.

### report.md

Add a section after the build/coverage summary and before detailed evidence
sections:

```text
## Build Environment Diagnostics

| Code | Tier | Rule | Evidence | Guidance | Limitation |
| --- | --- | --- | --- | --- | --- |
```

Rows should use safe relative paths and line spans. The report should also state
when build/project load failed and fallback syntax/config analysis continued.
The implementation task should place the section in the existing
`MarkdownReportWriter` order after the build/coverage summary and before
detailed evidence sections where feasible; if local report writer structure
requires a different nearby insertion point, tests must pin the final ordering.

### logs/analyzer.log

Logs written as scan artifacts must be sanitized by default. If a local-only
debug log is added later, it must be opt-in and clearly excluded from shareable
artifact guidance.

## Deterministic Ordering

Sort diagnostics by:

1. `diagnosticKind`
2. `diagnosticCode`
3. safe project/file path
4. start line
5. fact ID

Do not include timestamps, temporary directories, machine names, random IDs, or
native command ordering in diagnostic IDs.

## Rule Catalog Updates

Add rule catalog entries for every new rule ID. Each limitation must state:

- evidence is static and does not prove runtime behavior;
- guidance is environment compatibility guidance, not installation instruction;
- missing tooling means reduced coverage, not absence of code evidence;
- unsafe raw values are hashed or omitted;
- unsupported formats and generated-file gaps are analysis gaps.

The rule catalog is not changed by this spec-only PR because these rule IDs are
proposed but not active extractor rules yet. The implementation PR must add rule
catalog entries before adding code that emits any of the new rule IDs, and that
PR must not merge with diagnostics emitted under undocumented rules.

## Testing Strategy

Use generated temporary fixtures or checked-in neutral fixtures. Do not commit
local legacy repository names, absolute paths, raw remotes, raw SQL, config
values, secrets, or source snippets.

Focused tests:

- SDK-style `net8.0` project emits modern target-framework diagnostics without
  reduced legacy guidance.
- non-SDK-style project with `TargetFrameworkVersion` and `ToolsVersion` emits
  legacy target/toolset diagnostics.
- project with Web Application project type GUIDs/imports emits
  `WebApplicationProjectTargets`.
- unsupported project GUID emits known unsupported or unknown format gap.
- `packages.config` emits restore-shape guidance and does not claim missing
  packages when restore is skipped.
- explicit restore failure input maps to sanitized categories.
- generated/designer missing/malformed/unlinked cases emit gaps.
- semantic load failure still allows syntax/config/SQL/WCF/WebForms fallback.
- unsafe diagnostic strings are absent from all scan artifacts.
- the pre-existing workspace and restore `AnalysisGap` message paths no longer
  write unsafe raw messages into scan artifacts.
- `scan-manifest.json` `KnownGaps` contains only sanitized categories or hashes.
- the real restore stdout/stderr capture path is covered with intentionally
  unsafe output, not only synthetic pre-built gap facts.
- diagnostic fact IDs remain stable when two raw messages sanitize to the same
  category, and unsafe observed-value hashes remain stable for identical input.
- snapshot-diff or equivalent gap fingerprinting remains deterministic after
  raw messages migrate to `messageHash`-style properties.
- distinct unsafe observed values produce distinct hashes in ordinary cases.
- modern SDK-style successful scans do not emit noisy restore-not-requested
  report sections.
- multiple diagnostics for the same project remain separate facts.
- new diagnostic fact types do not break combine, snapshot diff, portfolio, or
  report readers.
- inventory changes for `.props`, `.targets`, `.resx`, `.settings`, or non-C#
  project files are either tested and rebaselined or explicitly deferred with
  gaps.
- report section ordering and row ordering are deterministic.

Validation commands:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Run relevant pinned smoke checks from `docs/VALIDATION.md` if extractor or
reporting behavior shared with language adapters changes. For this .NET scan
feature, at least run the CLI against a checked-in sample or generated temporary
fixture and verify the five required scan artifacts exist.

## Implementation Slices

1. Add diagnostic vocabulary, rules, and tests for static project inspection.
2. Add sanitized workspace/MSBuild diagnostic categorization.
3. Add restore-state diagnostics without changing default restore behavior.
4. Add generated/designer-file gap diagnostics.
5. Add report/manifest/SQLite presentation and artifact redaction tests.
6. Run validation and update implementation state/tasks.

The slices can land in one PR if small enough, but each should remain reviewable
and should not mix site changes or unrelated reducer behavior.
