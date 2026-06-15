# Requirements

## Introduction

Legacy .NET applications often call backend systems through WCF, ASMX, WSDL
generated proxies, `.svc` hosts, and `system.serviceModel` config rather than
modern HTTP clients. TraceMap currently extracts useful click-handler, call,
HTTP, SQL, config, and database facts from old repositories, but it does not
explain the static translation layer between a generated service client call and
the backend service operation it may represent.

This phase adds deterministic static evidence for that old service-reference
layer. It must not execute services, fetch WSDL, call endpoints, or render raw
addresses, config values, source snippets, local paths, private repository
names, or secrets.

Public claim level: hidden until validated against redacted legacy summaries.

## Requirements

### Requirement 1: WCF and Service Config Evidence

**User Story:** As a maintainer, I want TraceMap to identify old service
endpoints from config without leaking endpoint addresses.

Acceptance Criteria:

1. WHEN a config file contains `system.serviceModel/client/endpoint` entries
   THEN TraceMap SHALL emit endpoint facts with endpoint name, binding,
   contract, and address hash, not raw address.
2. WHEN a config file contains service/host endpoint declarations THEN TraceMap
   SHALL emit service host endpoint facts with service name, binding, contract,
   and address hash where available.
3. WHEN config endpoint data is partial THEN TraceMap SHALL keep the fact but
   mark missing fields through properties or analysis gaps.
4. WHEN endpoint addresses, connection strings, or config values are available
   THEN TraceMap SHALL hash or omit values and SHALL NOT render them in reports.

### Requirement 2: Service Contract and Operation Evidence

**User Story:** As a maintainer, I want TraceMap to see old WCF service
contracts and operations even when the project cannot build fully.

Acceptance Criteria:

1. WHEN C# code declares `[ServiceContract]` THEN TraceMap SHALL emit a service
   contract fact with rule ID and evidence tier.
2. WHEN C# code declares `[OperationContract]` methods THEN TraceMap SHALL emit
   operation facts linked to the containing contract or type.
3. WHEN generated service-reference or proxy classes are detected THEN TraceMap
   SHALL emit generated-client facts without storing source snippets.
4. WHEN semantic analysis is available THEN service contract/operation facts
   SHOULD use fully qualified symbols; otherwise syntax-level names are
   acceptable and must be labeled accordingly.

### Requirement 3: Service Host Evidence

**User Story:** As a maintainer, I want TraceMap to identify old `.svc` or ASMX
host declarations as static service surfaces.

Acceptance Criteria:

1. WHEN `.svc` files exist THEN TraceMap SHALL inventory them and emit service
   host facts for safe attributes such as service name and factory name.
2. WHEN ASMX service files exist THEN TraceMap SHOULD inventory them and emit
   host facts when safe service/class attributes are available.
3. WHEN host declarations cannot be parsed THEN TraceMap SHALL emit an analysis
   gap rather than claiming no host exists.

### Requirement 4: Probable Client-to-Service Mapping

**User Story:** As a maintainer, I want TraceMap to connect generated service
client calls to service contracts or host declarations when static evidence is
credible.

Acceptance Criteria:

1. WHEN a generated client method name matches an operation contract and config
   contract names align THEN TraceMap SHALL emit probable service-reference
   mapping evidence.
2. WHEN a click handler calls a generated service client method THEN downstream
   reports SHALL be able to show the static client-call edge and the probable
   service operation mapping.
3. WHEN only method names match without config or contract support THEN TraceMap
   SHALL classify the evidence as syntax/textual or needs-review.
4. WHEN mapping cannot be established THEN TraceMap SHALL emit a gap or omit the
   mapping, not choose an arbitrary backend implementation.

### Requirement 5: Validation Baseline

**User Story:** As a maintainer, I want to preserve the current parser snapshot
so improvements can be measured without retconning what the tool already did.

Acceptance Criteria:

1. WHEN this spec is added THEN a committed baseline SHALL record current
   label-only legacy validation results and current WCF/service-reference gaps.
2. WHEN future implementation improves WCF mapping THEN validation SHALL compare
   against that baseline using counts, labels, rule IDs, evidence tiers, and
   limitations.
3. WHEN results are published or committed THEN they SHALL remain redacted and
   SHALL NOT include local absolute paths, private repo names, raw remotes, raw
   endpoint addresses, raw SQL, config values, secrets, or source snippets.
