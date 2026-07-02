# TraceMap Validation Guide

This guide defines the repeatable checks used to validate language adapters and cross-index analysis. It complements `docs/ACCEPTANCE.md`: acceptance defines expected behavior, while this file describes the concrete sample and open-source smoke set.

TraceMap validation must stay deterministic and evidence-backed. Do not add LLM calls, embeddings, or prompt-based classification to validation.

## Required Matrix

Every language adapter should have:

| Check | Purpose |
| --- | --- |
| local modern sample | proves full semantic path when compiler/project loading works |
| local broken sample | proves syntax fallback and reduced coverage labels |
| reducer fixture | proves contract delta matching through shared facts/index schema |
| SQLite relationship queries | proves `call_edges`, `object_creations`, `argument_flows`, symbols, and relationship tables are populated when facts exist |
| value-origin flow queries | proves direct parameter forwarding, bounded local aliases, and unique constructor field origins are represented without crossing ambiguous boundaries |
| integration facts | proves HTTP/API, config, SQL/DB, serializer, and package/dependency facts where supported |
| combine/report/paths/reverse/export smoke | proves shared schema compatibility, combined dependency reporting, static dependency path queries, and reverse dependency-surface queries across adapters |
| public OSS smoke | proves larger real-world repos complete without unchecked assumptions |
| private-path guard | proves generated docs/scripts do not leak developer-local paths |

## Required Local Commands

