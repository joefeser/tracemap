# TypeScript Indexer Design

## Overview

Add a TypeScript TraceMap scanner under `src/typescript`. The package will produce TraceMap-compatible artifacts for TypeScript repositories:

- `scan-manifest.json`
- `facts.ndjson`
- `index.sqlite`
- `report.md`
- `logs/analyzer.log`

The scanner will use TypeScript compiler APIs for Tier1 semantic extraction and TypeScript AST traversal for Tier3 syntax fallback. It will write reducer-compatible facts and an MVP SQLite schema that the existing .NET reducer can read.

This is a sibling implementation, not a rewrite of the .NET scanner. The repository layout remains language-separated:

```text
src/
  dotnet/
  typescript/
```

## Goals

- Preserve TraceMap's deterministic, evidence-backed behavior for TypeScript.
- Support explicit TypeScript projects and project references without requiring package installation during scan.
- Emit enough TypeScript facts for contract reduction, flow reporting, relationship reporting, and review routing.
- Keep all limitations explicit in rule catalog entries and reports.

## Non-Goals

- No LLM calls or embeddings.
- No runtime execution of target repository code.
- No dependency installation as part of `scan`.
- No claim of runtime reachability, branch feasibility, dependency-injection binding, decorator execution, or structural-type equivalence beyond compiler/static evidence.
- No raw source snippets by default.
- No direct dependency on SCIP output as the canonical TraceMap artifact.
- No JavaScript semantic coverage in MVP.
- No package-manager command execution in MVP.
- No TypeScript field/property alias or parameter-forward-edge derivation in MVP.

## Locked Decisions From Review

- Initial CLI is standalone `tracemap-ts`; root `tracemap` dispatch is out of scope.
- Existing .NET `tracemap reduce` is the MVP reduction path for TypeScript indexes.
- TypeScript scanner emits `buildStatus = "Succeeded"` only when semantic analysis has no known gaps, matching the reducer's current full-coverage gate.
- TypeScript facts that should influence reducer classification reuse existing `FactTypes` strings and reducer-matching camelCase property keys.
- MVP SQLite writes `scan_manifest` and `facts` first; `symbols` and `symbol_occurrences` are included when symbol identity is emitted. Derived flow tables are follow-up.
- MVP integration detectors are `fetch`, `axios`, Express routes, Zod schemas, Prisma client calls, and `process.env` reads.
- TraceMap-native TypeScript symbol IDs are used; SCIP compatibility is not required.
- Workspace discovery is follow-up unless it can be implemented by parsing local files without external commands.

## Package Structure

Proposed files:

```text
src/typescript/
  package.json
  tsconfig.json
  vitest.config.ts
  src/
    cli.ts
    scan/ScanEngine.ts
    scan/AnalysisGapCollector.ts
    scan/DiagnosticAggregator.ts
    scan/FileInventory.ts
    scan/GitMetadataProvider.ts
    scan/ManifestWriter.ts
    facts/FactFactory.ts
    facts/Models.ts
    facts/RuleIds.ts
    extractors/TypeScriptProjectLoader.ts
    extractors/TypeScriptSemanticExtractor.ts
    extractors/TypeScriptSyntaxExtractor.ts
    extractors/PackageJsonExtractor.ts
    extractors/ConfigExtractor.ts
    extractors/IntegrationExtractor.ts
    storage/JsonlFactWriter.ts
    storage/SqliteIndexWriter.ts
    reporting/MarkdownReportWriter.ts
    symbols/TypeScriptSymbolIdentityProvider.ts
    util/CompilerHost.ts
    util/Paths.ts
    util/Hash.ts
  tests/
```

The first implementation can duplicate small cross-language helpers from .NET in TypeScript. A later refactor can introduce shared schema docs or generated contracts if duplication becomes risky.

## CLI Shape

Initial TypeScript CLI:

```bash
tracemap-ts scan --repo <path> --out <path>
```

Options:

- `--project <path>` repeatable: explicit `tsconfig.json` or project directory.
- `--include <glob>` repeatable.
- `--exclude <glob>` repeatable.
- `--max-file-byte-size <value>` default `1mb`.
- `--no-semantic`: force syntax fallback for debugging/reduced scan validation.

