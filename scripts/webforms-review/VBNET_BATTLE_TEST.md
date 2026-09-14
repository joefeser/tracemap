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

Check which legacy project shape is present. Search only the three configured
scope folders; do not crawl unrelated projects elsewhere under `$SourceRoot`:

```powershell
$ScopeFolders = @($WebFormsFolder, $BackendFolder, $ControlsFolder) |
  Sort-Object -Unique

$ProjectFiles = @(
  foreach ($Folder in $ScopeFolders) {
    Get-ChildItem -LiteralPath (Join-Path $SourceRoot $Folder) -Recurse -File |
      Where-Object Extension -In '.vbproj', '.csproj'
  }
) | Sort-Object FullName -Unique

$ProjectRelativePath = @(
  $ProjectFiles | ForEach-Object {
    [IO.Path]::GetRelativePath($SourceRoot, $_.FullName).Replace('\', '/')
  }
)
$ProjectRelativePath
```

If this prints a `.vbproj`, keep `$SolutionRelativePath` and use the normal
solution command below. If it prints nothing, the `.sln` is likely an old
ASP.NET Web Site container rather than a buildable Web Application project. Use
the explicitly projectless command instead; TraceMap will retain syntax and
structural evidence and label the missing semantic compilation as reduced
coverage.

For a repository with many unrelated solutions, pass the scoped
`$ProjectRelativePath` array directly instead of selecting one solution. Files
inside the three configured folders that are not owned by those projects remain
available to syntax and structural extraction:

```powershell
.\scripts\Invoke-FocusedWebFormsReview.ps1 `
  -SourceRoot $SourceRoot `
  -WebFormsFolder $WebFormsFolder `
  -BackendFolder $BackendFolder `
  -ControlsFolder $ControlsFolder `
  -ProjectRelativePath $ProjectRelativePath `
  -TimeoutSeconds 14400
```

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
The same folder contains `application-outliers.shareable.html` and
`application-outliers.shareable.json`. These alias-only projections rank bounded
review signals and omit retained paths, symbols, repository identifiers, packet
identifiers, scan identifiers, and commit SHAs. Use the private index to resolve a
`page-NNN` alias locally; the ordering is an inspection aid, not a migration-effort
or business-priority conclusion. The call columns distinguish retained projections,
unique evidence facts, and normalized source call sites. A separate signal identifies
chains at the 256-fact call-evidence ceiling; reaching that ceiling means additional
call evidence may be unavailable even when the packet cannot quantify an omitted
count.

Packets generated by the current build also retain compiler-resolved declaring type,
assembly, and an evidence-backed technology family (`application`, `framework`,
`telerik`, `third-party`, or `unresolved`) for call facts. The private page report
prefers semantic evidence and collapses a matching syntax fallback into the same
normalized site. Older packets can still generate a workbench and normalize by
retained path/span/name, but they cannot recover declaring-type or assembly metadata;
rerun step 3 from the existing index, then steps 4 and 5, to add that evidence. A new
source scan is not required.

## 6. Generate the supplemental exception review set

This exact-semantic review is supplemental. In projectless mode it may report
`not-applicable;reason=no-semantic-handler-cases`; that is a successful bounded
result, not a failure of the primary application workbench. The subsequent
review-set command will also exit cleanly and will not reuse an older
inspection.

```powershell
.\scripts\New-FocusedWebFormsBatchInspection.ps1

.\scripts\New-FocusedWebFormsCodePathReviewSet.ps1 `
  -SourceRoot $SourceRoot `
  -IndexPath $IndexPath `
  -EvidenceDocsRoot $EvidenceDocsRoot `
  -TriggerContextLines 10 `
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

## September 13, 2026 discovery notes

The first 20-page projectless Web Site review established these concrete mixed
Web Forms shapes:

- A page can combine ASPX markup, VB code-behind, dynamically compiled
  `App_Code` declarations, and inline jQuery in one repository. Folder switches
  are workflow scope only and must not define relationship semantics.
- One observed inline Save click shows a loader, hides an error, disables a
  Cancel control, and changes Save presentation before the normal Web Forms
  postback. A keyup callback maintains a 50-character remaining count.
- The same page contains inline ASP.NET expressions that reference types under
  `App_Code/Controls`. Positive resolution must require one exact repository
  declaration and must remain static syntax evidence.
- Two radio controls legitimately bind to the same checked-change handler. The
  two trigger chains remain distinct while the handler's control-state behavior
  is inventoried once.
- Observed VB server behavior includes `Response.Redirect(..., False)` followed
  by `CompleteRequest()`, Page_Load control initialization, and conditional
  control visibility/text/checked-state changes.

The follow-up implementation joins supported jQuery client events through a
unique retained Web Forms binding to its VB handler, joins bounded inline server
expressions to a unique repository or `App_Code` type declaration, adds a
fact-count-based behavior summary, and distinguishes zero boundaries caused by
no retained chains, partial coverage, and a completed bounded search. These are
review aids, not runtime or migration-correctness claims.

The next observed surface contained literal jQuery `$.ajax` POST requests to
repository `.ashx` endpoints, request-verification-token forwarding, and
success/error/complete callbacks. It also contained a hard-coded generated-ID
selector for `SaveBidGroup` with no control, HTML element, or handler declaration
anywhere in the scanned project; the only second textual occurrence was in an
excluded copy. Treat that selector as `no-static-target-declared`, which predicts
an empty jQuery selection and silent binding no-op for the retained static DOM,
but does not claim runtime dead code because dynamic injection remains unproven.

The next slice joins each uniquely resolved AJAX `.ashx` request through its
`WebHandler Class` directive to one retained `ProcessRequest` source declaration.
That handler becomes a bounded legacy-flow root, allowing the application
workbench to display retained calls and downstream boundaries from the HTTP
entry point. Missing directives, source declarations, or unique entry methods
remain explicit Tier4 gaps; deployment routing and runtime execution are not
claimed.

The retained application also uses the ASP.NET Web Site single-file handler
form: a `.ashx` file contains its `WebHandler` directive, imports, handler class,
and VB `ProcessRequest` body without `CodeFile` or `CodeBehind`. This form must
resolve directly from the `.ashx` source. Calls within that entry method are
bounded syntax evidence so reviewers can follow anti-forgery, shared controller
method calls, data loading, serialization, and response-writing candidates
without claiming receiver identity, branch execution, or runtime request
success. Property reads and writes remain outside this inline-handler call slice.

The application workbench separates handler-unavailable, downstream-without-a-
supported-terminal, no-observed-downstream, and truncated chain outcomes instead
of presenting them as one unexplained gap count. Each chain can also expand its
bounded retained call rows with safe callee names and exact evidence spans; call
arguments and source snippets remain omitted unless the existing private raw-
source option is explicitly enabled. Page and application summaries distinguish
chain-associated call projections, unique retained call facts, and normalized source
call sites so multiple event paths and matching syntax/semantic facts do not look like
distinct source operations. Site normalization uses the packet's deterministic site
identity when present and a path/span/callee fallback for older packets. It can still
merge indistinguishable same-name calls on one retained line when column-level source
identity is unavailable. Compiler-resolved call facts may expose declaring type,
assembly, and a bounded technology-family classification; syntax-only calls remain
`unresolved`. Chains at the 256-fact call-evidence ceiling are reported separately
from traversal truncation and do not establish that exactly 256 calls exist.
