# Legacy WCF Service Reference Mapping Implementation State

Status: implemented
Branch: codex/legacy-wcf-service-reference-mapping
Public claim level: hidden

## Why This Spec Exists

TraceMap preserves useful legacy evidence for UI entry points, call/object
edges, SQL/config/database surfaces, legacy data metadata, flow composition, and
old WCF/service-reference metadata: generated service clients,
`system.serviceModel` endpoint config, `[ServiceContract]`,
`[OperationContract]`, `.svc` hosts, ASMX host declarations, and probable
operation-to-implementation links.

This spec has been implemented on `dev`. Remaining WCF depth, including richer
semantic enrichment and runtime service proof, stays out of scope.

## Scope Decisions

- Keep the feature deterministic and static only.
- Do not fetch WSDL, call endpoints, activate services, probe networks, connect
  to databases, or evaluate config transforms.
- Store hashes for endpoint addresses and unsafe config values; do not render
  raw values in committed artifacts.
- Prefer explicit ambiguity and analysis gaps over arbitrary backend selection.
- Preserve syntax fallback when semantic/MSBuild project load fails.
- Treat WCF/service-reference mappings as probable static evidence, not runtime
  reachability or deployment proof.
- Keep public claim level hidden until redacted validation artifacts or checked-in
  public fixtures justify promotion.

## Imported Baseline

`baseline-current-parser.md` was imported with this packet as a safe
pre-implementation snapshot. It is label-only and intentionally omits local
absolute paths, private repository names, raw remotes, raw endpoint addresses,
raw config values, raw SQL, source snippets, and secrets.

Future implementation should compare against this baseline using counts, rule
IDs, evidence tiers, coverage labels, and limitations rather than raw artifacts.

## Validation

Implementation validation:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

The implementation also runs through focused WCF extractor tests and current
full-suite validation. Reconciliation coverage is tracked by
`.kiro/specs/legacy-story-reconciliation/`.

## Follow-Ups To Keep Out Of This Slice

- WCF metadata normalization from checked-in `.svcmap` / `.wsdl` files beyond
  the initial service-reference mapping. This landed in the separate
  `legacy-wcf-metadata-normalization` slice.
- WebForms click-handler path rendering into WCF mappings.
- DBML/EDMX/typed DataSet data metadata extraction. This landed in the separate
  `legacy-data-metadata-extraction` MVP slice.
- Runtime service reachability, binding compatibility, authorization, or
  deployment conclusions.
- Site copy or public AI/impact-analysis claims.