Run these before opening or updating a PR that changes scanner behavior:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
npm run check --prefix src/typescript
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test
python3 -m venv /tmp/tracemap-python-venv
/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"
/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests
PYTHON_BIN=/tmp/tracemap-python-venv/bin/python ./scripts/smoke-python-endpoints.sh
./scripts/check-private-paths.sh
```

## Public Demo Workflow

Run the public demo when validating the open-source walkthrough or generated public artifacts:

```bash
./scripts/demo-public.sh
./scripts/demo-public.sh .tracemap-demo
```

The default demo uses only checked-in samples. It does not clone public repositories, read private repositories, call external analysis services, query package registries, or run vulnerability/license/compatibility analysis. First-run build restore may still need network access for local toolchains such as NuGet or npm.

Current default behavior:

- checks `git`, `.NET`, `node`, and `npm`
- builds the .NET solution and TypeScript adapter
- scans `samples/modern-sample`
- scans `samples/endpoint-server-aspnet`
- scans `samples/typescript-modern-sample`
- scans `samples/endpoint-client-angular`
- scans `samples/public-demo/before`
- scans `samples/public-demo/after`
- combines the endpoint stack with labels `public-ts-client` and `public-dotnet-server`
- combines a mixed stack with labels `public-dotnet-modern`, `public-dotnet-server`, `public-ts-modern`, and `public-ts-client`
- combines before/after public-demo snapshots with label `public-demo-api`
- runs the combined dependency report and asserts endpoint evidence from the combined report
- runs targeted `tracemap paths`, `tracemap route-flow`, and `tracemap reverse` over the generated endpoint stack
- generates `portfolio-manifest.json` from generated combined indexes and runs `tracemap portfolio`
- runs `tracemap diff`, `tracemap impact`, and `tracemap release-review` over the generated public-demo before/after snapshots
- writes `demo-summary.md` and `demo-summary.json`
- runs a generated-output sentinel scan over public-shareable summaries and reports
- marks Python as `not_requested` unless `--include-python` is passed; requested Python scanning is currently `deferred` to a follow-up slice
- marks JVM as `unavailable` when Java 21 is absent

The release-review section is available as a deterministic static evidence packet over the public-demo before/after snapshots. Contract-delta, SQL/schema, package compatibility, path context, and reverse context sections remain not requested, unavailable, or deferred inside the release-review report unless compatible inputs are explicitly supplied.

Troubleshooting:

- If the demo refuses an in-repo output directory, use `.tracemap-demo/` or add a generic ignored output path before running the script.
- If .NET or TypeScript build restore fails, run the build/test commands above directly to restore local toolchain dependencies and inspect their native diagnostics.
- Reduced sample scan and report coverage is expected for samples that intentionally rely on syntax fallback or missing framework packages. The summary labels those sections as partial while preserving rule-backed evidence counts.
- If endpoint, path, reverse, or portfolio assertions fail, inspect the generated JSON reports under `reports/`; accepted evidence rows must include rule IDs, evidence tiers, source labels, commit SHAs, and supporting fact or edge IDs where the report exposes them.
- If the generated public-report sentinel fails, inspect the relative file paths and category it prints. Keep scan manifests, SQLite files, facts, and logs local-only; public summaries and reports must use hashes, labels, or relative paths.

Generated outputs under `scans/**`, SQLite files, facts, manifests, and logs are local-only artifacts and may contain temporary execution details. Public-shareable `demo-summary.*` and `reports/**/*.md|json` artifacts must not contain raw scripts, SQL, snippets, config values, connection strings, raw URLs with credentials, private paths, or local absolute paths.

Use `.tracemap-demo/` for an in-repo output root; it is ignored by git. Other in-repo output directories are rejected unless `git check-ignore` proves they are ignored.

For JVM CLI smoke, also run:

```bash
JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm installDist
```

For Swift adapter changes, run the Swift package and checked-in sample scans.
The Swift package currently resolves a pinned SwiftSyntax dependency for
source declaration/call extraction, so first-run validation may need network
access for SwiftPM dependency restore:

```bash
swift build --package-path src/swift
swift run --package-path src/swift tracemap-swift-smoke-tests
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-package-basic --out /tmp/tracemap-swift-package-basic
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-dependency-surfaces --out /tmp/tracemap-swift-dependency-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-http-api-client-surfaces --out /tmp/tracemap-swift-http-api-client-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-ui-surfaces --out /tmp/tracemap-swift-ui-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-storage-data-surfaces --out /tmp/tracemap-swift-storage-data-surfaces
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-diagnostics-reduced --out /tmp/tracemap-swift-diagnostics-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-reduced --out /tmp/tracemap-swift-metadata-reduced
swift run --package-path src/swift tracemap-swift scan --repo samples/swift-metadata-unsupported --out /tmp/tracemap-swift-metadata-unsupported
swift run --package-path src/swift tracemap-swift scan --repo samples/no-swift --out /tmp/tracemap-no-swift
test -f /tmp/tracemap-swift-package-basic/scan-manifest.json
test -f /tmp/tracemap-swift-package-basic/facts.ndjson
test -f /tmp/tracemap-swift-package-basic/index.sqlite
test -f /tmp/tracemap-swift-package-basic/report.md
test -f /tmp/tracemap-swift-package-basic/logs/analyzer.log
test -f /tmp/tracemap-swift-dependency-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-http-api-client-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-http-api-client-surfaces/index.sqlite
test -f /tmp/tracemap-swift-http-api-client-surfaces/report.md
test -f /tmp/tracemap-swift-ui-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-ui-surfaces/index.sqlite
test -f /tmp/tracemap-swift-ui-surfaces/report.md
test -f /tmp/tracemap-swift-storage-data-surfaces/scan-manifest.json
test -f /tmp/tracemap-swift-storage-data-surfaces/facts.ndjson
test -f /tmp/tracemap-swift-storage-data-surfaces/index.sqlite
test -f /tmp/tracemap-swift-storage-data-surfaces/report.md
test -f /tmp/tracemap-swift-storage-data-surfaces/logs/analyzer.log
test -f /tmp/tracemap-swift-diagnostics-reduced/scan-manifest.json
test -f /tmp/tracemap-swift-diagnostics-reduced/facts.ndjson
test -f /tmp/tracemap-swift-diagnostics-reduced/index.sqlite
test -f /tmp/tracemap-swift-diagnostics-reduced/report.md
test -f /tmp/tracemap-swift-diagnostics-reduced/logs/analyzer.log
test -f /tmp/tracemap-swift-metadata-reduced/scan-manifest.json
test -f /tmp/tracemap-swift-metadata-unsupported/scan-manifest.json
test -f /tmp/tracemap-no-swift/scan-manifest.json
dotnet run --project src/dotnet/TraceMap.Cli -- export --index /tmp/tracemap-swift-package-basic/index.sqlite --out /tmp/tracemap-swift-export --format json
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-package-basic/index.sqlite --label swift --out /tmp/tracemap-swift-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-combined.sqlite --out /tmp/tracemap-swift-report
dotnet run --project src/dotnet/TraceMap.Cli -- combine --index /tmp/tracemap-swift-storage-data-surfaces/index.sqlite --label swift --out /tmp/tracemap-swift-storage-combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index /tmp/tracemap-swift-storage-combined.sqlite --out /tmp/tracemap-swift-storage-report
./scripts/check-private-paths.sh
git diff --check
```

Expected Swift behavior: scans remain deterministic static evidence over
checked-in files, emit repo and commit SHA provenance, rule IDs, evidence
tiers, extractor versions, coverage labels, public-safe repo-relative paths,
and syntax-backed declarations, call candidates, construction candidates,
direct source-local symbol relationships, HTTP/API client surfaces, UI
surfaces, and storage/data surfaces where supported. Swift storage/data
evidence remains static metadata or syntax/text evidence and must not claim
runtime persistence, query execution, stored values, schema existence,
migration success, Keychain item presence, Realm live schema, production data,
or impact. Swift relationship facts remain syntax-backed and must not claim compiler semantic coverage, build
success, package compatibility, Xcode scheme behavior, simulator/device
behavior, runtime behavior, protocol witness selection, Objective-C dispatch,
dependency vulnerability/license/freshness, or impact. Generated Swift
artifacts must not contain raw source snippets, manifest snippets, plist
values, raw URLs, hostnames, local absolute paths, raw remotes, credentials,
secrets, or private labels.

### Swift Real-World API-Client Smoke

For Swift adapter changes that affect project inventory, dependency metadata,
HTTP/API client surfaces, UI surfaces, storage/data surfaces, reduced coverage,
or public Swift demo evidence, run the opt-in real-world Swift smoke:

```bash
scripts/smoke-swift-real-world.sh /tmp/tracemap-swift-real-world-cache /tmp/tracemap-swift-real-world-smoke
```

The smoke clones pinned public repositories into the cache directory, scans
them with `tracemap-swift`, verifies required artifacts, and writes sanitized
local summaries under the output directory. Generated summaries use public
repository slugs, pinned commit SHAs, artifact labels, counts, rule IDs,
coverage labels, and limitations. They must not include local absolute paths,
clone URLs, raw remotes, raw source snippets, raw SQL, credentials, config
values, hostnames, private labels, or runtime observations.

Pinned Swift real-world samples:

| Label | Repository | Pinned SHA | Why included |
| --- | --- | --- | --- |
| `icecubesapp` | `Dimillian/IceCubesApp` | `9c05a720597b3ff13de2e241bf58d3fba0863c09` | SwiftUI Mastodon client with real federated API client and UI surface evidence |
| `mastodon-ios` | `mastodon/mastodon-ios` | `95ac4a6d726ebf9fa867036dbf9d72f0a4b5f534` | Official Mastodon iOS app with real backend/API client and mobile app structure evidence |
| `kickstarter-ios` | `kickstarter/ios-oss` | `203971bdf40f3a3a5071ce0c1fbc4eb3cad5b094` | Product iOS app with real backend/API client, view model, dependency, and persistence-adjacent evidence |

Use `TRACEMAP_SWIFT_REAL_WORLD_REPOS=icecubesapp` or a comma-separated label
list for a focused smoke while developing the harness. Use
`TRACEMAP_SKIP_BUILD=1` only after `swift build --package-path src/swift` has
already succeeded for the current checkout. Use
`TRACEMAP_SWIFT_REAL_WORLD_OFFLINE=1` only when the cache already contains the
pinned commits; offline mode rejects missing commits instead of fetching from
GitHub. Unknown focused labels fail the smoke before scanning, and generated
Markdown summaries include only samples scanned in the current invocation.

Expected Swift real-world behavior: scans complete without Xcode builds,
SwiftPM dependency resolution, simulators, devices, app execution, network
calls, credentials, auth flows, or production telemetry. The smoke proves
artifact generation and static evidence extraction over messy public apps. It
does not prove runtime endpoint reachability, backend compatibility, complete
app navigation, package compatibility, production use, or impact.

For query-pattern report rendering changes, inspect generated scan reports from the affected adapters:

```bash
rg -n "Query Patterns|SQL shape|Query builder|static shape evidence|runtime execution" <scan-output>/report.md
rg -n "fields none" <scan-output>/report.md
```

`fields none` is acceptable for query-builder facts with no extracted field metadata. SQL-shape facts should render derived operation/table/column/source/hash metadata instead, and reports must not render raw SQL text, literal values, unsafe identifiers, or developer-local absolute paths.

For legacy data metadata changes in the .NET adapter, run the focused extractor
tests plus the normal .NET scanner checks:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyDataMetadataExtractorTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Focused validation should cover DBML, EDMX, typed DataSet/TableAdapter,
NHibernate `.hbm.xml`, config provider metadata, generated-code linkage, legacy
data model identity keys, unrelated XSD gating, malformed XML, DTD/entity
rejection, deterministic fact IDs, report redaction, and SQLite property
redaction. Any local legacy smoke
must stay ignored/local-only and use neutral labels/counts only; do not commit
raw facts, SQLite indexes, analyzer
logs, raw SQL, connection strings, config values, raw remotes, private sample
names, local absolute paths, or source snippets.

For legacy data model surface-projection or `surfaceSubtype` reporting changes,
also run focused report/query/export coverage:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter "LegacyDataModelDescriptorProjectionTests|CombinedDependencyReportTests|CombinedDependencyPathTests|CombinedRouteFlowTests|CombinedReverseQueryTests|CombinedDependencyDiffTests|VaultExportTests"
```

