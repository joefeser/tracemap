# Legacy WCF Service Reference Mapping Implementation State

Status: ready-for-implementation
Branch: codex/legacy-wcf-service-reference-mapping
Public claim level: hidden

## Why This Spec Exists

TraceMap can already preserve useful legacy evidence for UI entry points,
call/object edges, SQL/config/database surfaces, legacy data metadata planning,
and flow composition planning. The next missing static layer is old WCF and
service-reference metadata: generated service clients, `system.serviceModel`
endpoint config, `[ServiceContract]`, `[OperationContract]`, `.svc` hosts,
ASMX host declarations, and probable operation-to-implementation links.

This spec is intended as the next implementation runway after
`legacy-data-metadata-extraction` and `legacy-flow-composition-reporting`. It is
spec-only and does not implement scanner, reducer, reporting, rule catalog, or
CLI code.

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

## Validation Expectations

Spec-only delivery validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Implementation validation, once product code is changed:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

The implementation should also run a CLI scan against at least one checked-in or
temporary fixture and verify the required artifacts still appear:
`scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and
`logs/analyzer.log`.

## Follow-Ups To Keep Out Of This Slice

- Product implementation in this spec PR.
- WCF metadata normalization from checked-in `.svcmap` / `.wsdl` files beyond
  the initial service-reference mapping.
- WebForms click-handler path rendering into WCF mappings.
- DBML/EDMX/typed DataSet data metadata extraction.
- Runtime service reachability, binding compatibility, authorization, or
  deployment conclusions.
- Site copy or public AI/impact-analysis claims.
