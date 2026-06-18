# Legacy ASMX/SOAP Detection Design

## Overview

This spec adds deterministic ASMX/SOAP evidence as a sibling legacy
service-boundary family to WCF/SVC and .NET Remoting. The implementation should
extend the .NET scanner, config/metadata parsers, storage/report surfaces, and
legacy-flow consumers in additive slices.

The intended static evidence chain is:

```text
ASMX host directive or WebService class
  -> WebMethod or SOAP operation attribute
  -> generated SOAP proxy / checked-in WSDL or DISCO metadata
  -> static caller evidence from WebForms/controller/service code
  -> optional downstream legacy data or SQL evidence already indexed
```

Every arrow is static evidence. The feature must not prove runtime hosting,
SOAP request execution, endpoint reachability, deployment, credential validity,
production usage, vulnerability status, or impact.

## Relationship To Existing Legacy Work

ASMX/SOAP is a sibling evidence family:

| Existing area | Relationship |
| --- | --- |
| WCF/SVC | ASMX may share old service-reference folders and generated proxy ideas, but fact types/rule IDs remain ASMX-specific unless a future shared normalization spec exists. |
| .NET Remoting | Both are legacy service-boundary candidates. ASMX must not reuse Remoting registration/channel/object facts. |
| WebForms event flow | WebForms handlers may call generated SOAP clients; path composition can consume ASMX mapping evidence when available. |
| Legacy data metadata | ASMX service implementations may reach DBML/EDMX/typed DataSet/SQL surfaces through existing call/path evidence. |
| Combined reports/reverse/impact/release review | These consumers may show ASMX evidence or emit availability gaps. They must not infer runtime impact from ASMX presence alone. |

### Existing ASMX-In-WCF Behavior

Current TraceMap behavior already recognizes some `.asmx` host directives under
the WCF host rule:

- `legacy.wcf.host.v1` is currently documented as WCF and ASMX host evidence.
- `LegacyWcfExtractor` recognizes both `ServiceHost` and `WebService`
  directives and emits `WcfServiceHostDeclared`.
- file inventory currently treats `.asmx` as a service-host file kind.

The ASMX/SOAP implementation must therefore be a migration, not a brand-new
parallel detector. Slice 1 should narrow `legacy.wcf.host.v1` and
`WcfServiceHostDeclared` behavior to WCF/SVC host evidence for new indexes, add
`legacy.asmx.host.v1` and ASMX-specific host facts for `.asmx`, update the
existing WCF tests, and add older-index compatibility behavior. Consumers that
read older indexes must not claim clean ASMX absence when ASMX evidence is still
stored under `WcfServiceHostDeclared`; they should either expose it as legacy
ASMX-compatible host evidence with a migration limitation or emit an
availability gap.

This spec-only branch does not update `rules/rule-catalog.yml`; those catalog
edits belong in the implementation slice that first changes emitted facts or
report rows. That implementation slice must update the new `legacy.asmx.*`
entries and narrow the existing `legacy.wcf.host.v1` / `legacy.wcf.metadata.v1`
descriptions before shipping the new evidence.

Current WCF metadata behavior also owns service-reference metadata such as
checked-in WSDL/DISCO files when WCF service-reference evidence is present. The
ASMX implementation must coordinate with the shipped WCF metadata normalization
work so WCF-owned metadata stays WCF-owned, ASMX-owned metadata is claimed only
under the corroboration rules below, and no file emits duplicate family facts.

## Proposed Fact Types

Suggested scanner fact types:

- `AsmxHostDeclared`
- `AsmxServiceClassDeclared`
- `AsmxOperationDeclared`
- `AsmxSoapOperationDeclared`
- `AsmxGeneratedClientDeclared`
- `AsmxClientOperationDeclared`
- `AsmxProxyMetadataDeclared`
- `AsmxConfigDeclared`
- `AsmxServiceReferenceMapping`

Use existing `AnalysisGap` for malformed `.asmx`, ambiguous attributes,
unsupported generated proxies, external WSDL imports, encrypted config,
dynamic proxy factories, duplicate operation candidates, and missing extractor
capability.

