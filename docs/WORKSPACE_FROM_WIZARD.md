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

1. The framework **source repository folder** (the dependency DLL does not identify this folder).
2. Its project/solution: choose a displayed number, paste a path inside the repo,
   or explicitly choose `discovery`. The preview lists root-level files only.
3. The existing SQL manifest file from step 4.
4. The folder containing the loose SQL files; this folder needs no Git repo.

Each answer is saved immediately in the existing website folder. Type `later` to
pause; run the same command and choose `workspace` to resume without re-entering
saved choices. At the summary, `change` corrects one choice and `create` validates
and creates `workspace.local.json` in **that same root**. An existing workspace is
preserved and offered verification. Setup does not start a scan or rerun native
reports. Choose `reports` separately to verify/resume the retained native attempt.

`workspace-answers.local.json` is local-only selection state: exact generator and
bounded-answer SHA-256 values detect ordinary drift, not authenticity. Keep it
private with your existing configuration. Final setup revalidates inputs; saved
answers do not override missing files, stale receipts or source/hash mismatches.

## Advanced noninteractive setup

The following switches are available for scripting; the interactive wizard above
assembles them internally. For the same-root layout, use the same path for both
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
| `--framework-repo` | Second source repository, not the framework DLL path |
| `--framework-target` | Repository-relative project or solution; use the explicit word `discovery` only when discovery/fallback is intended |
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
