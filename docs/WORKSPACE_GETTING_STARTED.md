# Start a source collection workspace

Configure your web and framework source repositories once, verify the selection,
then collect and combine their evidence into a new run. This first workspace
walkthrough covers **source repositories**. To include an existing compiled
publication, use [the compiled workspace guide](WORKSPACE_COMPILED_GETTING_STARTED.md).
SQL definitions remain a separate evidence channel.

## 1. Prepare TraceMap

From the TraceMap checkout, with PowerShell 7 and the SDK required by the current
CLI project installed:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 -Build version
```

This publishes TraceMap itself in Release configuration to a fixed local tool
directory. The project determines the target framework; you do not construct a
`Debug/net10.0` path. It does not build your application. Run this command again
after changing or updating TraceMap. Subsequent examples use that published tool.

If you already have a matching installed `tracemap` command, use it in place of
`pwsh -NoProfile -File ./scripts/tm.ps1` throughout.

To try the complete path on disposable synthetic repositories before configuring
your application, run this after preparing the tool:

From macOS Terminal using zsh or bash, run this as one line:

```bash
pwsh -NoProfile -File ./scripts/tests/Test-WorkspaceLauncher.ps1 -OutputRoot "$HOME/tracemap-reviews/workspace-demo"
```

From a PowerShell prompt:

```powershell
pwsh -NoProfile -File ./scripts/tests/Test-WorkspaceLauncher.ps1 -OutputRoot (Join-Path $HOME 'tracemap-reviews/workspace-demo')
```

Choose a fresh folder. The replay creates two small Git repositories, runs the
commands below, checks the reports, then checks a one-repository filter. It prints
the resulting `START-HERE.md` path. Skip to that output to inspect the demo, or
continue below to configure your own sources.
If that folder already contains completed runs, open
`workspace/runs/first/START-HERE.md` there. Repeating the demo refuses the existing
folder to preserve the results; it does not mean the previous collection failed.

## 2. Initialize and edit

The application examples below use Windows paths. Substitute absolute paths for
your machine on macOS or Linux. Choose a workspace outside the source repositories
and published website:

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 workspace init --root C:/reviews/WebApp
```

Open `C:/reviews/WebApp/workspace.local.json`. Add the repositories you own and
want to scan. For example:

```json
{
  "schemaVersion": "tracemap-workspace.v1",
  "repositories": [
    { "id": "web", "path": "C:/source/WebApp", "projects": [] },
    { "id": "framework", "path": "C:/source/GenericFramework", "projects": [] }
  ],
  "scopeFile": null,
  "webFormsReport": true
}
```

Replace the paths. An empty `projects` array uses the scanner's discovery and
fallback behavior; select projects or a solution explicitly when the scope needs
it. `webFormsReport: true` requires exactly one selected repository containing
recognized Web Forms page facts. Use `false` for ordinary source collections.
See the [configuration reference](WORKSPACE_CONFIGURATION.md) for selection.

Init refuses to overwrite an existing configuration. The generated template's
optional `templateProvenance` describes template creation, not later edits; it
may be preserved or omitted. Collection hashes the actual edited configuration.

## 3. Verify, then collect

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 workspace verify --root C:/reviews/WebApp
pwsh -NoProfile -File ./scripts/tm.ps1 workspace collect --root C:/reviews/WebApp --out C:/reviews/WebApp/runs/first
```

Verification reads configuration, selected paths and Git HEADs. It does not scan,
restore, build, fetch or contact a database. Collection scans the selected source
repositories, combines their raw indexes, renders a dependency report and, when
selected, a Web Forms modernization packet. Existing scanner limitations and
project evaluation behavior still apply. Use trusted source repositories.

Open `C:/reviews/WebApp/runs/first/START-HERE.md`:

| Output | Read it for |
| --- | --- |
| `report.md` | Combined source/dependency summary |
| `sources/<id>/report.md` | Each source scan's coverage and gaps |
| `sources/<id>/index.sqlite` | Individual raw source evidence |
| `combined.sqlite` | Combined source evidence |
| `webforms/webforms-modernization.md` | Optional Web Forms packet |
| `workspace-run.local.json` | Exact tool/input fingerprints, source commits and stage status |

A successful command may report **partial** coverage. It does not imply a clean
build, full application coverage or proven runtime behavior. A failed stage stops
later stages and preserves diagnostics. Do not overwrite or delete the failed run
to retry; select a new output folder. This initial command does not resume or
automatically reuse old scans.

## Optional repository filter

Create `C:/reviews/WebApp/repositories.txt`:

```text
# Repository IDs from workspace.local.json
web
framework
```

Set `"scopeFile": "repositories.txt"` in the configuration, or pass
`--scope C:/reviews/WebApp/repositories.txt` to both verify and collect. This is a
filter over configured repositories. Empty, duplicate and unknown IDs fail;
an empty file never means scan everything. All configured repositories are
selected when no filter is supplied.

## Add compiled publication or database evidence

Use the [compiled workspace guide](WORKSPACE_COMPILED_GETTING_STARTED.md) for
native fresh collection with framework source. Use the
[source, compiled-site and SQL collection runbook](SOURCE_COMPILED_SQL_COLLECTION_RUNBOOK.md)
to inventory those channels and the [native compiled workflow](WEBFORMS_NATIVE_WORKFLOW.md)
for its existing admission path. The historical
`webforms-compile-web-config.v1` config is not this workspace schema; migration
still needs investigation. Do not rename its schema or assume matching fields.
Native attachment also validates the retained source inventory; do not assume
every ordinary workspace scan already meets that separate admission contract.

All workspace inputs, outputs and hashes are local-only. Keep real paths, source
identities and database exports outside public documentation. This source path
does not replace or claim output parity with the legacy page-inspection workflows.

For the opt-in offline SQL sidecar and the combined synthetic trial, see
[SQL collection getting started](WORKSPACE_SQL_GETTING_STARTED.md).