For combined dependency report, path-query, route-flow, reverse-query, diff, contract-diff, or snapshot-diff changes, run a combine/report/paths/route-flow/reverse/diff/contract-diff/snapshot-diff smoke over any two existing local scan outputs:
For combined change-impact changes, include the `impact` command in the same smoke.
For release-review changes, include `release-review` in the same smoke and verify `release-review.md` plus `release-review.json` are produced. For review-priority scoring changes, also run release-review with `--include-priority` and verify the Markdown Review Priority section plus JSON `reviewPriority` and `reviewPriorityRows` sidecar fields.

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index <first>/index.sqlite --label first \
  --index <second>/index.sqlite --label second \
  --out <tmp>/combined.sqlite
dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/combined.sqlite --out <tmp>/combined-report
dotnet run --project src/dotnet/TraceMap.Cli -- paths --index <tmp>/combined.sqlite --out <tmp>/combined-paths
dotnet run --project src/dotnet/TraceMap.Cli -- route-flow --index <tmp>/combined.sqlite --from-source first --out <tmp>/route-flow
dotnet run --project src/dotnet/TraceMap.Cli -- property-flow --index <tmp>/combined.sqlite --property fact:<combinedFactId> --out <tmp>/property-flow
dotnet run --project src/dotnet/TraceMap.Cli -- reverse --index <tmp>/combined.sqlite --surface sql-query --to endpoints --out <tmp>/combined-reverse
dotnet run --project src/dotnet/TraceMap.Cli -- diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/combined-diff
dotnet run --project src/dotnet/TraceMap.Cli -- contract-diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/contract-diff
dotnet run --project src/dotnet/TraceMap.Cli -- snapshot-diff --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/snapshot-diff
dotnet run --project src/dotnet/TraceMap.Cli -- impact --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/combined-impact
dotnet run --project src/dotnet/TraceMap.Cli -- release-review --before <tmp>/combined.sqlite --after <tmp>/combined.sqlite --out <tmp>/release-review
test -f <tmp>/combined-report/dependency-report.md
test -f <tmp>/combined-report/dependency-report.json
test -f <tmp>/combined-paths/paths-report.md
test -f <tmp>/combined-paths/paths-report.json
test -f <tmp>/route-flow/route-flow-report.md
test -f <tmp>/route-flow/route-flow-report.json
test -f <tmp>/property-flow/property-flow-report.md
test -f <tmp>/property-flow/property-flow-report.json
test -f <tmp>/combined-reverse/reverse-report.md
test -f <tmp>/combined-reverse/reverse-report.json
test -f <tmp>/combined-diff/diff-report.md
test -f <tmp>/combined-diff/diff-report.json
test -f <tmp>/contract-diff/contract-diff-report.md
test -f <tmp>/contract-diff/contract-diff-report.json
test -f <tmp>/snapshot-diff/snapshot-diff-report.md
test -f <tmp>/snapshot-diff/snapshot-diff-report.json
test -f <tmp>/combined-impact/impact-report.md
test -f <tmp>/combined-impact/impact-report.json
test -f <tmp>/release-review/release-review.md
test -f <tmp>/release-review/release-review.json
```

For docs-export changes, run the focused tests plus the normal .NET and safety
gates:

```bash
dotnet test src/dotnet/TraceMap.sln --filter EvidenceDocs
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

