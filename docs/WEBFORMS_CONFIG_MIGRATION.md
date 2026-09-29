# Migrate existing private Web Forms configs

From the migration branch on Windows:

```powershell
.\scripts\wmigrate.ps1
```

Enter the copied Web Forms review folder and copied backend review folder when
prompted. These are the folders containing `config/webforms-review.jsonc` (or
the legacy `.json`), not the source repositories or compiled website. Leave the
second prompt blank for one config. The helper builds the .NET CLI once and
delegates all conversion to it; no PowerShell migration engine is introduced.
With no arguments the helper explicitly requests input; a blank first answer
stops immediately. All folders are checked for exactly one legacy config before
building. Quoted pasted folder paths are accepted. Missing or ambiguous configs
produce a local selection diagnostic without running collection or conversion.

Each review folder gains a new `native-config/` containing:

- `review.draft.json`: plain native JSON with the existing source root, three
  folder scopes, project selection and selected pages preserved.
- `migration.local.json`: local-only generator/input/output SHA-256 bindings,
  missing settings and limitations. It retains the old output root for reference.

Original configs, scans, receipts, source and compiled website are not changed.
Existing destinations are refused. If one conversion fails after another
succeeded, keep the successful draft; rerun only the failed review root:

```powershell
.\scripts\wmigrate.ps1 -ReviewRoots 'C:/private/backend-review'
```

## What requires owner input

The old schema does not contain the exact source commit, compiled-site root,
primary/dependency DLL inventory, source-to-publish membership or binding
receipts. These cannot be honestly guessed from config or TEMP names. The draft
therefore reports `readyToRun=false` and leaves mandatory new fields empty.
Fill them using the [native workflow guide](WEBFORMS_NATIVE_WORKFLOW.md), then
run native preflight against a new output root before collection. Configuration
conversion is not source-to-DLL attestation, scan success or runtime SQL proof.

Legacy `discover` mode needs an explicit native project selection; it is not
silently changed to projectless. Leading-slash virtual page routes require
repository-relative `.aspx` paths. Path availability and repository scope are
validated by native preflight, not by migration. Native default budgets are
written without inferring capacity from an old scan.

Each source repository remains independent. Converting the two configs does
not make the native command a two-repository merge replacement. Keep the old
merge workflow and known-good reports until the equivalent scope is validated.

The direct .NET form is:

```text
tracemap webforms-review migrate-config --review-root old-review --out native-config
tracemap webforms-review migrate-config --config old.jsonc --out native-config
```

The converter accepts only the known `focused-webforms-review-config.v1`
schema, bounded to 1 MiB. JSONC comments are supported; duplicate/unknown
properties, trailing commas and ambiguous config-file selection are refused.
It does not support the older three-field page-list-only config, which lacks
source and project scope. Failed staging folders are retained, not admitted as
completed migrations. Keep all generated files private; do not commit or upload
them. The migration receipt binds the exact draft bytes, not later owner edits.
