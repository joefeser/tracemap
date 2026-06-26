# Route Flow Service/Data Composition Final Implementation State

Status: task-7-legacy-data-storage-attachment-precision-ready-for-review
Readiness: task-7-legacy-data-storage-ready-for-pr-review-loop
Spec branch: `codex/spec-route-flow-service-data-composition-final`
Implementation branch: `codex/implement-route-flow-service-data-composition-final`
Target base: `dev`
Primary issues: `#159`, `#179`, `#201`
Public claim level: static evidence only

## Reconciliation State

Reconciled against `origin/dev` on 2026-06-25 at `086ad376`.

Task 5 closure branch: `codex/route-flow-task5-matrix`.
Task 5 audited base: `origin/dev` at
`625e6fef9c9a88539545334c3fcd3e979e7d3244`.

Task 5 scope selected on 2026-06-25:

- Audit the remaining endpoint/root method to service-call stitching matrix
  after PR #325/reconciliation without moving to Task 6.
- Keep existing source-local symbol/fact/edge identity stitching intact.
- Add the smallest missing product/test slice:
  `SelectorNoMatch` now blocks clean `NoRouteFlowEvidence` suppression, and
  inherited path no-evidence gaps preserve `FullEvidenceAvailable` coverage
  when the path report is full coverage.
- Add focused route-flow tests for deterministic direct service-call ordering
  and full-coverage no-direct-call selector-blocker suppression.
- Leave implementation-candidate continuation, attached dependency precision,
  and broader Task 8/9/10 work for later tasks.

Task 5 validation status:

- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
  errors.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 39 tests.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 642 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Merged evidence on `dev`:

- PR #311 merged this final spec packet (`43426b7c`).
- PR #318 merged duplicate normalized endpoint/root selector ambiguity handling
  (`1e9c5660`).
- PR #320 merged source-local service-call cycle `TraversalBounds` gap
  handling (`565d7b64`).
- Current code contains `RouteFlowReport.contextGroups`, entry
  `bridgeState`, bounded duplicate-root supporting evidence, cycle-gap
  metadata, `MissingCallEdge`/`DataSurfaceAttachmentMissing`/projection schema
  gaps, route-flow CLI exit-code tests, and focused `CombinedRouteFlowTests`
  coverage for the merged slices.

Remaining slices after reconciliation:

- Task 6: implementation-candidate continuation and downgrade hardening is
  closed by PR #330 plus branch `codex/route-flow-task6-gap-matrix`, which
  audited `origin/dev` at `086ad376e387ea8d87e430175ef2673cbc74c0f1`.
- Task 7: attached versus unjoinable service/data/query/dependency/value-origin
  precision, including the large projection SQL parameter cap noted below.
- Task 8/9/10: only the downgrade, compatibility, exit-code, rule-catalog, and
  safety checks directly affected by the selected product slice.

Recommended next order after any in-flight route-flow branch is merged or
explicitly closed:

1. Fetch `origin/dev`, record the audited commit SHA, and re-audit this spec's
   Task 5 remainder against that head.
2. Either close Task 5 with evidence or pick the smallest still-missing
   route-flow direct-call/gap slice.
3. Then move to Task 6 candidate continuation if Task 5 is closed.
4. Then move to Task 7 service/data/query/dependency attachment precision.
5. Only after the route-flow contract is stable, start
   `.kiro/specs/ui-field-property-lineage-composition/` PR 1.

Do not reopen `route-flow-service-data-composition-next` or
`route-flow-endpoint-stitching` as fresh implementation queues unless a future
audit proves this final spec does not cover the needed slice.

## Task 7 ASMX/SOAP Attachment Slice

Branch: `codex/task7-http-wcf-attachments-20260625-182659`
Audited base: `origin/dev` at `cb70ba7def4313cce34034421618062b60cb0c01`.
Selected family: ASMX/SOAP dependency surfaces.

Scope:

- Keep this PR limited to route-flow attachment precision for ASMX/SOAP surface
  kinds already produced by the deterministic legacy ASMX extractor and shared
  surface projection.
- Add `asmx-service`, `asmx-operation`, `asmx-client`, `asmx-config`, and
  `asmx-metadata` to the route-flow terminal surface selector vocabulary.
- Attach ASMX/SOAP dependency surfaces only when the surface node is reached by
  the selected static route-flow path via existing source-local graph evidence.
- Preserve `DataSurfaceAttachmentMissing` when ASMX/SOAP evidence is adjacent
  in the same combined index but not connected to the selected route-flow path.

Scope decisions:

- This slice does not start UI property lineage, site work, reducer behavior, or
  scanner extraction changes.
- This slice does not add runtime SOAP endpoint claims; all ASMX/SOAP rows stay
  static evidence with existing rule IDs and evidence tiers.
- WCF, remoting, package/config, legacy-data, storage, validation,
  serializer/contract, async/callback, and flow-boundary families remain
  follow-up Task 7 slices unless separately verified in a later PR.

Oddities:

- The shared surface projection already recognized ASMX/SOAP facts through the
  `surfaceKind` property, but route-flow rejected those surface kinds in
  `--to-surface` validation before this slice.
- The focused tests use synthetic public-safe ASMX facts rather than invoking
  full legacy extractors so they isolate selected-route attachment behavior.
- ACK review found that real `LegacyAsmxExtractor` client-operation rows attach
  the generated client type as `sourceSymbol` and the callable operation as
  `targetSymbol`; the patch now links selected ASMX operation surfaces through
  that callable target symbol.
- Qodo review found that legacy projected flow terminals and CLI help also
  needed the ASMX/SOAP surface vocabulary; the patch now preserves ASMX surface
  kinds in legacy projection terminal nodes and lists them in `paths` and
  `route-flow` help.

Validation status:

- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
  errors.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~CombinedRouteFlowTests|FullyQualifiedName~LegacyFlowCompositionTests"`:
  passed locally with 73 tests.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 658 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Follow-ups:

- Continue Task 7 with one separate family at a time, such as remoting,
  legacy-data/storage, package/config, validation/serializer, or
  async/callback/flow-boundary.

## Task 7 WCF Operation Attachment Slice

Branch: `codex/route-flow-task7-next-attachment-precision-20260625`
Audited base: `origin/dev` at
`122ca28d61a28c5b0e9cadf96ab9191aef39811f`.
Selected family: WCF operation dependency surfaces.

Scope:

- Keep this PR limited to route-flow attachment precision for WCF
  service-reference mapping facts already emitted by the deterministic legacy
  WCF extractor.
- Project `WcfServiceReferenceMapping` facts as `wcf-operation` dependency
  surfaces through the shared combined surface projection.
- Attach WCF operation dependency surfaces only when the generated client
  operation is reached by the selected static route-flow path.
- Preserve `DataSurfaceAttachmentMissing` when WCF mapping evidence is present
  in the same combined index but not connected to the selected route-flow path.

Scope decisions:

- This slice does not start UI property lineage, site work, reducer behavior,
  or scanner extraction changes.
- This slice does not add runtime WCF endpoint, deployment, channel, or service
  activation claims. Rows remain deterministic static evidence backed by
  existing WCF rule IDs and evidence tiers.
- Remoting, package/config, legacy-data/storage, validation/serializer,
  async/callback, and flow-boundary families remain follow-up Task 7 slices
  unless separately verified in a later PR.

Oddities:

- `route-flow --to-surface wcf-operation` was already in the terminal-surface
  vocabulary, but shared surface projection did not create `wcf-operation`
  rows from `WcfServiceReferenceMapping` facts unless a future extractor
  happened to add explicit `surfaceKind` metadata.
- The projection patch uses `normalizedOperationName` as the safe operation
  display source and leaves endpoint/channel/runtime details out of the
  route-flow surface row.
- ACK review found that same-operation WCF mappings from different generated
  clients could otherwise collapse under the route-flow stable surface key; the
  patch now carries `mappingHash`/`metadataHash` as WCF shape identity.

Validation status:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_wcf_operation_surface_only_from_selected_static_path|FullyQualifiedName~Route_flow_keeps_same_operation_wcf_surfaces_distinct_by_mapping_identity|FullyQualifiedName~Route_flow_does_not_infer_adjacent_wcf_operation_surface_without_selected_join"`:
  passed locally with 3 tests.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 51 tests.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Legacy_paths_treat_wcf_operation_as_terminal"`:
  passed locally with 1 test after the legacy-root duplicate terminal-path
  guard.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 errors and the
  existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 660 tests and the
  existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3`.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

## Task 7 Remoting Attachment Slice

Branch: `codex/task7-next-attachment-precision`
Audited base: `origin/dev` at
`edf9c7de8f7bc5eba06bbf6cd9b0a3636aa0c117`.
Selected family: remoting endpoint dependency surfaces.

Scope:

- Keep this PR limited to route-flow attachment precision for remoting static
  evidence already emitted by the deterministic legacy remoting extractor.
- Project remoting fact families as shared dependency surfaces without enabling
  legacy-flow mode in route-flow.
- Attach `remoting-endpoint` dependency surfaces only when the surface node is
  reached by the selected static route-flow path.
- Preserve `DataSurfaceAttachmentMissing` when remoting evidence is present in
  the same combined index but not connected to the selected route-flow path.

Scope decisions:

- This slice does not start UI property lineage, site work, reducer behavior,
  or scanner extraction changes.
- This slice does not add runtime remoting endpoint, channel, activation,
  hosting, deployment, or reachability claims. Rows remain deterministic static
  evidence backed by existing remoting rule IDs and evidence tiers.
- Package/config, HTTP client, legacy-data/storage, validation/serializer,
  async/callback, and flow-boundary families remain follow-up Task 7 slices
  unless separately verified in a later PR.

Oddities:

- Remoting terminal nodes already existed in the legacy-flow graph hook, but
  route-flow uses the normal dependency path graph. The patch therefore
  projects remoting facts as shared dependency surfaces instead of turning on
  legacy-flow mode for route-flow.
- Remoting display identity follows the existing legacy path graph convention
  by preferring safe hash labels such as `objectUri-*` and `url-*`; raw
  endpoint/config values are not surfaced.
- ACK review found that production remoting config facts attach through the
  `RemotingConfiguration.Configure` config-file relationship rather than a
  direct `sourceSymbol`. The patch now reuses the deterministic configure
  caller-to-config join for shared route-flow surfaces.
- ACK/Qodo review found that remoting hash identity must be scoped to remoting
  surfaces and must not rewrite unrelated surface shape hashes. The patch now
  gates remoting hash metadata by remoting surface kind, keeps short safe labels
  for display, and uses longer safe hash tokens for remoting shape identity.

Validation status:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_remoting_endpoint_surface_only_from_selected_static_path|FullyQualifiedName~Route_flow_does_not_infer_adjacent_remoting_endpoint_without_selected_join"`:
  passed locally with 2 tests and the existing NU1903 warning for
  `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 54 tests and the existing NU1903 warning.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~LegacyFlowCompositionTests`:
  passed locally with 24 tests and the existing NU1903 warning.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedDependencyReportTests`:
  passed locally with 18 tests and the existing NU1903 warning after the
  ACK-authorized remoting hash scoping regression test was added.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 errors and the
  existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 663 tests and the
  existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3`.
