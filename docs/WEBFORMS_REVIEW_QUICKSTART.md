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

## Run the four retained stages

### 1. Scan the bounded source

From the TraceMap repository root:

```powershell
.\scripts\Invoke-FocusedWebFormsReview.ps1 `
  -SourceRoot C:\path\to\authorized-source `
  -WebFormsFolder Web `
  -BackendFolder Backend `
  -ControlsFolder SharedControls `
  -SolutionRelativePath Application.sln
```

For a projectless Web Site, omit the solution/project parameters and add
`-Projectless`. If the solution name or path is wrong, stop and correct it; do
not treat `SOLUTION_SCOPE_UNAVAILABLE` as a clean or complete result.

Keep the resulting `focused-webforms-<scan>\scan` folder. Its `index.sqlite`,
manifest, facts, report, and analyzer log are the retained source of truth.

### 2. Build one selected-page packet

Create the ignored workstation configuration once:

```powershell
Copy-Item .\scripts\Run-FocusedWebFormsPageList.example.json .\scripts\Run-FocusedWebFormsPageList.json
notepad .\scripts\Run-FocusedWebFormsPageList.json
.\scripts\Run-AndTriage-FocusedWebFormsPageList.ps1
```

Set `indexPath`, `outputRoot`, and `forms`. Keep the exact printed
`webforms-modernization.json` path; do not guess which timestamped folder is
newest.

### 3. Export the evidence corpus

```powershell
dotnet run --project .\src\dotnet\TraceMap.Cli\TraceMap.Cli.csproj -- docs-export `
  --index C:\work\tracemap-output\focused-webforms-<scan>\scan\index.sqlite `
  --webforms-packet C:\work\tracemap-output\webforms-page-list-<packet>\webforms-modernization.json `
  --families webforms-modernization,gap,limitation `
  --out C:\work\tracemap-output\evidence-docs-<run> `
  --format markdown,jsonl
```

Keep `manifest.json`, `query-recipes.json`, and `chunks.jsonl` together. A large
`chunks.jsonl` is expected. The manifest binds the corpus to its inputs and
records generated-output hashes.

### 4. Generate the application workbench

```powershell
.\scripts\New-FocusedWebFormsApplicationWorkbench.ps1 `
  -PacketPath C:\work\tracemap-output\webforms-page-list-<packet>\webforms-modernization.json `
  -OutputRoot C:\work\tracemap-output `
  -EvidenceDocsRoot C:\work\tracemap-output\evidence-docs-<run>
```

Use `-IncludeRawSource -SourceRoot ...` only on an authorized private machine.
Open the exact `applicationWorkbenchIndex` printed by the command.

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
