# Run the focused three-folder Web Forms review on Windows

## PowerShell-only execution (recommended)

No coding agent is required on the Windows validation machine. First fetch and
check out the intended TraceMap revision yourself; the launcher does not select
or update branches. From that clean TraceMap checkout, run:

```powershell
pwsh -File .\scripts\Invoke-FocusedWebFormsReview.ps1
```

The script asks for five bounded local values once, validates and builds the
current checkout at its existing HEAD, performs exactly one focused scan,
writes the retained output and diagnostic receipts, and creates sanitized
gap/extractor and accuracy-readiness summaries. The accuracy summary separates
compiler-resolved, structural, syntax-only, and unknown evidence across the
three selected folder roles and legacy artifact kinds without returning their
private names. It does not ask an agent for permission between commands and
does not retry a failed or partial scan. Pass the same values as named
parameters when fully unattended execution is preferred.

To summarize an already retained run without rescanning, use the same three
local folder values supplied to the review:

```powershell
pwsh -NoProfile -File .\scripts\Export-FocusedWebFormsAccuracySummary.ps1 `
    -ReviewOutputPath "C:\work\tracemap-output\<retained-focused-run>" `
    -WebFormsFolder $WebFormsFolder `
    -BackendFolder $BackendFolder `
    -ControlsFolder $ControlsFolder
```

Use this packet when one private Git repository contains exactly three relevant
application areas:

1. the Web Forms application, which may be a projectless Web Site;
2. the backend code;
3. the shared or custom Web Forms controls.

The values entered below stay local. Do not commit private repository names,
folder names, paths, project names, output, or results to TraceMap.

## Agent prompt

```text
Follow prompts/run-focused-webforms-one-repo-windows.md exactly.

This is one Git repository with exactly three in-scope folders: the Web Forms
application, its backend code, and its shared/custom Web Forms controls. Ask me
locally for the five values in the Local values section if they are not already
available. Do not search other drives or repository folders to infer them.

Run one new focused review. Do not retry automatically. Keep the complete
output local and private. After the run, follow
prompts/run-focused-webforms-monorepo-scan.md to inspect the retained output
and return only its compact sanitized result block.

Do not modify either repository, commit, push, publish, upload, use a hosted
service, or return private identifiers, paths, source, configuration, logs,
routes, symbols, hashes, commit SHAs, or raw evidence.
```

## Local values

Open PowerShell 7 and set these values. Repository-relative folder and project
paths must use forward slashes and must not begin with a slash.

```powershell
$TraceMapRoot = "C:\work\tracemap"
$SourceRoot = "C:\work\<private-source-repository>"

$WebFormsFolder = "<repository-relative-webforms-folder>"
$BackendFolder = "<repository-relative-backend-folder>"
$ControlsFolder = "<repository-relative-controls-folder>"

# Fill this after the bounded project discovery step. A projectless Web Forms
# folder intentionally contributes no project path.
$ProjectRelativePaths = @(
    "<repository-relative-backend-project.csproj>",
    "<repository-relative-controls-project.csproj>"
)
```

Do not add the repository's solution or projects outside the three selected
folders. Remove either placeholder project row when it does not exist. Add an
actual Web Forms project only when that project owns the Web Forms folder.

## 1. Select the current TraceMap branch

The TraceMap checkout must be clean. This uses a detached remote head so the
workstation does not create or rewrite a local branch.

```powershell
Set-Location $TraceMapRoot

if (git status --porcelain) {
    throw "TRACEMAP_WORKTREE_DIRTY"
}

git fetch origin dev
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_FETCH_FAILED" }

git switch --detach origin/dev
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_CHECKOUT_FAILED" }

git status --short --branch
dotnet build "$TraceMapRoot\src\dotnet\TraceMap.sln"
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_BUILD_FAILED" }
```

Do not return the TraceMap commit SHA. Its only purpose is local reproducibility.

## 2. Verify the single source repository and three-folder boundary

```powershell
$ResolvedSourceRoot = (Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd('\')
$GitRoot = (git -C $ResolvedSourceRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw "SOURCE_GIT_ROOT_UNAVAILABLE" }

$ResolvedGitRoot = (Resolve-Path -LiteralPath $GitRoot).Path.TrimEnd('\')
if ($ResolvedGitRoot -ne $ResolvedSourceRoot) {
    throw "SOURCE_ROOT_NOT_GIT_ROOT"
}

if (git -C $ResolvedSourceRoot status --porcelain) {
    throw "SOURCE_WORKTREE_DIRTY"
}

$SelectedFolders = @($WebFormsFolder, $BackendFolder, $ControlsFolder)
if (($SelectedFolders | Select-Object -Unique).Count -ne 3) {
    throw "THREE_FOLDER_SCOPE_INVALID"
}

foreach ($RelativeFolder in $SelectedFolders) {
    if ([IO.Path]::IsPathRooted($RelativeFolder) -or $RelativeFolder -match '(^|/|\\)\.\.($|/|\\)') {
        throw "THREE_FOLDER_SCOPE_UNSAFE"
    }

    $Candidate = [IO.Path]::GetFullPath((Join-Path $ResolvedSourceRoot $RelativeFolder))
    if (-not $Candidate.StartsWith($ResolvedSourceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "THREE_FOLDER_SCOPE_OUTSIDE_REPOSITORY"
    }
    if (-not (Test-Path -LiteralPath $Candidate -PathType Container)) {
        throw "THREE_FOLDER_SCOPE_UNAVAILABLE"
    }
}

"source-boundary=verified;selectedFolderCount=3;worktreeClean=true"
```

