# Implementation State

Status: implemented
Branch: codex/legacy-codebase-validation-impl
Public claim level: hidden

## Summary

TraceMap can currently scan very old .NET codebases with reduced coverage and
extract useful click-handler, call-edge, SQL/config/database, and HTTP evidence.
The missing layer is old WCF/service-reference mapping: generated service
clients, `system.serviceModel` endpoint config, `[ServiceContract]`,
`[OperationContract]`, `.svc` hosts, and probable operation-to-implementation
links.

## Scope Decisions

- Keep this deterministic and static only.
- Do not fetch WSDL or call endpoints.
- Store hashes for addresses and config values.
- Prefer explicit ambiguity gaps over arbitrary backend selection.
- Keep the current baseline hidden and label-only.

## Validation Baseline

Use `baseline-current-parser.md` as the safe pre-implementation snapshot.

## Implementation Notes

- Added `LegacyWcfExtractor` in `TraceMap.Core`.
- Added inventory support for `.svc` and `.asmx` service host files.
- Added WCF config endpoint, service contract, operation contract, generated
  client, service host, and probable mapping fact types.
- Added explicit ambiguity gaps for multiple static mapping candidates.
- Added WCF rule IDs and limitations to `rules/rule-catalog.yml`.
- Added focused tests for extraction, address redaction, mapping, and malformed
  host gaps.
- Updated the legacy validation summary to include WCF fact counts.
- After Opus/Sonnet review, tightened generated-client detection to require
  WCF `ClientBase`-style inheritance, removed short-name-only contract matching,
  suppressed normal mapping facts when endpoint/host/operation ambiguity exists,
  and added ASMX `Class` attribute support.

## Post-Implementation Local Validation

Against the label-only `legacy-winforms-app` sample, the implementation produced:

| Fact type | Count |
| --- | ---: |
| `WcfClientEndpointDeclared` | 7 |
| `WcfGeneratedClientDeclared` | 24 |
| `WcfOperationContractDeclared` | 18 |
| `WcfServiceContractDeclared` | 8 |
| `WcfServiceHostDeclared` | 9 |
| `WcfServiceReferenceMapping` | 9 |

The `large-public-dotnet-client` and `legacy-unknown-dotnet-app` labels produced
zero WCF facts in this validation run.

## Follow-Ups

- Decide whether richer click-to-service-to-SQL paths belong in the legacy
  validation harness, combined path query, or a new legacy report command.
- Decide whether old EF/EDMX entity/table mapping deserves its own follow-up
  spec after service-reference mapping lands.