Follow-up options:

- `--infer-config` for JavaScript/loose TypeScript repos.
- `--workspaces` for package workspace discovery.
- root `tracemap` language dispatch.

## Artifact Compatibility

### Manifest

Use the same manifest fields as .NET:

- `scanId`
- `repo`
- `remoteUrl`
- `branch`
- `commitSha`
- `scannerVersion`
- `scannedAt`
- `analysisLevel`
- `buildStatus`
- `solutions` equivalent: empty for TypeScript
- `projects`: config/workspace project paths
- `targetFrameworks` equivalent: TypeScript compiler target/module summaries
- `knownGaps`

TypeScript analysis levels:

- `Level1SemanticAnalysis`: all selected projects loaded without semantic gaps.
- `Level1SemanticAnalysisReduced`: at least one project/file had semantic gaps, syntax fallback still ran.
- `Level3SyntaxAnalysis`: semantic analysis disabled or no loadable config.

TypeScript build status:

- `Succeeded`: semantic analysis completed for all selected TypeScript projects with `commitSha != "unknown"` and no known gaps. This value is intentionally reused for reducer compatibility; it does not mean `npm test`, `tsc --build`, or bundler execution succeeded.
- `FailedOrPartial`: at least one selected project had semantic diagnostics/gaps that reduce confidence.
- `NotRun`: semantic analysis was disabled or no TypeScript project was loadable.

### Facts

Use the existing TraceMap fact envelope:

- deterministic `factId`
- `scanId`
- repo and commit SHA
- `factType`
- `ruleId`
- `evidenceTier`
- source/target symbols when available
- optional `contractElement`
- `EvidenceSpan`
- sorted properties

No property should store raw source text. Raw string/template/config values become hashes, lengths, kinds, and spans.

Reducer-compatible facts must use existing fact type names and property keys. The most important MVP fact types are:

- `TypeDeclared`
- `PropertyAccessed`
- `MethodInvoked`
- `ConfigKeyDeclared`
- `HttpCallDetected`
- `HttpRouteBinding`
- `SqlTextUsed`
- `SerializationLogic`
- `SymbolRelationship`
- `AnalysisGap`

The implementation must include tests that run the existing .NET reducer against a hand-written or scanner-produced TypeScript `index.sqlite` before broad extractor work proceeds.

### SQLite

MVP tables:

- `scan_manifest`
- `facts`
- `symbols`
- `symbol_occurrences`

Follow-up derived/common tables:

- `fact_symbols`
- `call_edges`
- `object_creations`
- `argument_flows`
- `local_aliases`
- `field_aliases`
- `parameter_forward_edges`
- `symbol_relationships`

If TypeScript requires additional columns, add tables rather than changing existing table semantics. Candidate additive tables for later:

- `package_dependencies`
- `routes`
- `config_uses`

## Project Loading

Borrow the `scip-typescript` approach:

1. Normalize repo path and options.
2. Collect file inventory before semantic load.
3. Discover project roots:
   - explicit `--project`
   - `tsconfig.json`
   - project references
   - `jsconfig.json`, package workspaces, and inferred config in follow-up
4. Load configs with:
   - `ts.readConfigFile`
   - `ts.parseJsonConfigFileContent`
   - `ts.parseCommandLine` for project path handling
5. Traverse `projectReferences` before parent projects.
6. Create a custom compiler host through `util/CompilerHost.ts`, borrowing SCIP's parsed-config/source-file caching pattern.
7. Enforce max-file-size behavior before source files are indexed; TypeScript may still need to parse config membership, but oversized files are skipped as extraction inputs and recorded as gaps.
8. Create a `ts.Program` and `TypeChecker`.
9. Record diagnostics through `DiagnosticAggregator` as bounded `AnalysisGap` facts without storing source snippets.
10. Run syntax fallback for selected source files regardless of semantic success; deduplicate or prefer Tier1 facts where needed.

Semantic load should avoid running package managers. Missing dependencies reduce coverage; they do not fail the scan.