When validating against a local combined index, generate docs into ignored
temporary storage only:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- docs-export --index <tmp>/combined.sqlite --out <tmp>/evidence-docs
test -f <tmp>/evidence-docs/manifest.json
test -f <tmp>/evidence-docs/chunks.jsonl
test -f <tmp>/evidence-docs/README.md
```

Docs-export output must preserve rule IDs, evidence tiers, source labels,
commit SHAs, coverage labels, supporting IDs, gaps, and limitations. It must
not contain raw SQL, raw config values, connection strings, raw URLs, endpoint
addresses, local absolute paths, raw remotes, source snippets, credentials,
private sample names, production data, prompt text, embeddings, vector database
configuration, or natural-language answer templates. Demo/public output
requires reviewed claim metadata plus `--date YYYY-MM`; hidden output without a
date uses `local-only`.

For property-flow changes, run the focused .NET and TypeScript tests plus the
normal report safety gates:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter PropertyFlowTests
npm run check --prefix src/typescript
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Expected behavior: Angular template fixtures emit `UiTemplateBinding`,
`UiFormControlBinding`, `UiEventBinding`, `UiTemplateVariable`, and
`UiBindingGap` facts with rule IDs and safe metadata only; Razor fixtures emit
`RazorBinding`, `RazorFormTarget`, and `RazorBindingGap` facts; property-flow
reports reject single-language indexes, keep input SQLite files read-only, emit
route-flow/schema gaps where needed, and write deterministic Markdown/JSON
without source snippets, raw SQL, raw URLs, connection strings, secrets, remotes,
or local absolute paths. If `--observed-evidence <path>` is used, the observed
rows remain demo metadata only, reject unsafe keys/values, and do not change
static lineage classifications.

For value-origin flow changes, also inspect the source `parameter_forward_edges` table from a semantic .NET sample or focused fixture:

```bash
sqlite3 <out>/index.sqlite "select source_method_symbol, source_parameter_symbol, target_method_symbol, target_parameter_name, rule_id from parameter_forward_edges order by source_method_symbol, target_method_symbol;"
```

Expected behavior: direct parameter forwarding is present, same-method aliases are bounded to 3 hops, and ambiguous constructor/member origins are omitted or represented as gaps by future reporting slices rather than being promoted to forwarding edges.

For callback/lambda/async boundary changes, inspect semantic .NET fixtures for `CallbackBoundary` and `AsyncBoundary` facts under `csharp.semantic.flowboundary.v1`. Expected behavior: direct calls inside callback bodies may still emit normal `ArgumentPassed` rows, captured outer parameters/locals are labeled review-tier boundary evidence, expression-tree lambdas use expression-tree metadata instead of delegate-callback metadata, and event subscriptions, delegate arguments on invocations/object creation, `await`, `await foreach`, `await using`, task scheduling/continuation calls, thread-pool queueing calls, and iterator `yield` are boundaries rather than proof of runtime invocation, ordering, async disposal, async-stream enumeration, or task completion.

For TypeScript/JVM/Python value-origin adapter alignment, inspect adapter fixtures for shared `ArgumentPassed` role properties:

- TypeScript semantic facts should include `argumentSymbolId`, `argumentSymbolLanguage`, `argumentSymbolDisplayName`, `parameterSymbolId`, `parameterSymbolLanguage`, and `parameterSymbolDisplayName` when the compiler resolves both sides.
- Java semantic facts should include parameter role properties for resolved calls and argument role properties only when javac resolves the argument expression to a symbol.
- Python AST facts should mark unresolved callee parameters with `parameterIdentityStatus=unresolvedOrdinalPlaceholder` while still emitting shared role metadata for syntax-visible arguments, local aliases, and `self.field = parameter` aliases.

For changes to `combine`, `report`, `paths`, `reverse`, endpoint extraction, call edges, SQL/query extraction, or dependency-surface projection, run the public combined-path smoke:

```bash
./scripts/smoke-combined-paths.sh
```

The smoke is sample-only and does not clone repositories or read external application paths. It scans `samples/endpoint-client-angular` and `samples/endpoint-server-aspnet`, combines them as `sample-client` and `sample-server`, runs `report`, runs default and targeted `paths` queries, runs a reverse SQL-surface query, and verifies:

- required scan, combined, report, and paths artifacts exist
- the combined report has exactly `sample-client` and `sample-server`
- the sample endpoint `/api/admin/runner/get-by-id/{}` has endpoint alignment evidence; duplicate syntax/semantic server route facts may classify this as review-tier `AmbiguousMatch`
- a targeted path reaches a `sql-query` terminal from the client through an endpoint match, server call edge, source-local symbol reconciliation edge, and surface evidence edge
- `DatabaseColumnMapping` facts, when present, are selectable as `sql-persistence` terminal surfaces rather than `sql-query` terminal surfaces
- path edges and gaps carry rule IDs and evidence tiers
- a reverse SQL-surface query finds endpoint roots and path evidence with rule IDs and evidence tiers
- a bogus endpoint selector returns a valid zero-path report with a rule-backed gap
- repeated targeted `paths` JSON output is byte-stable
- generated Markdown does not render the synthetic SQL sentinel or developer-local absolute paths

The smoke writes generated manifests, logs, SQLite files, and reports under a caller-provided directory or `mktemp -d`. Generated manifests/logs may contain absolute paths to the checked-in samples or temporary output roots; they must not be committed.

For portfolio report changes, run the .NET solution build and test suite plus `./scripts/check-private-paths.sh` and `git diff --check`. The focused portfolio tests cover direct inputs, manifest inputs, combined-source expansion, before/after manifest source comparison, projected surface/edge comparison, deterministic output, read-only input handling, and public-output redaction. Run the public combined-path smoke only when the portfolio change also modifies language adapters, combine/report behavior, endpoint extraction, dependency-surface projection, paths, reverse, diff, impact, or release-review code shared outside `tracemap portfolio`.

## Legacy Baseline Regression Artifacts

When changing `tracemap baseline` creation, validation, or comparison behavior,
run the .NET build/test suite plus the baseline smoke over the checked-in
synthetic scan fixture:

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose original-parser-snapshot \
  --out .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06 \
  --created-at 2026-06 \
  --dry-run

dotnet run --project src/dotnet/TraceMap.Cli -- baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose original-parser-snapshot \
  --out .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06 \
  --created-at 2026-06

dotnet run --project src/dotnet/TraceMap.Cli -- baseline create \
  --scan-output samples/synthetic-legacy-scan \
  --label synthetic-alpha \
  --purpose candidate \
  --out .tmp/legacy-baselines/synthetic-alpha__candidate__2026-07 \
  --created-at 2026-07

dotnet run --project src/dotnet/TraceMap.Cli -- baseline compare \
  --baseline .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06/baseline-manifest.json \
  --candidate .tmp/legacy-baselines/synthetic-alpha__candidate__2026-07/baseline-manifest.json \
  --out .tmp/legacy-baselines/comparisons/synthetic-alpha \
  --generated-at 2026-07

dotnet run --project src/dotnet/TraceMap.Cli -- baseline validate \
  --manifest .tmp/legacy-baselines/synthetic-alpha__original-parser-snapshot__2026-06/baseline-manifest.json

git check-ignore .tmp/legacy-baselines/example
./scripts/check-private-paths.sh
git diff --check
```

