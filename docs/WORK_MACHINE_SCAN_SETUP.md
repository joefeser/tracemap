# Work-machine setup: two repositories, compiled ASP.NET and offline SQL

Use this as the single entry point for a new local collection. The supported V3
workspace collects two source repositories, a prepared compiled publication and
an independently admitted offline SQL definition set. It links their reports
from one `START-HERE.md`; it does not automatically prove every call between them.
All real configuration, inputs, outputs and fingerprints stay on the work machine.

This guide targets the workspace commands introduced in #938. Use one clean,
committed TraceMap revision throughout preparation and collection. Record its
actual SHA and tool version locally. Do not use the old lifecycle-only tool build
for this workspace feature. Application conversion and database execution are
separate tasks.

For the owner test, use branch `codex/work-scan-setup-guide`. Preserve dirty
work when switching; use a separate checkout if needed. Fetch the branch before
preparing the tool, then record its actual HEAD. A PR to dev follows owner
verification; promotion to main is a separate owner step.

## 1. Locate the inputs once

Fill out this checklist privately before running commands. Example paths below
are placeholders, not required folder names.

| Input | Establish locally |
| --- | --- |
| TraceMap checkout | Exact committed revision; PowerShell 7 and the SDK required by the CLI project |
| Web repository | Git root and HEAD, website source root, selected project/solution and page scope |
| Framework repository | Separate Git root and HEAD; explicit project or solution selection |
| Existing publication | Site root **containing** `bin`, selected primary/dependency DLLs, optional PDBs/maps, and correspondence receipts |
| Offline SQL | Original read-only schema/definition export, exact files and hashes, export scope and omissions; no rows or credentials |
| Workspace | New private folder outside source/publication/tool/SQL inputs |
| Existing lifecycle evidence | Preserve completed root-specific receipts, facts and annotations separately; no rerun needed for this setup |

A source commit is not proof that a particular publication came from it. Obtain
an honest owner declaration and existing receipts; do not infer correspondence
from filenames, timestamps or matching method names. Missing binding is a setup
blocker, not permission to invent hashes. Preserve all old successful/failed runs
and dirty files.

## 2. Prepare the tool and optionally check the public example

Run from the TraceMap checkout in **PowerShell 7**:

```powershell
git status --short
git rev-parse HEAD
pwsh -NoProfile -File ./scripts/tm.ps1 -Build version
if ($LASTEXITCODE -ne 0) { throw 'TraceMap preparation failed' }
```

This publishes TraceMap in Release; it does not build the application. Use this
launcher consistently so an unrelated installed `tracemap` cannot be selected.
After updating TraceMap, prepare again. No application restore/build is requested
by workspace collection; normal scanner project evaluation still applies.

Optional public rehearsal, using a new folder:

```powershell
pwsh -NoProfile -File ./scripts/tests/Test-WorkspaceLauncher.ps1 -WithSql -OutputRoot (Join-Path $HOME 'tracemap-reviews/work-setup-demo-01')
if ($LASTEXITCODE -ne 0) { throw 'Public rehearsal failed; preserve its output' }
```

This authors two tiny Git repositories, compiles toy managed assemblies and
collects SQL definitions. It also checks filtering. It does not test IIS, legacy
Windows ASP.NET compilation, the work inputs or arbitrary SQL dumps. If already
completed, read that output instead of repeating it.

## 3. Prepare the native compiled configuration

The workspace requires a prepared strict JSON native config:
`schemaVersion: webforms-compiled-review-config.v1`, `operation: fresh`.
An existing `attach` config, wizard root config or legacy compile config is not
interchangeable with it. Do not change schema labels to force compatibility.

