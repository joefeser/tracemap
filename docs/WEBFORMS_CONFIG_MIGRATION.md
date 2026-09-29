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

### Reuse the unchanged retained publish proof

If the earlier compiled-site proof is still available, avoid manually guessing
DLL ownership or issuing a new attestation. On this branch run:

```powershell
pwsh -NoProfile -File .\scripts\wverify.ps1 -Run
```

The helper requests the copied **Web Forms** review folder containing
`native-config`, explicitly selects a retained proof folder (a numbered immediate
TEMP candidate or an exact path), and requests the original compiled-site folder
(parent of `bin`). No newest-folder selection is performed. It does not ask for
or build the original solution. It builds only the TraceMap CLI unless `-NoBuild`
is explicitly supplied.

It imports only the selected proof's existing `publish-receipt.local.json` and
`compiled-binding.local.json`. The native importer checks the receipt's source
commit and repository, source bytes and committed membership, clean source
scope, every recorded DLL hash, inventory/receipt commitments, and actual
primary metadata binding admission. Selected DLLs remain primary; recorded
artifact context becomes unbound dependency context. Original receipt bytes and
their provenance are not rewritten. Missing or mismatched evidence stops it;
it does not substitute current HEAD for a historical commit or create a new
source-to-DLL attestation.

The new default output is `<copied-review>/native-proof-verification/`:

- `configuration/review-config.local.json`: verified settings, bound by the
  paired import receipt and native configuration provenance.
- `configuration/proof-import.local.json`: exact generator/input/output hashes,
  carried receipt commitments, scope, gaps and limitations.
- `review/run/`: only with `-Run`, a new native run with application and compiled
  document sets. Use its printed workbench/handoff paths.

Without `-Run`, the command verifies inputs only. Existing output is refused;
use an explicit new `-OutputRoot` for another attempt. Failures preserve any
diagnostic staging, but a failed staging directory is not an admitted import or
completed run. After an interrupted native run, use native status/resume on its
printed `review/run` path rather than rerunning the import into existing output.

This is a bounded baseline verification run: its **selected pages come from the
retained proof receipt**, even if the migrated draft requested all pages. The
draft's source/project scopes and budgets are preserved. It is not all-pages
coverage. A backend draft with a different source scope cannot inherit the
website receipt or commit. Both projects being in one solution does not bypass
source-byte or receipt checks. Keep the backend scan and original two-source
merge workflow; this helper does not replace that merge.

Successful generation is **not** a parity verdict. Compare the actual grouped
method identities, all retained variants, rules, tiers, DLL/commit provenance,
gaps and truncation against the known-good document set before cleanup. Matching
path counts alone is insufficient. No private Windows acceptance is claimed by
public synthetic tests.

The direct native command is:

```text
tracemap webforms-review import-proof --config review.draft.json --proof-root retained-proof --published-root original-publish --out new-config-folder
```

### When retained proof is unavailable

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