- Post-ACK `dotnet build src/dotnet/TraceMap.sln`: passed with 0 errors and
  the existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3`.
- Post-ACK `dotnet test src/dotnet/TraceMap.sln`: passed locally with 664
  tests and the existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet run --project src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -- scan --repo samples/modern-sample --out /tmp/tracemap-modern-smoke-46229`:
  passed and produced `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log`.
- Post-ACK `dotnet run --project src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -- scan --repo samples/dotnet-remoting-sample --out /tmp/tracemap-remoting-smoke-27142`:
  passed and produced `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log`.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Post-ACK `./scripts/check-private-paths.sh`: passed.
- Post-ACK `git diff --check`: passed.

## Task 7 Package/Config Attachment Slice

Branch: `codex/task7-package-config-precision-20260626`
Audited base: `origin/dev` at
`3e987d7d46d2bdae7b9c10441bc16ce2c9332010`.
Selected family: package/config dependency surfaces.

Scope:

- Keep this PR limited to route-flow attachment precision for package/config
  facts already projected as deterministic dependency surfaces.
- Project selected package/config fact-symbol rows as route-flow
  `dependency-surface` logic context only when they attach through selected
  source-local route-flow rows.
- Attach `package-config` dependency surfaces only when the surface node is
  reached by the selected static route-flow path.
- Preserve `DataSurfaceAttachmentMissing` when package/config evidence is
  present in the same combined index but not connected to the selected
  route-flow path.

Scope decisions:

- This slice does not start UI property lineage, site work, reducer behavior,
  scanner extraction changes, or package-upgrade impact behavior.
- This slice does not claim package restore, resolved transitive dependencies,
  runtime loading, config value use, deployment, or production behavior.
- HTTP client, legacy-data/storage, validation/serializer, async/callback,
  flow-boundary, and broader service/repository/object/projection breadth
  remain follow-up Task 7 slices unless separately verified in a later PR.

Oddities:

- `package-config` was already accepted by `route-flow --to-surface` and the
  shared surface projection, so the product change is intentionally narrow:
  route-flow fact-symbol projection now treats selected config fact families as
  dependency-surface context alongside package facts and records config-key
  context by hash.
- ACK/Qodo review found that real `ConnectionStringDeclared` and
  `ConfigBinding` rows use `connectionName` and `sectionName` properties, while
  the first pass hashed only `configKey`/`keyPath`-style aliases. The patch now
  recognizes those emitted aliases, plus `configurationKey`, in both shared
  package/config surface display selection and route-flow fact-symbol metadata.
- Existing generic fact-symbol projection can still emit
  `FactSymbolUnsupportedTypeSkipped` for unrelated selected `CallEdge` rows;
  this PR does not broaden call-edge projection semantics.

Validation status:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_package_config_surfaces_only_from_selected_static_path|FullyQualifiedName~Route_flow_does_not_infer_adjacent_package_config_surface_without_selected_join"`:
  passed locally with 2 tests and the existing NU1903 warning for
  `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 56 tests and the existing NU1903 warning for
  `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 errors and the
  existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 666 tests and the
  existing NU1903 warning for `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet run --project src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -- scan --repo samples/modern-sample --out /tmp/tracemap-modern-smoke-package-config-20260626`:
  passed and produced `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log`.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Post-ACK `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_package_config_surfaces_only_from_selected_static_path|FullyQualifiedName~Route_flow_does_not_infer_adjacent_package_config_surface_without_selected_join"`:
  passed locally with 2 tests and the existing NU1903 warning.
- Post-ACK `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 56 tests and the existing NU1903 warning.
- Post-ACK `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedDependencyReportTests`:
  passed locally with 18 tests and the existing NU1903 warning.
- Post-ACK `dotnet build src/dotnet/TraceMap.sln`: passed with 0 errors and
  the existing NU1903 warning.
- Post-ACK `dotnet test src/dotnet/TraceMap.sln`: passed locally with 666
  tests and the existing NU1903 warning.
- Post-ACK `dotnet run --project src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -- scan --repo samples/modern-sample --out /tmp/tracemap-modern-smoke-package-config-postack-20260626`:
  passed and produced `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log`.
- Post-ACK `./scripts/check-private-paths.sh`: passed.
- Post-ACK `git diff --check`: passed.

## Task 7 HTTP Client Attachment Slice

Branch: `codex/task7-http-client-precision-20260626`
Audited base: `origin/dev` at
`268b6d082d18004af0980940f458c42e8fa80407`.
Selected family: HTTP client dependency surfaces.

Scope:

- Keep this PR limited to route-flow attachment precision evidence for
  `HttpCallDetected` rows already projected as deterministic `http-client`
  dependency surfaces.
- Prove selected HTTP client terminal surfaces attach only when the
  `HttpCallDetected` row is reached by the selected static route-flow path
  through source-local symbol evidence.
- Prove adjacent unjoined HTTP client evidence remains excluded from
  dependency surfaces and terminal flow rows, while preserving
  `DataSurfaceAttachmentMissing`.
- Record the HTTP client sub-slice without closing broader Task 7 families.

Scope decisions:

- This slice does not change scanner extraction, route/client alignment,
  reducer behavior, UI property lineage, site work, or runtime HTTP reachability
  claims.
- The route-flow implementation already attached generic dependency surfaces
  through selected static paths; this branch adds the missing focused
  regression evidence rather than adding HTTP-client-specific product logic.
- Legacy-data/storage, validation/serializer, async/callback, flow-boundary,
  and broader service/repository/object/projection breadth remain follow-up
  Task 7 slices unless separately verified in a later PR.

Oddities:

- HTTP client facts are dual-use in combined pathing: they can be selector/root
  evidence for client-call flows and terminal dependency surfaces when a server
  route reaches an outbound client call. The tests isolate the terminal-surface
  behavior so the slice does not broaden endpoint alignment semantics.
- The attached test uses the selected route path
  `controller -> payment gateway -> http-client surface` and keeps an unrelated
  HTTP client fact in the same combined index to prove adjacent evidence is not
  inferred.
- The unjoined test keeps a matching route and downstream service call but only
  an unrelated HTTP client surface, proving route-flow emits the scoped
  attachment gap rather than a clean no-evidence conclusion.

Validation status:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_http_client_surface_only_from_selected_static_path|FullyQualifiedName~Route_flow_does_not_infer_adjacent_http_client_surface_without_selected_join"`:
  passed locally with 2 tests and the existing NU1903 warning for
  `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 58 tests and the existing NU1903 warning.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 errors and the
  existing NU1903 warning.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 668 tests and the
  existing NU1903 warning.
- `dotnet run --project src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -- scan --repo samples/modern-sample --out /tmp/tracemap-modern-smoke-http-client-20260626`:
  passed and produced `scan-manifest.json`, `facts.ndjson`, `index.sqlite`,
  `report.md`, and `logs/analyzer.log`.
- Post-Qodo doc-drift patch
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_http_client_surface_only_from_selected_static_path|FullyQualifiedName~Route_flow_does_not_infer_adjacent_http_client_surface_without_selected_join"`:
  passed locally with 2 tests and the existing NU1903 warning.
- Post-Qodo doc-drift patch `./scripts/check-private-paths.sh`: passed.
- Post-Qodo doc-drift patch `git diff --check`: passed.
- Post-Qodo stale-phrase patch
  `rg -n 'spec-only|limited to this spec folder|limited to \\.kiro' .kiro/specs/route-flow-service-data-composition-final`:
  passed with no matches.
- Post-Qodo stale-phrase patch
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_http_client_surface_only_from_selected_static_path|FullyQualifiedName~Route_flow_does_not_infer_adjacent_http_client_surface_without_selected_join"`:
  passed locally with 2 tests and the existing NU1903 warning.
- Post-Qodo stale-phrase patch `./scripts/check-private-paths.sh`: passed.
- Post-Qodo stale-phrase patch `git diff --check`: passed.

## Task 7 Legacy-Data/Storage Attachment Slice

Branch: `codex/task7-storage-precision-20260626`
Audited base: `origin/dev` at
`45fc97b50115d6f7c99389d2b8e114352c387cd9`.
Selected family: legacy-data/storage dependency surfaces.

Scope:

- Keep this PR limited to route-flow attachment precision evidence for
  `legacy-data` terminal surfaces already projected from deterministic legacy
  data model facts.
- Prove selected legacy entity and storage-object descriptors attach only when
  reached by the selected static route-flow path through source-local symbol
  evidence.
- Prove adjacent unjoined legacy storage descriptors remain excluded from
  dependency surfaces and terminal flow rows while preserving
  `DataSurfaceAttachmentMissing`.
- Record the legacy-data/storage sub-slice without closing broader Task 7
  families.

Scope decisions:

- This slice does not change scanner extraction, legacy data model projection,
  reducer behavior, UI property lineage, site work, or runtime database/schema
  existence claims.
- The route-flow implementation already accepted `legacy-data` as a terminal
  surface; this branch adds the missing focused attachment and unjoined-gap
  regression evidence rather than adding legacy-data-specific product logic.
- Validation/guard, serializer/contract, async/callback, flow-boundary, and
  broader service/repository/object/projection breadth remain follow-up Task 7
  slices unless separately verified in a later PR.

Oddities:

- Existing route-flow legacy-data coverage only preserved the
  `surfaceSubtype = data-model` row shape; it did not prove selected versus
  adjacent-unjoined storage precision.
- Route-flow dependency surface evidence cites the original legacy DBML rule
  (`legacy.data.dbml.v1`) in supporting rule IDs; it does not currently expose
  the descriptor projection rule on the route-flow row.
- The focused tests assert public-safe redaction by excluding raw legacy
  descriptor names from Markdown and JSON while retaining hashed stable surface
  keys.
- ACK review found that the first storage fixture attached descriptors by
  setting `sourceSymbol` directly to the repository method. The patch now seeds
  scanner-shaped legacy metadata descriptors with null `sourceSymbol` and
  generated/model `targetSymbol` attachment, then reaches those descriptors
  through selected static route-flow calls.

