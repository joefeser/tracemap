# Legacy WCF Service Reference Mapping Tasks

## Implementation Tasks

- [ ] 1. Preserve the current parser baseline. Requirements: 5.
  - [ ] Keep `baseline-current-parser.md` label-only and safe for committed
        review.
  - [ ] Compare future validation against current generated service-client,
        WCF config, service host, and old ORM/service-reference gaps.
  - [ ] Keep baseline public claim level hidden.

- [ ] 2. Add WCF/service-reference rule catalog entries. Requirements: 1, 2, 3, 4.
  - [ ] Add `legacy.wcf.config.v1`.
  - [ ] Add `legacy.wcf.contract.v1`.
  - [ ] Add `legacy.wcf.host.v1`.
  - [ ] Add `legacy.wcf.mapping.v1`.
  - [ ] Document limitations for runtime reachability, generated proxies,
        dynamic endpoints, config transforms, service deployment, binding
        compatibility, and ambiguous mappings.

- [ ] 3. Extend inventory and config extraction. Requirements: 1, 3.
  - [ ] Include `.svc` and `.asmx` files in inventory with safe repo-relative
        paths.
  - [ ] Extract WCF client endpoint config with endpoint name, binding,
        contract, safe scheme, and hashed address where available.
  - [ ] Extract WCF service host endpoint config with service name, endpoint
        name, binding, contract, and hashed address where available.
  - [ ] Emit partial-field properties or `AnalysisGap` facts when endpoint
        metadata is incomplete.
  - [ ] Add tests proving raw addresses, config values, local paths, remotes,
        and secrets are not stored.

- [ ] 4. Extract service contract and operation facts. Requirements: 2.
  - [ ] Detect `[ServiceContract]` and `[ServiceContractAttribute]`.
  - [ ] Detect `[OperationContract]` and `[OperationContractAttribute]`.
  - [ ] Detect generated WCF/service-reference client classes using conservative
        generated-code and `ClientBase<T>`-style evidence.
  - [ ] Emit semantic evidence where symbols resolve and syntax/textual evidence
        when semantic analysis is unavailable.
  - [ ] Add tests for service contract, operation, generated client, semantic,
        and syntax fallback facts.

- [ ] 5. Extract `.svc` and ASMX host facts. Requirements: 3.
  - [ ] Parse safe directive attributes such as `Service`, `Class`, and
        `Factory`.
  - [ ] Emit service host facts without source snippets.
  - [ ] Emit parse gaps for malformed or unsupported host declarations.
  - [ ] Add tests for `.svc`, ASMX, malformed directives, and unsafe value
        suppression.

- [ ] 6. Add probable service-reference mapping. Requirements: 4.
  - [ ] Match config contract, generated client, operation contract, and host
        candidates where static evidence aligns.
  - [ ] Classify symbol-resolved mappings as semantic only when all required
        symbols resolve.
  - [ ] Classify config/client/operation name alignment as structural or
        syntax/textual according to supporting evidence.
  - [ ] Emit ambiguity or missing-link gaps instead of selecting arbitrary
        backend candidates.
  - [ ] Preserve supporting fact IDs and evidence tiers on mapping facts.
  - [ ] Add deterministic tests for clear match, name-only match, missing
        support, and ambiguity.

- [ ] 7. Update reporting, validation, and docs. Requirements: 4, 5.
  - [ ] Include WCF/service-reference fact counts and known limitations in
        `report.md` and validation summaries where appropriate.
  - [ ] Ensure `facts.ndjson` and `index.sqlite` contain rule IDs, tiers, paths,
        line spans, commit SHA, extractor IDs, and extractor versions.
  - [ ] Update docs or adapter contracts if new fact types or validation
        behavior are added.
  - [ ] Preserve redaction behavior for raw addresses, WSDL URLs, SQL, config
        values, source snippets, local absolute paths, private repo names,
        remotes, and secrets.

- [ ] 8. Validate implementation. Requirements: 5.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run `python3 -m unittest scripts.tests.test_legacy_codebase_validation`.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Run the CLI against at least one checked-in or temporary sample and
        verify `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
        `report.md`, and `logs/analyzer.log` are produced.
  - [ ] If relevant local legacy samples are available, run ignored smoke
        validation and commit only redacted label/count summaries.
