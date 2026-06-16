# Legacy Remoting Detection Requirements

## Introduction

Older .NET Framework systems sometimes expose service boundaries through .NET
Remoting instead of WCF, ASMX, HTTP APIs, or modern RPC stacks. These codebases
may register `TcpChannel`, `HttpChannel`, or `IpcChannel` instances, configure
well-known service or client types, derive service objects from
`MarshalByRefObject`, and keep `<system.runtime.remoting>` declarations in app
configuration files.

TraceMap already extracts deterministic evidence for several legacy boundaries,
including WCF/SVC and WebForms flow. This phase adds a sibling Remoting detector
that produces static evidence only. It must not execute remoting hosts, connect
to remoting endpoints, infer exploitability, prove production usage, or fold
Remoting-specific evidence into WCF/SVC rules.

Initial evidence may be `Tier2Structural` or `Tier3SyntaxOrTextual` heavy.
Roslyn semantic success improves identity quality but must not be required for
useful Remoting findings.

Public claim level: hidden until validated against redacted public or local
legacy summaries.

## Requirements

### Requirement 1: Remoting Namespace and Type Evidence

**User Story:** As a maintainer, I want TraceMap to identify code that uses
.NET Remoting APIs even when the project does not build.

Acceptance Criteria:

1. WHEN C# code imports or references `System.Runtime.Remoting` namespaces THEN
   TraceMap SHALL emit Remoting API usage evidence with rule ID, evidence tier,
   file path, line span, commit SHA, and extractor version.
2. WHEN C# code references known Remoting types such as `RemotingConfiguration`,
   `ChannelServices`, `WellKnownObjectMode`, `ObjRef`, or `RemotingServices`
   THEN TraceMap SHALL emit deterministic usage facts.
3. WHEN semantic analysis resolves a known Remoting symbol THEN the fact SHOULD
   use `Tier1Semantic` and the fully qualified symbol identity.
4. WHEN semantic analysis is unavailable or unresolved THEN syntax-only matches
   SHALL still emit `Tier3SyntaxOrTextual` facts and SHALL mark coverage
   limitations.
5. WHEN a name match is ambiguous with a project-defined type or alias THEN
   TraceMap SHALL cap the finding at review-tier evidence or emit an analysis
   gap rather than claiming a Remoting boundary.
6. WHEN syntax fallback searches for Remoting references THEN it SHALL use
   Roslyn syntax nodes and SHALL NOT treat comments, string literals, or
   inactive `#if` regions as Remoting evidence.

### Requirement 2: MarshalByRefObject Boundary Evidence

**User Story:** As a maintainer, I want Remoting-capable service objects to be
visible as static service-boundary candidates.

Acceptance Criteria:

1. WHEN a class directly derives from `MarshalByRefObject` and the symbol
   resolves THEN TraceMap SHALL emit a Remoting object fact with
   `Tier1Semantic` evidence.
2. WHEN a class only indirectly derives from `MarshalByRefObject` through a
   resolved framework or project base type THEN TraceMap SHALL emit a Remoting
   object fact only when Remoting-specific evidence is present in the same
   repository, file, or explicit registration/config context; otherwise it
   SHALL omit the Remoting object fact or emit a review gap to avoid broad
   desktop/framework false positives.
3. WHEN syntax shows a base type named `MarshalByRefObject` or
   `System.MarshalByRefObject` but semantic analysis is unavailable THEN
   TraceMap SHALL emit syntax-level evidence with limitations.
4. WHEN a derived type is abstract, generic, nested, partial, or generated THEN
   TraceMap SHALL preserve safe type identity metadata and SHALL NOT infer that
   the type is hosted or reachable.
5. WHEN multiple partial declarations contribute evidence THEN TraceMap SHALL
   emit deterministic facts or supporting line spans without duplicate unstable
   identities.

### Requirement 3: Channel Registration Evidence

**User Story:** As a maintainer, I want channel setup code to identify likely
Remoting host or client configuration points.

Acceptance Criteria:

1. WHEN code creates `TcpChannel`, `HttpChannel`, `IpcChannel`, or their server
   and client channel variants THEN TraceMap SHALL emit channel construction
   facts.
2. WHEN code calls `ChannelServices.RegisterChannel` THEN TraceMap SHALL emit a
   channel registration fact and SHALL link it to channel construction only when
   v1 deterministic local evidence rules are satisfied.
3. WHEN channel properties include ports, names, provider chains, URLs, or other
   config-like values THEN TraceMap SHALL hash or omit unsafe values and SHALL
   NOT store raw endpoint addresses or raw config values.
4. WHEN channel registration uses dynamic variables, reflection, dependency
   injection, config values, or unsupported factory helpers THEN TraceMap SHALL
   emit review-tier evidence or `AnalysisGap` facts rather than guessing.
5. WHEN only syntax evidence is available THEN channel facts SHALL remain
   `Tier3SyntaxOrTextual`; v1 SHALL reserve `Tier2Structural` channel evidence
   for parseable config and other explicitly documented non-name-only
   structures.

### Requirement 4: Remoting Configuration API Evidence

**User Story:** As a maintainer, I want well-known service and client
registration calls to explain static Remoting boundaries.

Acceptance Criteria:

1. WHEN code calls
   `RemotingConfiguration.RegisterWellKnownServiceType` THEN TraceMap SHALL emit
   a service registration fact with safe service type identity, optional
   mode/classification, and hashed object URI when present.