## File Inventory

MVP included source/config files:

- `.ts`
- `.tsx`
- `.d.ts`
- `tsconfig*.json`
- `package.json`

Follow-up included files:

- `.js`
- `.jsx`
- `.mjs`
- `.cjs`
- `jsconfig*.json`
- framework config files: `next.config.*`, `vite.config.*`, `webpack.config.*`, `nest-cli.json`, `angular.json`, `remix.config.*`
- schema/config files: `.graphql`, `.gql`, `.json`, `.yaml`, `.yml`, `.env.example`

Excluded by default:

- `.git`
- TraceMap output path
- `node_modules`
- `.yarn/cache`
- `.pnpm-store`
- `dist`
- `build`
- `coverage`
- `.next`
- `.nuxt`
- `.turbo`
- generated files where filename/path strongly indicates generated output

## Symbol Identity

Use a deterministic TypeScript symbol ID format inspired by SCIP but scoped to TraceMap:

```text
typescript package <packageName> <versionOrHEAD> <modulePath> <descriptor>
```

Descriptors:

- namespace/module: `name/`
- type/interface/class/enum/type alias: `name#`
- term/property/variable: `name.`
- method/function/constructor: `name(<signatureDisambiguator>).`
- parameter: `(name)`
- type parameter: `[name]`
- local: `local <filePath>:<startLine>:<startColumn>:<name>`

Rules:

- Use nearest `package.json` name/version; fallback to repo name and `HEAD`.
- Include normalized module path relative to package root.
- Include signature disambiguator for overloads and call signatures when available.
- Include compiler-resolved declarations when TypeScript exposes them.
- Use local source span for symbols that cannot be globally named.

This does not need to be byte-for-byte SCIP compatible, but it should be stable, readable, and capable of linking facts across projects.

Fact IDs exclude extractor version from the hash input. Extractor identity/version is stored on the evidence span, matching .NET behavior. Proposed fact ID input:

```text
scanId | factType | ruleId | evidenceTier | filePath | startLine | endLine | sourceSymbol | targetSymbol | contractElement | sortedProperties
```

`TypeScriptSymbolIdentityProvider` must expose and test a `localSymbolKey(sourceFile, node, name)` helper before semantic extractors use local IDs.

## Extractors

### Package and Config Extractors

Emit Tier2 facts for:

- package name/version.
- dependencies/devDependencies/peerDependencies/optionalDependencies.
- workspace declarations.
- scripts with command hashes.
- config key paths.
- env-like key declarations and reads.

### Syntax Extractor

Emit Tier3 facts for:

- type/interface/class/enum/function/method/property/parameter declarations.
- import/export declarations.
- decorators by name.
- member access names.
- invocation names.
- object creation via `new`.
- basic call edges.
- logic shape: arithmetic, branches, validation-like conditionals, retry/backoff, transformations.
- boilerplate/generated path classification.

Syntax fallback should never claim symbol identity or overload target.

### Semantic Extractor

Emit Tier1 facts for:

- resolved declaration symbols.
- property access and mutation.
- function/method invocation.
- call edges.
- object creation.
- argument-to-parameter mapping.
- local alias.
- symbol relationships.
- imports/exports resolved to package/module symbols.

MVP does not emit field/property alias facts or parameter-forward edges. Those require a separate flow derivation design.

Semantic gaps:

- unresolved config.
- missing dependency diagnostics.
- oversized file skips.
- TypeScript internal errors.
- project reference load failures.

### Integration Extractor

Emit Tier2/Tier3/Tier1 depending on evidence. Tier decisions:

- Identifier/call-shape-only detection: Tier3.
- Structural framework pattern with package/config evidence: Tier2.
- Compiler-resolved known symbol evidence: Tier1 when the resolved symbol proves the API surface.

MVP detectors:

- HTTP client calls: `fetch`, `axios`.
- Server routes: Express route methods.
- Serialization: `JSON.parse`, `JSON.stringify`.
- Schema/DTO: Zod.
- Database: Prisma client calls.
- Config/env reads: `process.env.X`.