`AsmxServiceReferenceMapping` is intentionally distinct from
`WcfServiceReferenceMapping`. Report inventories may group both under old
service-reference evidence, but fact type, rule ID, mapping limitations, and
consumer behavior remain separate.

## Proposed Rule IDs

Initial rule catalog entries:

- `legacy.asmx.host.v1`
  - `.asmx` inventory and WebService directive evidence.
  - Limitations: checked-in host declarations only; no ASP.NET hosting,
    deployment, route availability, or runtime activation proof.
- `legacy.asmx.service.v1`
  - `[WebService]`, `[WebServiceBinding]`, `[ScriptService]`, and service class
    evidence.
  - Limitations: static attributes only; aliases/lookalikes may reduce
    confidence without semantic resolution.
- `legacy.asmx.operation.v1`
  - `[WebMethod]`, `[SoapDocumentMethod]`, `[SoapRpcMethod]`, and operation
    evidence.
  - Limitations: static operation declarations only; no request execution,
    binding compatibility, auth, or endpoint reachability proof.
- `legacy.asmx.client.v1`
  - generated SOAP client/proxy class and operation evidence.
  - Limitations: generated client shape only; dynamic proxy factories and
    runtime endpoint selection are gaps or review-tier evidence.
- `legacy.asmx.metadata.v1`
  - checked-in WSDL/DISCO/proxy metadata.
  - Limitations: checked-in metadata only; external imports and remote WSDL are
    not fetched.
- `legacy.asmx.config.v1`
  - config keys/sections relevant to ASMX/SOAP clients and services.
  - Limitations: checked-in config only; raw values are omitted or hashed.
- `legacy.asmx.mapping.v1`
  - static generated-client to service/operation mapping evidence.
  - Limitations: probable static mapping only; ambiguity does not choose a
    winner and does not prove runtime calls.
- `legacy.asmx.flow.v1`
  - optional report/composition rule for ASMX path/reverse surfaces if needed.
  - Limitations: possible static path only; no runtime execution or impact.

## Evidence Tiers

| Evidence | Tier |
| --- | --- |
| Compiler-resolved framework ASMX/SOAP attributes and method/type symbols | `Tier1Semantic` |
| Compiler-resolved `SoapHttpClientProtocol` inheritance or generated proxy base symbols | `Tier1Semantic` |
| Parseable `.asmx` directives, checked-in WSDL/DISCO/proxy metadata, and safe config structures | `Tier2Structural` |
| Syntax-only attributes, proxy naming patterns, generated-code markers, and name-only operation matches | `Tier3SyntaxOrTextual` |
| Malformed files, ambiguous attributes, external imports, encrypted config, dynamic factories, duplicate mappings, missing extractor capability | `Tier4Unknown` through `AnalysisGap` |

Semantic evidence improves identity and de-duplication but must not be required
for syntax/config/metadata fallback.

## File And Metadata Inventory

The extractor should inspect:

- `.asmx`
- `.asmx.cs`
- generated service reference/proxy `.cs`
- `.wsdl`
- `.disco`
- `.discomap`
- `.map`
- legacy ASMX `Web References` folders and equivalent generated web-reference
  metadata shapes
- `.svcmap` remains WCF-owned
- `web.config`, `app.config`, and related XML config files

Inventory uses repository-relative paths only. Source snippets and raw local
paths are never stored.

Metadata ownership must be deterministic:

- `.svcmap`-gated WCF service-reference metadata remains under
  `legacy.wcf.metadata.v1`.
- WCF `Service References` folder shapes remain WCF-owned unless ASMX/SOAP
  evidence is stronger and no `.svcmap` ownership signal exists.
- Legacy ASMX `Web References` folder shapes may be ASMX-owned when corroborated
  by `.asmx`, `SoapHttpClientProtocol`, SOAP attributes, WSDL/DISCO, or
  generated SOAP proxy evidence.
- `.discomap` files may be ASMX-owned when they appear in a corroborated ASMX
  web-reference context.
