# Create a workspace from saved wizard setup and loose SQL

Use this instead of manually copying fields into a V3 workspace. The existing
`scripts/tm.ps1` launcher now exposes `workspace init-from-wizard`. It reads the
saved wizard selection, follows its pinned attempt to the **prepared receipt
configuration**, validates SQL and framework selection, and creates one verified
`workspace.local.json`. It does not start collection, build an application,
execute SQL, infer source/publication correspondence or import completed scans.

Prepare TraceMap after updating to the branch containing this command:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 -Build version
if ($LASTEXITCODE -ne 0) { throw 'Tool preparation failed' }
```

## Continue the existing wizard (recommended)

Use your **one existing configuration root**. You do not need a second wizard
root, another DLL selection, or a command containing all the workspace switches:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 webforms-review wizard --root "C:/reviews/Application" --continue
```

Choose `workspace` when prompted. The wizard reuses the saved website, source
commit, primary/dependency DLL selection and prepared receipts. It asks only for:

1. An **optional additional source repository folder**. If you only have the dependency DLL, or do not want its source scanned, type `skip`. The saved DLL selection stays intact.
2. If adding source, its project/solution: choose a displayed number, paste a path inside the repo,
   or explicitly choose `discovery`. The preview lists root-level files only.
3. The existing SQL manifest file from step 4.
4. SQL paths are first resolved relative to that manifest's folder and the resolved
   files are shown. If all files and hashes verify, no folder question is asked.
   Only when those files are absent does setup ask for an alternate **base** for
   the paths listed in the manifest. For `sql/schema.sql`, choose the parent of
   `sql`; for `schema.sql`, choose the directory containing that file. No Git repo
   is required for SQL. A previously saved doubled base is corrected only when
   the manifest-relative files and hashes verify.

Each answer is saved immediately in the existing website folder. Type `later` to
pause; run the same command and choose `workspace` to resume without re-entering
saved choices. At the summary, `change` corrects one choice and `create` validates
and creates `workspace.local.json` in **that same root**. An existing workspace is
preserved and offers `verify`, `collect`, or `later`. After creation, choose
`collect` to run the workspace and produce SQLite/report outputs, or `later` to
finish configuration only. Configuration alone does not produce SQLite evidence.
Collection uses a fresh `runs/workspace-<id>` folder in the same root and prints
the exact `START-HERE.md` and `combined.sqlite` paths. Choose `reports` separately
to verify/resume the retained native attempt.

`workspace-answers.local.json` is local-only selection state: exact generator and
bounded-answer SHA-256 values detect ordinary drift, not authenticity. Keep it
private with your existing configuration. Final setup revalidates inputs; saved
answers do not override missing files, stale receipts or source/hash mismatches.

## Advanced noninteractive setup

The following switches are available for scripting; the interactive wizard above
assembles them internally. Omit both framework options to keep only the saved website source and compiled dependency evidence; providing only one is refused. For the same-root layout, use the same path for both
root arguments.

From PowerShell 7 in the TraceMap checkout, substitute the actual private paths.
Put any path containing spaces in double quotes:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 workspace init-from-wizard `
  --root C:/reviews/Application `
  --wizard-root C:/reviews/Application `
  --framework-repo C:/source/GenericFramework `
  --framework-target src/GenericFramework.csproj `
  --sql-manifest C:/reviews/Application/configuration/sql-definitions.local.json `
  --sql-source-root C:/reviews/Application-sql-inputs
