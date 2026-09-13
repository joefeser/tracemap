# VB.NET Web Forms battle test

This is the copy-and-run path for testing TraceMap against an authorized legacy
VB.NET Web Forms application. Run every command from the TraceMap repository
root in PowerShell 7. Replace only the values in the first block.

Keep all scan, packet, corpus, workbench, and source-bearing review outputs on
the authorized machine. Only sanitized console summaries and generated
`*.shareable.html` / `*.shareable.json` files are intended to leave it.

## 1. Set the application-specific values

```powershell
$SourceRoot = 'C:\path\to\application-repository'
$WebFormsFolder = '.'
$BackendFolder = '.'
$ControlsFolder = '.'
$SolutionRelativePath = 'Application.sln'
$OutputRoot = 'C:\work\tracemap-output'
```

The three folder values are relative to `$SourceRoot`; they may all be `.` when
the application is stored in one directory. The solution path is also relative
to `$SourceRoot`. The source checkout and TraceMap checkout must both be clean
because the retained commit and source snapshot are part of the evidence
provenance.

Check which legacy project shape is present:

```powershell
$ProjectFiles = @(Get-ChildItem -LiteralPath $SourceRoot -Recurse -File -Include '*.vbproj','*.csproj')
$ProjectFiles | Select-Object FullName
```

If this prints a `.vbproj`, keep `$SolutionRelativePath` and use the normal
solution command below. If it prints nothing, the `.sln` is likely an old
ASP.NET Web Site container rather than a buildable Web Application project. Use
the explicitly projectless command instead; TraceMap will retain syntax and
structural evidence and label the missing semantic compilation as reduced
coverage.

## 2. Build TraceMap and run one full focused scan

```powershell
git status --short
dotnet build .\src\dotnet\TraceMap.sln

.\scripts\Invoke-FocusedWebFormsReview.ps1 `
  -SourceRoot $SourceRoot `
  -WebFormsFolder $WebFormsFolder `
  -BackendFolder $BackendFolder `
  -ControlsFolder $ControlsFolder `
  -SolutionRelativePath $SolutionRelativePath `
  -TimeoutSeconds 14400
```

For an old Web Site solution with no `.vbproj` or `.csproj`, run this variant
instead. Do not pass the `.sln` in projectless mode:

```powershell
.\scripts\Invoke-FocusedWebFormsReview.ps1 `
  -SourceRoot $SourceRoot `
  -WebFormsFolder $WebFormsFolder `
  -BackendFolder $BackendFolder `
  -ControlsFolder $ControlsFolder `
  -Projectless `
  -TimeoutSeconds 14400
```

The collector writes beneath `C:\work\tracemap-output` and prints the retained
folder name. After it completes, select that exact folder rather than combining
artifacts from different runs:

```powershell
$ScanRoot = Get-ChildItem 'C:\work\tracemap-output' -Directory -Filter 'focused-webforms-*' |
  Sort-Object LastWriteTimeUtc -Descending |
  Select-Object -First 1 -ExpandProperty FullName
$IndexPath = Join-Path $ScanRoot 'scan\index.sqlite'
Test-Path $IndexPath
```

`Test-Path` must print `True`. A reduced or partial scan is still useful; do not
reinterpret it as complete coverage.

If the scan completed but a later summary step failed, pull the fix and resume
the latest retained scan without rerunning extraction:

```powershell
.\scripts\Resume-FocusedWebFormsReview.ps1
```

The resume command requires retained `scan/facts.ndjson` and
`scan/scan-manifest.json`. It writes fresh summaries only; it does not modify or
delete the retained scan.

If the scan reports a retained VB fallback phase gap, inspect it locally with:

```powershell
Get-Content (Join-Path $ScanRoot 'scan\facts.ndjson') |
  ForEach-Object { $_ | ConvertFrom-Json } |
  Where-Object { $_.properties.gapKind -eq 'VisualBasicSyntaxFallbackPhaseFailed' } |
  Select-Object @{n='file';e={$_.evidence.filePath}}, `
                @{n='phase';e={$_.properties.phase}}, `
                @{n='category';e={$_.properties.failureCategory}}, `
                @{n='typeHash';e={$_.properties.failureTypeHash}}