Validation status:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_legacy_data_storage_surfaces_only_from_selected_static_path|FullyQualifiedName~Route_flow_does_not_infer_adjacent_legacy_data_storage_without_selected_join"`:
  passed locally with 2 tests and the existing NU1903 warning for
  `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 59 tests and the existing NU1903 warning.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 errors and the
  existing NU1903 warning.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 669 tests and
  the existing NU1903 warning.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Post-ACK patch
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_legacy_data_storage_surfaces_only_from_selected_static_path|FullyQualifiedName~Route_flow_does_not_infer_adjacent_legacy_data_storage_without_selected_join"`:
  passed locally with 2 tests and the existing NU1903 warning.
- Post-ACK patch
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 59 tests and the existing NU1903 warning.
- Post-ACK patch `dotnet build src/dotnet/TraceMap.sln`: passed with 0
  errors and the existing NU1903 warning.
- Post-ACK patch first `dotnet test src/dotnet/TraceMap.sln` rerun failed one
  unrelated `BuildEnvironmentDiagnosticTests.Cli_restore_failure_artifacts_are_sanitized`
  assertion; an immediate focused rerun of that test passed.
- Post-ACK patch second `dotnet test src/dotnet/TraceMap.sln`: passed locally
  with 669 tests and the existing NU1903 warning.
- Post-ACK patch `./scripts/check-private-paths.sh`: passed.
- Post-ACK patch `git diff --check`: passed.

## Summary

This packet now tracks both the original spec delivery and subsequent
focused implementation/test slices for the remaining route-centered static
service/data composition work. The target product behavior is a conservative
`tracemap route-flow` trace that can start at endpoint/root method evidence,
stitch into service methods, continue through review-tier implementation
candidates where rule-backed static evidence allows, and render data/query/
dependency/value-origin rows or narrower gaps with evidence provenance.

The merged product implementation PRs for this spec changed route-flow
reporting code, focused route-flow tests, and this spec state/task tracking.
No site files, generated outputs, scanner extraction logic, LLM/vector/prompt
logic, or sample artifacts were part of those route-flow slices.

## Source Material Reviewed

- `AGENTS.md`
- GitHub issue #159: Add route-centered static call flow report
- GitHub issue #179: Compose route-flow evidence through service and data facts
- GitHub issue #201: Route-flow should stitch endpoint roots through call edges
  and implementation relationships
- `.kiro/specs/route-flow-service-data-composition/`
- `.kiro/specs/route-flow-service-data-composition-next/`
- `.kiro/specs/route-centered-endpoint-trace-completeness/`
- `.kiro/specs/route-centered-static-flow-report/`
- `.kiro/specs/route-flow-endpoint-stitching/`
- `scripts/kiro-review.mjs`

## Scope Decisions

- This spec is a continuation and completion packet, not a rewrite.
- The implementation target remains `tracemap route-flow`.
- The JSON report type remains `route-flow`.
- JSON version remains `1.0`.
- The route-flow classification vocabulary remains unchanged.
- The `combined.route-flow.*` rule namespace remains the authority.
- The next implementation PR should focus on one route-centered composition
  slice: endpoint/root method to service/data trace completion over existing
  combined evidence, plus associated gaps, downgrades, deterministic output,
  and safety tests.
- This spec owns only service/data/query/dependency/value-origin composition and
  downgrade behavior for selected route-flow rows. It does not own broad
  endpoint trace summaries, site copy, scanner extraction, runtime proof, or
  public marketing claims.
- Interface/implementation evidence remains static candidate context and cannot
  prove runtime DI target selection.
- Adjacent but unjoined data/query/dependency/value-origin facts become scoped
  gaps or path-context rows, not inferred executed path edges.

## Safety Boundaries

The spec forbids committing private local paths, private sample names, private
route strings, raw SQL, raw config values, connection strings, secrets, raw
endpoint URLs, query strings, raw remotes, source snippets, hostnames, private
labels, or generated private outputs.

The implementation must not claim runtime request execution, endpoint
reachability, dependency-injection binding, dynamic dispatch, branch
feasibility, authorization behavior, SQL execution, database state, data
contents, production traffic, release approval, AI impact analysis, LLM
analysis, embeddings, vector search, semantic search, fuzzy matching, or prompt
classification.

## Review Plan

Required review loop for the original spec delivery branch:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Patch Medium+ actionable findings. Then run one bounded final re-review with
Sonnet or Opus and record the command, coverage, artifact, findings, and
disposition here. If either model or tool path is unavailable or times out,
record the exact command, exit state, and evidence; do not invent approval.

## Review Log

Initial Opus spec review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T030749-645Z-spec-claude-opus-4.8.clean.md`
- Session: `050a3cfd-e4e2-4c6f-b03c-353aa433e92b`.
- Findings patched:
  - canonical sort order mismatch between requirements and design;
  - already-shipped versus remaining delta was too implicit;
  - gap-code versus rule-ID wording could encourage a parallel namespace;
  - implementation task-to-PR-boundary mapping was too loose;
  - missing tests for `--exit-code`, `classificationCap`, context-group weakest
    rollup, redaction rule citation, single-candidate caps, and Tier3-only
    downstream evidence.
- Local disposition for branch/diff blocker: the shared checkout had unrelated
  untracked/staged spec folders from other work. The final commit was prepared
  from a clean temporary worktree on
  `codex/spec-route-flow-service-data-composition-final`, and only this spec
  folder is staged there.

Initial Sonnet spec review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, full coverage.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T031107-726Z-spec-claude-sonnet-4.6.clean.md`
- Session: `45ee925f-88e6-4427-ae8f-90ea5b07b673`.
- Findings patched:
  - review and validation state needed to be completed before merge;
  - strong classification needed an explicit Tier1/Tier2 stitched downstream
    evidence prerequisite;
  - full relevant coverage needed a concrete definition;
  - task scope needed to make clear that Tasks 5-10 are sequenced across PR
    boundaries, not one large implementation PR;
  - task 4 needed explicit gap-code catalog and live JSON field audits.

Bounded Sonnet re-review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T031524-218Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `a8b690b0-021d-47b8-b0d3-75fffafcbb16`.
- Findings patched after re-review:
  - relationship table now acknowledges an optional
    `static-dispatch-candidate-bridges` shared candidate contract when present
    on the implementation base;
  - duplicate/ambiguous endpoint root gap mapping now names `SelectorNoMatch`,
    `MissingRouteRoot`, and the need to amend `combined.route-flow.gap.v1` for
    any new code;
  - design now references Requirement 5.8 as the normative full-coverage
    definition to avoid drift;
  - additive field list now warns implementers to audit existing fields before
    treating them as new work.

Final readiness note: no product code was changed. Medium+ actionable spec
findings from the usable Opus/Sonnet reviews were patched. Remaining Kiro
re-review blockers were process-state observations that this file and
`tasks.md` were pending at review time; those are completed in this state
update.

Final Sonnet re-review from the clean worktree:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, full coverage.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T032003-892Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `993d3ffa-0eb8-4b58-8a4c-0bf983293a95`.
- Result: no blocking findings. The review said the spec is ready to merge as
  is and suggested two optional clarifications. Both were patched:
  - Task 4 now explicitly audits requested predecessor spec ownership.
  - Follow-up notes now explain that `static-dispatch-candidate-bridges` is a
    conditional reference and may not exist on the implementation base.

## Validation Plan

Spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Also discover whether a dedicated spec/docs validation command exists. If none
exists, record the discovery command and result here.

Product implementation validation is documented in `tasks.md` and `design.md`,
but it is intentionally deferred because the original branch delivered only
spec files.

## Validation Log

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Spec/docs validation discovery:
  `find scripts -maxdepth 1 -type f | sort | rg 'spec|kiro|validate|lint|check'`
  found `scripts/check-private-paths.sh`, `scripts/kiro-review.mjs`, and
  `scripts/validate-legacy-codebases.sh`; no separate dedicated spec lint
  command was identified.
- Safety scan:
  a targeted `rg` scan for local path, raw URL, raw SQL, secret/config, private
  key, and private-route patterns returned no matches before this validation
  note was recorded.
- Diff scope in the clean worktree:
  `git status --short --branch` showed only
  `.kiro/specs/route-flow-service-data-composition-final/` as untracked before
  staging. Unrelated untracked/staged spec folders observed in the original
  shared checkout were not staged or committed for this PR.

## PR Loop Notes

- PR #311 opened against `dev` from
  `codex/spec-route-flow-service-data-composition-final`.
- First ACK PR loop returned `actionable_findings` with five unresolved Gemini
  review threads.
- Patched review findings:
  - changed the JSON `reportCoverage` example from placeholder wording to the
    live `FullEvidenceAvailable` value;
  - moved duplicate/ambiguous endpoint-root mapping out of the gap-code bullet
    list;
  - fixed a split Markdown code span for
    `route-centered-endpoint-trace-completeness`;
  - changed the Task 4 audit reference to requested predecessor specs so it
    does not introduce an extra ownership dependency.
- Post-patch validation: `git diff --check`, `./scripts/check-private-paths.sh`,
  and the targeted safety scan passed.

## Implementation Guidance For Next PR

Recommended next implementation flow:

1. Audit live `dev` route-flow state and update this file with exactly which
   unchecked task is selected.
2. Choose the smallest bridge/gap slice that improves endpoint/root method to
   service/data trace completion without broad refactors.
3. Add focused synthetic tests first where practical.
4. Patch route-flow report code only where evidence-backed tests expose a live
   gap.
5. Run focused route-flow tests, full .NET build/test, safety checks, and
   relevant pinned route-flow/reporting smokes or record explicit deferrals.

## Product Implementation PR 1

Branch: `codex/implement-route-flow-service-data-composition-final`
Base: `origin/dev` at `848e76f7eff53b6ddf427f50d8971d9bbd78bb8d`

Selected slice: Task 4 audit plus the smallest Task 5/endpoint-stitching gap
left by the live audit: duplicate normalized endpoint roots now emit a
deterministic selector ambiguity gap and downgrade the route-flow report instead
of allowing duplicate roots to look like a clean strong route-flow conclusion.

### Live Audit Notes

- `CombinedRouteFlowReport` already preserves `reportType = "route-flow"`,
  JSON `version = "1.0"`, the five `RouteFlowClassifications` values, the
  existing `combined.route-flow.*` rules, and the existing command surface.
- Live JSON top-level fields are `reportType`, `version`, `reportCoverage`,
  `coverageWarnings`, `query`, `snapshot`, `summary`, `entryEvidence`,
  `flowRows`, `logicRows`, `dependencySurfaces`, `touchedFiles`,
  `touchedSymbols`, `gaps`, `limitations`, and additive `contextGroups`.
