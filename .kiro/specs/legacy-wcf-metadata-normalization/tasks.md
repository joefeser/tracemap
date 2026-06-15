# Legacy WCF Metadata Normalization Tasks

## Implementation Tasks

- [ ] 1. Preserve the post-smoke baseline. Requirements: 5.
  - [ ] Record the label-only WCF/SVC smoke observations in implementation state.
  - [ ] Keep local sample paths, raw report output, and raw metadata out of git.
  - [ ] Note that `zero-k-infrastructure` currently has generated clients and
        operations but zero service-reference mappings.

- [ ] 2. Add rule catalog entries. Requirements: 1, 2, 3, 4, 6.
  - [ ] Add `legacy.wcf.metadata.v1`.
  - [ ] Add `legacy.wcf.operation-normalization.v1`.
  - [ ] Update `legacy.wcf.mapping.v1` limitations for metadata-backed and
        normalized mapping paths.
  - [ ] Document no remote WSDL fetch, no runtime reachability, no binding
        compatibility proof, and no fuzzy matching.
  - [ ] Add required `FactTypes`, `RuleIds`, and `ScannerVersions.LegacyWcfExtractor`
        version bump in `src/dotnet/TraceMap.Core/Models.cs`.

- [ ] 3. Extend inventory for service-reference metadata. Requirements: 1.
  - [ ] Add `.svcmap`, `.wsdl`, `.disco`, and gated service-reference `.xsd`
        handling.
  - [ ] Gate `.wsdl`, `.disco`, and `.xsd` to files co-located with `.svcmap` or
        paths containing `Service Reference` / `ServiceReference`; do not
        globally inventory all files with those extensions.
  - [ ] Keep metadata files distinct from config and service host files.
  - [ ] Add tests for metadata inventory and ignored local/raw values.

- [ ] 4. Extract `.svcmap` metadata facts. Requirements: 1, 2.
  - [ ] Parse checked-in `.svcmap` XML with line info.
  - [ ] Use XXE-safe XML parser settings: prohibit or ignore DTDs, set
        `XmlResolver = null`, and avoid external entity expansion.
  - [ ] Emit safe metadata facts with local metadata basenames/hashes.
  - [ ] Hash or omit remote URLs and absolute paths.
  - [ ] Emit parse gaps with classification `MalformedWcfMetadata` for malformed
        `.svcmap`.
  - [ ] Add raw URL/path suppression tests.

- [ ] 5. Extract WSDL operation metadata. Requirements: 2.
  - [ ] Parse checked-in WSDL files using local-name XML matching.
  - [ ] Use the same XXE-safe XML parser settings as `.svcmap`.
  - [ ] Emit safe operation facts for `portType/operation`.
  - [ ] Capture safe service/port/binding identifiers only when valid.
  - [ ] Hash or omit SOAP actions, endpoint locations, imports, and unsafe
        identifiers.
  - [ ] Hash or omit URL-like namespace values including `targetNamespace`,
        XML namespace URIs, SOAP action namespaces, and schema namespaces.
  - [ ] Do not use WSDL operation names for alias corroboration unless they are
        connected to the generated client by `.svcmap`, service-reference folder,
        or safe portType/contract identity.
  - [ ] Add tests for safe operations, unsafe identifiers, and malformed WSDL.

- [ ] 6. Add operation alias derivation. Requirements: 3.
  - [ ] Derive `FooAsync -> Foo` aliases for WCF-generated client methods.
  - [ ] Derive `BeginFoo`/`EndFoo -> Foo` aliases only when the pair exists on
        the same contract/type.
  - [ ] Do not derive aliases for lone `BeginFoo` or lone `EndFoo` methods.
  - [ ] Keep original operation names as live candidates; do not replace them
        with aliases.
  - [ ] Require `FooAsync -> Foo` aliases to be corroborated by WSDL metadata,
        same-contract sync sibling, or aligned service operation before they feed
        mapping.
  - [ ] Exclude lifecycle methods by raw name and normalized base name unless
        metadata explicitly supports them.
  - [ ] Cover `BeginOpen`/`EndOpen`, `BeginClose`/`EndClose`, `BeginAbort`/`EndAbort`,
        and any `Begin`/`End` pair whose base name is excluded.
  - [ ] Preserve original names and normalization kind in properties.
  - [ ] Add tests for async suffix, APM pair, lone begin, lifecycle exclusion,
        and non-WCF-generated no-alias behavior.

- [ ] 7. Improve service-reference mapping. Requirements: 4.
  - [ ] Prefer exact existing mapping behavior first.
  - [ ] Add metadata-backed normalized mapping path.
  - [ ] Add APM-pair normalized mapping path.
  - [ ] Keep generated-code-only normalized mapping no stronger than review-tier.
  - [ ] Collapse convergent aliases (`Foo`, `FooAsync`, `BeginFoo`/`EndFoo`) on
        the same contract/metadata identity into one logical operation before
        ambiguity counting.
  - [ ] Deduplicate mapping facts by logical operation identity so sync/async/APM
        generated forms do not create duplicate mappings.
  - [ ] Emit ambiguity gaps instead of choosing arbitrary winners.
  - [ ] Use exact gap classification strings:
        `AmbiguousWcfNormalizedMapping`, `AmbiguousWcfMetadataContractMapping`,
        `MissingLocalWcfMetadata`, `MalformedWcfMetadata`, and
        `UnlinkedWcfMetadata`.
  - [ ] Include supporting fact IDs or safe metadata hashes where possible.
  - [ ] Add tests for clear metadata-backed mapping, APM mapping, generated-only
        mapping, convergent alias deduplication, and ambiguity.

- [ ] 8. Update validation summary and docs. Requirements: 5, 6.
  - [ ] Include metadata fact counts and normalized mapping counts in the legacy
        validation summary.
  - [ ] Extend `collect_wcf_counts` in `scripts/legacy_codebase_validation.py`
        for new metadata fact types and metadata/normalization gap counts.
  - [ ] Update validation docs with label-only WCF/SVC smoke guidance.
  - [ ] Update language adapter contract or acceptance docs if new fact types are
        added.
  - [ ] Ensure public claim level remains hidden until promoted by a safe public
        artifact.

- [ ] 9. Validate. Requirements: 5, 6.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `python3 -m unittest scripts.tests.test_legacy_codebase_validation`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] Rerun the ignored local WCF/SVC smoke and compare label-only counts.

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
