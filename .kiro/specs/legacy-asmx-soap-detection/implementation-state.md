# Legacy ASMX/SOAP Detection Implementation State

Status: implementation-mvp
Branch: codex/implement-legacy-asmx-soap-detection
Scope: first implementation slice
Public claim level: hidden
Readiness: ready-for-pr-review-loop

## Summary

This slice adds a deterministic ASMX/SOAP evidence family to the .NET scanner.
It migrates new `.asmx` host detection out of WCF host facts, adds ASMX-specific
fact types and rule IDs, extracts syntax/static evidence for service classes,
operations, SOAP operation attributes, generated SOAP clients, checked-in
Web References metadata, selected config structures, and probable static
client-operation mappings.

The implementation remains static. It does not host ASP.NET, execute SOAP calls,
download WSDL, resolve remote imports, infer deployment, prove endpoint
reachability, validate credentials, classify vulnerabilities, or claim runtime
impact.

## Scope Decisions

- `.asmx` files are inventoried as `AsmxServiceHost`; `.svc` files remain WCF
  `ServiceHost` inventory.
- `legacy.wcf.host.v1` is narrowed for new indexes to WCF `.svc` directives.
  Historical ASMX-under-WCF compatibility remains a follow-up for older-index
  consumers.
- `legacy.asmx.flow.v1` is not used in this slice. Report and combined-surface
  rows cite ASMX source rules (`legacy.asmx.*`) and reuse existing generic
  projection/report behavior; deeper path traversal remains a follow-up under
  existing `legacy.flow.*` rule families.
- C# ASMX attribute extraction is syntax-first in this slice and emits
  `Tier3SyntaxOrTextual` review-tier facts with alias/lookalike limitations.
  Compiler-resolved `Tier1Semantic` ASMX attribute and proxy evidence remains a
  follow-up.
- ASMX metadata ownership is limited to old Web References-style paths. `.svcmap`
  gated metadata remains WCF-owned even when ASMX host evidence exists nearby.
- Credential-like values are omitted rather than hashed. Allowed non-secret
  endpoint-ish or namespace values are represented with context-separated hashes
  or safe identifiers only.

## Implemented

- Added ASMX fact constants and rule IDs for host, service, operation, generated
  client, client operation, metadata, config, and mapping evidence.
- Added `LegacyAsmxExtractor` for `.asmx` directives, C# attributes, generated
  SOAP client/proxy patterns, WSDL/DISCO/DISCOMAP/proxy map metadata, config
  structures, and conservative mapping facts.
- Updated file inventory to classify `.asmx` as `AsmxServiceHost` and old Web
  References metadata as `AsmxServiceReferenceMetadata`.
- Narrowed the WCF directive parser so `WebService` directives no longer emit
  `WcfServiceHostDeclared` in new indexes.
- Added ASMX report section and limitations, plus combined surface display names
  for `asmx-service`, `asmx-operation`, `asmx-client`, `asmx-config`, and
  `asmx-metadata`.
- Added rule catalog entries and WCF host migration limitation text.
- Added focused tests for directive parsing, malformed directives, attributes,
  dual WebMethod/SOAP attribute facts, generated SOAP clients, WSDL metadata,
  config redaction, mapping, WCF `.svcmap` ownership, generic source-map
  rejection, report wording, and `.DS_Store` ignore coverage.

## Validation

Completed:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~LegacyAsmxExtractorTests|FullyQualifiedName~LegacyWcfExtractorTests"
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out .tmp/legacy-asmx-soap-scan-smoke
./scripts/check-private-paths.sh
git diff --check
```

Result:

- Focused ASMX/WCF tests: 29 passed, 0 failed.
- Solution build: succeeded with 0 warnings and 0 errors.
- Full .NET tests: 379 passed, 0 failed.
- WCF-adjacent validation-summary unit tests: 11 passed, 0 failed.
- CLI scan smoke over a checked-in sample completed and emitted required scan artifacts.
- Private-path guard passed.
- Whitespace diff check passed.

Pinned smoke note:

- No ASMX-specific pinned smoke exists yet in `docs/VALIDATION.md`.
- The WCF/SVC validation-summary unit test is planned because this slice narrows
  WCF host ownership and metadata-adjacent inventory.
- Broader cross-language adapter commands from the global validation matrix are
  deferred because this slice changes only the .NET legacy scanner/report
  surfaces.

## Follow-Ups

- Add semantic `Tier1Semantic` ASMX attribute, service symbol, method symbol,
  and `SoapHttpClientProtocol` inheritance evidence.
- Add older-index consumer compatibility for historical ASMX evidence stored as
  `WcfServiceHostDeclared` under `legacy.wcf.host.v1`.
- Add richer generated proxy ambiguity and dynamic endpoint/factory gaps.
- Expand metadata parsing beyond WSDL operation rows while preserving WCF/ASMX
  ownership boundaries.
- Add directive-to-service-class mapping and stronger generated-client mapping
  when semantic/config/metadata legs align.
- Add explicit availability gaps or consumption behavior for combined report,
  paths, reverse, impact, release-review, and portfolio consumers.
- Add an ASMX-specific pinned smoke or public fixture after reviewed redacted
  summaries or checked-in synthetic fixtures are available.
