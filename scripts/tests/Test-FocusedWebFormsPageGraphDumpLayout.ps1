$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$subject = Join-Path $scripts 'New-FocusedWebFormsPageGraphDump.ps1'
$subjectText = [IO.File]::ReadAllText($subject)
foreach ($expected in @('WEBFORMS_PAGE_GRAPH_DUMP_PAGE_NOT_RECEIPTED', 'WEBFORMS_PAGE_GRAPH_DUMP_PAGE_ARTIFACT_MISMATCH', 'sourceIndexSha256')) {
    if (!$subjectText.Contains($expected, [StringComparison]::Ordinal)) { throw "Page graph dump omitted receipted-input guard: $expected" }
}
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-page-graph-layout-' + [Guid]::NewGuid().ToString('N'))

function Write-Json([string]$Path, [object]$Value, [int]$Depth = 20) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, (($Value | ConvertTo-Json -Depth $Depth) + "`n"), [Text.UTF8Encoding]::new($false))
}

try {
    $applicationPath = Join-Path $temp 'workbench/application-handoff.json'
    Write-Json $applicationPath ([ordered]@{
        schemaVersion = 'webforms-application-handoff.v1'
        pages = @([ordered]@{ pageId = 'page-001'; filePath = 'Pages/Fixture.aspx' })
    })
    $application = Get-Item -LiteralPath $applicationPath
    $applicationHash = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Json (Join-Path $temp 'run-receipt.json') ([ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        layout = [ordered]@{ scan = 'combined' }
        stages = [ordered]@{ workbench = [ordered]@{
            state = 'completed'
            artifacts = @([ordered]@{ path = 'workbench/application-handoff.json'; bytes = $application.Length; sha256 = $applicationHash })
        } }
    })
    [IO.Directory]::CreateDirectory((Join-Path $temp 'combined')) | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $temp 'combined/index.sqlite'), [byte[]](1))

    try {
        & $subject -ReviewRoot $temp -PriorPageId page-001 | Out-Null
        throw 'Page graph dump unexpectedly accepted a combined index.'
    }
    catch {
        if ($_.Exception.Message -ne 'WEBFORMS_PAGE_GRAPH_DUMP_COMBINED_INDEX_UNSUPPORTED_USE_TRAVERSAL_SUMMARY') { throw }
    }

    Write-Host 'PASS focused Web Forms page graph combined-index guard'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