- Existing implementation already covers route/root method bridges, downstream
  static call rows, object creation rows, parameter-forward traversal,
  interface candidate rows, attached query/data/dependency rows, fact-symbol and
  argument projection rows, context groups, touched files/symbols, schema gaps,
  identity/reduced-coverage gaps, redaction, byte-stability, and rule-catalog
  resolution tests.
- Existing route-flow gap catalog entries under `combined.route-flow.gap.v1`
  include the design taxonomy needed by this slice: `SelectorNoMatch`,
  `MissingRouteRoot`, `MissingMethodSymbolBridge`, `MissingCallEdge`,
  `MissingImplementationBridge`, `DataSurfaceAttachmentMissing`,
  `SchemaMissing`, `ExtractorUnavailable`,
  `ImplementationCandidateUnavailable`, `AmbiguousImplementationCandidates`,
  `IdentityGap`, `RuntimeBindingNotProven`, `ReducedCoverage`,
  `UnknownCommitSha`, `NoRouteFlowEvidence`, `UnknownAnalysisGap`,
  `TraversalBounds`, `TruncatedByLimit`, and `UnsafeValueOmitted`.
- Requested predecessor specs were audited for ownership boundaries:
  `route-centered-static-flow-report`, `route-flow-service-data-composition`,
  `route-flow-service-data-composition-next`,
  `route-centered-endpoint-trace-completeness`, and
  `route-flow-endpoint-stitching`. A conditional
  `static-dispatch-candidate-bridges` spec exists on this base.
- The unchecked predecessor gap that fit this PR boundary was duplicate
  normalized route roots. Broader direct-call, no-call, candidate, attachment,
  truncation, and smoke-hardening work remains follow-up scope.
- `classificationCap` was audited as an allowed additive field but is not
  emitted by the live route-flow model on this base. This PR does not add it
  because the selected duplicate-root slice needs only existing gap,
  classification, and coverage fields. If a future candidate or downgrade slice
  adds `classificationCap`, that PR should add the corresponding field tests.
- No new command, report type, JSON version, traversal engine, persisted rows,
  rule namespace, LLM calls, embeddings, vector database, semantic search, or
  prompt-based classification were introduced.

### Implementation Notes

- Added endpoint-root ambiguity detection in `CombinedRouteFlowReport` after
  source-scoped route/client root resolution and before traversal.
- When a normalized selector resolves to multiple endpoint roots, route-flow now
  emits a single deterministic `SelectorNoMatch` gap using
  `combined.route-flow.selector.v1`, `Tier4Unknown`, `ReducedCoverage`, a safe
  source label, affected root node ID, supporting route fact IDs, file span, and
  limitations.
- The report still renders static rows for the matched roots as review context,
  but the blocking selector gap forces `ReducedCoverage` and
  `UnknownAnalysisGap`; narrowing with `--from-source` removes the ambiguity.
- Added a synthetic two-source regression test proving the duplicate-root gap,
  downgrade, safe supporting evidence, and narrowed-selector behavior.
- Preserved the existing route-flow pattern where selector-miss gaps that arise
  from selector handling cite `combined.route-flow.selector.v1`; the broader
  `combined.route-flow.gap.v1` catalog entry still documents `SelectorNoMatch`
  as a public gap kind.

### Validation Log For PR 1

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`: passed, 32 tests after the first implementation slice and 33 tests after Kiro re-review patches. NuGet emitted existing
  `SQLitePCLRaw.lib.e_sqlite3` high-severity vulnerability warnings.
- `dotnet build src/dotnet/TraceMap.sln`: passed with the same existing
  `SQLitePCLRaw.lib.e_sqlite3` NuGet warnings.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 632 tests before review
  patches, 633 tests after the first Kiro patch set, and 634 tests after the
  final CLI precedence test, with the same existing `SQLitePCLRaw.lib.e_sqlite3`
  NuGet warnings.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- `./scripts/demo-public.sh <temporary-public-demo-output>`: passed.
  Generated artifacts stayed under a temporary output directory.
- Explicit public-safe route-flow smoke over the generated endpoint combined
  index passed:
  `dotnet run --no-build --project src/dotnet/TraceMap.Cli -- route-flow --index <temporary-public-demo-output>/combined/endpoint-stack.sqlite --route "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out <temporary-public-demo-output>/reports/route-flow/endpoint-to-sql`.
  It produced `route-flow-report.md` and `route-flow-report.json` with
  `reportType = "route-flow"`, `version = "1.0"`, static rows, dependency
  surfaces, and reduced-coverage gaps.
- Targeted safety scan of the explicit route-flow smoke artifacts found no
  raw local workspace path, raw SQL wildcard, private connection-string sample,
  password token, raw URL, or raw GitHub remote matches.

### Kiro Implementation Review