Baseline manifests and comparisons are redacted summaries. Do not commit raw
scan outputs, facts, SQLite files, analyzer logs, source snippets, SQL text,
config values, remotes, endpoint addresses, connection strings, secrets, local
absolute paths, or private sample identities. Local-only outputs must remain
under ignored `.tmp/legacy-baselines/`. Public-safe promotion requires
`tracemap baseline validate`, the redaction validator, and the private-path
guard over the promoted files.

## Legacy WCF/SVC Metadata Smoke

When changing legacy WCF extraction, service-reference metadata parsing, or WCF operation normalization, run the .NET build/test suite plus the validation summary unit tests:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

If the ignored local WCF/SVC smoke manifest exists, also run:

```bash
python3 scripts/legacy_codebase_validation.py \
  .tmp/legacy-codebase-validation/wcf-svc-smoke.local.json \
  .tmp/legacy-codebase-validation/wcf-svc-smoke-out
```

The summary must stay label-only. Do not commit local sample paths, raw scan outputs, raw WSDL/DISCO/XSD contents, endpoint addresses, SOAP actions, namespace URIs, config values, secrets, or generated smoke outputs. WCF metadata facts are static checked-in design-time evidence; they do not prove runtime reachability, deployment, service version compatibility, authorization, binding compatibility, or branch feasibility.

## Legacy WebForms Event Flow Smoke

When changing WebForms markup, code-behind, designer, handler-resolution, or event-flow extraction, run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

Checked-in fixtures should cover explicit markup event bindings, missing or stale designer files, semantic or syntax-only code-behind resolution, ambiguity gaps, explicit `AutoEventWireup="true"` for `Page_Load`/`Page_Init`, false or unknown auto-wireup gaps, direct WCF/SQL reachability, reduced coverage, no-backend-evidence cases, static logic signals, UI-boilerplate signals, deterministic duplicate bindings, and privacy redaction.

