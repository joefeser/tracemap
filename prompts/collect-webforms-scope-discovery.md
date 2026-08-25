# Collect Web Forms scope discovery

```text
Perform one private, read-only Web Forms scope-discovery review. Do not rescan,
build, restore, or modify any repository.

1. Read `prompts/README.md`,
   `docs/examples/tracemap-webforms-extraction-readiness-summary.json`, and
   `prompts/collect-webforms-extraction-readiness.md` completely.
2. Ask the local owner to identify which configured source label is intended to
   contain the Web Forms application. Keep the label and response local; do not
   include them in output.
3. Ask for the exact authorized interaction output directory and interaction
   configuration file if either is not already in task context. Do not search
   unrelated directories or drives.
4. Read only:
   - the interaction configuration;
   - the intended source's existing `scan-manifest.json`;
   - the intended source's existing `facts.ndjson`.
5. Do not read source files, configuration contents from the application,
   SQLite indexes, raw reports, analyzer logs, or generated documentation.

Determine these categorical facts for the intended source:

- whether the source label exists in the interaction configuration;
- whether its kind is `dotnet`;
- whether its configured repository root corresponds to the intended source,
  based solely on the owner's local confirmation;
- count of existing `FileInventoried` facts by these exact `kind` values:
  - `WebFormsMarkup`
  - `WebFormsCodeBehind`
  - `WebFormsDesigner`
  - `AspNetApplication`
  - `AspNetHandler`
  - `AspNetSiteMap`
- count of positive facts under `legacy.webforms.inventory.v1` and
  `legacy.aspnet.surface.v1`, excluding `AnalysisGap` facts;
- whether explicit include globs are configured;
- whether explicit exclude globs are configured;
- whether the configured include/exclude rules categorically permit the six
  inventory kinds above. Inspect glob values locally but return only one state:
  `permitted`, `blocked`, `mixed-or-unknown`, or `not-configured`.

Classify the result using exactly one value:

- `target-source-not-configured`
- `target-source-kind-mismatch`
- `target-root-not-confirmed`
- `webforms-inventory-excluded`
- `no-webforms-candidates-in-existing-inventory`
- `webforms-candidates-inventoried-no-projection`
- `webforms-positive-evidence-present`

Return only one line:

webforms-scope-discovery=<classification>;targetConfigured=<true|false>;dotnetScope=<true|false>;rootConfirmed=<true|false>;markup=<count>;codeBehind=<count>;designer=<count>;application=<count>;handler=<count>;siteMap=<count>;webFormsInventoryFacts=<count>;aspNetSurfaceFacts=<count>;includeRules=<present|absent>;excludeRules=<present|absent>;scopePermission=<permitted|blocked|mixed-or-unknown|not-configured>;nextAction=<categorical-action>

Allowed `nextAction` values:

- `add-intended-source-to-interaction-config`
- `correct-source-kind`
- `correct-repository-root`
- `correct-include-exclude-scope`
- `publish-inventory-selection-diagnostic`
- `reproduce-webforms-projection-defect-synthetically`
- `continue-webforms-evidence-review`

Do not return source labels, paths, filenames, project names, glob values,
repository identities, run IDs, commit SHAs, routes, symbols, source values,
diagnostic messages, SQL, configuration values, credentials, or logs. Do not
commit, push, publish, or open a pull request.

Before responding, search the proposed line for the private source label,
repository name, path fragments, glob values, run ID, and commit SHA. If any
match remains, remove it and repeat the check.
```
