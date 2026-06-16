# Legacy ASMX/SOAP Detection Requirements

## Introduction

Older ASP.NET and WebForms-era systems often expose service boundaries through
ASMX web services, SOAP attributes, checked-in WSDL/DISCO/proxy files, generated
SOAP clients, and `web.config` declarations instead of modern HTTP APIs or WCF.
TraceMap already has deterministic legacy evidence for WebForms, WCF/SVC,
Remoting, SQL/query surfaces, and legacy data metadata. This phase adds a
sibling ASMX/SOAP detector that produces static evidence only.

The feature must not execute services, host ASP.NET, download WSDL, call SOAP
endpoints, resolve runtime configuration, infer deployment, prove reachability,
or classify vulnerability/impact. It must preserve rule IDs, evidence tiers,
file spans, commit SHA, extractor versions, coverage labels, and limitations.

Public claim level: hidden until validated through reviewed redacted summaries
or checked-in synthetic/public fixtures.

## Requirements

### Requirement 1: ASMX Host And Directive Evidence

**User Story:** As a maintainer, I want TraceMap to identify checked-in ASMX
service host declarations without needing the old project to build.

#### Acceptance Criteria

1. WHEN a repository contains `.asmx` files THEN TraceMap SHALL inventory them
   as ASMX host candidates with safe repository-relative paths and line spans
   when available.
2. WHEN an `.asmx` file contains a WebService directive such as
   `<%@ WebService Language="C#" CodeBehind="..." Class="..." %>` THEN
   TraceMap SHALL emit ASMX host facts with safe directive metadata including
   service class, code-behind file name, language, and source kind.
3. WHEN directive attributes contain paths, URLs, config-like values, unknown
   expressions, or unsafe text THEN TraceMap SHALL hash or omit unsafe values
   and SHALL NOT store raw snippets.
4. WHEN `.asmx` markup is malformed, missing a directive, generated, or not
   parseable by the tolerant parser THEN TraceMap SHALL emit `AnalysisGap`
   evidence rather than claiming no ASMX host exists.
5. WHEN `.asmx` inventory is emitted THEN WCF/SVC and Remoting fact types SHALL
   remain separate; ASMX hosts SHALL NOT be folded into `.svc` host facts in
   new indexes.
6. WHEN implementing this split from existing TraceMap behavior THEN the
   implementation SHALL explicitly migrate the current ASMX host coverage that
   is emitted through `legacy.wcf.host.v1` and `WcfServiceHostDeclared` into
   ASMX-specific rules and facts, while preserving older-index compatibility
   through availability or migration notes.
7. WHEN older indexes still contain ASMX directive evidence under
   `WcfServiceHostDeclared` and `legacy.wcf.host.v1` THEN consumers SHALL NOT
   report clean ASMX absence; they SHALL either interpret the legacy WCF-host
   row as legacy ASMX-compatible host evidence with a migration limitation or
   emit an older-index availability gap.

### Requirement 2: WebService And WebMethod Code Evidence

**User Story:** As a reviewer, I want ASMX service classes and operations to be
visible from attributes even under reduced build coverage.

#### Acceptance Criteria

1. WHEN C# code declares `[WebService]`, `[WebServiceBinding]`,
   `[ScriptService]`, `[SoapDocumentService]`, or `[SoapRpcService]` THEN
   TraceMap SHALL emit ASMX service class facts with rule IDs, evidence tiers,
   file spans, commit SHA, and extractor version.
2. WHEN C# methods declare `[WebMethod]` THEN TraceMap SHALL emit ASMX operation
   facts linked to the containing type or directive class when static evidence
   supports that link.
3. WHEN C# methods declare SOAP operation attributes such as
   `[SoapDocumentMethod]` or `[SoapRpcMethod]` THEN TraceMap SHALL emit SOAP
   operation-shape facts with safe action/binding metadata represented by safe
   names or hashes only.
