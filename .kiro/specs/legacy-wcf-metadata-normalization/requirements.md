# Legacy WCF Metadata Normalization Requirements

## Introduction

The first WCF/service-reference implementation can extract useful `system.serviceModel`,
`[ServiceContract]`, `[OperationContract]`, generated-client, `.svc`, and ASMX host
evidence. A follow-up smoke against old public WCF/SVC-heavy repositories showed a
specific gap: some generated clients and service contracts are present, but no
`WcfServiceReferenceMapping` facts are emitted because old generated proxies use
different static operation names for the same service operation.

Common examples include:

- generated client method `FooAsync`;
- generated interface operation `BeginFoo` / `EndFoo`;
- WSDL operation `Foo`;
- checked-in `.svcmap`, `.wsdl`, `.disco`, and `.xsd` service-reference metadata.

This phase adds deterministic local metadata extraction and conservative operation
normalization so TraceMap can map more old service-reference clients without
executing services, downloading WSDL, or claiming runtime reachability.

Public claim level: hidden until validated with redacted label-only summaries.

## Scope

In scope:

- Parse checked-in WCF/ASMX service-reference metadata files from the repository.
- Extract safe structural facts from `.svcmap` and `.wsdl` where deterministic.
- Inventory `.disco` and service-reference `.xsd` files as metadata presence
  evidence without inferring operations or DTO/schema mappings.
- Normalize generated-client and service-contract operation names using conservative
  WCF patterns.
- Improve `WcfServiceReferenceMapping` when client contract, metadata, endpoint,
  and operation evidence align.
- Emit explicit gaps for ambiguous, partial, remote-only, or unsupported metadata.
- Update rule catalog, docs, tests, and legacy validation summaries.

Out of scope:

- No WSDL download from remote URLs.
- No runtime service calls, service activation, or network probing.
- No binding compatibility analysis.
- No proof that an endpoint is deployed or reachable.
- No dynamic endpoint/config transform evaluation.
- No semantic WCF enrichment through `MSBuildWorkspace`; keep that as a separate
  follow-up.
- No WebForms event extraction, DBML/EDMX mapping, or old ORM expansion in this
  slice.
- No raw endpoint addresses, raw WSDL URLs, raw schemas, source snippets, local
  absolute paths, private repo names, config values, secrets, or connection strings
  in facts or reports.
- No LLM calls, embeddings, vector DBs, or prompt-based classification.

## Requirements

### Requirement 1: Service-Reference Metadata Inventory

**User Story:** As a maintainer, I want TraceMap to notice checked-in WCF service
reference metadata so old generated proxies can be explained without loading the
project or calling a service.

#### Acceptance Criteria

1. WHEN a repository contains checked-in `.svcmap` files in a service-reference
   folder THEN TraceMap SHALL inventory them as service-reference metadata
   evidence.
2. WHEN a repository contains checked-in `.wsdl`, `.disco`, or `.xsd` files in a
   service-reference folder THEN TraceMap SHALL inventory them as service-reference
   metadata evidence.
3. A service-reference folder SHALL be defined as either a folder co-located with
   a `.svcmap` file or a repository-relative path containing a service-reference
   segment such as `Service Reference` or `ServiceReference`; `.wsdl`, `.disco`,
   and `.xsd` files outside that scope SHALL remain out of scope for this slice.
4. WHEN metadata references remote URLs, endpoint addresses, schema locations,
   or discovery URLs THEN TraceMap SHALL hash or omit those values and SHALL NOT
   render raw URLs or addresses.
5. WHEN metadata references URL-like namespace values such as WSDL
   `targetNamespace`, XML namespace URIs, SOAP action namespaces, or schema
   namespaces THEN TraceMap SHALL hash or omit those values and SHALL only retain
   safe local identifiers such as NCName operation or portType names.
6. WHEN metadata cannot be parsed THEN TraceMap SHALL emit an `AnalysisGap`
   with rule ID and `Tier4Unknown` evidence rather than claiming no metadata
   exists.
7. Metadata inventory SHALL be deterministic and SHALL include file span,
   commit SHA, extractor version, rule ID, and evidence tier through the
   existing fact model.

### Requirement 2: WSDL and SVCMAP Operation Evidence

**User Story:** As a maintainer, I want TraceMap to derive service operation names
from checked-in metadata when generated C# names do not match exactly.

#### Acceptance Criteria

1. WHEN a checked-in WSDL file contains safe operation declarations THEN TraceMap
   SHALL emit metadata operation facts containing safe operation names and a
   metadata identity hash.
2. WHEN a checked-in `.svcmap` file links generated code to local metadata files
   THEN TraceMap SHALL emit a metadata-link fact that names only safe local
   metadata basenames or hashes, not local absolute paths or remote URLs.
3. WHEN WSDL metadata includes service/port/portType/binding names that are safe
   identifiers THEN TraceMap MAY include them as structural metadata.
4. WHEN WSDL metadata includes unsafe identifiers, complex imports, or remote
   locations THEN TraceMap SHALL omit or hash those fields.
5. Metadata-derived operation facts SHALL be no stronger than `Tier2Structural`
   and SHALL state that WSDL metadata does not prove runtime service behavior.

### Requirement 3: Operation Name Normalization

**User Story:** As a maintainer, I want generated WCF naming patterns to align
without overclaiming unrelated methods.

#### Acceptance Criteria

