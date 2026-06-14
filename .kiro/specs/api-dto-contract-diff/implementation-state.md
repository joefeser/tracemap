# API and DTO Contract Diff Implementation State

Status: implemented

Branch/PR: `codex/api-dto-contract-diff`

## Shipped Scope

- Added `tracemap contract-diff --before <index.sqlite> --after <index.sqlite> --out <path>`.
- Supports single-language indexes and combined indexes.
- Rejects mixed single/combined comparisons.
- Opens SQLite inputs read-only.
- Emits deterministic Markdown and JSON:
  - `contract-diff-report.md`
  - `contract-diff-report.json`
- Compares static evidence rows for:
  - endpoints;
  - route shapes;
  - DTO/type declarations;
  - DTO property/member declarations;
  - method signatures;
  - request/response attachments where explicit attachment facts exist.
- Emits `AttachmentEvidenceUnavailable` when credible endpoint-to-DTO attachment evidence is absent.
- Handles selectors:
  - `--scope`
  - `--source`
  - `--endpoint`
  - `--type`
  - `--property`
  - `--change-kind`
- Handles caps and `--exit-code`.
- Adds `api.dto.contract.diff.*` rule catalog entries with documented limitations.

## Evidence Inventory Notes

Current v1 uses existing indexed facts only:

- Endpoint evidence comes from `HttpRouteBinding`.
- DTO/type evidence comes from `TypeDeclared`, `EnumDeclared`, `ObjectShapeInferred`, and `DeserializedObject`.
- DTO/member evidence comes from `PropertyDeclared`, `FieldDeclared`, `SerializerContractMember`, and `DatabaseColumnMapping`.
- Method evidence comes from `MethodDeclared`.
- Request/response attachment evidence is projected only when facts carry explicit attachment metadata such as `attachmentKind`, `requestResponseKind`, or `bodyKind`.

No adapter behavior changed in this slice.

## Intentional Caveats

- This is not OpenAPI generation.
- This is not runtime serializer mapping proof.
- This is not binary compatibility analysis.
- This is not runtime traffic, auth, proxy, deployment, or reachability proof.
- Path-only, syntax-only, duplicate, generic property-only, source-identity-conflicted, and reduced-coverage rows are downgraded.
- Request/response attachment absence is reported as a gap, not a clean result.

## Validation

Focused validation already passed during implementation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln --no-build --filter ApiDtoContractDiffTests
```

Full validation is tracked in `tasks.md` and should be checked off after the full suite and guard commands pass.
