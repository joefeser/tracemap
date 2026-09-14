# Web Forms Review Quickstart

This is the shortest supported path from an authorized ASP.NET Web Forms
checkout to a private application workbench and an optional evidence handoff.
It applies to C# and VB.NET applications, including old Web Site projects that
do not have a usable `.csproj` or `.vbproj`.

TraceMap produces deterministic static evidence. It does not execute the
application, infer business intent, predict migration effort, or approve a
conversion.

## Before you start

Keep the application source and all generated private artifacts on an
authorized machine. Use a clean, committed source checkout when practical so
the recorded repository and commit SHA identify the reviewed state.

Identify three bounded roots:

- the Web Forms pages;
- the backend or shared application code; and
- shared server controls.

These roots may be the same folder. They may each contain multiple projects.
Solution and project discovery must remain inside those three roots, except for
an explicitly named solution file. A projectless Web Site is a supported
reduced-coverage input; it is not a failed scan.

## Create one review root and run the pipeline

From the TraceMap repository root, create an empty private review root:

```powershell
$ReviewRoot = 'C:\work\webforms-review'
.\scripts\Initialize-FocusedWebFormsReview.ps1 -ReviewRoot $ReviewRoot
notepad (Join-Path $ReviewRoot 'config\webforms-review.json')
```

The config has seven operational settings: source root, Web Forms folder,
backend folder, controls folder, project selection, output root, and page
selection. Leave `outputRoot` equal to `$ReviewRoot`.

Use forward slashes in JSON paths, including on Windows: `C:/source/application`.
A path such as `C:\source\application` contains unescaped JSON backslashes and
is rejected before parsing with corrective guidance. A doubled backslash is
valid JSON, but forward slashes are easier to read and edit safely.

Project selection modes:

- `solution`: one explicit `.sln`; only projects beneath the three configured
  folders are admitted;
- `projects`: an explicit JSON array of `.csproj`/`.vbproj` paths beneath those
  folders;
- `discover`: find all `.csproj`/`.vbproj` files beneath only those folders; and
- `projectless`: retain syntax/structural Web Site evidence without pretending
  semantic compilation was available.

Page selection mode `all` retains every discovered Web Forms surface up to the
documented 1,000-surface bound. Mode `selected` uses the explicit `forms` array.

After editing the config, run one command:

```powershell
.\scripts\Invoke-FocusedWebFormsPipeline.ps1 -ReviewRoot $ReviewRoot
```

After the pipeline completes, print the receipt-validated alias-only totals
with one short command:

```powershell
.\scripts\Show-FocusedWebFormsOutlierSummary.ps1 -ReviewRoot $ReviewRoot
```

The summary verifies the completed run receipt and the recorded outlier-file
size and SHA-256 before reading it. It prints page, call, and gap totals; gap
classifications; and the generator/input hashes. It never selects a folder by
timestamp and does not print private page identities.

The pipeline uses one run ID and writes exact paths and hashes to
`run-receipt.json`. Rerunning the same command validates and reuses completed
stages. It never selects an artifact by “newest timestamp.” A changed config,
source commit, TraceMap commit, generator, or completed artifact fails resume
validation rather than silently mixing runs.

Wrong solution and project paths report the requested value and up to ten
bounded candidates. Project candidates are searched only beneath the three
configured folders.

## First-run recovery

The project modes are a deliberate progression, not interchangeable labels:

- `SOLUTION_SCOPE_HAS_NO_IN_SCOPE_PROJECTS` means the solution was readable,
  but none of its C# or VB.NET projects were beneath the three configured
  folders. Correct the solution/folder relationship or try `discover`.
- `PROJECT_DISCOVERY_EMPTY` means no `.csproj` or `.vbproj` was found beneath
  those folders. Use `projectless`; this retains reduced syntax and structural
  evidence for an old Web Site without claiming semantic compilation.
- `WEBFORMS_PIPELINE_RESUME_PROVENANCE_MISMATCH` after editing a failed
  first-run config means the receipt still identifies the previous config.

When the failure occurred before the `scan` stage completed and no `scan/`,
`packet/`, `evidence-docs/`, or `workbench/` directory exists, remove only the
failed receipt and rerun:

```powershell
Remove-Item (Join-Path $ReviewRoot 'run-receipt.json')
.\scripts\Invoke-FocusedWebFormsPipeline.ps1 -ReviewRoot $ReviewRoot
```

Do not remove the receipt from a run with completed retained stages. Restore
the original config to resume it, or initialize a different empty review root
for the changed config.

## One-root artifact map

| Path | Retention | Purpose |
| --- | --- | --- |
| `config/` | Keep | Editable seven-setting input and optional selected-page list. |
| `run-receipt.json` | Keep | Run identity, commits, generator/config hashes, exact stage paths, artifact hashes, and state. |
| `scan/` | Keep | Required scan manifest, facts, index, report, and analyzer log. |
| `packet/` | Keep | Complete Web Forms modernization JSON and Markdown packet. |
| `evidence-docs/` | Keep | Manifest, closed query recipes, `chunks.jsonl`, and rendered evidence docs. |
| `workbench/` | Keep | Private application/page review and explicitly named shareable outlier files. |
| `logs/` | Diagnostic | Progress, local-review receipt, and bounded summaries. Archive or delete only after accepting the retained run. |

Do not delete individual required folders, mix them with another review root,
or copy the whole root outside the authorized environment. Only files explicitly
named `*.shareable.html` or `*.shareable.json` are intended for identity-free
exchange, and they should still be inspected before sending.

## Manual compatibility workflow

The older individual scripts remain supported for diagnostics and recovery.
Use the [full focused review reference](../scripts/webforms-review/README.md)
when a particular stage must be run manually. The single pipeline is the
default for a new clean run.

## Read the call accounting correctly

`P / F / S` means:

- `P`: chain-associated call projections;
- `F`: unique retained call facts; and
- `S`: normalized source call sites.

`P` can exceed `F` when more than one chain projects the same retained fact.
`F` can exceed `S` when syntax and semantic evidence describe the same source
site. None of these values is a runtime invocation count.

The workbench separately identifies evidence ceilings, explicitly omitted
projections, unresolved handlers, incomplete chains, and coverage gaps. A
ceiling means additional static evidence may be unavailable; it does not prove
that a method contains exactly the ceiling count.

`Handler unavailable` means an event source was retained but the packet did not
contain a usable handler fact and source span from which to continue the chain.
It is an evidence limitation, not proof that the application has no handler or
that the control is broken. `Recorded gap facts` is a separate count of explicit
gap records associated with the page.

New packets retain a bounded private control display projection: markup control
ID, control type, stable control identity, and supporting fact ID. The private
application index displays the full retained route and all retained control
names/types beneath each aliased page row. Shareable outlier artifacts continue
to contain aliases and counts only.

## Private and shareable outputs

The application workbench and page handoffs are private. They can contain paths,
symbols, scan identity, commit identity, and optional raw source.

Only outputs explicitly named `*.shareable.html` or `*.shareable.json` are
designed for identity-free exchange. Inspect them before sending. Shareable
provenance hashes the sanitized alias/count projection rather than the private
packet, avoiding a private packet fingerprint.

Every newly derived machine-readable artifact must record:

- the exact generator SHA-256; and
- a bounded SHA-256 of the input it actually used.

## What is optional

The WITS review overlay and exceptional-handler code-path review set are
supplemental. They do not replace the packet, evidence corpus, or full
application workbench. Use the [full focused review
reference](../scripts/webforms-review/README.md) for those steps, detailed
artifact contracts, limits, and recovery procedures.
