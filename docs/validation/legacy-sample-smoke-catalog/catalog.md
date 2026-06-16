<!-- catalog-json-sha256: bc917af603983f8cfc73724fc62b900106d4dccd692130995cdde02352d888dc -->
# Legacy Sample Smoke Catalog

Schema: `legacy-sample-smoke-catalog.v1`
Generated: `2026-06`
Classification: `public-safe`

This catalog is deterministic validation metadata for legacy sample smoke coverage. It is not raw scan output, an evidence pack, a baseline, a site page, or an impact-analysis result.

## Entries

| Sample | Claim | Source | Commit identity | Families | Commands | Relationships |
| --- | --- | --- | --- | --- | --- | --- |
| Large public dotnet client (large-public-dotnet-client) | public-safe | synthetic-fixture; neutral-label; tracemap-fixture | fixture-version; large-dotnet-client-fixture@2026-06 | analysis-gap-reporting [required; analysis-gap, reduced, truncated]; fallback-syntax-scan [required; analysis-gap, observed, reduced]; large-repo-stress [exploratory; analysis-gap, observed, reduced, truncated] | create-evidence-pack, scan-sample | redacted-validation-summary:large-public-dotnet-client-summary:public-safe; evidence-pack-summary:large-public-dotnet-client-pack:public-safe |
| Legacy build diagnostics synthetic (legacy-build-diagnostics-synthetic) | public-safe | synthetic-fixture; neutral-label; tracemap-fixture | fixture-version; legacy-build-diagnostics-fixture@2026-06 | analysis-gap-reporting [required; analysis-gap, reduced]; binding-redirects [optional; analysis-gap, observed]; build-environment-diagnostics [required; analysis-gap, observed, reduced]; msbuild-project-load-failure [required; analysis-gap, reduced]; packages-config [optional; analysis-gap, observed] | scan-sample, validate-catalog | redacted-comparison-report:legacy-build-diagnostics-synthetic-comparison:public-safe; redacted-validation-summary:legacy-build-diagnostics-synthetic-summary:public-safe |
| Legacy data metadata public fixture (legacy-data-metadata-public-fixture) | public-safe | synthetic-fixture; neutral-label; tracemap-fixture | fixture-version; legacy-data-metadata-fixture@2026-06 | dbml-linq-to-sql [required; analysis-gap, observed]; edmx-entity-framework [required; analysis-gap, observed]; legacy-sql-or-query-surface [optional; analysis-gap, observed, reduced]; typed-dataset [required; analysis-gap, observed] | create-evidence-pack, scan-sample | redacted-baseline-snapshot:legacy-data-metadata-public-fixture-baseline:public-safe; evidence-pack-summary:legacy-data-metadata-public-fixture-pack:public-safe |
| Legacy Remoting demo sample (legacy-remoting-demo-sample) | public-safe | synthetic-fixture; neutral-label; tracemap-fixture | fixture-version; legacy-remoting-fixture@2026-06 | remoting-channel-config [optional; analysis-gap, observed, reduced]; remoting-registration [required; analysis-gap, observed, reduced] | create-evidence-pack, scan-sample | redacted-validation-summary:legacy-remoting-demo-sample-summary:public-safe; evidence-pack-summary:legacy-remoting-demo-sample-pack:public-safe |
| Legacy WCF public fixture (legacy-wcf-public-fixture) | public-safe | synthetic-fixture; neutral-label; tracemap-fixture | fixture-version; legacy-wcf-fixture@2026-06 | wcf-config-endpoint-shape [required; analysis-gap, observed]; wcf-service-reference [required; analysis-gap, observed, reduced] | scan-sample, validate-catalog | redacted-validation-summary:legacy-wcf-public-fixture-summary:public-safe; evidence-pack-summary:legacy-wcf-public-fixture-pack:public-safe |
| Legacy WebForms public app (legacy-webforms-public-app) | public-safe | synthetic-fixture; neutral-label; tracemap-fixture | fixture-version; legacy-webforms-fixture@2026-06 | webforms-event-binding [required; analysis-gap, observed, reduced]; webforms-markup-codebehind [required; analysis-gap, observed, reduced] | scan-sample, summarize-validation | redacted-validation-summary:legacy-webforms-public-app-summary:public-safe; evidence-pack-summary:legacy-webforms-public-app-pack:public-safe |

## Family Expectations