- ASMX metadata claims `.wsdl`, `.disco`, `.discomap`, `.map`, or proxy metadata
  only when corroborated by ASMX host/proxy evidence such as `.asmx` files,
  `SoapHttpClientProtocol`, SOAP attributes, or ASMX-specific proxy metadata.
- Ambiguous `.map` files are ignored or reported as gaps unless they are in an
  ASMX/SOAP service-reference context; generic JS/CSS source maps are not ASMX
  evidence.
- One metadata file should not produce both WCF and ASMX metadata facts. If
  ownership cannot be decided, emit a sanitized ambiguity gap instead of
duplicate facts.

The target file inventory kind for `.asmx` files in new indexes should be
`AsmxServiceHost`, replacing the current generic service-host classification for
this file extension.

## ASMX Directive Parser

Use a tolerant directive parser because `.asmx` files often contain one line of
ASP.NET directive syntax rather than XML.

Safe directive fields:

- `language`
- `codeBehindFile`
- `codeFile`
- `serviceClassName`
- `sourceKind`
- `directiveHash` when unsupported attributes exist

Code-behind and code-file values should be reduced to a bare file name when
safe; values containing path separators, environment markers, URLs, or other
unsafe content are omitted or hashed. Other unsafe directive values are omitted
or hashed. Malformed directives emit `AnalysisGap` with `legacy.asmx.host.v1`.

## C# Attribute Extraction

Detect attributes using Roslyn syntax nodes:

- `WebService`
- `WebServiceBinding`
- `ScriptService`
- `WebMethod`
- `SoapDocumentService`
- `SoapRpcService`
- `SoapDocumentMethod`
- `SoapRpcMethod`

Semantic pass:

- resolve attribute symbols against framework assemblies when possible;
- include fully qualified service class and operation symbols;
- include assembly name/version when available through existing symbol
  metadata conventions.

Syntax fallback:

- emit review-tier evidence for attribute names;
- preserve line spans and safe identifiers;
- mark limitations for aliases/project-defined lookalikes.

Do not scan comments, strings, disabled preprocessor regions, or XML docs as
ASMX evidence.

When a method has both `[WebMethod]` and SOAP operation attributes, emit one
`AsmxOperationDeclared` fact for the public ASMX operation declaration and one
`AsmxSoapOperationDeclared` fact for SOAP binding/action shape when each
attribute is independently visible. Link them through supporting fact IDs or
shared method symbol metadata; do not collapse them into one row because the
rules and limitations differ.

## Generated SOAP Client Detection

Candidate generated clients:

- class derives from `SoapHttpClientProtocol`;
- class or methods carry generated-code attributes and SOAP method attributes;
- methods call `Invoke`, `BeginInvoke`, `EndInvoke`, or generated async wrappers
  in a `SoapHttpClientProtocol` subclass;
- files live under old service-reference/proxy folders with checked-in WSDL or
  DISCO metadata;
- metadata maps generated proxy operations to WSDL operation names.

Generated proxy evidence is not proof of runtime endpoint use. Dynamic endpoint
assignment, config-backed URL values, credentials, and factory-generated proxy
instances remain gaps or review-tier evidence.

## WSDL/DISCO/Proxy Metadata

Parse checked-in metadata only. Never fetch imports.

Safe fields:

- service name
- port type name
- binding name
- operation name
- target namespace hash or safe namespace token
- generated proxy class name when metadata explicitly maps it
- metadata file kind and parser coverage

Unsafe fields:

- endpoint addresses;
- SOAP action URIs;
- import/schema locations;
- credentials or tokens;
- hostnames and environment-specific values.

Unsafe fields are omitted or represented by context-separated hashes. Hashing
must not be used for secret-like or credential-like values.

## Config Extraction

Config extraction should focus on safe ASMX/SOAP indicators:

- `system.web/webServices`
- SOAP extension declarations
- generated proxy URL keys in `appSettings` only when key names are safe and
  values are hashed/omitted
- protocol settings or old web service client configuration where parseable

Config evidence can support mapping only when static key names or generated
metadata align with the proxy/service evidence. Hash equality alone is not
enough to stitch client and service evidence.