## 3. Discover projects only inside the three selected folders

This command prints candidates locally for owner review. It does not select
projects automatically.

```powershell
foreach ($RelativeFolder in $SelectedFolders) {
    $Folder = Join-Path $ResolvedSourceRoot $RelativeFolder
    Get-ChildItem -LiteralPath $Folder -Recurse -File -ErrorAction Stop |
        Where-Object { $_.Extension -in @('.csproj', '.vbproj') } |
        ForEach-Object {
            [IO.Path]::GetRelativePath($ResolvedSourceRoot, $_.FullName).Replace('\', '/')
        }
}
```

Review the printed candidates and update `$ProjectRelativePaths` in the Local
values block. Select only genuine application, backend, and controls projects.
Do not select test, sample, generated, migration-only, or unrelated projects.
A Web Forms folder containing `.aspx`, `.ascx`, `.master`, or `.ashx` files but
no owning project is a valid projectless Web Site; do not assign a neighboring
project as its owner.

Validate the selected project paths:

```powershell
foreach ($RelativeProject in $ProjectRelativePaths) {
    if ([IO.Path]::IsPathRooted($RelativeProject) -or $RelativeProject -match '(^|/|\\)\.\.($|/|\\)') {
        throw "PROJECT_SCOPE_UNSAFE"
    }

    $Project = [IO.Path]::GetFullPath((Join-Path $ResolvedSourceRoot $RelativeProject))
    if (-not $Project.StartsWith($ResolvedSourceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "PROJECT_SCOPE_OUTSIDE_REPOSITORY"
    }
    if (-not (Test-Path -LiteralPath $Project -PathType Leaf)) {
        throw "PROJECT_SCOPE_UNAVAILABLE"
    }

    $ProjectAllowed = $false
    foreach ($RelativeFolder in $SelectedFolders) {
        $Folder = [IO.Path]::GetFullPath((Join-Path $ResolvedSourceRoot $RelativeFolder)).TrimEnd('\')
        if ($Project.StartsWith($Folder + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $ProjectAllowed = $true
            break
        }
    }
    if (-not $ProjectAllowed) { throw "PROJECT_OUTSIDE_THREE_FOLDER_SCOPE" }
}

"project-boundary=verified;selectedProjectCount=$($ProjectRelativePaths.Count)"
```

## 4. Run one new focused review

The output and progress checkpoint must be outside both repositories. The
output directory must not exist before the command starts.

```powershell
$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$OutputParent = "C:\work\tracemap-output"
$ProgressParent = "C:\work\tracemap-progress"
$OutRoot = Join-Path $OutputParent "focused-webforms-$RunStamp"
$ProgressPath = Join-Path $ProgressParent "focused-webforms-$RunStamp.json"

New-Item -ItemType Directory -Path $OutputParent -Force | Out-Null
New-Item -ItemType Directory -Path $ProgressParent -Force | Out-Null

if (Test-Path -LiteralPath $OutRoot) { throw "OUTPUT_ALREADY_EXISTS" }
if (Test-Path -LiteralPath $ProgressPath) { throw "PROGRESS_ALREADY_EXISTS" }

$ReviewArguments = @(
    "run",
    "--repo", $ResolvedSourceRoot,
    "--out", $OutRoot
)

foreach ($RelativeFolder in $SelectedFolders) {
    $ReviewArguments += @("--include", ($RelativeFolder.TrimEnd('/', '\') + "/**"))
}

foreach ($RelativeProject in $ProjectRelativePaths) {
    $ReviewArguments += @("--project", $RelativeProject)
}

$Exclusions = @(
    ".vs/**",
    "**/bin/**",
    "**/obj/**",
    "**/node_modules/**",
    "**/dist/**",
    "**/coverage/**",
    "**/TestResults/**",
    "**/.angular/**",
    "**/.next/**"
)

foreach ($Pattern in $Exclusions) {
    $ReviewArguments += @("--exclude", $Pattern)
}

$ReviewArguments += @(
    "--webforms-modernization",
    "--diagnostic-progress", $ProgressPath,
    "--timeout-seconds", "7200"
)

Set-Location $TraceMapRoot
dotnet run --project "$TraceMapRoot\src\dotnet\TraceMap.Cli" -- local-review @ReviewArguments
$ReviewExitCode = $LASTEXITCODE

"focused-review-process-exit=$ReviewExitCode"
"output-retained=$([bool](Test-Path -LiteralPath $OutRoot))"
"progress-retained=$([bool](Test-Path -LiteralPath $ProgressPath))"
```

