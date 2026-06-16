# Design

## Overview

The WCF/service-reference phase fills the static gap between old UI or service
client calls and backend service operation evidence. It extends existing C#
syntax/config extraction rather than introducing runtime probing.

The intended evidence chain is:

```text
click handler call edge
  -> generated service client method
  -> service endpoint config / contract name
  -> service contract operation
  -> service host or implementation candidate
  -> existing downstream call / SQL / database facts
```

Every arrow remains static evidence. TraceMap must not claim runtime execution,
network reachability, service deployment, WSDL compatibility, dynamic endpoint
resolution, or dependency-injection/service-host selection.

## Current Baseline

See `baseline-current-parser.md` for the frozen pre-implementation snapshot.
That file is the local-safe reference point for deciding whether this phase is
worth further investment.

## Fact Types

Add or reuse deterministic fact types:

- `WcfClientEndpointDeclared`
- `WcfServiceEndpointDeclared`
- `WcfServiceContractDeclared`
- `WcfOperationContractDeclared`
- `WcfGeneratedClientDeclared`
- `WcfServiceHostDeclared`
- `WcfServiceReferenceMapping`

Suggested rules:

- `legacy.wcf.config.v1`
- `legacy.wcf.contract.v1`
- `legacy.wcf.host.v1`
- `legacy.wcf.mapping.v1`

## Config Extraction

Extend XML config extraction to inspect `system.serviceModel`.

For `client/endpoint`:

- endpoint name
- binding
- contract
- address hash
- address scheme, if parseable without rendering the address
- config source format

For `services/service/endpoint`:

- service name
- endpoint name
- binding
- contract
- address hash when present

No raw endpoint address is stored. Hashes should use the existing TraceMap hash
helper.

## C# Syntax/Semantic Extraction

Syntax pass:

- detect attributes named `ServiceContract` or `ServiceContractAttribute`
- detect attributes named `OperationContract` or `OperationContractAttribute`
- detect generated service-client classes by common patterns:
  - classes ending in `Client`
  - base types or member names suggesting `ClientBase<T>`
  - generated-code attributes
  - service reference namespaces or files
- emit syntax/textual evidence when semantic model is unavailable

Semantic pass:

- when symbols resolve, include fully qualified contract, operation, and client
  symbols using existing symbol identity conventions
- preserve assembly name/version when available

## Host Extraction

Extend inventory to include `.svc` and `.asmx` files.

Parse directive-style declarations such as:

```text
<%@ ServiceHost Service="..." Factory="..." %>
```

Only safe attribute names and hashes/identifiers should be emitted. Source
snippet text must not be stored.

## Mapping Strategy

Build mapping candidates from static evidence:

1. Config contract name equals or suffix-matches a service contract symbol.
2. Generated client method name equals an operation contract method name.
3. Generated client type or namespace aligns with config endpoint contract.
4. `.svc` service attribute equals or suffix-matches a contract or
   implementation type.

Classification guidance:

- `Tier1Semantic`: symbol-resolved generated client, contract, operation, and
  implementation candidate align.
- `Tier2Structural`: config contract + generated client + operation names align.
- `Tier3SyntaxOrTextual`: name-only matches without config or symbol support.
- `Tier4Unknown`: missing generated file, dynamic endpoint, unsupported host
  shape, duplicate candidates, or unresolved ambiguity.

Do not pick one winner when multiple backend candidates match. Emit all review
candidates or emit an ambiguity gap.

## Output and Reporting

Existing reports can surface the new facts through counts immediately. A later
phase can add richer path/report rendering if needed.

The legacy validation harness should include WCF/service-reference counts in its
redacted summary once the facts exist.

## Safety

Never render:

- raw endpoint addresses
- raw WSDL URLs
- raw config values
- raw source snippets
- local absolute paths
- private repository names
- secrets or connection strings

## Non-Goals

- No WSDL download or parsing from remote URLs.
- No runtime service activation.
- No proof that a client endpoint points to a deployed service.
- No full WCF binding compatibility analysis.
- No dynamic endpoint selection or config transform evaluation.
- No service authorization/authentication conclusions.
