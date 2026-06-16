# Legacy Remoting Detection Design

## Overview

This spec adds deterministic evidence for .NET Remoting as a sibling legacy
service-boundary detector to WCF/SVC. The first implementation should extend the
existing .NET scanner, config extraction, rule catalog, storage, and report
surfaces without introducing a new runtime analysis layer.

The intended static evidence chain is:

```text
Remoting API reference
  -> channel construction or channel registration
  -> service/client registration or config declaration
  -> MarshalByRefObject service object candidate
  -> optional call edge or flow report context in later consumers
```

Every arrow is static evidence. TraceMap must not claim runtime reachability,
actual host startup, remoting object lifetime, network availability, production
usage, security exposure, exploitability, or branch feasibility.

## Relationship To Existing Legacy Boundary Work

Remoting is not WCF/SVC. The implementation should avoid reusing WCF fact types
such as `WcfClientEndpointDeclared`, `WcfServiceContractDeclared`,
`WcfServiceHostDeclared`, or `WcfServiceReferenceMapping`.

Shared reporting may refer to "legacy service boundaries" when presenting WCF,
ASMX, SVC, and Remoting counts together, but source facts and rule IDs must stay
Remoting-specific.

Flow composition or path reporting may consume Remoting facts in a future slice.
This spec only creates the scanner and report evidence needed for those later
consumers.

## Proposed Fact Types

Add fact types only where existing generic facts cannot safely express the
evidence. Suggested v1 fact types:

- `RemotingApiUsageDeclared`
- `RemotingMarshalByRefObjectDeclared`
- `RemotingChannelDeclared`
- `RemotingChannelRegistered`
- `RemotingServiceTypeRegistered`
- `RemotingClientTypeRegistered`
- `RemotingClientActivationDeclared`
- `RemotingConfigSectionDeclared`
- `RemotingConfigChannelDeclared`
- `RemotingConfigServiceDeclared`
- `RemotingConfigClientDeclared`
- `RemotingConfigProviderDeclared`

Use existing `AnalysisGap` facts for malformed config, unsupported dynamic
registration, unresolved ambiguity, external includes, encrypted sections, and
semantic analysis gaps.

## Proposed Rules

Suggested rule catalog entries:

- `legacy.remoting.api.v1`
  - Covers namespace references, known Remoting API symbols, and syntax fallback
    for Remoting API usage.
  - Limitations: static references only; ambiguous aliases or project-defined
    lookalikes are review evidence unless semantic symbols resolve.

- `legacy.remoting.marshal-by-ref.v1`
  - Covers `MarshalByRefObject` inheritance evidence.
  - Limitations: inheritance only shows Remoting-capable object shape; it does
    not prove hosting, registration, activation, reachability, or lifetime.

- `legacy.remoting.channel.v1`
  - Covers `TcpChannel`, `HttpChannel`, `IpcChannel`, server/client channel
    variants, and `ChannelServices.RegisterChannel`.
  - Limitations: static construction/registration only; dynamic properties,
    config-backed values, provider chains, and factory methods may be gaps or
    review-tier evidence.

- `legacy.remoting.registration.v1`
  - Covers `RemotingConfiguration.RegisterWellKnownServiceType`,
    `RegisterWellKnownClientType`, related activated type registration APIs,
    `Configure`, and `Activator.GetObject`.
  - Limitations: static registration/activation evidence only; object URI and
    URL values are hashed or omitted; overloads and dynamic arguments can reduce
    coverage.

- `legacy.remoting.config.v1`
  - Covers `<system.runtime.remoting>` config sections, channels, service
    declarations, client declarations, and providers.
  - Limitations: checked-in XML only; config transforms, external includes,
    encrypted sections, machine.config, and runtime configuration mutation are
    not resolved in v1.

No separate report rule is proposed for v1. Report summaries should preserve
the source Remoting extraction rule IDs and limitations rather than emitting a
new report-only fact. If a future report emits independent Remoting summary
facts, that implementation must add a separate documented rule first.

## Evidence Tiers

Use conservative evidence tiering:

| Evidence | Tier |
| --- | --- |
| Compiler-resolved known Remoting API symbol | `Tier1Semantic` |
| Compiler-resolved `MarshalByRefObject` inheritance | `Tier1Semantic` |
| Parseable `<system.runtime.remoting>` section with known child elements | `Tier2Structural` |
| Syntax-only channel or registration pattern with unresolved symbols | `Tier3SyntaxOrTextual` |
| Syntax-only namespace, type, method, or attribute-name match | `Tier3SyntaxOrTextual` |
| Malformed config, unsupported dynamic setup, ambiguous aliases, missing semantics, external includes, encrypted sections | `Tier4Unknown` through `AnalysisGap` |

Semantic evidence can improve identities and deduplicate syntax evidence, but it
must not be a prerequisite for the syntax and config passes.

## C# Extraction

### File Selection

Use the existing C# scanner file inventory. Remoting extraction should run over
`.cs` files even when MSBuild project loading fails, using the scanner's syntax
fallback path.

### Namespace And API Usage

Detect using Roslyn syntax nodes rather than raw text scanning:

- `using System.Runtime.Remoting`
- `using System.Runtime.Remoting.Channels`
- `using System.Runtime.Remoting.Channels.Tcp`
- `using System.Runtime.Remoting.Channels.Http`
- `using System.Runtime.Remoting.Channels.Ipc`
- fully qualified references beginning with `System.Runtime.Remoting`
- known type references such as `RemotingConfiguration`, `ChannelServices`,
  `WellKnownObjectMode`, `ObjRef`, `RemotingServices`, `IChannel`,
  `IChannelReceiver`, and `IChannelSender`

When semantic resolution succeeds, use symbol identity and known framework
assembly metadata where available. When it fails, emit syntax evidence with
explicit limitations for aliasing and project-defined lookalikes.

Syntax fallback must ignore comments, string literals, XML doc text, and
inactive preprocessor regions. A string containing `System.Runtime.Remoting` is
not Remoting evidence by itself.

### MarshalByRefObject

Detect classes inheriting from:

- `MarshalByRefObject`
- `System.MarshalByRefObject`
- semantic symbol `System.MarshalByRefObject`

Semantic detection should account for indirect inheritance when the compilation
can resolve the base type chain. Syntax fallback should only claim direct visible
base-list evidence.

Suggested safe properties:

- `typeName`
- `containingNamespace`
- `symbolId` when available
- `baseTypeName`
- `isAbstract`
- `isPartial`
- `isGenerated` when existing generated-code detection supports it
- `sourceKind`: `semantic` or `syntax`

Do not infer service hosting from inheritance alone.

For partial classes, emit one fact per contributing partial declaration using
the shared stable type identity and the declaration's own line span. Do not
merge partial declarations into one synthetic merged fact in v1.

### Channel Evidence

Detect construction or type references for:

- `TcpChannel`
- `HttpChannel`
- `IpcChannel`
- `TcpServerChannel`
- `TcpClientChannel`
- `HttpServerChannel`
- `HttpClientChannel`
- `IpcServerChannel`
- `IpcClientChannel`

Detect calls to:

- `ChannelServices.RegisterChannel`
- optional future support: `ChannelServices.UnregisterChannel`

Suggested safe properties:

- `channelKind`
- `channelDirection`: `server`, `client`, or `unknown`
- `registrationCall`: true/false
- `valueHash` for unsafe constructor/config arguments
- `sourceKind`

Do not store raw port numbers, URLs, provider values, channel names, or config
properties unless an existing safe-value policy explicitly allows them. Prefer
hashes for values that may reveal endpoint topology.

V1 channel-to-registration linking is intentionally narrow:

- link inline registrations such as `RegisterChannel(new TcpChannel(...))`;
- link same-method registrations where a single local is assigned exactly one
  visible channel construction and is passed unchanged to
  `RegisterChannel(local)`;
- do not link across methods, fields, properties, lambdas, branches,
  reassignments, collection lookups, factory helpers, dependency injection, or
  reflection.

When this boundary is not met, emit construction and registration facts
independently and add an `AnalysisGap` when the code shape suggests a possible
but unsupported link.

### Registration And Activation Evidence

Detect calls to:

- `RemotingConfiguration.RegisterWellKnownServiceType`
- `RemotingConfiguration.RegisterWellKnownClientType`
- `RemotingConfiguration.RegisterActivatedServiceType`
- `RemotingConfiguration.RegisterActivatedClientType`
- `RemotingConfiguration.Configure`
- `Activator.GetObject`
- optional future support: `Activator.CreateInstance` when semantic evidence
  proves remoting activation, otherwise emit no Remoting-specific conclusion

Suggested safe properties:

- `registrationKind`: `well-known-service`, `well-known-client`,
  `activated-service`, `activated-client`, `configure`, or `client-activation`
- `targetTypeName` or `targetSymbolId` when safe
- `objectMode`: `Singleton`, `SingleCall`, or `unknown`
- `objectUriHash`
- `urlHash`
- `configFileName` basename only for `Configure` when statically visible
- `sourceKind`

Only retain enum names and safe type identifiers. Hash or omit URI, URL, object
URI, application name, channel property, and arbitrary string values.

`Activator.GetObject` is Remoting evidence only when one of these v1 gates is
met:

- semantic analysis resolves the requested type argument to a type that derives
  from `System.MarshalByRefObject`;
- the call appears in the same file as Remoting channel construction,
  `ChannelServices.RegisterChannel`, or `RemotingConfiguration` registration
  evidence.

If neither gate is met, emit an `AnalysisGap` with `Tier4Unknown` when the call
looks potentially relevant, or omit Remoting-specific facts when there is no
Remoting context.

## Config Extraction

### File Selection

Inspect existing XML config inventory, including common `.config` files. Do not
add broad XML scanning beyond repository config files for v1.

### XML Parser Safety

Use a `LoadSafeXml`-style helper equivalent to the protected WCF metadata XML
loader, not the bare config `XDocument.Load` pattern. Remoting config files are
untrusted input, so XML parsing must:

- prohibit or ignore DTD processing;
- set `XmlResolver = null`;
- avoid external entity resolution;
- bound document size and node count where existing helpers support it;
- preserve line info when possible;
- emit `AnalysisGap` rather than raw parser messages when parsing fails.

Do not use bare `XDocument.Load` without `XmlReaderSettings` for Remoting config
files.

### Supported Shape

Detect `<system.runtime.remoting>` sections and common descendants:

- `application`
- `channels`
- `channel`
- `serverProviders`
- `clientProviders`
- `service`
- `wellknown`
- `activated`
- `client`

Use namespace-insensitive local-name matching when safe. Preserve line spans for
the section and child elements when the XML reader supplies them.

Suggested config fact properties:

- `configKind`: `section`, `channel`, `service`, `client`, `provider`
- `channelKind`
- `registrationKind`
- `typeName` only when it passes safe identifier policy
- `assemblyName` only when safe and already allowed by existing assembly
  identity conventions
- `objectMode`
- `objectUriHash`
- `urlHash`
- `applicationNameHash`
- `valueHash`
- `sourceFormat`: `xml-config`

Do not store raw values from `url`, `port`, `ref`, `name`, `objectUri`,
`application`, provider properties, or arbitrary attributes unless they pass the
same safe identifier policy used by existing WCF/data/report code. Safe type,
namespace, assembly, and basename identifiers may be retained only when that
policy allows them; when uncertain, hash or omit.

## Storage And Output

The scanner should emit Remoting facts to the existing `facts.ndjson` and
`index.sqlite` outputs with stable schema behavior. Report updates may initially
summarize counts and limitations in existing scan report sections, with richer
flow/path rendering deferred.

Fact rows must include:

- rule ID;
- evidence tier;
- repository-relative path;
- line span when available;
- commit SHA;
- extractor version;
- safe fact properties;
- supporting fact IDs where a fact links channel construction to registration.

Store supporting fact IDs as a semicolon-delimited string in a fact property
named `supportingFactIds`, consistent with existing extractor conventions. Omit
the property when no supporting facts are linked; do not store an empty string.

Register the new extractor in the scanner orchestration path, for example by
adding a `LegacyRemotingExtractor.Extract` call from `ScanEngine.cs` alongside
the existing legacy extractors. Scanner integration tests should exercise the
full scan path, not only direct extractor calls.

If MSBuild or semantic analysis fails, the scan is still partial rather than
clean. Syntax and config extraction should run and `AnalysisGap` facts should
explain the reduced coverage.

Any JSON export or index export path that preserves existing fact properties
must round-trip the new Remoting fact types and `supportingFactIds` values.

## Determinism

Implement deterministic ordering by repository-relative path, line span, fact
type, rule ID, and stable identity. Stable fact IDs should be derived from safe
inputs only. Repeated scans of the same commit should produce byte-stable JSON
where the existing scanner contract expects it.

Reuse the repository's existing value-hash/snippet-hash helper and normalization
conventions for `valueHash`, `objectUriHash`, `urlHash`,
`applicationNameHash`, and related redacted values. Do not introduce ad hoc
hashing for Remoting.

Avoid source snippets. If a future debugging option allows snippets, it must use
the existing explicit raw-snippet opt-in behavior and remain disabled by
default.

## Validation Strategy

Add focused fixtures in the .NET analyzer tests. Cover:

- namespace-only syntax evidence;
- known API symbol semantic evidence;
- `MarshalByRefObject` semantic and syntax fallback;
- channel construction and registration;
- well-known service registration with `Singleton` and `SingleCall`;
- well-known client registration;
- `Activator.GetObject`;
- `RemotingConfiguration.Configure`;
- activated service/client registration calls, including a visible-call
  deferred-support case that must emit a documented gap rather than silence;
- `<system.runtime.remoting>` section, channel, service, client, and provider
  config;
- malformed config and external/unsupported shapes;
- comments, strings, and inactive preprocessor regions that mention Remoting but
  must not emit Remoting facts;
- `Activator.GetObject` with and without Remoting gates;
- channel registration linking positive cases and unsupported cross-method or
  reassigned-local cases;
- mixed WCF and Remoting fixtures proving fact types do not bleed across
  detectors;
- scanner integration against a checked-in synthetic Remoting sample;
- JSON/export round-trip of Remoting fact types and `supportingFactIds`;
- redaction of raw URLs, object URIs, ports, config values, local paths,
  remotes, snippets, and secret-like tokens.

Future local or manual smoke can use public repositories named in the
requirements, but committed files should include only neutral labels and
redacted counts if a later spec explicitly asks for a baseline. Do not commit
raw clone paths, raw remotes, generated scan artifacts, or source snippets.

## Non-Goals

- No runtime Remoting activation, hosting, networking, or endpoint probing.
- No proof of deployment, production use, security exposure, vulnerability, or
  exploitability.
- No branch feasibility, reflection execution, dependency injection resolution,
  config transform resolution, or machine.config evaluation.
- No WCF/SVC fact reuse for Remoting-specific evidence.
- No path/impact/reducer claims beyond exposing static facts to future
  consumers.
- No LLM, embedding, vector, or prompt-based classification.