Initial Sonnet implementation review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T041519-756Z-implementation-claude-sonnet-4.6.clean.md`
- Session: `49738086-13df-4a6c-9e32-aa0a19324c44`.
- Medium+/blocking findings patched:
  - added an explicit summary-level non-strong assertion for single
    implementation-candidate continuation;
  - added a strong-route-root plus Tier3 downstream service edge fixture proving
    Tier3 downstream evidence cannot satisfy `StrongStaticRouteFlow`;
  - recorded the `classificationCap` audit and deferral for this PR boundary.
- Non-blocking finding disposition:
  - duplicate-root selector ambiguity intentionally cites
    `combined.route-flow.selector.v1`, matching existing selector gap behavior;
    this state file records the decision.

Bounded Sonnet implementation re-review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, full coverage.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T041939-659Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `3e3af9fe-ed24-4551-8d16-0d0a56ca299a`.
- Medium+/blocking findings patched or dispositioned:
  - explicit Task 5 follow-up mapping was added below for each still-unchecked
    Requirement 8.1 subcase;
  - added route-flow CLI validation-error precedence coverage for `--exit-code`
    so argument validation returns before classification mapping;
  - tightened the single implementation-candidate test to assert the summary
    cannot upgrade to `StrongStaticRouteFlow` or `ProbableStaticRouteFlow`. The
    live fixture currently rolls up to `UnknownAnalysisGap`, which is weaker
    than `NeedsReviewStaticRouteFlow` and remains inside the spec cap.

Final bounded Sonnet re-review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T042537-020Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `407ab576-8775-4499-8fcf-7b22d171371e`.
- Disposition:
  - corrected this state file so it no longer claims a positive
    `NeedsReviewStaticRouteFlow` summary assertion for the single-candidate
    fixture;
  - recorded the `NoRouteFlowEvidence` `--exit-code` follow-up below;
  - no further Kiro re-review was run because this was the second bounded
    re-review cycle and no product-code safety blocker remained.

### ACK PR Loop Review

Initial ACK PR loop for PR #318 waited for the required Codex/Qodo batch before
authorizing patches. After both required reviewers returned, ACK reported
`actionable_findings` with three unresolved review threads and
`patchAuthorized=true`.

ACK-authorized findings patched in the follow-up commit:

- preserved duplicate endpoint-root ambiguity gaps on the no-terminal early
  return path instead of dropping them when the selected terminal surface has no
  reachable nodes;
- removed redundant resorting inside duplicate-root gap generation by relying
  on the already sorted route-root list;
- added optional commit/extractor metadata to `RouteFlowGap` and threaded the
  duplicate-root root commit, extractor name, and extractor version into emitted
  evidence.

After the first follow-up commit was pushed, ACK reported one additional
actionable Qodo top-level finding about unbounded ambiguity-gap allocations.
The second local ACK patch:

- caps duplicate-root gap `SupportingFactIds` at 20 using a bounded sorted
  prefix, matching existing route-flow gap payload conventions;
- replaces the gap-ID `string.Join` over all ambiguous root IDs with an
  incremental SHA-256 hash over the deterministic root sequence;
- adds a truncation limitation that reports the cap and non-empty supporting
  fact reference count;
- adds a regression fixture with 25 duplicate route roots proving the cap and
  stable repeated gap ID.

Focused validation after the local ACK patch:

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
  passed, 33 tests, with the existing `SQLitePCLRaw.lib.e_sqlite3` NuGet
  vulnerability warnings.
- After the second ACK patch, the same focused command passed, 34 tests, with
  the same existing NuGet warnings.
- `dotnet build src/dotnet/TraceMap.sln`: passed with the same existing
  `SQLitePCLRaw.lib.e_sqlite3` NuGet warnings.
- First post-ACK `dotnet test src/dotnet/TraceMap.sln` run had one unrelated
  diagnostic test miss for `NuGetRestoreFailed` in generated artifact text.
  The single failing test then passed in isolation, and a full solution rerun
  passed, 634 tests, with the same existing NuGet warnings.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- `./scripts/demo-public.sh <temporary-public-demo-output>`: passed.
- Explicit route-flow smoke over the refreshed public endpoint combined index
  passed and produced `UnknownAnalysisGap`, `ReducedCoverage`, 3 entry evidence
  rows, 8 static flow rows, 5 business/data logic rows, 4 dependency surfaces,
  and 45 gaps.
- Targeted safety scan of the refreshed route-flow smoke artifacts found no
  raw local workspace path, raw SQL wildcard, private connection-string sample,
  password token, raw URL, raw GitHub remote, or private feed/token matches.
- After the second ACK patch, `dotnet build src/dotnet/TraceMap.sln` passed,
  `dotnet test src/dotnet/TraceMap.sln` passed with 635 tests,
  `./scripts/check-private-paths.sh` passed, and `git diff --check` passed.
- The explicit route-flow smoke was rerun against the refreshed temporary
  public demo endpoint combined index and again produced `UnknownAnalysisGap`,
  `ReducedCoverage`, 3 entry evidence
  rows, 8 static flow rows, 5 business/data logic rows, 4 dependency surfaces,
  and 45 gaps. A targeted artifact scan again found no raw local path, raw SQL
  wildcard, private connection-string sample, password token, raw URL, raw
  GitHub remote, or private feed/token matches.

### Oddities

- The duplicate-root gap reuses `SelectorNoMatch` with
  `combined.route-flow.selector.v1`, matching existing route-flow selector
  filter behavior. No new gap code was added.
- The focused test intentionally leaves both static root traces visible because
  the rows are still evidence-backed review context; the summary downgrade is
  what prevents a clean conclusion.

### Follow-Ups

- Complete the remaining Task 5 direct-call tests: no direct call under full
  coverage, no direct call under reduced coverage, and deterministic ordering.
  This is the remaining Task 5 PR-1 backlog after the duplicate-root and
  cycle-gap sub-slices.
  - No direct call under full coverage: existing
    `Route_flow_emits_no_route_flow_evidence_only_after_clean_bridge_checks`
    covers the clean bridge/no downstream evidence shape; additional
    MissingCallEdge-specific full-coverage dead-end coverage remains a Task 5
    follow-up.
  - No direct call under reduced coverage: deferred to the next Task 5
    follow-up because this PR touched duplicate-root selector ambiguity only.
  - Cycles: covered in the takeover cycle-gap slice. Source-local service-call
    cycles now emit a deterministic `TraversalBounds` gap, suppress inherited
    clean `NoRouteFlowEvidence` path gaps when blocking endpoint-composition
    gaps are present, and downgrade the route-flow summary to
    `UnknownAnalysisGap`.
  - Deterministic ordering: existing route-flow smoke and primary route-flow
    test cover byte-stable repeated output, but duplicate-root-specific row/gap
    permutation coverage remains a Task 5 follow-up.
- Add a `NoRouteFlowEvidence`-specific `--exit-code` non-zero assertion in the
  next Task 9 PR boundary. This PR added validation-error precedence coverage
  for `--exit-code`, and existing route-flow CLI coverage still proves non-zero
  behavior for review/unknown summary mappings.
- Add the broader Task 10 negative safety matrix for raw SQL, raw config, URLs,
  private labels, snippets, remotes, and logs in the attached-row/public-safety
  PR boundary. This PR preserved existing route-flow safety checks and ran the
  private-path guard plus explicit route-flow artifact scan.
- Continue Task 6/7 follow-ups for candidate continuation and unjoinable
  service/data/query/dependency/value-origin attachment precision.
- For Task 7 projection/attachment precision, cap or batch large SQL parameter
  lists used when reading argument-pair and fact-symbol projection rows so
  large selected route-flow graphs do not exceed SQLite parameter limits.
- Run the ACK PR loop before merge readiness is claimed.

## Product Implementation PR 2 Takeover

Branch: `codex/implement-route-flow-service-data-composition-final`
Base: `origin/dev` at `1e9c56602d18a9e62f4523a66314d3755553b4fa`

Takeover notes:

- Reused the existing implementation worktree and branch.
- Fetched latest `origin/dev`; the previous duplicate-root slice was already
  merged as PR #318, so the branch fast-forwarded to current `origin/dev`.
- Left generated `.agent-control/tmp/` output untracked and unstaged.
- Selected the smallest remaining Task 5 cycle-gap slice rather than broadening
  into candidate continuation, attachment precision, scanner extraction, site
  work, or rule-catalog changes.

### Cycle-Gap Scope

- Endpoint route-flow traversal now records skipped source-local cycle edges
  when a method cannot expand without revisiting a node already in the current
  static path.
- The cycle gap reuses `TraversalBounds` under
  `combined.route-flow.gap.v1`, carries Tier4/ReducedCoverage, keeps safe file
  span evidence from the skipped cycle edge, and caps supporting fact IDs at the
  existing route-flow bounded-evidence limit.
- Inherited path-level `NoRouteFlowEvidence` gaps are filtered when endpoint
  composition has blocking gaps, so a known cycle/coverage gap is not rendered
  beside a clean absence claim.
- Added a synthetic regression proving deterministic repeated gap ID,
  rule/evidence/coverage fields, service-file span evidence, no completed flow
  rows, no clean no-evidence gap, and summary downgrade to
  `UnknownAnalysisGap`.

### Validation Log For PR 2

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
  passed, 35 tests, with the existing `SQLitePCLRaw.lib.e_sqlite3` NuGet
  vulnerability warnings.
- `dotnet build src/dotnet/TraceMap.sln`: passed with the same existing
  `SQLitePCLRaw.lib.e_sqlite3` NuGet vulnerability warnings.
- First `dotnet test src/dotnet/TraceMap.sln` run had one unrelated
  diagnostic test miss for `NuGetRestoreFailed` in generated artifact text.
  The single failing diagnostic test passed in isolation, and the full solution
  rerun passed, 636 tests, with the same existing NuGet warnings.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- After the Kiro B1 patch, focused route-flow tests passed again, 35 tests,
  with the same existing NuGet warnings.
- Final post-Kiro-patch `dotnet build src/dotnet/TraceMap.sln`: passed with
  the same existing `SQLitePCLRaw.lib.e_sqlite3` NuGet warnings.
- Final post-Kiro-patch `dotnet test src/dotnet/TraceMap.sln`: passed, 636
  tests, with the same existing NuGet warnings.
- Final post-Kiro-patch `./scripts/check-private-paths.sh`: passed.
- Final post-Kiro-patch `git diff --check`: passed.
- `./scripts/demo-public.sh /tmp/tracemap-route-flow-pr2-demo-20260624`:
  passed with the same existing NuGet warnings while building the .NET tools.
- Explicit route-flow smoke over the refreshed public endpoint combined index
  passed:
  `dotnet run --no-build --project src/dotnet/TraceMap.Cli -- route-flow --index <temporary-public-demo-output>/combined/endpoint-stack.sqlite --route "GET /api/admin/runner/get-by-id/{}" --to-surface sql-query --out <temporary-public-demo-output>/reports/route-flow/endpoint-to-sql`.
  It produced `UnknownAnalysisGap`, `ReducedCoverage`, 3 entry evidence rows,
  8 static flow rows, 5 business/data logic rows, 4 dependency surfaces, and
  43 gaps.
- Targeted safety scan of the refreshed route-flow smoke artifacts found no raw
  local workspace path, raw SQL wildcard, private connection-string sample,
  password token, raw URL, raw GitHub remote, private feed, secret-token,
  query-token, or connection-string key matches.
- PR #320 opened against `dev` from
  `codex/implement-route-flow-service-data-composition-final`.
- Initial ACK loop for PR #320 waited for the required Codex/Qodo batch before
  authorizing patches. After both required reviewers returned, ACK reported
  `actionable_findings` with three unresolved review threads and
  `patchAuthorized=true`.
- ACK-authorized findings patched locally:
  - changed bounded supporting fact ID emission to sort the selected `List` in
    place before returning it;
  - delayed cycle `TraversalBounds` gap emission until after implementation
    candidate expansion has had a chance to continue traversal;
  - populated cycle gap commit SHA, extractor name, and extractor version from
    available source/edge evidence.
- Added focused regression coverage proving cycle gap metadata and proving no
  premature cycle gap is emitted when a static implementation candidate can
  continue to a terminal surface.
- Focused validation after the local ACK patch:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`
  passed, 36 tests, with the same existing NuGet warnings.
- Post-ACK `dotnet build src/dotnet/TraceMap.sln`: passed with the same
  existing NuGet warnings.
- Post-ACK `dotnet test src/dotnet/TraceMap.sln`: passed, 637 tests, with the
  same existing NuGet warnings.
- Post-ACK `./scripts/check-private-paths.sh`: passed.
- Post-ACK `git diff --check`: passed.
- Post-ACK explicit route-flow smoke over the refreshed public endpoint
  combined index passed and produced `UnknownAnalysisGap`, `ReducedCoverage`, 3
  entry evidence rows, 8 static flow rows, 5 business/data logic rows, 4
  dependency surfaces, and 43 gaps.
- Post-ACK targeted safety scan of the refreshed route-flow smoke artifacts
  found no raw local workspace path, raw SQL wildcard, private
  connection-string sample, password token, raw URL, raw GitHub remote, private
  feed, secret-token, query-token, or connection-string key matches.
- After the first ACK patch was pushed, ACK reported one remaining actionable
  Qodo top-level finding about unbounded `string.Join` allocation in the cycle
  gap ID hash.
- Second ACK-authorized patch replaced the joined edge-ID hash input with
  incremental SHA-256 hashing over the deterministic edge ID sequence and edge
  count.
- Focused validation after the second ACK patch:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`
  passed, 36 tests, with the same existing NuGet warnings.
- Post-second-ACK `dotnet build src/dotnet/TraceMap.sln`: passed with the same
  existing NuGet warnings.
- Post-second-ACK `dotnet test src/dotnet/TraceMap.sln`: passed, 637 tests,
  with the same existing NuGet warnings.
- Post-second-ACK `./scripts/check-private-paths.sh`: passed.
- Post-second-ACK `git diff --check`: passed.
- Post-second-ACK explicit route-flow smoke over the refreshed public endpoint
  combined index passed and produced `UnknownAnalysisGap`, `ReducedCoverage`, 3
  entry evidence rows, 8 static flow rows, 5 business/data logic rows, 4
  dependency surfaces, and 43 gaps.
- Post-second-ACK targeted safety scan of the refreshed route-flow smoke
  artifacts found no raw local workspace path, raw SQL wildcard, private
  connection-string sample, password token, raw URL, raw GitHub remote, private
  feed, secret-token, query-token, or connection-string key matches.
- The second ACK patch was pushed, ACK was rerun by the implementation worker,
  and PR #320 merged to `dev` as `565d7b64`. This reconciliation pass found no
  remaining PR #320-specific local action to push.

### Kiro Implementation Review For PR 2

Initial Sonnet implementation review:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind implementation --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T050158-646Z-implementation-claude-sonnet-4.6.clean.md`
- Session: `38454468-69de-4cad-963d-042fcd53de03`.
- Medium+/blocking findings patched or dispositioned:
  - Patched B1 by changing bounded duplicate-root supporting fact selection to
    keep the first distinct fact IDs from deterministic root order and sort
    only the emitted bounded set. Added a regression assertion that the 20
    emitted duplicate-root supporting fact IDs come from the canonical first 20
    route facts, not the lexicographically smallest 20 IDs.
  - Dispositioned B2 as not correct for the live combined graph: the current
    method symbol node can retain caller-file evidence from the incoming call
    fact, while the skipped cycle edge carries the precise service-file span
    where the cycle edge was observed. Keeping the gap affected row on the
    current node and the file span on the skipped edge gives more precise
    evidence provenance for this slice. The regression asserts the service edge
    span.
- Non-blocking findings recorded as follow-ups unless a future PR selects that
  scope: `NoRouteFlowEvidence` and probable-route `--exit-code` boundary tests,
  redaction citation precision, large projection SQL parameter caps, and
  `combined_symbol_relationships` schema-gap coverage.

Bounded Sonnet re-review after the B1 patch:

```bash
node scripts/kiro-review.mjs --phase route-flow-service-data-composition-final --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- Result: wrapper exit code 0, reduced coverage because Kiro reported denied
  tool access.
- Artifact:
  `.tmp/kiro-reviews/route-flow-service-data-composition-final/2026-06-24T050642-522Z-re-review-claude-sonnet-4.6.clean.md`
- Session: `01719e6f-c797-4027-9546-af5a71ee1ff4`.
- Findings:
  - Product-code readiness was accepted for the cycle-gap slice.
  - Remaining blockers were process closure items: record post-patch full
    validation and record the re-review result. Both are now recorded above.
  - Non-blocking notes were to preserve the cycle/no-evidence filtering
    design decision and large projection SQL parameter caps as follow-ups.

### Oddities / Design Decisions For PR 2

- Cycle gaps use `TraversalBounds` under `combined.route-flow.gap.v1`; no new
  gap code or rule namespace was added.
- For skipped cycle edges, the gap keeps the affected row on the method node
  where traversal stopped, but uses the skipped cycle edge file span because
  the live combined method node can retain caller-file evidence from the
  incoming call fact. This keeps the rendered gap span on the observed cycle
  edge.
- Path-level clean `NoRouteFlowEvidence` gaps are filtered when endpoint
  composition emits blocking gaps such as `TraversalBounds`, `MissingCallEdge`,
  or `DataSurfaceAttachmentMissing`, so the report does not render a clean
  absence claim beside a known coverage/composition gap.

## Follow-Up Notes

- Private legacy smoke validation may be useful during implementation, but any
  output must remain local-only and summarized generically.
- Public examples should use synthetic selectors such as
  `GET /api/admin/users/roles` or checked-in public fixtures.
- `static-dispatch-candidate-bridges` is referenced conditionally in
  `design.md` but may not exist as a checked-in spec on the implementation base.
  During Task 4, confirm whether a shared candidate contract is present in live
  code under another name or consume candidate evidence directly through
  existing route-flow interface bridge rows.
- If implementation requires a new gap code or row kind, update
  `rules/rule-catalog.yml` before emitting it and document limitations in the
  implementation state.

## Product Implementation PR 3

Branch: `codex/route-flow-service-data-stitching`
Base: `origin/dev` at `87fe78a31ce9204230b00dab8dfeaabf79d1b206`

Selected slice: the next remaining Task 5 no-direct-call/reduced-coverage
subcase, plus the directly touched Task 8 guard that clean
`NoRouteFlowEvidence` gaps require full relevant route-flow coverage.

### Live Audit Notes

- The prior duplicate-root and cycle Task 5 sub-slices are already present on
  `origin/dev`.
- Direct source-local service/repository call stitching and MissingCallEdge
  behavior already exist in `CombinedRouteFlowReport`.
- The remaining gap selected for this PR was narrower: when a route root has no
  downstream flow rows and source coverage is reduced, route-flow should keep
  the reduced/unknown gap and must not also emit a clean absence
  `NoRouteFlowEvidence` gap.

### Implementation Notes

- Added a route-flow report guard that removes inherited clean
  `NoRouteFlowEvidence` gaps and avoids adding a new one whenever reduced
  coverage, schema/extractor/unknown, projection, attachment, or endpoint
  composition gaps block a clean absence conclusion.
- Preserved the existing full-coverage clean no-evidence fixture and avoided
  broad path-reporter behavior changes.
- Added a synthetic regression with a failed-build source, selected route root,
  no downstream call rows, and a required terminal surface. The report now
  emits the reduced-coverage gap, downgrades to `UnknownAnalysisGap`, and does
  not emit `NoRouteFlowEvidence` or `MissingCallEdge`.

### Validation Log For PR 3

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
  passed, 37 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
  errors.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 640 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- PR #325 opened against `dev` from
  `codex/route-flow-service-data-stitching`.
- Initial ACK PR loop returned `actionable_findings` with two unresolved
  review threads and `patchAuthorized=true`.
- ACK-authorized findings patched:
  - removed a redundant `DataSurfaceAttachmentMissing` branch from the clean
    no-evidence blocker predicate because it is already covered by endpoint
    composition blocker handling;
  - treated `TruncatedByLimit` gaps as blockers for clean
    `NoRouteFlowEvidence` and updated the truncation-adjacent fixture to assert
    the safer unknown conclusion.
- Focused validation after the ACK patch:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`
  passed, 37 tests.
- Post-ACK `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and
  0 errors.
- First post-ACK `dotnet test src/dotnet/TraceMap.sln` had one known
  intermittent diagnostic artifact assertion miss for `NuGetRestoreFailed`.
  The single diagnostic test passed in isolation, and the full solution rerun
  passed, 640 tests.
- Post-ACK `./scripts/check-private-paths.sh`: passed.
- Post-ACK `git diff --check`: passed.
- After the ACK patch, the live PR diff showed an unrelated site
  troubleshooting commit on this branch. A normal revert commit removed that
  unrelated site slice from the net PR diff; the PR diff is back to this spec
  folder plus route-flow reporting/tests only.
- Final post-cleanup validation:
  - `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
    errors.
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
    passed, 37 tests.
  - `dotnet test src/dotnet/TraceMap.sln`: passed, 640 tests.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.
- ACK rerun after cleanup showed the two review threads resolved but one Qodo
  top-level actionable finding remained: inherited clean no-evidence gaps were
  cleaned before projection gaps were appended.
- Patched the Qodo finding by rerunning clean no-evidence gap cleanup after
  projection gaps are appended, and added projection-gap coverage asserting
  `NoRouteFlowEvidence` is absent when projection blockers exist.
- Post-Qodo validation:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
    passed, 37 tests.
  - `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
    errors.
  - `dotnet test src/dotnet/TraceMap.sln`: passed, 640 tests.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.
- Follow-up push completed at
  `bb881c95f5146b86af411e69e309c1f2ca6bd5a9`.
- ACK rerun returned `merge_ready`, stop reason `NONE`, next action
  `merge_ready`; Qodo's remaining finding was patched and dispositioned, Codex
  stale review remained residual medium risk under the configured trusted
  review quorum policy.
- After PR #326 merged the spec reconciliation branch, PR #325 became dirty.
  The branch merged `origin/dev`, preserved the completed no-direct-call
  reduced/projection-blocker checklist items, and kept the broader
  direct/no-call/reduced-coverage/deterministic-ordering remainder unchecked for
  a future route-flow slice.

### Oddities / Design Decisions For PR 3

- A legacy full-coverage no-downstream fixture inherits a path-reporter
  `TruncatedByLimit` gap with coverage-relative evidence. This PR leaves that
  behavior unchanged and only suppresses clean absence when reduced/unknown or
  endpoint-composition blockers are present.

### Follow-Ups For PR 3

- Remaining Task 5 backlog after this slice: MissingCallEdge-specific
  full-coverage dead-end coverage and deterministic ordering coverage.
- Broader Task 6/7 candidate and attachment precision work remains deferred.

## Product Implementation PR 4

Branch: `codex/task6-route-flow-candidates`
Base: `origin/dev` at `80afdc3bd4b9`

Selected slice: Task 6 implementation-candidate continuation audit plus the
smallest downgrade-hardening patch after PR #328 closed Task 5. This slice
does not start UI property lineage, site work, attachment precision, scanner
extraction, or broader Task 7/8/9/10 work.

### Live Audit Notes

- `CombinedRouteFlowReport` already consumes source-local
  `combined_symbol_relationships` through the combined dependency graph and
  emits `interface-implementation-candidate` rows from implementation
  relationships.
- Existing focused route-flow coverage already proves single compiler-backed
  implementation candidates continue to downstream surfaces only as
  `NeedsReviewStaticRouteFlow`; candidate paths and summaries do not upgrade to
  `StrongStaticRouteFlow` or `ProbableStaticRouteFlow`.
- Existing tests cover no-candidate `ImplementationCandidateUnavailable`,
  multiple-candidate ambiguity, high fan-out capped candidate traversal,
  syntax/name-only candidate caps, Tier3 downstream caps, and reduced-coverage
  clean-absence suppression.
- The remaining broad Task 6 matrix item stays open because cross-source and
  cross-language candidate fixtures need a dedicated follow-up. The live graph
  builder source-scopes symbol relationships, so this PR avoids corrupting
  combined-index source identity merely to manufacture that fixture.

### Implementation Notes

- Route-flow now separates all implementation-candidate relationship edges from
  source-compatible candidates. If the graph contains incompatible
  cross-source/cross-language implementation candidate evidence for the
  selected interface node, it emits a deterministic `RuntimeBindingNotProven`
  gap and does not traverse that candidate.
- Ambiguous implementation-candidate gap IDs now use incremental SHA-256 over
  deterministic candidate/supporting-evidence sequences instead of joining the
  full candidate set into one allocation.
- Added a focused regression proving runtime-adjacent facts such as dependency
  registration, service-locator-style resolution evidence, reflection target
  evidence, and dynamic dispatch candidate evidence do not become route-flow
  implementation candidates, terminal dependency surfaces, stronger summary
  classifications, or runtime-proof wording.

### Validation Log For PR 4

- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
  errors.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
  passed, 40 tests before the ACK patch and 41 tests after the ACK patch.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 643 tests before the ACK
  patch and 644 tests after the ACK patch.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Initial ACK PR loop for PR #330 returned `actionable_findings` with two
  unresolved review threads and `patchAuthorized=true`.
- ACK-authorized findings patched:
  - partitioned implementation candidate edges in a single pass to avoid
    redundant LINQ allocations and repeated node lookups;
  - populated `RuntimeBindingNotProven` gaps with commit SHA, extractor name,
    and extractor version metadata;
  - suppressed contradictory `MissingImplementationBridge` and
    `ImplementationCandidateUnavailable` gaps when incompatible candidate
    evidence exists and `RuntimeBindingNotProven` explains the blocked bridge;
  - added focused regression coverage for runtime-binding gap metadata.
- Post-ACK patch validation:
  - `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
    errors.
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter CombinedRouteFlowTests`:
    passed, 41 tests.
  - `dotnet test src/dotnet/TraceMap.sln`: passed, 644 tests.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.

### Oddities / Design Decisions For PR 4

- The `RuntimeBindingNotProven` gap path is intentionally conservative: it
  labels incompatible candidate evidence as reduced coverage and static review
  context only. It does not infer runtime dependency-injection targets,
  service-locator targets, factory outputs, reflection targets, or dynamic
  dispatch targets.
- No new rule ID or gap code was added; the existing
  `combined.route-flow.gap.v1` and `combined.route-flow.interface-bridge.v1`
  limitations already document this boundary.

### Follow-Ups For PR 4

- Add dedicated, non-corrupt cross-source/cross-language route-flow candidate
  fixtures if the combined graph gains an explicit persisted bridge contract
  that can represent those relationships without violating source-local symbol
  identity.
- Task 6 deterministic gap matrix is closed by the follow-up slice below.

## Product Implementation PR 5

Branch: `codex/route-flow-task6-gap-matrix`
Base: `origin/dev` at `086ad376e387ea8d87e430175ef2673cbc74c0f1`

Selected slice: finish the remaining Task 6 implementation-candidate
continuation/downgrade matrix after PR #330 and static dispatch builder PR
#331. This slice does not start Task 7, site work, scanner extraction, runtime
binding proof, or attached dependency/data precision.

### Live Audit Notes

- PR #330 already closed source-local candidate traversal, single-candidate
  review-tier continuation, multiple/no-candidate gaps, runtime-binding
  non-proof gaps, syntax/name-only caps, reduced-coverage clean-absence
  suppression, and runtime-adjacent non-proof coverage.
- PR #331 added `StaticDispatchCandidateBuilder` for deterministic static
  dispatch candidate derivation and fan-out gaps.
- Remaining Task 6 work on this audited base was to let route-flow reuse the
  shared builder where safe and to preserve a route-flow-native deterministic
  `DispatchCandidateFanOut` gap for high fan-out candidate sets.

### Implementation Notes

- Route-flow now derives implementation candidates through
  `StaticDispatchCandidateBuilder` and maps builder output back to existing
  route-flow row/gap vocabulary.
- Candidate flow rows remain `interface-implementation-candidate` rows with
  `combined.route-flow.interface-bridge.v1` evidence and
  `NeedsReviewStaticRouteFlow` classification.
- Builder fan-out gaps are translated into `DispatchCandidateFanOut` route-flow
  gaps under `combined.route-flow.gap.v1`, `Tier4Unknown`, and
  `ReducedCoverage`; the gap documents deterministic capping and runtime
  non-proof limitations.
- Runtime-binding gaps now consume the builder candidate edge shape. This keeps
  candidate bridges as static review context only and does not claim runtime DI
  target selection, service locator behavior, factory output, reflection target
  execution, or dynamic dispatch.
- `rules/rule-catalog.yml` now documents `DispatchCandidateFanOut` under
  `combined.route-flow.interface-bridge.v1` limitations.

### Validation Log For PR 5

- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
  errors.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed, 41 tests.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedDependencyPathTests`:
  passed, 27 tests.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 646 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- ACK rerun on PR #332 returned `actionable_findings` with four unresolved
  review threads and `patchAuthorized=true`.