Useful inspection queries:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts where fact_type like 'WebForms%' group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select fact_type, rule_id, evidence_tier, file_path, start_line, properties_json from facts where fact_type like 'WebForms%' order by fact_type, file_path, start_line;"
grep -E "WebForms Events|WebForms Event Flow|WebForms Static Logic Signals" <out>/report.md
```

WebForms smoke summaries must remain hidden public-claim level until reviewed. Do not commit local sample paths, raw remotes, raw markup/code snippets, raw SQL, config values, endpoint URLs, secrets, or generated private outputs. WebForms event-flow evidence is static and does not prove runtime page lifecycle execution, event firing, event bubbling, service reachability, SQL execution, branch feasibility, deployment, or production usage.

## Legacy WinForms Event Navigation Smoke

When changing WinForms form/control inventory, designer parsing, event binding,
handler resolution, navigation, callback, resource metadata, or handler-flow
projection, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyWinFormsExtractorTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 -m unittest scripts.tests.test_legacy_codebase_validation
./scripts/check-private-paths.sh
git diff --check
```

Useful inspection queries:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts where fact_type like 'WinForms%' group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select fact_type, rule_id, evidence_tier, file_path, start_line, properties_json from facts where fact_type like 'WinForms%' order by fact_type, file_path, start_line;"
grep -E "WinForms Static Evidence|WinForms Events|WinForms Navigation And Callbacks|WinForms Handler Flow" <out>/report.md
```

WinForms smoke summaries must remain hidden public-claim level until reviewed.
Use checked-in or temporary synthetic fixtures only. Do not commit local sample
paths, private sample names, raw remotes, raw source snippets, raw SQL, config
values, resource values, endpoint URLs, hostnames, secrets, or generated private
outputs. WinForms evidence is static and does not prove runtime event firing,
form visibility, user reachability, branch feasibility, auth/role outcome,
scheduling, service reachability, SQL execution, database existence, deployment,
or production usage.

## Legacy ASP.NET Route And Navigation Smoke

When changing classic ASP.NET route, config, handler, PageMethod, sitemap, or
navigation extraction, run:

```bash
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter LegacyAspNetExtractorTests
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Focused fixtures should cover `.aspx`, `.ascx`, `.master`, `.ashx`,
`Global.asax`, code-behind partial classes, designer files, checked-in
`web.config` structures, `MapPageRoute`, simple static route registration,
dynamic route gaps, config handlers/modules/pages/controls/urlMappings,
PageMethods, ScriptMethods, ScriptService classes, static markup navigation,
sitemap nodes, C# `Response.Redirect`/`Server.Transfer`, ambiguous or unsafe
targets, malformed files, reduced semantic coverage, deterministic output, and
redaction.

Useful inspection queries:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts where fact_type like 'AspNet%' group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select fact_type, rule_id, evidence_tier, file_path, start_line, properties_json from facts where fact_type like 'AspNet%' or rule_id like 'legacy.aspnet.%' order by fact_type, file_path, start_line;"
grep -E "Legacy ASP.NET Static Surface Evidence|Legacy ASP.NET Surface Limitations|route candidate|navigation reference candidate" <out>/report.md
```

No pinned public route/navigation smoke baseline exists yet beyond checked-in
synthetic unit fixtures. Any local legacy ASP.NET smoke output must stay
ignored/local-only, and any future catalog entry must use neutral labels,
rule IDs, tiers, states, sanitized command templates, and reviewed public or
synthetic identity metadata only. Do not commit raw scan outputs, local sample
paths, raw remotes, raw routes, raw endpoint URLs, hostnames, config values,
query strings, fragments, source snippets, credentials, secrets, or generated
private outputs. ASP.NET route/navigation evidence is static and does not prove
runtime route matching, IIS deployment, URL rewriting, authorization, browser
behavior, JavaScript execution, request handling, page rendering, user
reachability, or runtime impact.

## Legacy Remoting Smoke

When changing .NET Remoting API, `MarshalByRefObject`, channel, registration, activation, config, or Remoting report extraction, run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/dotnet-remoting-sample --out <tmp>/dotnet-remoting-scan
./scripts/check-private-paths.sh
git diff --check
```

The synthetic sample scan should produce the standard scan artifacts plus `Remoting*` facts in `facts.ndjson`, `index.sqlite`, and `report.md`. Inspect with:

```bash
sqlite3 <tmp>/dotnet-remoting-scan/index.sqlite "select fact_type, count(*) from facts where fact_type like 'Remoting%' group by fact_type order by fact_type;"
grep -E "Legacy Remoting Static Evidence|Legacy Remoting Limitations" <tmp>/dotnet-remoting-scan/report.md
```

No pinned public Remoting smoke baseline exists yet. Public-repository Remoting baselines require a separate reviewed baseline task or spec. Remoting evidence is static only and must not claim host activation, runtime reachability, endpoint availability, deployment, exploitability, security posture, or production usage. Generated scan artifacts are local-only and must not be committed.

## Legacy Static Flow Reporting Smoke