```

The file path is private. The phase, category, and type hash are sanitized and
are sufficient to distinguish repeated failure shapes without retaining the
exception message or source text.

## 3. Generate a packet for every ASPX page

This discovers `.aspx` files under the selected Web Forms folder and writes the
ignored three-value local configuration without hand-editing JSON:

```powershell
$WebFormsRoot = Join-Path $SourceRoot $WebFormsFolder
$Forms = @(
  Get-ChildItem -LiteralPath $WebFormsRoot -Recurse -File -Filter '*.aspx' |
    ForEach-Object {
      [IO.Path]::GetRelativePath($SourceRoot, $_.FullName).Replace('\', '/')
    } |
    Sort-Object -Unique
)

[ordered]@{
  indexPath = $IndexPath
  outputRoot = $OutputRoot
  forms = $Forms
} | ConvertTo-Json -Depth 4 |
  Set-Content -LiteralPath .\scripts\Run-FocusedWebFormsPageList.json -Encoding utf8

"aspx-pages=$($Forms.Count)"
.\scripts\Run-AndTriage-FocusedWebFormsPageList.ps1
```

Select the exact packet emitted by that command:

```powershell
$PacketPath = Get-ChildItem $OutputRoot -Recurse -File -Filter 'webforms-modernization.json' |
  Where-Object FullName -Match '[\\/]webforms-page-list-[^\\/]+[\\/]webforms-modernization\.json$' |
  Sort-Object LastWriteTimeUtc -Descending |
  Select-Object -First 1 -ExpandProperty FullName
$PacketPath
```

## 4. Export the deterministic agent/RAG evidence corpus

```powershell
$EvidenceDocsRoot = Join-Path $OutputRoot ("evidence-docs-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))

dotnet run --project .\src\dotnet\TraceMap.Cli\TraceMap.Cli.csproj -- docs-export `
  --index $IndexPath `
  --webforms-packet $PacketPath `
  --families webforms-modernization,gap,limitation `
  --out $EvidenceDocsRoot `
  --format markdown,jsonl

Get-Item (Join-Path $EvidenceDocsRoot 'manifest.json'), `
         (Join-Path $EvidenceDocsRoot 'query-recipes.json'), `
         (Join-Path $EvidenceDocsRoot 'chunks.jsonl')
```

Keep the complete corpus. `chunks.jsonl` is expected to be large and is the
agent retrieval layer, not a disposable intermediate file.

## 5. Generate the complete private application workbench

```powershell
.\scripts\New-FocusedWebFormsApplicationWorkbench.ps1 `
  -PacketPath $PacketPath `
  -OutputRoot $OutputRoot `
  -EvidenceDocsRoot $EvidenceDocsRoot `
  -SourceRoot $SourceRoot `
  -IncludeRawSource `
  -SourceContextLines 8
```

Open the printed `webforms-application-workbench-*\index.html`. This is the
complete selected-page view and is private because raw source was requested.

## 6. Generate the supplemental exception review set

```powershell
.\scripts\New-FocusedWebFormsBatchInspection.ps1

.\scripts\New-FocusedWebFormsCodePathReviewSet.ps1 `
  -SourceRoot $SourceRoot `
  -IndexPath $IndexPath `
  -EvidenceDocsRoot $EvidenceDocsRoot `
  -TriggerContextLines 50 `
  -IncludeRawSource
```

The opened review-set `index.html` is private. Its adjacent
`case-NNN.shareable.html` and `case-NNN.shareable.json` files are the anonymous
case artifacts that may be reviewed outside the authorized machine.

## What to capture from the first run

- Whether the full scan completed, reduced, timed out, or failed.
- The sanitized workspace, evidence, accuracy, progress, and performance
  summaries printed by the collector.
- ASPX page count, matched/unmatched/ambiguous page counts, and retained gap
  categories printed by the page-list workflow.
- The anonymous `case-NNN.shareable.*` files for representative failures.
- Any categorical error code exactly as printed; do not substitute private
  paths, symbols, SQL, or source text.

The initial run is diagnostic. Do not manually label missing evidence as absent
runtime behavior, and do not delete the retained scan or corpus while follow-up
queries use them.