If a prepared fresh config already exists, preserve it and use its actual path.
Otherwise follow [Native compiled Web Forms workflow](WEBFORMS_NATIVE_WORKFLOW.md)
and its linked configuration contract. The terminal wizard entry points are:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 webforms-review wizard --root C:/reviews/Application/native-setup
# After editing the requested forms list, continue the SAME setup:
pwsh -NoProfile -File ./scripts/tm.ps1 webforms-review wizard --root C:/reviews/Application/native-setup --continue
```

The wizard can progress into a native run; it is not a configuration-only preview.
Read each displayed action before accepting it. Select existing publication rather
than authorizing an application build unless that build is separately intended.
A selected-forms pause (exit 2) is expected; do not create another setup root.
Choose a bounded page scope first. Point publication at the site root, not `bin`.
Projectless publication requires `PrecompiledApp.config`. Attest exact source
correspondence only when the owner has established it. Keep the whole generated
configuration and receipts, including staged dependency copies. Use the selected
attempt's prepared `evidence/review-config.local.json`, printed as
`webFormsPreparedConfig`, as `nativeConfigPath` below. The project's initial
`native.config.json` (including a repair config) is not necessarily prepared;
`root.config.json` is wizard state, not a native receipt config. If the source is a subfolder of a
larger repo, use the exact source-root/commit convention required by the native
config; do not guess a workspace root that conflicts with it.

Detailed receipt migration/preparation lives in the native guide; this walkthrough
does not manufacture correspondence or silently rerun an old proof. A native run
performed during setup remains preserved; workspace fresh collection is a separate
run and currently does not reuse it. If source collection is too expensive or
native scope is uncertain, stop at setup/verification rather than expanding scope.

## 4. Prepare the bounded SQL manifest

Use [SQL collection getting started](WORKSPACE_SQL_GETTING_STARTED.md) for the
supported local-only definition-set schema. Initial limits: 32 UTF-8 `.sql` files,
256 KiB/file, 1 MiB total, 512 objects and 4,096 references, plus lexical limits.
Do not silently truncate, remove unsupported statements or change the original.
Larger install templates require the explicit whole-batch selection contract in
[SQL definition selection](SQL_DEFINITION_SELECTION.md), not manual cleanup.

Put the exact selected definitions in a dedicated directory outside the workspace,
for example `C:/reviews/Application-sql-inputs`. Record their real export scope.
For a supported single file, obtain the contextual application commit and digest:

```powershell
git -C C:/source/WebApp rev-parse HEAD
(Get-FileHash -LiteralPath C:/reviews/Application-sql-inputs/schema.sql -Algorithm SHA256).Hash.ToLowerInvariant()
```

Create the local `configuration` directory if absent (this is configuration
setup, not a scan). Create `C:/reviews/Application/configuration/sql-definitions.local.json` with
actual values replacing BOTH angle-bracket placeholders:

```json
{
  "version": "sqlserver-local-definitions-input/1",
  "visibility": "local-only",
  "contextRepository": "web",
  "contextCommitSha": "<web repository HEAD>",
  "definitionSet": "review-schema-export",
  "server": "ExampleServer",
  "database": "ExampleDatabase",
  "files": [ { "path": "schema.sql", "sha256": "<exact lowercase SHA-256>" } ]
}
```

Use the real local context labels, not the example server/database. `USE`, if
present, must match the manifest database. The context commit associates the
collection with the web source; it is not a SQL deployment revision. Procedure
results with filters, joins, dynamic SQL or other unsupported constructs remain
unknown. Setup statements or oversize dumps can refuse admission. A work export
must never be marked `publicSynthetic: true`. Do not use the older synthetic DDL
contract producer as a private importer. No SQL connection or execution is needed.

## 5. Configure all inputs in one V3 workspace

**If the wizard is already complete and the SQL manifest exists, continue that
same wizard in your one existing root.** Update/build the tool from branch
`codex/workspace-scope-preflight`, then run:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 -Build version
pwsh -NoProfile -File ./scripts/tm.ps1 webforms-review wizard --root "C:/reviews/Application" --continue
```