When changing `tracemap paths --include-legacy-roots`, legacy flow classification, WCF operation terminal handling, legacy data metadata terminal handling, path output redaction, or related path selectors, run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln --filter LegacyFlowCompositionTests
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Focused fixtures should cover WebForms event roots, WebForms lifecycle roots, direct handler-to-service paths, WCF service-reference paths with `wcf-operation` terminals, SQL/query terminals, legacy data metadata terminals when available, reduced coverage, missing extractor availability, selector no-match and classification-filter gaps, truncation, deterministic JSON, and privacy suppression.

Legacy static flow reports use `legacy-flow.v1` schema metadata and must phrase results as static evidence or possible static paths. They must not claim runtime execution, guaranteed backend reachability, SQL execution, database existence, production dependency, or impact. Generated Markdown and JSON must omit or hash local absolute paths, raw remotes, private labels, raw SQL, WSDL/SOAP/endpoint URLs, connection strings, config values, source snippets, and secret-looking values.

## Legacy Data Metadata Smoke

When changing DBML, EDMX, typed DataSet/TableAdapter, legacy data config, generated data-code linkage, XML parser safety, or safe identifier redaction, run:

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Checked-in fixtures should cover DBML entities/tables/columns/associations/routines, EDMX CSDL/SSDL/MSL mappings and unsupported shapes, typed DataSet XSD gating, TableAdapter command hashing, normalized model identity keys, config provider/connection metadata, generated-code links, unsupported old ORM descriptor gaps, malformed XML, DTD/entity rejection, deterministic output, and privacy suppression in facts, reports, logs, and SQLite.

Useful inspection queries:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts where fact_type like 'LegacyData%' group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select fact_type, rule_id, evidence_tier, file_path, start_line, properties_json from facts where fact_type like 'LegacyData%' or rule_id like 'legacy.data.%' order by fact_type, file_path, start_line;"
grep -E "Legacy Data Metadata|Legacy Data Metadata Limitations" <out>/report.md
```

Legacy data metadata smoke summaries remain hidden public-claim level until reviewed. Do not commit local sample paths, raw remotes, raw DBML/EDMX/XSD/XML snippets, raw SQL, connection strings, config values, provider secrets, URLs, local absolute paths, or generated private outputs. Legacy data metadata evidence is static design-time evidence and does not prove runtime data access, SQL execution, database existence, provider compatibility, transform selection, generated-code freshness, branch feasibility, deployment, or production usage.

## Public OSS Smoke

Use `scripts/smoke-open-source-repos.sh` to clone pinned public repositories into a cache directory and scan them into a separate output directory:

```bash
scripts/smoke-open-source-repos.sh /tmp/tracemap-oss-cache /tmp/tracemap-oss-smoke
```

The script uses exact commit SHAs so results are comparable over time.

| Label | Language | URL | Commit SHA | Expected coverage |
| --- | --- | --- | --- | --- |
| `ProjectExtensions.Azure.ServiceBus` | C# | `https://github.com/ProjectExtensions/ProjectExtensions.Azure.ServiceBus.git` | `2a8e72c8f5680edf2096b05ac08c39d47a95cef8` | usually `Level1SemanticAnalysisReduced` |
| `fluentjdf` | C# | `https://github.com/joefeser/fluentjdf.git` | `9490e699a89bb21f4aabf198173fc6382f84a53f` | usually `Level1SemanticAnalysisReduced` |
| `scip-typescript` | TypeScript | `https://github.com/sourcegraph/scip-typescript.git` | `891eb4293709a6a587bf4468dfa1b45a85182fd9` | usually `Level1SemanticAnalysisReduced` |
| `scip-java` | JVM | `https://github.com/sourcegraph/scip-java.git` | `825463cb15d540d45c680593aad1f634330435cf` | usually `Level1SemanticAnalysisReduced` |
| `spring-petclinic` | JVM | `https://github.com/spring-projects/spring-petclinic.git` | `a2c2ef994340d3970eb6db51247456a51bb161f8` | usually `Level1SemanticAnalysisReduced` |
| `okio` | JVM/Kotlin | `https://github.com/square/okio.git` | `cad7ff1057307142149b1a28dfcb49117e89b0d3` | usually reduced or syntax fallback for Kotlin-heavy areas |
| `full-stack-fastapi-template` | Python | `https://github.com/fastapi/full-stack-fastapi-template.git` | `1c1175eb5045e6e8fca3bcbc4134630f3ae640ba` | `Level1SemanticAnalysisReduced` |
| `microblog` | Python | `https://github.com/miguelgrinberg/microblog.git` | `a975ef64864354867c88e0ed3a17ba7d17dca752` | `Level1SemanticAnalysisReduced` |
| `sqlalchemy` | Python | `https://github.com/sqlalchemy/sqlalchemy.git` | `bfe559a7e4d69e5699c390ac9cafd2a5a2d38078` | `Level1SemanticAnalysisReduced` |

Reduced coverage is acceptable for OSS smoke when project/dependency/classpath gaps are recorded as `AnalysisGap` facts. A successful smoke means the scan completes, artifacts exist, the manifest is honest about coverage, and important relationship tables can be queried.

## JVM Smoke Expectations

The JVM modern sample is the minimum high-signal fixture. It should produce:

- `Level1SemanticAnalysis`
- `buildStatus = "Succeeded"`
- exactly one Java route binding: `GET /api/orders/{id}` mapped to `com.example.orders.OrderController.getOrder`
- semantic call edges from `OrderController.getOrder` to `OrderResponse.setStatus` and `OrderService.calculateTotal`
- object creation rows for `OrderService`, `OrderResponse`, and `OrderRepository`
- argument-flow rows from controller/service calls into callee parameters
- SQL facts for the JDBC `prepareStatement` literal and `schema.sql`
- config key facts for `application.properties`
- a reducer `DefiniteImpact` finding for `OrderResponse.status`

Example query set:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select count(*) from call_edges;"
sqlite3 <out>/index.sqlite "select count(*) from object_creations;"
sqlite3 <out>/index.sqlite "select count(*) from argument_flows;"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='HttpRouteBinding';"
```

## What SQL Means Here

SQL should be treated as a first-class cross-language data dependency surface, not as the next application language adapter. The SQL layer should eventually parse:

- query text from application code
- `.sql` files and migration files
- tables, columns, joins, projections, predicates, and write operations
- stored procedures, views, and function calls where dialect support exists
- query-to-schema relationships across app indexes and database artifacts

SQL validation should therefore plug into every app-language adapter, because C#, TypeScript, JVM, and Python can all emit SQL evidence.

## Python Adapter

Python validation fixtures should cover:

- FastAPI routes and Pydantic DTOs
- Flask routes where syntax can prove them
- SQLAlchemy declared columns and direct SQL literals
- direct SQL literals
- environment/config reads
- requests/httpx client calls

Python follows the same matrix: modern sample, broken sample, reducer fixture, relationship tables, integration facts, public OSS smoke, and private-path guard.

Python MVP no-match reducer outcomes are expected to be `NoEvidenceReducedCoverage` because MVP scans use reduced AST/package/config coverage, not full type-checker semantic coverage.

## Python Smoke Expectations

The Python FastAPI sample is the minimum high-signal fixture. It should produce:

- `Level1SemanticAnalysisReduced`
- `buildStatus = "FailedOrPartial"`
- route facts for FastAPI/Flask decorators when static decorator syntax is visible
- serializer contract member facts for Pydantic and dataclass-like DTO fields
- SQLAlchemy column mapping facts for declarative mapped columns
- SQL file and direct SQL literal facts with hashed SQL text
- query-pattern facts with operation, table, column, text hash, and query shape hash metadata when simple static SQL is visible
- config key facts for config module assignments and static `os.getenv` or `os.environ[...]` reads
- HTTP client facts for `requests` and `httpx` static URL calls
- endpoint alignment smoke from `samples/python-client-sample` to `samples/python-fastapi-sample` produces at least one `MatchedEndpoint`
- shared SQLite rows for `call_edges`, `object_creations`, `argument_flows`, `symbol_relationships`, and `symbols`
- a reducer `ProbableImpact` or stronger structural finding for `OrderResponse.status`

Example query set:

```bash
sqlite3 <out>/index.sqlite "select fact_type, count(*) from facts group by fact_type order by fact_type;"
sqlite3 <out>/index.sqlite "select count(*) from call_edges;"
sqlite3 <out>/index.sqlite "select count(*) from object_creations;"
sqlite3 <out>/index.sqlite "select count(*) from argument_flows;"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='HttpRouteBinding';"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='DatabaseColumnMapping';"
sqlite3 <out>/index.sqlite "select target_symbol, properties_json from facts where fact_type='QueryPatternDetected';"
sqlite3 <out>/index.sqlite "select rule_id, json_extract(properties_json, '$.sqlSourceKind'), json_extract(properties_json, '$.queryShapeHash') from facts where fact_type='QueryPatternDetected' and json_extract(properties_json, '$.sqlSourceKind') is not null order by rule_id, fact_id;"
sqlite3 <combined>/combined.sqlite "select sources.label, facts.fact_type, json_extract(facts.properties_json, '$.sqlSourceKind'), json_extract(facts.properties_json, '$.queryShapeHash'), json_extract(facts.properties_json, '$.textHash') from combined_facts facts join combined_sources sources on sources.source_index_id = facts.source_index_id where facts.fact_type in ('SqlTextUsed','QueryPatternDetected','DatabaseColumnMapping','DapperCallDetected','SqlCommandDetected') order by sources.label, facts.combined_fact_id;"
grep "orm-text" <out>/report.md
grep "orders" <out>/report.md
```

For SQL dependency-surface changes, also inspect hash-only and weak-identity behavior:

```bash
sqlite3 <combined>/combined.sqlite "select sources.label, facts.fact_type, facts.properties_json from combined_facts facts join combined_sources sources on sources.source_index_id = facts.source_index_id where facts.fact_type in ('SqlTextUsed','QueryPatternDetected') order by sources.label, facts.combined_fact_id;"
dotnet run --project src/dotnet/TraceMap.Cli -- diff --before <before-combined.sqlite> --after <after-combined.sqlite> --out <tmp>/sql-diff --scope surfaces --surface sql-query --format json
grep -E "HashOnlyEvidence|VolatileIdentity" <tmp>/sql-diff/diff-report.json
```

When checking mapping-only persistence evidence, use `--to-surface sql-persistence`, `--surface sql-persistence`, or `--scope surfaces --surface sql-persistence`; these surfaces do not claim that a SQL query executes.