1. WHEN a generated client method is named `FooAsync` THEN TraceMap SHALL keep
   the original `FooAsync` operation candidate and MAY derive normalized
   operation alias `Foo` only when the client is already recognized as
   WCF-generated evidence and the stripped alias is corroborated by checked-in
   WSDL metadata, a same-contract sync sibling, or an aligned service operation.
2. WHEN an operation contract method is named `BeginFoo` and a matching `EndFoo`
   exists on the same contract THEN TraceMap SHALL derive normalized operation
   alias `Foo` for APM-style WCF operations.
3. WHEN an operation contract method is named `BeginFoo` without a credible
   matching `EndFoo` THEN TraceMap SHALL NOT derive alias `Foo`; it MAY emit a
   gap or leave the original operation unchanged.
4. WHEN an operation contract method is named `EndFoo` without a credible
   matching `BeginFoo` THEN TraceMap SHALL NOT derive alias `Foo`; it MAY emit a
   gap or leave the original operation unchanged.
5. WHEN a method is a framework lifecycle method such as `Open`, `Close`,
   `Abort`, `Dispose`, `OpenAsync`, or `CloseAsync` THEN TraceMap SHALL NOT map
   it to a service operation unless checked-in metadata explicitly names it as
   a service operation.
6. Lifecycle exclusion SHALL apply to the normalized base name as well as the
   raw method token, so `BeginOpen`/`EndOpen`, `BeginClose`/`EndClose`,
   `BeginAbort`/`EndAbort`, and any `Begin`/`End` pair whose base name is an
   excluded lifecycle verb SHALL NOT produce a service-operation alias.
7. Operation normalization SHALL preserve original operation names in fact
   properties and SHALL record the normalization rule used.
8. Operation normalization SHALL be deterministic, case-sensitive by default,
   and SHALL NOT use fuzzy matching, edit distance, stemming, or prompt-based
   classification.

### Requirement 4: Safer Service-Reference Mapping

**User Story:** As a maintainer, I want TraceMap to connect generated client
calls to service operations when metadata and contract evidence agree.

#### Acceptance Criteria

1. WHEN generated client contract, normalized client operation alias, metadata
   operation, and service contract operation align THEN TraceMap SHALL emit
   `WcfServiceReferenceMapping` evidence with mapping kind describing the
   metadata/normalization path.
2. WHEN config endpoint contract evidence also aligns THEN mapping MAY be
   `Tier2Structural`; otherwise mapping SHALL be no stronger than
   `Tier3SyntaxOrTextual` unless a future semantic pass proves stronger evidence.
   This cap applies to the final mapping fact even if supporting metadata or
   alias evidence is itself `Tier2Structural`.
3. WHEN multiple aliases converge on the same logical operation on the same
   contract, such as `Foo`, `FooAsync`, and `BeginFoo`/`EndFoo`, TraceMap SHALL
   collapse those convergent aliases into one logical operation before ambiguity
   counting.
4. Convergent aliases SHALL NOT be treated as ambiguity by themselves; ambiguity
   exists only when distinct logical operations, contracts, endpoint identities,
   metadata identities, or host candidates remain after normalization and
   deduplication.
5. WHEN multiple metadata operations, endpoint contracts, service contracts, or
   host candidates remain after normalization THEN TraceMap SHALL emit an
   ambiguity gap and SHALL NOT choose an arbitrary winner.
6. WHEN metadata exists but cannot be connected to a generated client contract
   or operation THEN TraceMap SHALL emit a metadata-unlinked gap or omit mapping,
   not a false positive.
7. Mapping facts SHALL include supporting fact IDs or supporting metadata hashes
   where the current fact model can store them safely.

### Requirement 5: Validation Against Ugly Public Samples

**User Story:** As a contributor, I want repeatable local validation that proves
the new metadata behavior improves real old-code coverage without leaking local
paths or raw artifacts.

#### Acceptance Criteria

1. Validation SHALL include the existing focused unit tests plus a local-only
   smoke against ignored WCF/SVC sample repositories.
2. The smoke summary SHALL remain label-only and SHALL NOT include local
   absolute paths, raw remotes, raw endpoint addresses, raw WSDL URLs, raw SQL,
   raw snippets, config values, secrets, or connection strings.
3. The local smoke SHALL compare before/after counts for:
   `WcfServiceReferenceMapping`, metadata facts, metadata gaps, generated clients,
   service contracts, operation contracts, endpoints, and hosts.
4. The `Zero-K-Infrastructure` public sample at commit
   `48f6f09bc1d0266f204026580671ce867f75d6bd` SHOULD be used as a validation
   target because it contains checked-in `.svcmap`, `.wsdl`, `.disco`, and `.xsd`
   service-reference metadata and currently produces generated clients and
   operations but zero service-reference mappings.
5. Validation results SHALL clearly label reduced coverage when project load or
   semantic analysis is partial.

### Requirement 6: Documentation and Rules

**User Story:** As a reviewer, I want every new metadata conclusion to carry rule
IDs and documented limitations.

#### Acceptance Criteria

1. New or changed rule IDs SHALL be documented in `rules/rule-catalog.yml`
   before implementation is considered complete.
2. `docs/LANGUAGE_ADAPTER_CONTRACT.md`, `docs/VALIDATION.md`, or
   `docs/ACCEPTANCE.md` SHALL be updated if new fact types or validation
   commands are added.
3. Documentation SHALL say that checked-in metadata is static design-time
   evidence and does not prove runtime reachability, deployment, service
   version compatibility, authorization, binding compatibility, or branch
   feasibility.
4. Reports and JSON outputs SHALL remain deterministic and safe to publish.