if ($LASTEXITCODE -ne 0) { throw 'Workspace setup failed; preserve the diagnostic' }
```

| Value | Where it comes from |
| --- | --- |
| `--root` | Private workspace folder outside Git; must not contain an existing `workspace.local.json` |
| `--wizard-root` | Saved wizard configuration root containing `root.config.json`; not a native run or publication folder |
| `--website-id` (optional) | Required if the wizard holds more than one website; otherwise its one saved website is used |
| `--framework-repo` (optional pair) | Additional source repository, not the dependency DLL path |
| `--framework-target` (optional pair) | Repository-relative project or solution; use the explicit word `discovery` only when discovery/fallback is intended |
| `--sql-manifest` | The local manifest already created for the offline SQL file(s) |
| `--sql-source-root` | Directory containing those loose `.sql` files; SQL needs no Git repo |

The command derives the web source root, commit and native scope from the wizard,
and the web repository ID from the SQL manifest's context. It uses `framework`
for the second repo ID; a conflicting SQL context ID is refused. No hashes need
to be retyped. SQL context commit must match the selected web source Git HEAD.
The manifest and SQL file hashes are rechecked, not rewritten to force a match.

The original wizard `native.config.json` may be an unprepared declaration.
After explicit attestation, its saved run owns
`evidence/review-config.local.json` plus receipts. The helper follows that exact
saved run, never chooses a newest folder, and checks that the prepared config
retains the original source/page/assembly selection. Preparation inputs and
metadata/publication receipt admission must still pass. A selected attempt with
valid prepared receipts can be used even if its later scan failed; no completion
claim is made or fabricated. An unprepared setup stops with a named diagnostic;
this command does not create a new attestation or silently re-prepare receipts.

It prints the scope preview before publishing configuration. Existing workspace
configs (including an empty V1 template), wizard files, receipts, failed attempts
and SQL inputs are preserved. If a workspace config already exists, choose a new
workspace folder; do not delete an old run just to use this command. Inputs are
referenced by absolute paths and must remain available. The generated config is
local-only and records exact CLI generator and bounded setup-input SHA-256 values
in `templateProvenance`; those hashes do not authenticate source correspondence.

## Next action

Inspect the printed scope and generated configuration. You may verify again:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 workspace verify --root C:/reviews/Application
```

When collection is intended, use the ordinary V3 command with a fresh output:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 workspace collect --root C:/reviews/Application --out C:/reviews/Application/runs/first
```

Collection currently performs fresh native/source collection; it does **not**
reuse the wizard's completed raw scan or import the separately completed lifecycle
root scans. Keep those outputs intact and use their existing handoffs separately.
This setup helper removes manual configuration copying, not that existing reuse
boundary. SQL remains a separate sidecar and calls are not automatically bound.
See [workspace configuration](WORKSPACE_CONFIGURATION.md) and
[SQL collection](WORKSPACE_SQL_GETTING_STARTED.md) for the retained limitations.

## Repeatable public terminal proof

From the TraceMap checkout, choose a fresh disposable folder:

```powershell
python scripts/tests/Test-WorkspaceWizardLauncher.py --output-root "C:/reviews/synthetic-wizard-proof"
```

Use `python3` on systems where Python is installed under that name. Add
`--with-source` in a second fresh folder to include the dependency source repo.
The replay authors public synthetic web/dependency source and loose SQL, builds
only that fixture, runs the actual PowerShell launcher/wizard using terminal
answers, pauses/resumes SQL setup, creates and verifies the workspace, chooses
collect in the wizard, and opens the combined, native and SQL SQLite outputs
read-only with `PRAGMA quick_check`. It checks SQL inventory and report paths.
Logs remain under the replay root. Existing roots are refused and preserved.
This is a managed synthetic static-evidence proof, not IIS/Windows publication,
application runtime, private input or migration acceptance.

### One-root SQL layout

The manifest does not belong inside the per-website folder. This layout is supported:

```text
Application/
  root.config.json
  sql-definitions.local.json    # files[].path = "sql/schema.sql"
  sql/schema.sql
  website/project.config.json
  website/workspace-answers.local.json
  workspace.local.json          # created after setup
  runs/                        # generated outputs
