# Legacy WCF Metadata Normalization Tasks

## Implementation Tasks

- [x] 1. Preserve the post-smoke baseline. Requirements: 5.
  - [x] Record the label-only WCF/SVC smoke observations in implementation state.
  - [x] Keep local sample paths, raw report output, and raw metadata out of git.
  - [x] Note that `zero-k-infrastructure` currently has generated clients and
        operations but zero service-reference mappings.

- [x] 2. Add rule catalog entries. Requirements: 1, 2, 3, 4, 6.
  - [x] Add `legacy.wcf.metadata.v1`.
  - [x] Add `legacy.wcf.operation-normalization.v1`.
  - [x] Update `legacy.wcf.mapping.v1` limitations for metadata-backed and
        normalized mapping paths.
  - [x] Document no remote WSDL fetch, no runtime reachability, no binding
        compatibility proof, and no fuzzy matching.
  - [x] Add required `FactTypes`, `RuleIds`, and `ScannerVersions.LegacyWcfExtractor`
        version bump in `src/dotnet/TraceMap.Core/Models.cs`.

- [x] 3. Extend inventory for service-reference metadata. Requirements: 1.
  - [x] Add `.svcmap`, `.wsdl`, `.disco`, and gated service-reference `.xsd`
        handling.
  - [x] Gate `.wsdl`, `.disco`, and `.xsd` to files co-located with `.svcmap` or
        paths containing `Service Reference` / `ServiceReference`; do not
        globally inventory all files with those extensions.
  - [x] Keep metadata files distinct from config and service host files.
  - [x] Add tests for metadata inventory and ignored local/raw values.

- [x] 4. Extract `.svcmap` metadata facts. Requirements: 1, 2.
  - [x] Parse checked-in `.svcmap` XML with line info.
  - [x] Use XXE-safe XML parser settings: prohibit or ignore DTDs, set
        `XmlResolver = null`, and avoid external entity expansion.
  - [x] Emit safe metadata facts with local metadata basenames/hashes.
  - [x] Hash or omit remote URLs and absolute paths.
  - [x] Emit parse gaps with classification `MalformedWcfMetadata` for malformed
        `.svcmap`.
  - [x] Add raw URL/path suppression tests.

- [x] 5. Extract WSDL operation metadata. Requirements: 2.
  - [x] Parse checked-in WSDL files using local-name XML matching.
  - [x] Use the same XXE-safe XML parser settings as `.svcmap`.
  - [x] Emit safe operation facts for `portType/operation`.
  - [x] Capture safe service/port/binding identifiers only when valid.
  - [x] Hash or omit SOAP actions, endpoint locations, imports, and unsafe
        identifiers.
  - [x] Hash or omit URL-like namespace values including `targetNamespace`,
        XML namespace URIs, SOAP action namespaces, and schema namespaces.
  - [x] Do not use WSDL operation names for alias corroboration unless they are
        connected to the generated client by `.svcmap`, service-reference folder,
        or safe portType/contract identity.
  - [x] Add tests for safe operations, unsafe identifiers, and malformed WSDL.

- [x] 6. Add operation alias derivation. Requirements: 3.
  - [x] Derive `FooAsync -> Foo` aliases for WCF-generated client methods.
  - [x] Derive `BeginFoo`/`EndFoo -> Foo` aliases only when the pair exists on
        the same contract/type.
  - [x] Do not derive aliases for lone `BeginFoo` or lone `EndFoo` methods.
  - [x] Keep original operation names as live candidates; do not replace them
        with aliases.
  - [x] Require `FooAsync -> Foo` aliases to be corroborated by WSDL metadata,
        same-contract sync sibling, or aligned service operation before they feed
        mapping.
  - [x] Exclude lifecycle methods by raw name and normalized base name unless
        metadata explicitly supports them.
  - [x] Cover `BeginOpen`/`EndOpen`, `BeginClose`/`EndClose`, `BeginAbort`/`EndAbort`,
        and any `Begin`/`End` pair whose base name is excluded.
  - [x] Preserve original names and normalization kind in properties.
  - [x] Add tests for async suffix, APM pair, lone begin, lifecycle exclusion,
        and non-WCF-generated no-alias behavior.

- [x] 7. Improve service-reference mapping. Requirements: 4.
  - [x] Prefer exact existing mapping behavior first.
  - [x] Add metadata-backed normalized mapping path.
  - [x] Add APM-pair normalized mapping path.
  - [x] Keep generated-code-only normalized mapping no stronger than review-tier.
  - [x] Collapse convergent aliases (`Foo`, `FooAsync`, `BeginFoo`/`EndFoo`) on
        the same contract/metadata identity into one logical operation before
        ambiguity counting.
  - [x] Deduplicate mapping facts by logical operation identity so sync/async/APM
        generated forms do not create duplicate mappings.
  - [x] Emit ambiguity gaps instead of choosing arbitrary winners.
  - [x] Use exact gap classification strings:
        `AmbiguousWcfNormalizedMapping`, `AmbiguousWcfMetadataContractMapping`,
        `MissingLocalWcfMetadata`, `MalformedWcfMetadata`, and
        `UnlinkedWcfMetadata`.
  - [x] Include supporting fact IDs or safe metadata hashes where possible.
  - [x] Add tests for clear metadata-backed mapping, APM mapping, generated-only
        mapping, convergent alias deduplication, and ambiguity.

- [x] 8. Update validation summary and docs. Requirements: 5, 6.
  - [x] Include metadata fact counts and normalized mapping counts in the legacy
        validation summary.
  - [x] Extend `collect_wcf_counts` in `scripts/legacy_codebase_validation.py`
        for new metadata fact types and metadata/normalization gap counts.
  - [x] Update validation docs with label-only WCF/SVC smoke guidance.
  - [x] Update language adapter contract or acceptance docs if new fact types are
        added.
  - [x] Ensure public claim level remains hidden until promoted by a safe public
        artifact.

- [x] 9. Validate. Requirements: 5, 6.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `python3 -m unittest scripts.tests.test_legacy_codebase_validation`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`
  - [x] Rerun the ignored local WCF/SVC smoke and compare label-only counts. Deferred because `.tmp/legacy-codebase-validation/wcf-svc-smoke.local.json` is absent in this worktree.

## Recommended PR Slices

- [ ] PR 1: Rule catalog + fact model + metadata inventory/extraction tests.
- [ ] PR 2: Operation alias derivation + mapping improvements.
- [ ] PR 3: Validation summary/docs + ignored local smoke comparison.

## Deferred Follow-Ups

- Semantic WCF enrichment.
- WebForms click/event path reporting into WCF mappings.
- ASMX method extraction beyond host declarations.
- DBML/EDMX mapping.
- WSDL/XSD DTO schema-to-code mapping.
- Legacy smoke progress reporting and artifact pruning.