| Sample | Family | Rules | Tiers | Coverage | Extractors | States | Limitations |
| --- | --- | --- | --- | --- | --- | --- | --- |
| large-public-dotnet-client | analysis-gap-reporting | build.environment.workspace-diagnostic.v1, legacy.flow.gap-propagation.v1 | Tier4Unknown | AnalysisGap, Reduced | extractor-reduced-coverage | analysis-gap, reduced, truncated | gap-is-not-absence |
| large-public-dotnet-client | fallback-syntax-scan | csharp.syntax.declarations.v1, csharp.syntax.invocation.v1 | Tier3SyntaxOrTextual, Tier4Unknown | Reduced, SyntaxFallback | csharp.syntax | analysis-gap, observed, reduced | syntax-fallback-review-tier |
| large-public-dotnet-client | large-repo-stress | legacy.validation.bounds.v1, legacy.validation.summary.v1 | Tier4Unknown | Reduced, Truncated | extractor-reduced-coverage | analysis-gap, observed, reduced, truncated | bounded-stress-only |
| legacy-build-diagnostics-synthetic | analysis-gap-reporting | build.environment.workspace-diagnostic.v1, legacy.flow.gap-propagation.v1 | Tier4Unknown | AnalysisGap, Reduced | extractor-reduced-coverage | analysis-gap, reduced | gap-is-not-absence |
| legacy-build-diagnostics-synthetic | binding-redirects | build.environment.project-format.v1, legacy.validation.environment.v1 | Tier2Structural, Tier4Unknown | AnalysisGap, Reduced | xml.config | analysis-gap, observed | binding-redirect-shape-only |
| legacy-build-diagnostics-synthetic | build-environment-diagnostics | build.environment.project-format.v1, build.environment.restore.v1, build.environment.toolset.v1 | Tier2Structural, Tier4Unknown | AnalysisGap, Reduced | project.file, xml.config | analysis-gap, observed, reduced | environment-static-only |
| legacy-build-diagnostics-synthetic | msbuild-project-load-failure | build.environment.workspace-diagnostic.v1, csharp.semantic.workspace.v1 | Tier4Unknown | AnalysisGap, Reduced | extractor-reduced-coverage | analysis-gap, reduced | failed-build-reduced-coverage |
| legacy-build-diagnostics-synthetic | packages-config | build.environment.restore.v1, project.file.v1 | Tier2Structural, Tier4Unknown | AnalysisGap, Reduced | project.file | analysis-gap, observed | package-shape-only |
| legacy-data-metadata-public-fixture | dbml-linq-to-sql | legacy.data.dbml.v1, legacy.data.metadata.inventory.v1 | Tier2Structural, Tier4Unknown | AnalysisGap, Reduced | xml.dbml | analysis-gap, observed | dbml-static-metadata |
| legacy-data-metadata-public-fixture | edmx-entity-framework | legacy.data.edmx.v1, legacy.data.metadata.inventory.v1 | Tier2Structural, Tier4Unknown | AnalysisGap, Reduced | xml.edmx | analysis-gap, observed | edmx-static-metadata |
| legacy-data-metadata-public-fixture | legacy-sql-or-query-surface | csharp.syntax.querypattern.v1, database.sql.shape.v1 | Tier2Structural, Tier3SyntaxOrTextual, Tier4Unknown | AnalysisGap, Reduced | csharp.syntax, sql.shape | analysis-gap, observed, reduced | query-shape-no-raw-sql |
| legacy-data-metadata-public-fixture | typed-dataset | legacy.data.metadata.inventory.v1, legacy.data.typed-dataset.v1 | Tier2Structural, Tier4Unknown | AnalysisGap, Reduced | xml.typed-dataset | analysis-gap, observed | dataset-static-metadata |
| legacy-remoting-demo-sample | remoting-channel-config | legacy.remoting.channel.v1, legacy.remoting.config.v1 | Tier2Structural, Tier3SyntaxOrTextual, Tier4Unknown | AnalysisGap, Reduced | csharp.syntax, xml.config | analysis-gap, observed, reduced | channel-values-omitted |
| legacy-remoting-demo-sample | remoting-registration | legacy.remoting.api.v1, legacy.remoting.registration.v1 | Tier3SyntaxOrTextual, Tier4Unknown | AnalysisGap, Reduced | csharp.syntax | analysis-gap, observed, reduced | registration-static-only |
| legacy-wcf-public-fixture | wcf-config-endpoint-shape | legacy.wcf.config.v1 | Tier2Structural, Tier4Unknown | AnalysisGap, Reduced | xml.config | analysis-gap, observed | endpoint-values-omitted |
| legacy-wcf-public-fixture | wcf-service-reference | legacy.wcf.mapping.v1, legacy.wcf.metadata.v1 | Tier2Structural, Tier3SyntaxOrTextual | Level1SemanticAnalysisReduced, Reduced | csharp.syntax, xml.service-reference | analysis-gap, observed, reduced | static-service-reference-only |
| legacy-webforms-public-app | webforms-event-binding | legacy.webforms.event-binding.v1, legacy.webforms.handler-resolution.v1 | Tier2Structural, Tier3SyntaxOrTextual, Tier4Unknown | AnalysisGap, Reduced | aspx.markup, csharp.syntax | analysis-gap, observed, reduced | event-binding-static-only |
| legacy-webforms-public-app | webforms-markup-codebehind | legacy.webforms.designer-control.v1, legacy.webforms.inventory.v1 | Tier2Structural, Tier3SyntaxOrTextual, Tier4Unknown | AnalysisGap, Reduced | aspx.markup, csharp.syntax | analysis-gap, observed, reduced | markup-linkage-static-only |