- ACK-authorized findings patched:
  - added a null/whitespace guard around static-dispatch extractor-version
    lookup;
  - changed route-flow static-dispatch gap extractor metadata from evidence
    scope to extractor name;
  - normalized inherited path `DispatchCandidateFanOut` gaps as route-flow
    fan-out gaps and removed duplicate endpoint-composition fan-out gaps when
    the inherited path gap already exists for the same candidate node;
  - preserved fan-out span/commit/extractor metadata only for the reviewed
    dispatch fan-out path to avoid unrelated touched-file duplication.
- Post-ACK patch validation:
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
    passed, 41 tests.
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedDependencyPathTests`:
    passed, 27 tests.
  - `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
    errors.
  - `dotnet test src/dotnet/TraceMap.sln`: passed, 646 tests.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.

### Oddities / Design Decisions For PR 5

- The shared builder emits `combined.dispatch-candidate.v1` and
  `combined.dispatch-gap.v1` for paths. Route-flow intentionally keeps its
  public row/gap contract under `combined.route-flow.*` and uses builder output
  as an internal deterministic derivation source.
- High fan-out is review-tier, not a hard clean-absence blocker. It caps
  candidate-dependent rows and summaries without implying a runtime target was
  selected or omitted.

### Follow-Ups For PR 5

- Task 7 remains the next implementation task: attached versus unjoinable
  service/data/query/dependency/value-origin precision. Do not start it from
  this Task 6 branch.

## Product Implementation PR 6

Branch: `codex/route-flow-task7-attachment-precision-20260625163442`
Base: `origin/dev` at `9bb459587475864cab9c484b29ab2360369c0aa3`

Selected slice: first Task 7 attachment-precision slice for fact-symbol
projection. This PR keeps the public `route-flow` report type and JSON
version `1.0`; it does not start broad scanner extraction, site work, runtime
DI proof, runtime endpoint reachability, AI/LLM analysis, or Task 8/9/10
hardening beyond tests directly touched by the projection behavior.

### Live Audit Notes For PR 6

- `CombinedRouteFlowReport` already emits selected route-flow rows,
  dependency surfaces, path-context logic rows, argument projection rows,
  fact-symbol projection rows, `DataSurfaceAttachmentMissing`,
  `ArgumentProjectionUnavailable`, `FactSymbolProjectionUnavailable`,
  context groups, touched files/symbols, and rule-catalog resolution tests.
- The remaining Task 7 gap selected for this small PR was fact-symbol
  attachment precision: target-role or same-source symbol adjacency could be
  read as projection context unless route-flow required the fact-symbol row to
  attach through a selected source-local symbol identity.
- Argument-flow projection and parameter-forward rows were audited as already
  requiring selected route-flow pair/path evidence on this base, but the larger
  parameter batching/cap follow-up remains open.

### Implementation Notes For PR 6

- Fact-symbol projection now attaches only through `combined_fact_symbols`
  rows with `role = source` that match selected source-local route-flow symbol
  identities. Target-role adjacency is not rendered as route-flow context.
- Fact-symbol projection candidate reads now filter to source-role links, and
  fallback `FactSymbolProjectionUnavailable` probes look for projectable
  source-role fact types in the selected source so unsupported selected
  symbols cannot hide adjacent unjoinable projectable facts.
- Added a synthetic regression with an unrelated query fact that names the
  selected repository as a target-role symbol. Route-flow now emits
  `FactSymbolProjectionUnavailable` and does not render fact-symbol logic rows
  or context groups for that misleading target adjacency.

### Validation Log For PR 6

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_optional_projection_tables_emit_scoped_gaps_when_rows_cannot_join_selected_path|FullyQualifiedName~Route_flow_fact_symbol_projection_requires_selected_source_symbol_identity"`:
  passed, 2 tests, with the existing `SQLitePCLRaw.lib.e_sqlite3` NuGet
  vulnerability warnings.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed, 42 tests, with the same existing NuGet warnings.
- `dotnet test src/dotnet/TraceMap.sln`: passed, 650 tests, with the same
  existing NuGet warnings.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `./scripts/demo-public.sh /tmp/tracemap-route-flow-task7-demo-20260625`:
  passed. The sample-only public demo produced route-flow/reporting artifacts
  under the temporary output root and retained reduced coverage where the
  checked-in samples intentionally have partial evidence.
- Diff-scope review before commit showed only route-flow reporting code,
  focused route-flow tests, and this spec's task/state files.
- Initial ACK PR loop for PR #335 returned `actionable_findings` with two
  unresolved review threads and `patchAuthorized=true`.
- ACK-authorized findings patched:
  - restored combined-symbol-ID matching for source-role fact-symbol
    projection by adding deterministic combined symbol keys to the selected
    path model and checking `row.CombinedSymbolId` after the `role = source`
    guard;
  - enriched `FactSymbolProjectionUnavailable` fallback gaps with source
    label, file span, commit SHA, extractor name, and extractor version
    metadata from the unprojected fact evidence.
- Post-ACK focused projection validation passed:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_optional_projection_tables_emit_scoped_gaps_when_rows_cannot_join_selected_path|FullyQualifiedName~Route_flow_fact_symbol_projection_requires_selected_source_symbol_identity"`.
- Post-ACK focused route-flow validation passed:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`
  with 42 tests.
- Post-ACK `dotnet test src/dotnet/TraceMap.sln` passed with 650 tests.
- Post-ACK `git diff --check` passed.
- Post-ACK `./scripts/check-private-paths.sh` passed.
- Post-ACK `./scripts/demo-public.sh /tmp/tracemap-route-flow-task7-demo-postack-20260625`
  passed, with generated outputs kept under the temporary output root.

### Follow-Ups For PR 6

- Complete the rest of Task 7's broad attachment matrix for repository,
  object/projection, SQL/query, legacy data, package/config, HTTP client, WCF,
  ASMX/SOAP, remoting, event/message, storage, validation/guard,
  serializer/contract, async/callback, and flow-boundary facts.
- Add the larger attached versus path-context labeling and deterministic stable
  ID matrix across all service/data/dependency surface families.
- Cap or batch large argument-pair and fact-symbol projection SQL filters so
  very large selected route-flow graphs cannot exceed SQLite parameter limits.

## Product Implementation PR 7

Branch: `codex/route-flow-task7-attachments`
Base: `origin/dev` at `9bb459587475`

Selected slice: Task 7 event/message terminal-surface attachment precision
after Task 5, Task 6, and the fact-symbol attachment slice. This slice does not
start UI property lineage, site work, scanner extraction, runtime execution
claims, or the full remaining service/data/query/dependency taxonomy.

### Live Audit Notes For PR 7

- Current `dev` already contains selected-path-only route-flow traversal,
  dependency surface rendering from selected `routePaths`, argument-flow and
  fact-symbol projection joins through selected route-flow rows, schema gaps,
  `ArgumentProjectionUnavailable`, `FactSymbolProjectionUnavailable`,
  `FactSymbolUnsupportedTypeSkipped`, and `DataSurfaceAttachmentMissing`.
- `CombinedDependencyPathReporter` already supports message terminal surface
  kinds (`message-queue`, `message-topic`, `message-subscription`,
  `message-exchange`, `message-stream`, `message-event`, `message-channel`,
  and `message-unknown`) as selected static path terminals.
- `CombinedRouteFlowReport` still rejected those message surface selectors,
  which meant route-flow could not request already-supported event/message
  terminals through `--to-surface`.

### Implementation Notes For PR 7

- Added the existing message terminal surface kinds to route-flow's
  `--to-surface` allow-list and validation message.
- No new traversal rule, report type, JSON version, rule ID, scanner
  extractor, or graph edge kind was added.
- Added focused synthetic route-flow coverage proving:
  - selected message terminal surfaces render as dependency surfaces only when
    joined through the selected route-flow static path;
  - adjacent same-source message surface evidence for an unrelated publisher is
    not inferred as a selected terminal;
  - unjoined adjacent message surface evidence preserves
    `DataSurfaceAttachmentMissing` instead of a clean no-evidence conclusion;
  - dependency surface and gap IDs are deterministic across repeated renders.

### Validation Log For PR 7

- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
  errors.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 43 tests.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 651 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Initial ACK PR loop on PR #334 returned `actionable_findings` with one
  unresolved review thread and `patchAuthorized=true`.
- ACK-authorized finding patched:
  - added a reporting-internal terminal surface kind contract shared by
    `CombinedDependencyPathReporter` and `CombinedRouteFlowReporter`;
  - derived both `paths --to-surface` and `route-flow --to-surface`
    validation messages from the shared surface-kind allow-list, so future
    surface additions do not drift between the path engine, route-flow
    selector validation, and error text.
- Post-ACK patch validation:
  - `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
    errors.
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
    passed locally with 43 tests.
  - `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedDependencyPathTests`:
    passed locally with 30 tests.
  - `dotnet test src/dotnet/TraceMap.sln`: passed locally with 651 tests.
  - `./scripts/check-private-paths.sh`: passed.
  - `git diff --check`: passed.