4. WHEN a method has both `[WebMethod]` and SOAP operation attributes THEN
   TraceMap SHALL emit distinct ASMX operation and SOAP operation-shape facts
   when each attribute is independently visible, preserving separate rule IDs
   and limitations rather than collapsing them into one row.
5. WHEN semantic analysis resolves the service class, method, attribute, or
   containing assembly THEN TraceMap SHOULD emit `Tier1Semantic` evidence with
   fully qualified symbols and assembly/package metadata where available.
6. WHEN semantic analysis is unavailable or unresolved THEN syntax-only
   attribute evidence SHALL still be emitted at `Tier3SyntaxOrTextual` with
   alias/lookalike limitations.
7. WHEN a name such as `WebMethod` or `SoapDocumentMethod` may refer to a
   project-defined attribute and semantic analysis cannot disambiguate it THEN
   TraceMap SHALL cap the evidence at review tier or emit an ambiguity gap.
8. Syntax fallback SHALL use Roslyn syntax nodes and SHALL NOT treat comments,
   string literals, XML doc text, inactive preprocessor regions, or arbitrary
   source text as ASMX evidence.

### Requirement 3: Generated SOAP Client And Proxy Evidence

**User Story:** As a maintainer, I want old generated SOAP client proxies to
connect caller code to static ASMX operation evidence where credible.

#### Acceptance Criteria

1. WHEN generated SOAP proxy code is statically identifiable by safe patterns
   such as `SoapHttpClientProtocol`, `[WebServiceBinding]`, generated-code
   attributes, service-reference folder shape, `.disco`, `.wsdl`, `.map`, or
   proxy metadata THEN TraceMap SHALL emit generated SOAP client facts.
2. WHEN generated client methods can be matched to ASMX operations by service
   binding name, operation name, proxy class, checked-in WSDL/proxy metadata, or
   compiler-resolved symbols THEN TraceMap SHALL emit probable ASMX mapping
   evidence with deterministic supporting fact IDs.
3. WHEN only method names match without binding/config/proxy support THEN the
   mapping SHALL remain `Tier3SyntaxOrTextual` or `NeedsReview` and SHALL NOT
   choose a backend service arbitrarily.
4. WHEN multiple operation candidates match a generated proxy method THEN
   TraceMap SHALL emit an ambiguity gap and SHALL NOT pick a winner.
5. WHEN generated proxy code contains raw endpoint URLs, namespaces, SOAP action
   URIs, credentials, config values, source snippets, or WSDL addresses THEN
   TraceMap SHALL omit credential-like values and SHALL hash or omit other
   allowed non-secret unsafe values before writing facts or reports.
6. Generated SOAP client evidence SHALL be separate from WCF generated-client
   evidence unless a later spec explicitly defines shared service-reference
   normalization.
7. `AsmxServiceReferenceMapping` SHALL remain a distinct fact type from
   `WcfServiceReferenceMapping`; combined reports MAY group both under old
   service-reference evidence, but SHALL NOT conflate their rule IDs, fact
   types, or limitations.

### Requirement 4: Checked-In WSDL/DISCO/Proxy Metadata Evidence

**User Story:** As an operator, I want checked-in service metadata to improve
ASMX mapping confidence without downloading anything at scan time.

#### Acceptance Criteria

1. WHEN checked-in `.wsdl`, `.disco`, `.discomap`, `.map`, proxy metadata, or
   old ASMX web-reference metadata files are present THEN TraceMap MAY parse
   safe structural metadata for service names, operation names, binding names,
   target namespaces, and generated proxy relationships. `.svcmap` remains
   WCF-owned per the deterministic ownership rule below.
2. WHEN metadata files include endpoint addresses, namespaces, SOAP action URIs,
   import locations, schema locations, credentials, or environment-specific
   values THEN TraceMap SHALL omit credentials, tokens, and other
   credential-like values rather than hashing them; only allowed non-secret
   values MAY be reduced to safe names or context-separated hashes.
