# Legacy WCF Service Reference Mapping Tasks

## Implementation Tasks

- [x] 1. Preserve the current parser baseline. Requirements: 5.
  - [x] Keep `baseline-current-parser.md` label-only and safe for committed
        review.
  - [x] Compare future validation against current generated service-client,
        WCF config, service host, and old ORM/service-reference gaps.
  - [x] Keep baseline public claim level hidden.

- [x] 2. Add WCF/service-reference rule catalog entries. Requirements: 1, 2, 3, 4.
  - [x] Add `legacy.wcf.config.v1`.
  - [x] Add `legacy.wcf.contract.v1`.
  - [x] Add `legacy.wcf.host.v1`.
  - [x] Add `legacy.wcf.mapping.v1`.
  - [x] Document limitations for runtime reachability, generated proxies,
        dynamic endpoints, config transforms, service deployment, binding
        compatibility, and ambiguous mappings.

- [x] 3. Extend inventory and config extraction. Requirements: 1, 3.
  - [x] Include `.svc` and `.asmx` files in inventory with safe repo-relative
        paths.
  - [x] Extract WCF client endpoint config with endpoint name, binding,
        contract, safe scheme, and hashed address where available.
  - [x] Extract WCF service host endpoint config with service name, endpoint
        name, binding, contract, and hashed address where available.
  - [x] Emit partial-field properties or `AnalysisGap` facts when endpoint
        metadata is incomplete.
  - [x] Add tests proving raw addresses, config values, local paths, remotes,
        and secrets are not stored.

- [x] 4. Extract service contract and operation facts. Requirements: 2.
  - [x] Detect `[ServiceContract]` and `[ServiceContractAttribute]`.
  - [x] Detect `[OperationContract]` and `[OperationContractAttribute]`.
  - [x] Detect generated WCF/service-reference client classes using conservative
        generated-code and `ClientBase<T>`-style evidence.
  - [x] Emit semantic evidence where symbols resolve and syntax/textual evidence
        when semantic analysis is unavailable.
  - [x] Add tests for service contract, operation, generated client, semantic,
        and syntax fallback facts.

- [x] 5. Extract `.svc` and ASMX host facts. Requirements: 3.
  - [x] Parse safe directive attributes such as `Service`, `Class`, and
        `Factory`.
  - [x] Emit service host facts without source snippets.
  - [x] Emit parse gaps for malformed or unsupported host declarations.
  - [x] Add tests for `.svc`, ASMX, malformed directives, and unsafe value
        suppression.

- [x] 6. Add probable service-reference mapping. Requirements: 4.
  - [x] Match config contract, generated client, operation contract, and host
        candidates where static evidence aligns.
  - [x] Classify symbol-resolved mappings as semantic only when all required
        symbols resolve.
  - [x] Classify config/client/operation name alignment as structural or
        syntax/textual according to supporting evidence.
  - [x] Emit ambiguity or missing-link gaps instead of selecting arbitrary
        backend candidates.
  - [x] Preserve supporting fact IDs and evidence tiers on mapping facts.
  - [x] Add deterministic tests for clear match, name-only match, missing
        support, and ambiguity.

- [x] 7. Update reporting, validation, and docs. Requirements: 4, 5.
  - [x] Include WCF/service-reference fact counts and known limitations in
        `report.md` and validation summaries where appropriate.
  - [x] Ensure `facts.ndjson` and `index.sqlite` contain rule IDs, tiers, paths,
        line spans, commit SHA, extractor IDs, and extractor versions.
  - [x] Update docs or adapter contracts if new fact types or validation
        behavior are added.
  - [x] Preserve redaction behavior for raw addresses, WSDL URLs, SQL, config
        values, source snippets, local absolute paths, private repo names,
        remotes, and secrets.

- [x] 8. Validate implementation. Requirements: 5.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run `python3 -m unittest scripts.tests.test_legacy_codebase_validation`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.
  - [x] Run the CLI against at least one checked-in or temporary sample and
        verify `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
        `report.md`, and `logs/analyzer.log` are produced.
  - [x] If relevant local legacy samples are available, run ignored smoke
        validation and commit only redacted label/count summaries.