2. WHEN code calls
   `RemotingConfiguration.RegisterWellKnownClientType` THEN TraceMap SHALL emit
   a client registration fact with safe type identity and hashed URL/object URI
   when present.
3. WHEN code calls `Activator.GetObject` AND semantic analysis resolves the
   requested type to a `MarshalByRefObject`-derived type, or the call is
   co-located in the same file with channel or Remoting registration evidence,
   THEN TraceMap SHALL emit Remoting client activation evidence with hashed URL
   or object URI when present; otherwise TraceMap SHALL emit a review gap rather
   than a Remoting activation fact.
4. WHEN code calls related configuration APIs such as
   `Configure`, `RegisterActivatedServiceType`, or
   `RegisterActivatedClientType` THEN TraceMap SHOULD emit supported facts or
   documented gaps when the v1 scope intentionally omits them.
5. WHEN registration argument parsing cannot safely identify the target type,
   mode, URI, or URL THEN TraceMap SHALL preserve the call evidence and emit a
   limitation or gap without fabricating missing fields.

### Requirement 5: `<system.runtime.remoting>` Config Evidence

**User Story:** As a maintainer, I want app configuration files to reveal
checked-in Remoting declarations without leaking endpoint values.

Acceptance Criteria:

1. WHEN XML config contains a `<system.runtime.remoting>` section THEN TraceMap
   SHALL emit a config-section fact with repository-relative path, line span
   when available, rule ID, evidence tier, commit SHA, and extractor version.
2. WHEN config declares application channels, service well-known entries,
   activated service types, client well-known entries, activated client types,
   or channel providers THEN TraceMap SHALL emit structured Remoting config
   facts for safe identifiers.
3. WHEN config contains URLs, ports, object URIs, application names, provider
   properties, or arbitrary values THEN TraceMap SHALL hash or omit unsafe
   values and SHALL NOT render raw values in `facts.ndjson`, `index.sqlite`,
   `report.md`, logs, or review summaries.
4. WHEN XML parsing fails, external config includes are unavailable, or config
   transforms cannot be resolved THEN TraceMap SHALL emit `AnalysisGap` facts
   and mark coverage reduced.
5. WHEN config files use unsupported custom sections or encrypted sections THEN
   TraceMap SHALL label the gap and SHALL NOT claim clean absence of Remoting.

### Requirement 6: Reporting, Facts, and Rule Catalog

**User Story:** As a reviewer, I want every Remoting conclusion to be
evidence-backed and distinguishable from WCF/SVC evidence.

Acceptance Criteria:

1. WHEN Remoting facts are emitted THEN each fact SHALL include a Remoting rule
   ID, evidence tier, supporting file path, line span when available, commit
   SHA, and extractor version.
2. WHEN scan reports summarize legacy service boundaries THEN Remoting MAY be
   grouped under generic legacy service-boundary sections, but Remoting facts
   SHALL keep Remoting-specific fact types and rule IDs.
3. WHEN WCF/SVC and Remoting evidence appear in the same repository THEN
   TraceMap SHALL not merge Remoting registrations into WCF endpoint, contract,
   operation, host, or mapping facts.
4. WHEN coverage is reduced because MSBuild load or semantic analysis fails THEN
   TraceMap SHALL still run Remoting syntax/config extraction and label the scan
   as partial or reduced coverage.
5. WHEN rule catalog entries are added THEN each rule SHALL document limitations
   for static evidence, dynamic configuration, runtime reachability, deployment,
   network availability, ambiguous names, and unsupported remoting shapes.

### Requirement 7: Validation and Safety

**User Story:** As a maintainer, I want implementation work to be testable on
fixtures and future public smoke repositories without committing private or
generated artifacts.

Acceptance Criteria:

1. Tests SHALL cover syntax fallback for namespaces, known Remoting API calls,
   `MarshalByRefObject`, channel construction/registration, registration APIs,
   `Activator.GetObject`, and `<system.runtime.remoting>` config.
2. Tests SHALL cover semantic symbol resolution where the .NET analyzer test
   harness supports it, while proving syntax fallback remains useful when
   semantics are unavailable.
3. Tests SHALL assert that raw remoting URLs, object URIs, ports, config values,
   local paths, remotes, source snippets, secrets, and generated smoke artifacts
   are not stored or rendered.
4. Future local or manual smoke MAY target public repositories such as EasyHook,
   SuperPuTTY, Kalman.Studio, MediaPortal, FlatRedBall, or Reactive. No
   committed file in this spec or its implementation PRs SHALL reference clone
   paths, raw remotes, generated scan outputs, or source snippets from those
   repositories; any committed baseline from those repositories requires a
   separate reviewed spec or explicit reviewed baseline task.
5. Validation SHALL include `dotnet build`, `dotnet test`, a sample `tracemap
   scan` run when implementation changes scanner behavior, private-path checks,
   `git diff --check`, and relevant pinned smoke checks from `docs/VALIDATION.md`
   or explicit deferral with rationale.

## Non-Goals

- No runtime Remoting host activation.
- No network calls to Remoting endpoints.
- No proof that a Remoting service is deployed, reachable, secure, exploited,
  or used in production.
- No security vulnerability classification.
- No WCF/SVC mapping changes except shared generic reporting labels where
  appropriate.
- No LLM, embedding, vector, or prompt-based classification in the scanner or
  reducer.
- No raw source snippets, endpoint values, config values, local absolute paths,
  remotes, secrets, or generated public-repo artifacts in committed files.