3. WHEN checked-in metadata is malformed, incomplete, generated by unsupported
   tools, or references external imports THEN TraceMap SHALL emit `AnalysisGap`
   evidence rather than fetching external resources or claiming clean absence.
4. WHEN WSDL/proxy metadata supports generated-client mapping THEN supporting
   fact IDs, file spans, rule IDs, evidence tiers, and coverage labels SHALL be
   preserved in deterministic order.
5. The scanner SHALL NOT perform network access, remote WSDL download, NuGet
   restore, service activation, or schema import resolution as part of ASMX
   metadata extraction.
6. WHEN metadata files could be claimed by both WCF and ASMX extractors THEN
   ownership SHALL be deterministic: `.svcmap`-gated WCF service-reference
   metadata remains owned by `legacy.wcf.metadata.v1`; ASMX metadata SHALL claim
   only files corroborated by ASMX host/proxy evidence such as `.asmx`,
   `SoapHttpClientProtocol`, ASMX proxy metadata, or SOAP attributes.
7. WHEN a metadata file is claimed by one family THEN the other family SHALL NOT
   emit duplicate facts for the same file unless it emits an explicit
   availability/gap row explaining why ownership was not clear.

### Requirement 5: Config Evidence

**User Story:** As a maintainer, I want old web/app configuration to reveal ASMX
client and service setup without leaking endpoint values.

#### Acceptance Criteria

1. WHEN `web.config`, `app.config`, or related XML config files contain
   ASMX/SOAP-relevant sections such as `appSettings` endpoint keys, generated
   proxy URL keys, `system.web/webServices`, SOAP extension declarations, or
   client protocol settings THEN TraceMap SHALL emit config evidence only for
   safe identifiers and hashed values.
2. WHEN config values contain URLs, hostnames, credentials, connection strings,
   SOAP endpoints, local paths, or environment-specific values THEN raw values
   SHALL NOT be written to `facts.ndjson`, `index.sqlite`, Markdown, JSON
   reports, logs, or review summaries.
3. WHEN config transforms, external includes, encrypted sections, machine.config
   dependencies, runtime mutations, or unsupported custom sections are required
   to understand the ASMX boundary THEN TraceMap SHALL emit `AnalysisGap`
   evidence and mark coverage reduced.
4. Config evidence SHALL support mapping only when static keys or generated
   proxy metadata make the relationship credible. Matching endpoint hashes
   alone SHALL NOT prove client-to-server reachability.
5. ASMX-specific config facts SHALL supplement generic `ConfigKeyDeclared`
   evidence. They SHALL NOT replace generic config-key facts unless a later
   config-normalization spec defines that migration.

### Requirement 6: Flow, Path, Reverse, And Report Consumption

**User Story:** As a TraceMap user, I want ASMX/SOAP evidence to participate in
existing static dependency reports without creating a one-off analyzer island.

#### Acceptance Criteria

1. WHEN ASMX/SOAP facts are emitted THEN `tracemap scan`, `facts.ndjson`,
   `index.sqlite`, and `report.md` SHALL include deterministic counts, gaps,
   rule IDs, evidence tiers, coverage labels, and limitations.
2. WHEN a combined index imports ASMX/SOAP facts THEN combined report, paths,
   reverse, impact, release-review, and portfolio commands SHALL either consume
   them where explicitly supported or emit availability gaps when the precision
   is unavailable.
3. WHEN legacy flow composition is extended THEN terminal surfaces SHALL include
   `asmx-service`, `asmx-operation`, `asmx-client`, and related config/proxy
   evidence only where existing selector and terminal models can represent them
   without overclaiming.
4. WHEN WebForms/controller/service code calls a generated ASMX proxy method and
   static mapping evidence exists THEN path reports MAY show a possible static
   path to ASMX operation evidence.
5. WHEN no ASMX evidence is found under full ASMX extractor availability THEN
   reports MAY say no ASMX evidence was found under available static coverage.
   Under reduced coverage, older indexes, missing metadata, or parse gaps,
   absence SHALL be reported as unavailable or reduced coverage, not clean
   absence.