### Oddities / Design Decisions For PR 7

- Message terminal surfaces are rendered in `dependencySurfaces` and selected
  terminal `flowRows`; route-flow does not create extra generic logic rows for
  message terminals because `LogicKind` currently only projects SQL/object-ish
  shapes as path-context logic rows.
- The synthetic fixtures use public-safe route strings, source labels,
  destination keys, and stable hashes only.

### Follow-Ups For PR 7

- Continue Task 7 for the remaining taxonomy items, especially ASMX/SOAP
  route-flow selection if the path reporter gains those terminal surface kinds,
  plus storage, validation/guard, serializer/contract, async/callback, and any
  broader attached-versus-path-context labeling coverage not already proven by
  existing projection tests.
- Keep Task 8/9/10 unchecked except for validation/safety behavior directly
  touched by this event/message sub-slice.

## Product Implementation PR 8

Branch: `codex/task7-attachment-precision`
Base: `origin/dev` at `7ac6e1ac`

Selected slice: continue Task 7 after PR #334 and PR #335 with a small
SQL/query dependency-surface attachment precision hardening slice. This slice
does not start UI property lineage, site work, scanner extraction, runtime
execution claims, or the full remaining service/data/query/dependency taxonomy.

### Live Audit Notes For PR 8

- Current `dev` already contains selected route-flow traversal, dependency
  surface rendering from selected `routePaths`, parameter-forward seed edges,
  argument-flow projection joins through selected route-flow caller/callee
  pairs, source-role fact-symbol projection joins through selected source-local
  symbols, and the projection/attachment gap kinds
  `ArgumentProjectionUnavailable`, `FactSymbolProjectionUnavailable`,
  `FactSymbolUnsupportedTypeSkipped`, and `DataSurfaceAttachmentMissing`.
- PR #334's event/message terminal-surface precision and PR #335's source-role
  fact-symbol precision are present on the audited base.
- SQL/query terminal surfaces are already selected through route-flow path
  evidence. The missing small-slice evidence was focused coverage for selected
  SQL attachment versus adjacent same-source SQL evidence, path-context
  labeling, and deterministic dependency surface/gap IDs.

### Implementation Notes For PR 8

- Added focused `CombinedRouteFlowTests` coverage proving selected SQL/query
  surfaces render as dependency surfaces only when joined through the selected
  static route-flow path.
- Added adjacent same-source SQL/query evidence coverage proving route-flow
  does not infer an unjoined SQL surface and preserves
  `DataSurfaceAttachmentMissing` instead of a clean no-evidence conclusion.
- Added deterministic repeated-render assertions for selected SQL surface IDs,
  stable keys, and unjoinable gap IDs.
- Added path-context labeling assertions for the selected SQL logic row and its
  attachment to the selected terminal `flowRows` row.
- Extended the synthetic `QueryPatternFact` test helper with optional table,
  column, and shape values so fixtures can distinguish selected and adjacent
  public-safe SQL surfaces without raw SQL/config values.

### Validation Log For PR 8

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_selected_sql_surface_with_path_context_and_stable_ids|FullyQualifiedName~Route_flow_does_not_infer_adjacent_sql_surface_without_selected_join"`:
  passed locally with 2 tests.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 46 tests.
- `dotnet build src/dotnet/TraceMap.sln`: passed with 0 warnings and 0
  errors.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 654 tests.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

### Oddities / Design Decisions For PR 8

- The unjoinable SQL fixture's `DataSurfaceAttachmentMissing` gap currently
  carries rule, source label, deterministic gap ID, and route-root file span,
  but no commit SHA or supporting fact IDs on this traversal path. The test
  asserts the stable rule-backed evidence available on `dev` and leaves broader
  gap metadata enrichment out of this conservative slice.
- The adjacent SQL fixture uses public-safe synthetic table/shape labels only;
  no raw SQL, config values, secrets, private paths, remotes, or source snippets
  are rendered.

### Follow-Ups For PR 8

- Continue Task 7 for the remaining taxonomy items: service/repository rows
  beyond existing selected path rows, object/projection variants, legacy data,
  package/config, HTTP client, WCF, ASMX/SOAP, remoting, storage,
  validation/guard, serializer/contract, async/callback, flow-boundary breadth,
  and any broader attached-versus-path-context matrix not already covered.
- Consider enriching `DataSurfaceAttachmentMissing` gaps with commit SHA and
  supporting fact IDs where traversal paths have that evidence available, but
  keep the gap conservative when the join is unavailable.
- Keep the large argument-pair and fact-symbol projection SQL batching follow-up
  open so large selected route-flow graphs cannot exceed SQLite parameter
  limits.

## Product Implementation PR 9

Branch: `codex/route-flow-task7-value-origin-precision-20260625173937`
Base: `origin/dev` at `7ac6e1ac883998a7c09c87afc416f0c76be225f6`
Merge refresh: merged `origin/dev` at
`7dc99a8564ac3a46effce7492a5e21a1b243837a` to preserve the SQL/query
attachment precision slice from Product Implementation PR 8.

Selected slice: Task 7 argument-flow / parameter-forward value-origin
attachment precision after PR #335 and current `origin/dev`. This slice
preserves `route-flow`, JSON version `1.0`, and the existing
`combined.route-flow.*` rule namespace. It does not add runtime proof,
endpoint reachability, DI binding, production traffic, release-safety wording,
AI/LLM analysis, vector search, or scanner extraction changes.

### Live Audit Notes For PR 9

- Current `origin/dev` already includes Task 5, Task 6, fact-symbol
  source-role precision from PR #335, event/message terminal-surface
  attachment precision from PR #334, and the SQL/query attachment precision
  slice from Product Implementation PR 8.
- `CombinedRouteFlowReport` already renders selected parameter-forward
  value-origin rows through selected route-flow paths and already joins
  argument-flow projection rows through selected caller/callee route-flow row
  pairs.
- The remaining value-origin gap was narrower: when one selected argument-flow
  projection attached successfully, adjacent same-source argument-flow evidence
  that could not join a selected static route-flow pair was silently omitted
  instead of preserving `ArgumentProjectionUnavailable`.

### Implementation Notes For PR 9

- Argument projection gap evidence now reads unjoined same-source
  `combined_argument_flows` rows by excluding the selected route-flow
  caller/callee pair set.
- `ArgumentProjectionUnavailable` is emitted for adjacent unjoined
  argument-flow evidence even when other argument-flow projection rows attach
  successfully.
- The argument projection gap now carries source label, file span, commit SHA,
  extractor name, and extractor version metadata from the unjoined evidence,
  matching the fact-symbol projection gap evidence shape.
- Added focused synthetic coverage proving selected argument-flow projection
  rows attach to selected static route-flow rows, selected parameter-forward
  value-origin rows remain route-flow path context, adjacent unjoined
  argument-flow evidence does not render as flow/logic rows, and repeated
  row/gap IDs are deterministic.

### Validation Log For PR 9

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_value_origin_rows_only_from_selected_static_path"`:
  passed locally with 1 test before the merge refresh. NuGet emitted the
  existing `SQLitePCLRaw.lib.e_sqlite3` high-severity vulnerability warning.
- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`:
  passed locally with 45 tests before the merge refresh, with the same existing
  NuGet warning.
- `git diff --check`: passed before the merge refresh.
- `./scripts/check-private-paths.sh`: passed before the merge refresh.
- `dotnet test src/dotnet/TraceMap.sln`: passed locally with 653 tests before
  the merge refresh, with the same existing NuGet warning.
- Post-merge refresh validation:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`
  passed locally with 47 tests, `git diff --check` passed,
  `./scripts/check-private-paths.sh` passed, and
  `dotnet test src/dotnet/TraceMap.sln` passed locally with 655 tests. NuGet
  emitted the same existing `SQLitePCLRaw.lib.e_sqlite3` warning.
- ACK-authorized patch after the merge refresh replaced the SQL
  `not (caller/callee pair clauses)` gap-exclusion filter with deterministic
  in-memory selected-pair exclusion. This avoids large exclusion predicates and
  preserves null-caller/null-callee unjoined argument-flow rows as
  `ArgumentProjectionUnavailable` evidence.
- Post-ACK patch validation:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_value_origin_rows_only_from_selected_static_path"`
  passed locally with 1 test,
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`
  passed locally with 47 tests, `git diff --check` passed,
  `./scripts/check-private-paths.sh` passed, and
  `dotnet test src/dotnet/TraceMap.sln` passed locally with 655 tests. NuGet
  emitted the same existing `SQLitePCLRaw.lib.e_sqlite3` warning.
- Second ACK-authorized patch made the unjoined argument-flow gap evidence
  query fully static SQL and moved selected source filtering into the same
  deterministic in-memory pass as selected-pair exclusion.
- Post-second-ACK patch validation:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_value_origin_rows_only_from_selected_static_path"`
  passed locally with 1 test,
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`
  passed locally with 47 tests, `git diff --check` passed,
  `./scripts/check-private-paths.sh` passed, and
  `dotnet test src/dotnet/TraceMap.sln` passed locally with 655 tests. NuGet
  emitted the same existing `SQLitePCLRaw.lib.e_sqlite3` warning.
- Third ACK-authorized patch groups unjoined argument-flow projection gap
  evidence by source label, file span, commit SHA, rule ID, and extractor
  version before emitting `ArgumentProjectionUnavailable`, so each gap's
  metadata matches its supporting evidence location.
- Post-third-ACK patch validation:
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "FullyQualifiedName~Route_flow_attaches_value_origin_rows_only_from_selected_static_path"`
  passed locally with 1 test,
  `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~CombinedRouteFlowTests`
  passed locally with 47 tests, `git diff --check` passed,
  `./scripts/check-private-paths.sh` passed, and
  `dotnet test src/dotnet/TraceMap.sln` passed locally with 655 tests. NuGet
  emitted the same existing `SQLitePCLRaw.lib.e_sqlite3` warning.

### Follow-Ups For PR 9

- Complete the broader Task 7 taxonomy for storage, validation/guard,
  serializer/contract, async/callback, flow-boundary, ASMX/SOAP if supported by
  path terminals, and remaining attached-versus-path-context labeling coverage.
- Keep the large argument-pair and fact-symbol projection SQL batching/cap
  follow-up open for a dedicated performance/scale slice.
- Keep Task 8/9/10 unchecked except for the validation and safety behavior
  directly touched by this value-origin sub-slice.
