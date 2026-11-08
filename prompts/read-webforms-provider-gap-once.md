# Read the retained provider failure

> Use implementation `1d0a5408b601d4d203770c247baa360dfc91a124` or a docs-only descendant with identical code. On the approved work machine, when authorized, build the Debug CLI and use the same source selection and IIS mount for one fresh static scan. Confirm legacy-webforms/0.27.0. Read the provider gap directly from SQLite; preserve the scanner and input files.

```sql
SELECT file_path, start_line,
       json_extract(properties_json, '$.gapKind') AS gap_kind,
       json_extract(properties_json, '$.ownerFailure') AS owner_failure,
       json_extract(properties_json, '$.receiverKind') AS receiver_kind,
       json_extract(properties_json, '$.tokenKind') AS token_kind,
       json_extract(properties_json, '$.factoryCount') AS factory_count,
       json_extract(properties_json, '$.inlineInput') AS inline_input,
       json_extract(properties_json, '$.providerInputLine') AS provider_line,
       json_extract(properties_json, '$.supportingFactIds') AS support
FROM facts
WHERE rule_id = 'legacy.webforms.angularjs-service-http.v1'
  AND fact_type = 'AnalysisGap';
```

For each of the two original click IDs, follow its retained partial handler and service call to its gap by exact ID. An external owner gap is located at the provider property and supports the service call plus include evidence. For an inline provider, use inline_input and provider_line locally. AngularServiceOnlyUnboundProviders means no admissible registration was found after retaining one or more uncertain calls. AngularInlineProviderOwnerUnavailable means the unbound-provider branch encountered a non-external input.

Return sanitized gap_kind, owner_failure, receiver_kind, token_kind, factory_count, whether the provider is the intended registration, and a synthetic root-expression shape. If the controller is itself missing, report its gap first. Do not infer the failing branch from bundle membership or from the old service-call location.