ASMX-specific config evidence supplements generic `ConfigKeyDeclared` facts.
The ASMX extractor may emit `AsmxConfigDeclared` for service-boundary context,
but it should not suppress generic config-key extraction unless a future
config-normalization spec defines that migration.

## Mapping Strategy

Mapping candidates are created only from deterministic static evidence:

1. ASMX directive service class matches a `[WebService]` class symbol/name.
2. `[WebMethod]` operation matches checked-in WSDL/proxy operation metadata.
3. Generated proxy method matches operation metadata and service binding.
4. Caller code reaches generated proxy method through call/object evidence.
5. Optional WebForms handler roots reach the generated proxy call through
   existing WebForms event flow evidence.

Classification:

- `Tier1Semantic`: symbol-resolved generated proxy, service class, operation,
  and caller edges align.
- `Tier2Structural`: checked-in `.asmx` directive, WSDL/DISCO/proxy metadata,
  and generated client operation align.
- `Tier3SyntaxOrTextual`: name-only match or syntax-only generated proxy
  pattern.
- `Tier4Unknown`: duplicate candidates, missing metadata, dynamic endpoint,
  unsupported proxy factory, or parse failure.

Mapping classification is capped by the weakest required leg. A parseable WSDL
or DISCO file may be `Tier2Structural`, but a generated-client mapping that
lacks aligned config or semantic evidence remains `Tier3SyntaxOrTextual` or
`NeedsReview`. The mapper must not upgrade a name-only operation match merely
because an adjacent metadata file parsed successfully.

Do not select an arbitrary backend implementation when multiple candidates
match. Emit all review candidates or an ambiguity gap.

## Combined And Flow Consumption

Suggested terminal/surface kinds for downstream consumers:

- `asmx-service`
- `asmx-operation`
- `asmx-client`
- `asmx-config`
- `asmx-metadata`

Path/reverse output may consume these as static evidence once the scanner emits
facts. WCF, Remoting, and ASMX remain separate families in inventory and
selector behavior.

If `legacy.asmx.flow.v1` emits independent report rows in a later slice, its
rule catalog entry must be added before those rows ship. Slices that only reuse
existing traversal rules should cite the existing `legacy.flow.*` rules and the
source ASMX extraction rules instead.

Flow wording:

- Allowed: "static ASMX evidence", "possible static path", "SOAP proxy
  candidate", "ASMX operation candidate".
- Avoid: "SOAP request executed", "service is deployed", "endpoint reachable",
  "runtime backend", "impacted service", "vulnerable endpoint".

## Redaction And Determinism

All JSON/Markdown/log output must reject or sanitize:

- local absolute paths and home fragments;
- raw remotes and hostnames;
- raw WSDL URLs, SOAP actions, endpoint addresses, object values, and config
  values;
- connection strings, credentials, tokens, secrets;
- source snippets, analyzer diagnostics, stack traces;
- private sample names or operator-local labels.

Fully qualified framework/application type names and operation symbols are safe
identifiers when they come from source or compiler evidence. The redaction
target is operator-local labels, sample/repo names, paths, endpoints, values,
and raw diagnostic text, not ordinary code symbols already represented by
existing TraceMap facts.

Arrays/maps are sorted ordinally. IDs are stable and context separated. Generated
outputs are byte-stable under fixed inputs.

## Implementation Slices

Recommended PR slices:

1. ASMX host/directive and C# service/operation attribute extraction.
2. Generated SOAP client/proxy and checked-in WSDL/DISCO metadata extraction.
3. Mapping evidence and combined/path/reverse/report consumption.
4. Legacy sample smoke validation and public-safe evidence-pack hooks.

Each slice must update rules, tests, docs, and implementation state before PR.

## Non-Goals

- No runtime ASP.NET hosting or SOAP calls.
- No remote WSDL download or schema import resolution.
- No config-transform/machine.config execution.
- No dependency-injection, reflection, dynamic proxy, or branch-feasibility
  proof.
- No public site claims; site promotion requires separate reviewed proof.
