# Legacy WCF Metadata Normalization Implementation State

Status: not-started
Branch: codex/legacy-wcf-metadata-normalization-spec
Public claim level: hidden

## Why This Spec Exists

After the initial WCF/service-reference mapper landed, a local ignored smoke
against public old WCF/SVC-heavy repositories showed that TraceMap extracts
substantial WCF evidence but still misses mappings when old generated proxy
naming conventions differ from service operation names.

Most important observed case:

- sample label: `zero-k-infrastructure`
- commit: `48f6f09bc1d0266f204026580671ce867f75d6bd`
- current extracted evidence:
  - `WcfClientEndpointDeclared`: 1
  - `WcfGeneratedClientDeclared`: 68
  - `WcfOperationContractDeclared`: 77
  - `WcfServiceContractDeclared`: 6
  - `WcfServiceEndpointDeclared`: 4
  - `WcfServiceHostDeclared`: 5
  - `WcfServiceReferenceMapping`: 0
- local files include checked-in service-reference metadata types:
  `.svcmap`, `.wsdl`, `.disco`, and `.xsd`.

The observed mismatch is mostly naming:

- generated client methods use names like `BuildShipAsync`;
- operation contract evidence contains names like `BeginBuildShip`;
- checked-in WSDL metadata can name the underlying operation.

## Scope Decisions

- Keep the implementation static and deterministic.
- Parse only checked-in metadata files.
- Use `.svcmap` and `.wsdl` for operation metadata.
- Treat `.disco` and gated service-reference `.xsd` files as inventory-only in
  this slice.
- Do not fetch remote WSDL/discovery/schema URLs.
- Do not execute services or validate bindings.
- Do not claim runtime reachability.
- Do not use fuzzy matching.
- Keep full semantic WCF enrichment as a separate follow-up.

## Local Validation Notes

The local WCF/SVC smoke used an ignored manifest and ignored output under
`.tmp/legacy-codebase-validation/`. Raw artifacts are large and should remain
local-only.

Observed smoke output size: approximately 7.9 GB for raw scan artifacts.

Safe label-only sample commits used during spec creation:

| Label | Commit |
| --- | --- |
| `splendidcrm-community` | `34a56ece1fe3287c92ec80f4788fcc877716f0af` |
| `zero-k-infrastructure` | `48f6f09bc1d0266f204026580671ce867f75d6bd` |
| `skyquery` | `0afb09c2468f2790b25aac4744ca6943c181c28f` |
| `witsml` | `61e47ce2a0f0bf90bfaf08dc13f9bc361a6cc893` |
| `wcf-rest-no-svc` | `8ca2451ce488d826aedab4c420fe8e18474de7e5` |

## Validation Commands For Implementation

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

Optional local smoke, only when the ignored manifest exists:

```bash
python3 scripts/legacy_codebase_validation.py \
  .tmp/legacy-codebase-validation/wcf-svc-smoke.local.json \
  .tmp/legacy-codebase-validation/wcf-svc-smoke-out
```

## Follow-Ups To Keep Out Of This Slice

- WebForms click-handler path reporting into WCF mappings.
- ASMX operation extraction beyond service host facts.
- DBML/EDMX entity/table mapping.
- Semantic WCF enrichment when project load succeeds.
- WSDL/XSD DTO contract mapping.
- Legacy smoke progress output and raw artifact cleanup automation.