## Validation Commands

| Sample | Command | Mode | Template | Artifacts | Gates |
| --- | --- | --- | --- | --- | --- |
| large-public-dotnet-client | create-evidence-pack | redacted-artifact-only | tracemap evidence-pack create --input &lt;redacted-summary&gt; --input-kind legacy-validation-summary --label &lt;sample-label&gt; --claim-level &lt;claim-level&gt; --date &lt;YYYY-MM&gt; --out &lt;pack-output&gt; | evidence-pack-reference, redacted-summary | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| large-public-dotnet-client | scan-sample | checked-in-sample | tracemap scan --repo &lt;sample-root&gt; --out &lt;scan-output&gt; | analyzer-log-present, facts-ndjson-present, index-sqlite-present, report-md-present, scan-manifest | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-build-diagnostics-synthetic | scan-sample | checked-in-sample | tracemap scan --repo &lt;sample-root&gt; --out &lt;scan-output&gt; | analyzer-log-present, facts-ndjson-present, index-sqlite-present, report-md-present, scan-manifest | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-build-diagnostics-synthetic | validate-catalog | redacted-artifact-only | catalog validate --catalog &lt;catalog-json&gt; --expected-claim-level &lt;claim-level&gt; | catalog-json, catalog-md | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-data-metadata-public-fixture | create-evidence-pack | redacted-artifact-only | tracemap evidence-pack create --input &lt;redacted-summary&gt; --input-kind legacy-validation-summary --label &lt;sample-label&gt; --claim-level &lt;claim-level&gt; --date &lt;YYYY-MM&gt; --out &lt;pack-output&gt; | evidence-pack-reference, redacted-summary | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-data-metadata-public-fixture | scan-sample | checked-in-sample | tracemap scan --repo &lt;sample-root&gt; --out &lt;scan-output&gt; | analyzer-log-present, facts-ndjson-present, index-sqlite-present, report-md-present, scan-manifest | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-remoting-demo-sample | create-evidence-pack | redacted-artifact-only | tracemap evidence-pack create --input &lt;redacted-summary&gt; --input-kind legacy-validation-summary --label &lt;sample-label&gt; --claim-level &lt;claim-level&gt; --date &lt;YYYY-MM&gt; --out &lt;pack-output&gt; | evidence-pack-reference, redacted-summary | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-remoting-demo-sample | scan-sample | checked-in-sample | tracemap scan --repo &lt;sample-root&gt; --out &lt;scan-output&gt; | analyzer-log-present, facts-ndjson-present, index-sqlite-present, report-md-present, scan-manifest | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-wcf-public-fixture | scan-sample | checked-in-sample | tracemap scan --repo &lt;sample-root&gt; --out &lt;scan-output&gt; | analyzer-log-present, facts-ndjson-present, index-sqlite-present, report-md-present, scan-manifest | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-wcf-public-fixture | validate-catalog | redacted-artifact-only | catalog validate --catalog &lt;catalog-json&gt; --expected-claim-level &lt;claim-level&gt; | catalog-json, catalog-md | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-webforms-public-app | scan-sample | checked-in-sample | tracemap scan --repo &lt;sample-root&gt; --out &lt;scan-output&gt; | analyzer-log-present, facts-ndjson-present, index-sqlite-present, report-md-present, scan-manifest | ./scripts/check-private-paths.sh, catalog validate, git diff --check |
| legacy-webforms-public-app | summarize-validation | redacted-artifact-only | tracemap legacy-codebase-validation summarize --input &lt;scan-output&gt; --out &lt;redacted-summary&gt; | redacted-summary | ./scripts/check-private-paths.sh, catalog validate, git diff --check |

## Safety Limitations

- `catalog-is-not-proof`: Catalog entries describe expected static evidence families; downstream validated artifacts provide proof.
- `static-metadata-only`: Static catalog expectations do not prove execution, reachability, safety, release approval, customer effect, or reducer outcome.

## Entry Limitations

### large-public-dotnet-client

- `catalog-not-raw-output`: This entry records expected smoke coverage only; raw artifacts stay out of tracked catalog files.

### legacy-build-diagnostics-synthetic

- `catalog-not-raw-output`: This entry records expected smoke coverage only; raw artifacts stay out of tracked catalog files.

### legacy-data-metadata-public-fixture

- `catalog-not-raw-output`: This entry records expected smoke coverage only; raw artifacts stay out of tracked catalog files.

### legacy-remoting-demo-sample

- `catalog-not-raw-output`: This entry records expected smoke coverage only; raw artifacts stay out of tracked catalog files.

### legacy-wcf-public-fixture

- `catalog-not-raw-output`: This entry records expected smoke coverage only; raw artifacts stay out of tracked catalog files.

### legacy-webforms-public-app

- `catalog-not-raw-output`: This entry records expected smoke coverage only; raw artifacts stay out of tracked catalog files.
