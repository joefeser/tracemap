# Legacy ASMX/SOAP Detection Tasks

## Implementation Tasks

- [ ] 1. Add ASMX/SOAP schema, facts, and rules in the first implementation
  slice that emits ASMX facts or report rows. Requirements: 1, 2, 3, 4, 5, 7.
  - [x] Add ASMX/SOAP fact types or equivalent additive schema fields for host, service class, operation, generated client, client operation, metadata, config, and mapping evidence.
  - [x] Migrate current `.asmx` host coverage out of `legacy.wcf.host.v1` and `WcfServiceHostDeclared` for new indexes, narrowing WCF host behavior to WCF/SVC while adding ASMX-specific host facts and rule IDs.
  - [ ] Define older-index compatibility for ASMX evidence previously stored as `WcfServiceHostDeclared`, using migration limitations or availability gaps rather than clean absence.
  - [x] Add rule catalog entries for `legacy.asmx.host.v1`, `legacy.asmx.service.v1`, `legacy.asmx.operation.v1`, `legacy.asmx.client.v1`, `legacy.asmx.metadata.v1`, `legacy.asmx.config.v1`, `legacy.asmx.mapping.v1`, and any report/composition rule that emits independent rows in the same implementation PR that first emits those rows.
  - [x] If `legacy.asmx.flow.v1` is used, add it to the rule catalog in the same slice before emitting report/composition rows; otherwise cite existing `legacy.flow.*` traversal rules plus ASMX source rules.
  - [x] Update existing `legacy.wcf.host.v1` and `legacy.wcf.metadata.v1` descriptions/limitations in the same implementation PR when their ownership is narrowed or disambiguated.
  - [x] Document limitations for static evidence, generated proxies, aliases/lookalikes, dynamic endpoints, external WSDL imports, encrypted config, deployment, reachability, auth, runtime execution, and impact.
  - [x] Match the existing `rules/rule-catalog.yml` convention, including evidence tier fields, emitted fact types, emitted `AnalysisGap` rows, and documented limitations.
  - [x] Preserve commit SHA, extractor version, file path, line span, evidence tier, rule ID, coverage label, and supporting fact IDs in emitted facts.

- [x] 2. Implement `.asmx` host and directive extraction. Requirements: 1, 7, 8.
  - [x] Inventory `.asmx` and related code-behind files with safe repository-relative paths.
  - [x] Parse WebService directives for safe `Language`, `CodeBehind`, `CodeFile`, and `Class` metadata.
  - [x] Hash or omit unsupported/unsafe directive values.
  - [x] Emit `AnalysisGap` facts for malformed, missing, generated, or unsupported directives.
  - [x] Keep ASMX host facts separate from WCF `.svc` host facts.
  - [x] Update the existing WCF ASMX host test coverage so `.svc` remains under WCF and `.asmx` moves to ASMX-specific facts.
  - [x] Update file inventory so `.asmx` uses `AsmxServiceHost` rather than the generic WCF service-host kind for new indexes.

- [ ] 3. Implement C# service and operation attribute extraction. Requirements: 2, 7, 8.
  - [x] Detect `[WebService]`, `[WebServiceBinding]`, `[ScriptService]`, `[SoapDocumentService]`, and `[SoapRpcService]` class-level evidence.
  - [x] Detect `[WebMethod]`, `[SoapDocumentMethod]`, and `[SoapRpcMethod]` method-level evidence.
  - [x] Emit deterministic evidence for dual-attribute methods carrying both `[WebMethod]` and SOAP operation attributes without duplicate wrong-tier rows.
  - [ ] Use semantic symbol resolution when available and syntax fallback when builds fail.
  - [x] Cap ambiguous/project-defined attribute matches at review tier or emit gaps.
  - [x] Ignore comments, string literals, XML docs, and inactive preprocessor regions.
  - [x] Preserve safe service/operation identity metadata without source snippets.

- [ ] 4. Implement generated SOAP client/proxy detection. Requirements: 3, 7, 8.
  - [x] Detect `SoapHttpClientProtocol` inheritance and generated SOAP proxy shapes.
  - [x] Detect generated client methods, SOAP method attributes, `Invoke`/`BeginInvoke`/`EndInvoke` wrappers, and generated-code markers.
  - [x] Capture safe generated client class and operation identities.
  - [ ] Emit gaps for dynamic proxy factories, ambiguous generated shapes, or unresolved runtime endpoint assignment.
  - [x] Keep generated SOAP client evidence separate from WCF generated-client facts.

