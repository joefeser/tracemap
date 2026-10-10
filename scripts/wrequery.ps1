[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Root,
    [string]$Project,
    [string]$Handler,
    [switch]$Open,
    [switch]$AllowUpdatedReader,
    [switch]$InspectLatest,
    [switch]$MethodGraph,
    [switch]$LatestRefresh
)
Set-StrictMode -Version Latest
if ($MethodGraph -and $InspectLatest) { throw 'MethodGraph and InspectLatest cannot be combined.' }
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WebFormsRequeryDiagnostics.ps1')
function Read-Locator([string]$Path, [string]$Schema) {
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -gt 1048576) { throw 'WEBFORMS_WIZARD_LOCATOR_TOO_LARGE' }
    $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($document.schemaVersion -ne $Schema) { throw 'WEBFORMS_WIZARD_LOCATOR_SCHEMA_INVALID' }
    return $document
}
# Configurations are locators only here. Native status and requery remain the
# authority for retained artifact integrity and report/run admission.
$Root = [IO.Path]::GetFullPath($Root)
$rootDocument = Read-Locator (Join-Path $Root 'root.config.json') 'webforms-wizard-root.v1'
$projects = @($rootDocument.configuration.projects)
if (!$Project) {
    if ($projects.Count -ne 1) { throw 'Specify -Project with one of the saved project IDs.' }
    $Project = [string]$projects[0].id
}
if ($Project -cnotmatch '\A[a-z][a-z0-9-]{0,47}\z') { throw 'WEBFORMS_WIZARD_PROJECT_ID_INVALID' }
$reference = @($projects | Where-Object { $_.id -ceq $Project })
if ($reference.Count -ne 1) { throw 'WEBFORMS_WIZARD_PROJECT_NOT_FOUND' }
$projectPath = Join-Path $Root "$Project/project.config.json"
if ((Get-FileHash -LiteralPath $projectPath -Algorithm SHA256).Hash -ine $reference[0].configSha256) {
    throw 'WEBFORMS_WIZARD_PROJECT_HASH_MISMATCH'
}
$saved = Read-Locator $projectPath 'webforms-wizard-project.v1'
if ($saved.configuration.id -cne $Project -or $saved.configuration.step -ne 'completed') {
    throw 'WEBFORMS_WIZARD_PROJECT_NOT_COMPLETED'
}
$relative = [string]$saved.configuration.run.relativeRoot
if ($relative -cnotmatch ('\Aruns/' + [regex]::Escape($Project) + '-[a-f0-9]{32}\z')) {
    throw 'WEBFORMS_WIZARD_RUN_LOCATOR_INVALID'
}
$run = Join-Path (Join-Path $Root $relative) 'run'
if ($LatestRefresh) {
    if ($InspectLatest) { throw 'LatestRefresh and InspectLatest cannot be combined.' }
    $refreshes = @(Get-ChildItem -LiteralPath $Root -Directory | Where-Object {
        $_.Name -cmatch ('\Arefresh-' + [regex]::Escape($Project) + '-[a-f0-9]{32}\z')
    } | Select-Object -First 129)
    if ($refreshes.Count -eq 0 -or $refreshes.Count -gt 128) { throw 'WEBFORMS_REFRESH_SELECTION_UNAVAILABLE' }
    $fresh = $refreshes | Sort-Object LastWriteTimeUtc,Name -Descending | Select-Object -First 1
    $run = Join-Path $fresh.FullName 'run'
    Write-Output "Selected fresh run: $run"
    # Verify this exact run below. Never fall back to an older successful run.
}
$cli = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
if (!(Test-Path -LiteralPath $cli -PathType Leaf)) {
    throw 'Build TraceMap first: dotnet build src\dotnet\tracemap.sln'
}
if (!$InspectLatest) {
    if (!$Handler) { $Handler = Read-Host 'Handler name (for example Page_Load)' }
    if ($Handler -cnotmatch '\A[A-Za-z0-9_]{1,128}\z') { throw 'WEBFORMS_HANDLER_INVALID' }
}
$verified = Get-WebFormsVerifiedStatus $cli $run
$run = $verified.Run
$status = $verified.Status
if ($status.schemaVersion -ne 'webforms-review-status.v1' -or
    (!$AllowUpdatedReader -and $status.readerMatchesOriginalGenerator -ne $true) -or
    $status.retainedArtifactsVerified -ne $true -or
    $status.state -ne 'reports-completed-review-only') {
    throw 'WEBFORMS_HANDLER_COMPLETED_STATE_NOT_ADMITTED'
}
if ($status.readerMatchesOriginalGenerator -ne $true) {
    Write-Output 'Updated reader explicitly allowed; retained artifacts verified by native status. Original producer identity remains unchanged; this new query has its own provenance.'
}
$bundle = [IO.Path]::GetDirectoryName([string]$status.workbenchPath)
if ($InspectLatest) {
    $reports = @(Get-ChildItem -LiteralPath $Root -Directory | Where-Object {
        $_.Name -cmatch ('\Ahandler-requery-' + [regex]::Escape($Project) + '-[a-f0-9]{32}\z') -and
        (Test-Path -LiteralPath (Join-Path $_.FullName 'handler-requery.local.json') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName 'compiled-paths.handoff.local.json') -PathType Leaf)
    } | Select-Object -First 129)
    if ($reports.Count -gt 128) { throw 'WEBFORMS_HANDLER_REPORT_SELECTION_LIMIT' }
    if ($reports.Count -eq 0) { throw 'WEBFORMS_HANDLER_REPORT_NOT_FOUND' }
    $latest = $reports | Sort-Object LastWriteTimeUtc,Name -Descending | Select-Object -First 1
    Write-Output "Inspecting latest completed report for project $Project (not filtered by handler): $($latest.FullName)"
    Write-WebFormsTruncationSummary $latest.FullName
    return
}
$destination = Join-Path $Root ("handler-requery-$Project-" + [guid]::NewGuid().ToString('N'))
$extra = @()
if ($MethodGraph) { $extra = @('--view', 'method-graph') }
& dotnet $cli webforms-review requery-handler --run $run --bundle $bundle --handler $Handler --out $destination @extra
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_HANDLER_REQUERY_FAILED;originals-preserved;partial-output-preserved' }
$report = Join-Path $destination 'compiled-paths.local.html'
if ($MethodGraph) { $report = Join-Path $destination 'method-graph.local.html' }
Write-Output "Handler report: $report"
if ($MethodGraph) { Write-Output 'View: method graph only. Terminal path enumeration was not run; no compiled-paths report was generated.' }
else { Write-Output 'View: database terminal paths. Open the exact Handler report path above, not a report from an earlier graph-only folder.' }
if (!$MethodGraph) { Write-WebFormsTruncationSummary $destination }
if ($Open) { Invoke-Item -LiteralPath $report }