6. Reports SHALL use wording such as "static ASMX evidence", "SOAP proxy
   candidate", "possible static path", and "service boundary candidate". They
   SHALL NOT say a SOAP request was sent, an endpoint is deployed, a service was
   reachable, or a backend was impacted at runtime.

### Requirement 7: Rules, Tiers, And Limitations

**User Story:** As a reviewer, I want every ASMX/SOAP conclusion backed by a
documented rule and explicit limitations.

#### Acceptance Criteria

1. WHEN ASMX/SOAP facts or report rows are emitted THEN every row SHALL cite a
   rule ID documented in `rules/rule-catalog.yml`.
   Rule catalog changes are implementation-slice work and SHALL be completed in
   the same PR that first emits those facts or report rows.
2. Rule catalog entries SHALL document limitations for static evidence, old
   generated code, ambiguous attributes, dynamic proxy factories, config
   transforms, external WSDL imports, runtime endpoint selection, deployment,
   reachability, credentials, and service activation.
3. Evidence tiers SHALL follow this guidance:
   - `Tier1Semantic` for compiler-resolved framework attributes, service
     symbols, generated proxy inheritance, and operation symbols.
   - `Tier2Structural` for parseable `.asmx`, WSDL/DISCO/proxy metadata, and
     config structures with safe identifiers.
   - `Tier3SyntaxOrTextual` for syntax-only attributes, generated-client naming
     patterns, and unresolved method/name matches.
   - `Tier4Unknown` for malformed files, ambiguity, unsupported dynamic
     factories, external imports, encrypted config, or missing extractor
     capability.
4. Mapping evidence SHALL be capped by the weakest required leg. In particular,
   metadata-backed generated-client mappings without aligned config or semantic
   evidence SHALL remain `Tier3SyntaxOrTextual` or `NeedsReview` even when the
   metadata file itself is `Tier2Structural`.
5. ASMX/SOAP evidence SHALL NOT upgrade WebForms, WCF, Remoting, SQL, or
   release-review conclusions beyond the weakest required evidence tier.

### Requirement 8: Validation And Safety

**User Story:** As a maintainer, I want implementation work to be safe,
deterministic, and measurable on synthetic and optional public samples.

#### Acceptance Criteria

1. Tests SHALL cover `.asmx` directive parsing, `[WebService]`, `[WebMethod]`,
   SOAP operation attributes, generated SOAP client/proxy patterns, config
   evidence, WSDL/DISCO metadata, ambiguity, malformed files, and redaction.
2. Tests SHALL include semantic and syntax-fallback paths where the .NET test
   harness supports them.
3. Tests SHALL assert that raw URLs, SOAP actions, endpoint values, config
   values, credentials, source snippets, analyzer logs, raw remotes, private
   names, and local absolute paths are not stored or rendered.
4. Synthetic fixtures SHALL be checked in using neutral labels only. Optional
   public sample repos MAY be listed by neutral sample label in ignored or
   reviewed validation metadata, but committed spec files SHALL NOT include
   clone paths, raw remotes, generated scan output, or source snippets.
5. Validation SHALL run `dotnet build`, `dotnet test`, private-path guard,
   `git diff --check`, and relevant pinned smoke checks from
   `docs/VALIDATION.md` or explicitly defer those smokes with rationale.

## Non-Goals

- No network calls, WSDL downloads, SOAP requests, IIS/ASP.NET hosting, or
  service activation.
- No runtime proof of deployment, endpoint reachability, request execution,
  authentication, authorization, business impact, security posture, or release
  readiness.
- No dynamic endpoint selection, config-transform execution, machine.config
  expansion, reflection target resolution, dependency-injection expansion, or
  generated-code execution.
- No source snippets, raw SQL, raw config values, endpoint values, raw remotes,
  local absolute paths, private sample names, or generated public-repo artifacts
  in committed files.
- No LLM calls, embeddings, vector databases, or prompt-based classification in
  scanner/reducer/reporting logic.