Literal and template values are hashed. Template strings with expressions should store template shape/kind and hash, not raw text.

## Rule IDs

Add TypeScript rule IDs to `rules/rule-catalog.yml` during implementation. Proposed IDs:

- `typescript.project.v1`
- `typescript.package.v1`
- `typescript.config.v1`
- `typescript.syntax.declarations.v1`
- `typescript.syntax.memberaccess.v1`
- `typescript.syntax.invocation.v1`
- `typescript.syntax.callgraph.v1`
- `typescript.syntax.objectcreation.v1`
- `typescript.syntax.logicshape.v1`
- `typescript.semantic.declarations.v1`
- `typescript.semantic.propertyaccess.v1`
- `typescript.semantic.methodinvocation.v1`
- `typescript.semantic.callgraph.v1`
- `typescript.semantic.objectcreation.v1`
- `typescript.semantic.valueflow.v1`
- `typescript.semantic.localalias.v1`
- `typescript.semantic.fieldalias.v1`
- `typescript.semantic.parameterforwarding.v1`
- `typescript.semantic.symbolidentity.v1`
- `typescript.semantic.symbolrelationship.v1`
- `typescript.integration.http.v1`
- `typescript.integration.route.v1`
- `typescript.integration.serializer.v1`
- `typescript.integration.database.v1`
- `typescript.integration.contractmapping.v1`
- `typescript.integration.boundary.v1`

## Reporting

`report.md` should mirror the .NET scan report:

- repo metadata.
- analysis level and build status.
- project/config counts.
- fact counts by type/tier/rule.
- known gaps.
- top integration/route/database/config facts.
- limitations.

The report must not say a scan is clean when analysis is reduced.

## Testing Strategy

Use Node test runner or Vitest under `src/typescript`.

Unit tests:

- path normalization.
- scan ID determinism across checkout parent paths.
- fact ID determinism.
- JSON/config line mapping.
- package identity and symbol ID construction.
- no raw snippets in facts.
- reducer-compatible fact property keys.
- local symbol key stability.
- diagnostic aggregation caps.

Fixture tests:

- simple TypeScript project with full semantic coverage.
- broken project with missing dependency and syntax fallback.
- project references.
- React/TSX syntax.
- Express route fixture.
- Zod/Prisma/fetch fixture.
- relationship fixture for class/interface inheritance.
- reducer cross-read fixture for `DefiniteImpact`.

Follow-up fixture tests:

- JS-only project using inferred config.
- workspace package discovery.
- Nest/Next route fixture.
- flow fixture for parameter forwarding.

End-to-end tests:

- `tracemap-ts scan --repo <fixture> --out <tmp>`.
- verify required artifacts exist.
- verify SQLite row counts.
- verify reducer can read TypeScript index for a sample contract delta.

Smoke tests:

- scan at least one local open-source TypeScript repo.
- record expected reduced/full coverage honestly.

## Implementation Risks

- TypeScript structural typing can make interface/member relationships less explicit than C#; relationship facts must be conservative.
- Missing `node_modules` may produce many diagnostics. Gaps should be grouped and bounded to avoid noisy reports.
- Decorators are framework-sensitive. Route/schema facts should be labeled structural unless the compiler resolves decorator symbols.
- Overload and merged declaration handling can produce duplicate symbols. Symbol identity and occurrence insertion need de-duplication.
- JS projects can be very loose. JS semantic facts should be marked reduced when type information is weak.
- Monorepos can be huge. Scoping, file-size limits, and project de-duplication are required from the first slice.

## Resolved Review Questions

1. CLI dispatch: ship standalone `tracemap-ts` first; root CLI dispatch is follow-up.
2. SQLite schema: write reducer-compatible `scan_manifest` and `facts` in MVP, add symbol tables as semantic identity lands, and defer derived flow tables.
3. Workspace discovery: do not run external package-manager commands; follow-up support may parse local workspace files only.
4. MVP framework detectors: `fetch`, `axios`, Express, Zod, Prisma, and `process.env`.
5. Symbol IDs: use TraceMap-native TypeScript IDs inspired by SCIP, not SCIP-compatible IDs.
