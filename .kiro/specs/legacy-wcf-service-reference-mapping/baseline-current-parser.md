# Baseline: Current Parser Snapshot Before WCF Mapping

Public claim level: hidden

This file freezes the current label-only legacy validation result before adding
WCF/service-reference extraction. It is intentionally safe to commit: it omits
local absolute paths, raw remotes, raw endpoint addresses, raw config values,
raw SQL, source snippets, and private repository names.

## Legacy Validation Counts

| Label | Status | Coverage | Build | Facts | Gaps | UI evidence | Truncated |
| --- | --- | --- | --- | ---: | ---: | --- | --- |
| `large-public-dotnet-client` | completed | Level1SemanticAnalysisReduced | FailedOrPartial | 243,976 | 176 | not-applicable | false |
| `legacy-unknown-dotnet-app` | completed | Level1SemanticAnalysisReduced | FailedOrPartial | 24,859 | 8,572 | semantic-static-wiring | false |
| `legacy-winforms-app` | completed | Level1SemanticAnalysisReduced | FailedOrPartial | 216,910 | 117,165 | semantic-static-wiring | true |

## Current Legacy UI Evidence

For `legacy-winforms-app`, the current scanner can see semantic click-handler
call edges from UI handlers into generated or service-client-like methods. It
can also see intermediate helper calls from those handlers.

Current evidence quality:

- Click handler to local helper/service-client call: Tier1Semantic where symbols
  resolve.
- Generated service-client method to concrete backend implementation: not yet
  mapped.
- Runtime service endpoint reachability: not claimed.

## Current SQL and Data Evidence

For `legacy-winforms-app`, current SQL/data-related evidence includes:

| Fact type | Count |
| --- | ---: |
| `ConnectionStringDeclared` | 8 |
| `DbContextDeclared` | 4 |
| `DbChangeSaved` | 57 |
| `SqlFileDeclared` | 148 |
| `SqlTextUsed` | 164 |
| `QueryPatternDetected` | 373 |
| `DapperCallDetected` | 8 |

Limitations:

- Old ORM/entity mapping is not yet resolved to table-level dependency paths.
- Dapper-like method matches are syntax/textual evidence, not proof of Dapper
  runtime usage.
- Raw SQL and config values remain omitted or hashed.

## Current Backend/Service Evidence Gap

Current TraceMap output can see generated service-client-like calls and can see
some backend/web route and HTTP client evidence elsewhere in the same scan. It
does not yet connect generated service clients through WCF/WSDL/service
reference metadata to backend service operations or `.svc` hosts.

Missing evidence slice:

```text
generated service client call
  -> endpoint config contract
  -> service contract operation
  -> service host / implementation candidate
  -> downstream SQL or dependency facts
```

This gap is the reason for `legacy-wcf-service-reference-mapping`.