```

The manifest directory is the base, so `sql` is not added twice. The wizard shows
resolved paths and verifies hashes. Collection refuses outputs inside any selected
SQL file's directory or containing the manifest; sibling run folders are allowed.
To replay this layout locally, add `--sql-beside-workspace` to the synthetic replay
command above. The alternate layout with a separate SQL directory remains supported.

### Recover workspace setup without website repair

Workspace continuation reports its own named reason rather than replacing it with
`WEBFORMS_WIZARD_INPUT_OR_CONFIGURATION_INVALID`. If saved workspace answers are
invalid, malformed or refer to a prior native configuration, choose `restart` at
`[restart/later]` to archive only that answer file and re-enter workspace choices.
The archive is retained beside it; website project/configuration, receipts, SQL
inputs and native runs are preserved. An unchanged-byte check precedes archival.

If SQL validation fails, the wizard prints the reason and offers
`[change/retry/later]`: select another manifest, retry after correcting that file
locally, or pause. It does not require website repair or delete the old manifest.
No failed evidence is promoted. The synthetic replay options `--stale-answers-first`
and `--bad-manifest-first` exercise both recovery paths through completed SQLite.

For a persistent folder containing all generated inputs, configs, a frozen tool,
readable PowerShell scripts, completed SQLite outputs and measured size-limit
cases, use [the complete local lab](WORKSPACE_LOCAL_LAB.md).

### A refused input is not repaired by retry

For size-limit errors the wizard reports manifest/file/aggregate scope, path,
observed bytes and the admitted byte limit. It offers a different prepared manifest
or pause, not retry. Re-entering the same blocked manifest pauses instead of
repeating the question. Ordinary SQL limits remain 256 KiB/file and 1 MiB total;
larger originals need an explicit bounded definition-selection input, not silent
truncation or a budget raise. Other validation errors permit one requested retry
or change; if the same failure remains, setup pauses rather than looping. A retry
after a real correction can still succeed. Native setup/runs and failed inputs
are preserved. Replay `--size-limit-first --same-failure-first` covers these cases.

For ordinary exports larger than 256 KiB, the size-refusal prompt now offers
`stream`. It preserves the original and creates a versioned streaming manifest
with an 8 GiB source limit and independent bounded parser/evidence budgets. See
[streamed local definitions](SQL_STREAMED_DEFINITIONS.md) before interpreting that
limit as unrestricted SQL support.

## Add a missing framework source after workspace creation

Continue the SAME wizard/output root with the updated tool (`-Build` once).
The existing-workspace menu now shows the configured **source** count and offers
`add-framework`. A dependency DLL does not count as a source repository.

1. Choose `workspace`, then `add-framework`.
2. Give the framework's source Git checkout folder. If you select a subfolder,
   the wizard offers its detected repository root explicitly; it never silently
   uses a bin/publication folder as the source root.
3. Select its project relative to that root. Enter accepts the detected project
   when exactly one project exists, even if a solution also exists. A bad target
   repeats only that question; the valid folder is retained. Solution selection
   remains explicit; this change does not fix or retest the earlier sln failure.
4. Confirm `add`. The active workspace gains the source entry and an exact
   previous-config backup is retained. Existing website/native/DLL/SQL fields,
   source selections, original scope file and completed runs are preserved.
5. Choose `collect` to create a NEW run. Before collection the second source is
   **configured**, not yet scanned. Verify its source entry AND attributable
   implementation facts in the new run; review the new run, not the old one.

The signed initial wizard-answers file remains historical and is not rewritten.
For an existing workspace, the active `workspace.local.json` controls source
scope. An old cached skip value no longer hides the add-framework action.

CLI automation can use the same operation without interactive setup:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 -Build workspace add-source --root C:/tracemap-output --id framework --repo C:/source/framework --target Framework.csproj
```

Use actual paths. The command derives Git HEAD itself; no commit or DLL hash is
requested. It validates the new checkout/selection and configuration structure;
it does not scan, load MSBuild, reparse SQL or re-attest existing inputs. Full
admission still occurs at verify/collect. Duplicate IDs, overlapping roots,
bin/subfolder roots and invalid selections refuse without replacing the config.

A saved scope filter is extended through a new provenance-commented copy that
retains its previous selected IDs and adds the new source. The original scope
file remains unchanged. Explicit future `--scope` overrides still select exactly
what their owner specifies. Updated configuration provenance binds the prior
config, new source HEAD/selection and saved scope inputs to the exact generator.

Updates use a cooperative handle lock, input rechecks and atomic replacement
with an exact prior-config backup. Non-cooperating external edits can race the
last recheck; post-publication hash checks refuse a success claim if detected,
and complete versions remain available. Do not edit the config concurrently.
The empty `.workspace-update.lock` file may persist; its OS lock ends when the
process exits. Generated scope copies may remain after a failed update so an
I/O failure cannot leave an active configuration pointing to deleted input.

Adding source does not establish source-to-DLL correspondence, turn partial
analysis into semantic completeness, or change an earlier run's coverage.
