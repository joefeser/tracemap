# Tasks

- [x] 1. Preserve the current parser baseline.
  - Commit a label-only baseline snapshot with current legacy validation counts.
  - Record current gaps around generated service clients, WCF config, and old ORM
    mapping.
  - Keep baseline public claim level hidden.

- [x] 2. Add WCF/service-reference rule catalog entries.
  - Add `legacy.wcf.config.v1`.
  - Add `legacy.wcf.contract.v1`.
  - Add `legacy.wcf.host.v1`.
  - Add `legacy.wcf.mapping.v1`.
  - Document limitations for runtime reachability, generated proxies, dynamic
    endpoints, and ambiguous mappings.

- [x] 3. Extend inventory and config extraction.
  - Include `.svc` and `.asmx` files.
  - Extract WCF client endpoint config with hashed addresses.
  - Extract service host endpoint config with hashed addresses.
  - Add tests proving raw addresses are not stored.

- [x] 4. Extract service contract and operation facts.
  - Detect `[ServiceContract]` and `[OperationContract]`.
  - Detect generated service-client classes.
  - Emit syntax evidence and semantic evidence where available.
  - Add tests for service contract, operation, and generated client facts.

- [x] 5. Extract `.svc` and ASMX host facts.
  - Parse safe directive attributes.
  - Emit service host facts without snippets.
  - Add parse-gap coverage for malformed host declarations.

- [x] 6. Add probable service-reference mapping.
  - Match config contract, generated client, operation contract, and host
    candidates.
  - Emit review-tier mappings when evidence is structural or name-only.
  - Emit ambiguity gaps instead of selecting arbitrary winners.
  - Add deterministic tests for clear match, name-only match, and ambiguity.

- [x] 7. Update legacy validation summary.
  - Include WCF/service-reference fact counts.
  - Include mapping counts and limitations.
  - Preserve redaction behavior for raw addresses and config values.

- [x] 8. Validate.
  - Run `dotnet build src/dotnet/TraceMap.sln`.
  - Run `dotnet test src/dotnet/TraceMap.sln`.
  - Run `python3 -m unittest scripts.tests.test_legacy_codebase_validation`.
  - Run `./scripts/check-private-paths.sh`.
  - Run `git diff --check`.
  - Rerun local legacy validation from ignored `.tmp/` inputs and compare
    label-only results to `baseline-current-parser.md`.