- [ ] 5. Implement checked-in metadata extraction. Requirements: 4, 7, 8.
  - [x] Parse safe structure from checked-in `.wsdl`, `.disco`, `.discomap`, `.map`, and ASMX/SOAP proxy metadata files.
  - [x] Keep `.svcmap`-gated WCF metadata under `legacy.wcf.metadata.v1`; claim ASMX metadata only when ASMX/SOAP host or proxy evidence corroborates ownership.
  - [x] Ignore or gap generic `.map` files that are not in an ASMX/SOAP metadata context.
  - [x] Add tests proving a metadata file is claimed by exactly one family, not both WCF and ASMX.
  - [x] Extract safe service, binding, port type, operation, and generated proxy relationship metadata.
  - [x] Hash or omit endpoint addresses, SOAP actions, import locations, schema locations, namespaces that look unsafe, and hostnames.
  - [x] Emit gaps for malformed metadata, unsupported generated formats, external imports, or missing metadata.
  - [x] Do not fetch network resources or resolve remote imports.

- [ ] 6. Implement config extraction. Requirements: 5, 7, 8.
  - [x] Parse ASMX/SOAP-relevant `web.config` and `app.config` structures.
  - [x] Emit ASMX-specific config facts as a supplement to generic `ConfigKeyDeclared`, not as a replacement.
  - [x] Detect `system.web/webServices`, SOAP extensions, safe generated proxy URL key names, and old service protocol settings.
  - [x] Omit credentials, tokens, connection strings, and secret-like values rather than hashing them; hash or omit only allowed non-secret endpoint, hostname, local path, and config values.
  - [ ] Emit gaps for encrypted sections, transforms, external includes, machine.config dependencies, or unsupported custom sections.
  - [x] Do not stitch client and service evidence based on endpoint/config hash equality alone.

- [ ] 7. Implement static mapping evidence. Requirements: 3, 4, 6, 7.
  - [ ] Map `.asmx` directive service classes to `[WebService]` classes when static identity aligns.
  - [x] Map generated SOAP client methods to ASMX operations when service binding, operation, checked-in metadata, or semantic symbols align.
  - [x] Preserve supporting fact IDs and edge IDs in deterministic order.
  - [x] Emit ambiguity gaps for duplicate operation/service candidates.
  - [x] Keep name-only matches at Tier3 or NeedsReview and avoid arbitrary backend selection.

- [ ] 8. Integrate reports and combined consumers. Requirements: 6, 7, 8.
  - [x] Add scan report counts and limitations for ASMX/SOAP facts and gaps.
  - [x] Ensure combined indexes import ASMX/SOAP facts and preserve metadata.
  - [ ] Add or defer explicit availability gaps for combined report, paths, reverse, impact, release-review, and portfolio consumers.
  - [x] Add terminal/surface kinds such as `asmx-service`, `asmx-operation`, `asmx-client`, `asmx-config`, and `asmx-metadata` only where selector/output models can represent them safely.
  - [x] If `legacy.asmx.flow.v1` is not used in a slice, update that slice's implementation state note with the existing `legacy.flow.*` rule IDs cited instead.
  - [x] Keep WCF, Remoting, and ASMX inventories and selectors distinct.

- [ ] 9. Add safety and redaction gates. Requirements: 1, 3, 4, 5, 6, 8.
  - [x] Reject or sanitize raw WSDL URLs, SOAP actions, endpoint addresses, config values, hostnames, credentials, tokens, connection strings, local paths, remotes, source snippets, analyzer logs, stack traces, and private sample names.
  - [x] Use context-separated deterministic hashes only for allowed non-secret values.
  - [x] Ensure diagnostics include sanitized categories and file/JSON pointers without echoing unsafe values.
  - [x] Preserve deterministic ordering and byte-stable output for fixed inputs.
  - [x] Ensure `./scripts/check-private-paths.sh` passes without machine-specific allowlists.