Do not rerun automatically when `$ReviewExitCode` is nonzero. A failed or
partial run may still contain the evidence needed to identify the next bounded
product fix.

## 5. Check a long-running review without interrupting it

Roslyn and MSBuild can spend a long time in project loading, compilation, or
source verification. Memory use alone does not prove a hang. In a second
PowerShell window, locate the newest sanitized checkpoint and print only its
categorical state:

```powershell
$ProgressPath = Get-ChildItem "C:\work\tracemap-progress\focused-webforms-*.json" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

$Progress = Get-Content $ProgressPath -Raw | ConvertFrom-Json

"sequence=$($Progress.latest.sequence);operation=$($Progress.latest.operation);stage=$($Progress.latest.stage);state=$($Progress.latest.state);elapsedMs=$($Progress.latest.elapsedMilliseconds);lastSuccessfulStage=$($Progress.latest.lastSuccessfulStage);ordinal=$($Progress.latest.ordinal)"
```

Inspect process activity without reading command lines or environment values:

```powershell
Get-Process dotnet,MSBuild,VBCSCompiler -ErrorAction SilentlyContinue |
    Select-Object Id, ProcessName, CPU,
        @{Name="MemoryMB"; Expression={ [math]::Round($_.WorkingSet64 / 1MB) }},
        StartTime, Responding
```

Interpret the observations conservatively:

- A checkpoint timestamp and sequence that advance about every 15 seconds mean
  the progress reporter is alive.
- CPU that increases between checks means the process is actively working.
- Repeated heartbeats at one stage mean the operation is slow inside that
  bounded stage; they do not prove a deadlock.
- A checkpoint that stops changing is a stronger stuck-process signal, but it
  is still not a scan conclusion.
- Allow the configured timeout to classify the run. Do not kill and retry
  automatically merely because the output directory is absent or one stage is
  slow.

If coordinator help is required, return only the single sanitized checkpoint
line. Do not return the progress file, process IDs, start time, paths, command
lines, source identities, or raw logs.

## 6. Return the three sanitized result blocks

Use `prompts/run-focused-webforms-monorepo-scan.md`, beginning at its **Private
review** section, to calculate the aggregate result from `$OutRoot`. Return only
the compact `focused-webforms-scan` block defined there.

Also inspect the retained progress checkpoint and return exactly one additional
block:

```text
focused-webforms-progress=<completed|partial|unavailable>
terminalState=<categorical-value-or-unavailable>
terminalStage=<categorical-value-or-unavailable>
lastSuccessfulStage=<categorical-value-or-unavailable>
totalElapsedMs=<nonnegative-count-or-unavailable>
checkpointHistoryCount=<nonnegative-count>
longestObservedStage=<categorical-value-or-unavailable>
longestObservedStageElapsedMs=<nonnegative-count-or-unavailable>
stageTransitionCount=<nonnegative-count>
```

Derive this block only from the bounded checkpoint. `longestObservedStage` means
the longest stage visible in the retained checkpoint history; it is not a claim
about events that may have rotated out of the bounded history. Do not infer a
stage duration when the required start and terminal observations are absent.
Use `unavailable` instead.

The scan now writes an adjacent bounded performance receipt at
`$ProgressPath + ".performance.json"`. Return exactly one additional block
derived only from that receipt:

```text
focused-webforms-performance=<completed|partial|unavailable>
runState=<categorical-value-or-unavailable>
timingCoverage=<complete|partial|unavailable>
heartbeatObserved=<true|false|unavailable>
heartbeatCount=<nonnegative-count-or-unavailable>
timingsTruncated=<true|false|unavailable>
extractorTimingCount=<nonnegative-count>
activeExtractor=<categorical-value-or-unavailable>
slowestExtractor=<categorical-value-or-unavailable>
slowestExtractorVersion=<categorical-value-or-unavailable>
slowestElapsedMs=<nonnegative-count-or-unavailable>
slowestEmittedFactCount=<nonnegative-count-or-unavailable>
slowestEmittedGapCount=<nonnegative-count-or-unavailable>
nextAction=<categorical-value-or-unavailable>
```

`slowestExtractor` is eligible only when the receipt contains the retained
terminal timing row selected by TraceMap. When an extractor was active at a
timeout or process boundary but has no retained terminal observation, report
it only as `activeExtractor`; do not promote it to the slowest extractor. The
receipt's `inputCount` is the scan-inventory count presented to the extractor,
not proof that every inventory row was parsed.

Do not return either receipt file, raw JSON, process IDs, process start times,
memory values that were not actually recorded, paths, project names, filenames,
symbols, hashes, commit SHAs, logs, source, configuration, routes, or diagnostic
text. Do not turn heartbeat activity into a successful-scan claim.

The full output, progress checkpoint, source identity, paths, project names,
routes, symbols, logs, facts, reports, indexes, and configuration remain on the
workstation. Do not email, paste, commit, upload, or publish them.

For the separate top-gap and extractor-count summary, do not use an agent to
interpret raw facts. Run the deterministic utility documented in
`prompts/collect-focused-webforms-gap-extractor-summary.md` after this review.