Choose `workspace`. Existing website/source/DLL selections and prepared receipts
are reused. Additional dependency source is optional: type `skip` if you only
have the DLL or do not want another source checkout scanned. This retains the
existing compiled DLL selection and continues to the SQL manifest/folder, saving
each answer. `later` pauses; the same command resumes;
`change` corrects one choice; `create` validates and creates the workspace in that
same root. It then offers `collect` to create the reports and SQLite evidence, or
`later` to finish configuration only. Collection prints the exact output paths;
SQLite files are under the new run folder, not beside workspace.local.json.
An existing workspace offers `verify`, `collect`, or `later`. No second wizard
root or long switch list is needed.
See [interactive continuation](https://github.com/joefeser/tracemap/blob/codex/workspace-scope-preflight/docs/WORKSPACE_FROM_WIZARD.md).
Keep this docs branch for the walkthrough; no PR is implied.

The manual fallback below requires the **prepared receipt config**. The wizard's
initial project `native.config.json` may not contain receipts and is not
interchangeable with the prepared config printed as `webFormsPreparedConfig`.
Do not retype hashes or edit receipts to make the initial file appear prepared.

Initialize once (init refuses an existing config):

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 workspace init --root C:/reviews/Application
if ($LASTEXITCODE -ne 0) { throw 'Workspace initialization failed' }
```

If the folder was already initialized, edit its existing config instead. Replace
`workspace.local.json` with the following structure, substituting actual paths:

```json
{
  "schemaVersion": "tracemap-workspace.v3",
  "repositories": [
    { "id": "web", "path": "C:/source/WebApp", "projects": [] },
    { "id": "framework", "path": "C:/source/GenericFramework", "projects": ["src/GenericFramework.csproj"] }
  ],
  "scopeFile": null,
  "webFormsReport": true,
  "compiledReview": {
    "repositoryId": "web",
    "nativeConfigPath": "native-setup/runs/website-id-attempt/evidence/review-config.local.json"
  },
  "sqlEvidence": {
    "repositoryId": "web",
    "manifestPath": "configuration/sql-definitions.local.json",
    "sourceRoot": "C:/reviews/Application-sql-inputs"
  }
}
```

The web path must match the native config's source root and commit. Native config
owns web project, folder and page selection; keep workspace web `projects: []`
and do not add competing web solution/glob/target overrides. For framework source,
use the actual repository-relative projects or one `solution` with `projects: []`;
do not set both. Workspace-relative paths resolve from the workspace root. SQL
file paths resolve from `sqlEvidence.sourceRoot`. This folder must not be the
workspace configuration directory. Keep outputs outside all selected inputs.
See [Workspace configuration](WORKSPACE_CONFIGURATION.md) for the strict fields.

## 6. Verify, then collect once

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 workspace verify --root C:/reviews/Application
if ($LASTEXITCODE -ne 0) { throw 'Verification failed; fix only the named input problem before collecting' }
pwsh -NoProfile -File ./scripts/tm.ps1 workspace collect --root C:/reviews/Application --out C:/reviews/Application/runs/first
if ($LASTEXITCODE -ne 0) { throw 'Collection failed; preserve this run and report its stage' }
```

Verification is read-only and checks the selected repositories, native inputs
and SQL admission. It does not launch collection. Collect rechecks those inputs,
performs native fresh source/compiled review plus framework scanning, combines the
raw scan indexes, and writes SQL as a separate sidecar. No application launch,
build/publish orchestration or database connection is requested by collect.
A successful collection may still be `partial`; read coverage/build/gap fields.

The workspace is a source-plus-compiled workflow, **not** the compiled-only
lifecycle scan. The 120-second/2048-MiB Windows scan guard used for selected
lifecycle roots does not automatically wrap this workspace command. Do not claim
those caps for workspace collection, pass workspace arguments to that scan-only
guard, or add invented workspace budget flags. If the intended source scope is
not manageable, preserve diagnostics and stop; do not repeat a broad collection.

## 7. Open and hand off the outputs

Start at `C:/reviews/Application/runs/first/START-HERE.md`.

| Output | Meaning |
| --- | --- |
| `report.md`, `combined.sqlite` | Combined raw source/compiled evidence; not automatic end-to-end binding |
| Source reports and native workbench linked from start page | Per-channel coverage, exact native checkpoint-owned scan, source/publication gaps |
| `sql/report.md`, `sql/definitions.local.json`, `sql/sql-evidence.sqlite` | Local SQL inventory and partial results; SQL sidecar is not a raw scan index |
| `workspace-run.local.json` | Tool/input hashes, commits, stage/outcome and channel pointers |
| Existing root-specific lifecycle outputs | Separate completed method evidence, reused for code-flow understanding |

Never pass the SQL sidecar to `combine`, edit indexes to join calls, or treat
matching names as a proved source/compiled/SQL edge. Source-to-procedure comparison
has a separate narrow profile: [SQL source binding](SQL_SOURCE_BINDING.md).
Compilation provenance, deployed schema and runtime behavior remain distinct.

Keep one local collection summary linking these artifacts and the existing
lifecycle handoff. Mark each connection proven, modeled/conditional or unknown,
with rule/fact identifiers and provenance. A new lifecycle extraction, if separately
needed later, uses one module-bound root per guarded scan under the existing limits;
see [Budget diagnostics](validation/SQL_LIFECYCLE_BUDGET_DIAGNOSTICS.md).
Do not rescan the already completed roots merely to populate this summary.

## Work-machine test handoff

Test this branch before requesting a PR. Return a short sanitized report:
TraceMap revision/version; setup/verify status; collection attempted yes/no;
last stage and outcome; whether both repos, compiled publication and SQL report
are linked; retained gaps and the first concrete setup failure if any. Report
resource observations only if measured. Keep private names, paths, hashes, source,
SQL and full receipts local. Do not repair code, launch additional runs, change
budgets, open a PR or promote to main as part of this test. Preserve failed output.
The owner reviews this result before a PR to dev; main promotion is a later step.