- [ ] 10. Add fixtures and tests. Requirements: 1, 2, 3, 4, 5, 6, 7, 8.
  - [x] Add synthetic `.asmx` directive fixtures, malformed directive fixtures, and code-behind linkage fixtures.
  - [ ] Add semantic and syntax-fallback C# attribute fixtures for WebService/WebMethod/SOAP attributes.
  - [ ] Add generated SOAP client/proxy fixtures, including ambiguous and dynamic proxy cases.
  - [x] Add checked-in WSDL/DISCO/proxy metadata fixtures with safe and unsafe planted values.
  - [x] Add WCF `.svcmap` negative ownership tests proving ASMX emits no facts for `.svcmap`-gated metadata.
  - [x] Add tests proving `.svcmap` remains WCF-owned even when ASMX host or generated SOAP proxy evidence exists nearby.
  - [x] Add ASMX `Web References` versus WCF `Service References` folder-shape tests proving deterministic metadata ownership and no duplicate family facts.
  - [x] Add config fixtures for ASMX/SOAP keys, SOAP extensions, encrypted/unsupported sections, and raw value redaction.
  - [x] Add config fixture tests asserting that hash-only key/value alignment does not produce an `AsmxServiceReferenceMapping` edge.
  - [x] Add generic `.js.map` or CSS source map fixtures proving generic source maps are not claimed as ASMX metadata.
  - [ ] Add mapping tests for strong semantic, structural metadata-backed, syntax-only, duplicate candidate, and missing metadata cases.
  - [ ] Add mapping tier-cap tests proving a Tier2 metadata file does not upgrade a name-only generated-client or operation match beyond the weakest required leg.
  - [ ] Add combined/path/reverse/report tests or explicit availability-gap tests for consumers touched by the implementation.
  - [ ] Add tests proving WCF/SVC and Remoting facts remain separate from ASMX facts.
  - [x] Add regression tests that `.svc` hosts still emit WCF host facts and `.asmx` hosts no longer emit `WcfServiceHostDeclared` in new indexes.
  - [x] Add regression tests that `.asmx` hosts no longer emit `legacy.wcf.host.v1` in new indexes after the migration.
  - [ ] Add older-index tests for ASMX evidence stored under historical WCF host facts.
  - [ ] Add consumer-level older-index tests proving combined report, reverse, release-review, or another touched consumer exposes historical ASMX-under-WCF evidence with a migration limitation or availability gap rather than clean ASMX absence.
  - [x] Add file-inventory tests for the new `.asmx` inventory kind.
  - [x] Add file-inventory tests for ASMX-owned `.discomap` metadata in a corroborated web-reference context.
  - [x] Add dual-attribute method tests for `[WebMethod]` plus `[SoapDocumentMethod]` or `[SoapRpcMethod]`.
  - [x] Add metadata ownership tests for WCF `.svcmap`-gated metadata versus ASMX-corroborated WSDL/DISCO/proxy metadata.
  - [x] Add tests that no raw URLs, SOAP actions, endpoint values, config values, source snippets, local paths, raw remotes, private names, or secrets appear in generated artifacts.

- [ ] 11. Document operator and validation workflow. Requirements: 6, 7, 8.
  - [ ] Document ASMX/SOAP fact families, rule IDs, evidence tiers, limitations, and non-goals.
  - [ ] Document optional public sample smoke guidance using neutral labels only.
  - [ ] Document that public claims require reviewed redacted summaries or checked-in fixtures.
  - [ ] Document relevant `tracemap scan`, report, paths, reverse, impact, and release-review behavior.
  - [ ] Update `docs/VALIDATION.md` if a pinned smoke or fixture validation command is added.

- [x] 12. Validate implementation. Requirements: 8.
  - [x] Update `.kiro/specs/legacy-asmx-soap-detection/implementation-state.md` with branch, scope decisions, validation, and follow-ups.
  - [x] Run focused ASMX/SOAP scanner/config/metadata/mapping tests.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.
  - [x] Run or explicitly defer relevant pinned smoke checks from `docs/VALIDATION.md`.

## Recommended PR Slices

- Slice 1: host/directive and service/operation attribute extraction.
- Slice 2: generated SOAP client and checked-in metadata extraction.
- Slice 3: mapping facts plus combined/report/path/reverse consumption.
- Slice 4: validation catalog/evidence-pack integration and public-safe demos.

## Deferred Follow-Ups

- ASMX service-side implementation continuation into downstream SQL/legacy data.
- Shared old-service-reference normalization across WCF and ASMX.
- Optional public sample evidence packs.
- Site/demo pages that consume promoted public-safe proof.
- Portfolio-level ASMX evidence summaries across many repositories.
