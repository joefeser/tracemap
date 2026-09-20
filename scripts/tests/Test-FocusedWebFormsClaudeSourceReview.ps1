$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$entry = Join-Path $scripts 'Start-FocusedWebFormsClaudeSourceReview.ps1'
$repo = Split-Path -Parent $scripts
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-claude-source-' + [Guid]::NewGuid().ToString('N'))

function New-Artifact([string]$Root, [string]$RelativePath, [string]$Text) {
    $path = Join-Path $Root $RelativePath
    [IO.Directory]::CreateDirectory((Split-Path -Parent $path)) | Out-Null
    [IO.File]::WriteAllText($path, $Text, [Text.UTF8Encoding]::new($false))
    $file = Get-Item -LiteralPath $path
    return [ordered]@{
        path = $RelativePath.Replace('\', '/')
        bytes = [long]$file.Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

try {
    $review = Join-Path $temp 'review'
    $source = Join-Path $temp 'source'
    [IO.Directory]::CreateDirectory($review) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $source 'Pages')) | Out-Null
    $packet = New-Artifact $review 'packet/webforms-modernization.json' '{}'
    $manifest = New-Artifact $review 'evidence-docs/manifest.json' '{}'
    $recipes = New-Artifact $review 'evidence-docs/query-recipes.json' '{}'
    $chunks = New-Artifact $review 'evidence-docs/chunks.jsonl' "{}`n"
    $handoff = New-Artifact $review 'workbench/application-handoff.json' '{}'
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        stages = [ordered]@{
            packet = [ordered]@{ state = 'completed'; artifacts = @($packet) }
            evidenceDocs = [ordered]@{ state = 'completed'; artifacts = @($manifest, $recipes, $chunks) }
            workbench = [ordered]@{ state = 'completed'; artifacts = @($handoff) }
        }
    }
    [IO.File]::WriteAllText((Join-Path $review 'run-receipt.json'), ($receipt | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $source 'Pages/Review.aspx'), '<%@ Page Language="VB" %>', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $source 'Pages/Review.aspx.vb'), 'Public Class Review : End Class', [Text.UTF8Encoding]::new($false))

    $launcherPath = Join-Path $temp 'corporate-launcher.ps1'
    $capturedManifest = Join-Path $temp 'selection-manifest.json'
    $launcher = @'
$dirs = for ($index = 0; $index -lt $args.Count - 1; $index++) { if ($args[$index] -eq '--add-dir') { $args[$index + 1] } }
$selection = Join-Path $dirs[-1] 'selection-manifest.json'
[IO.File]::Copy($selection, $env:TRACEMAP_TEST_SOURCE_MANIFEST, $true)
Write-Output '# Selected source assessment — café'
$global:LASTEXITCODE = 0
'@
    [IO.File]::WriteAllText($launcherPath, $launcher, [Text.UTF8Encoding]::new($false))
    $env:TRACEMAP_TEST_SOURCE_MANIFEST = $capturedManifest
    $output = @(& $entry -ReviewRoot $review -SourceRoot $source -SourceRelativePath @('Pages/Review.aspx','Pages/Review.aspx.vb') -TraceMapRoot $repo -ClaudeLauncherPath $launcherPath)
    foreach ($expected in @(
        'webformsClaudeSourceHandoff=validated',
        'selectedSourceFiles=2',
        'sourceAccess=selected-files-explicitly-granted',
        'promptTransport=stdin',
        'claudeAssessment=agent-reviews/claude-selected-source-review.md')) {
        if ($output -notcontains $expected) { throw "Missing source-review status: $expected" }
    }
    $selection = [IO.File]::ReadAllText($capturedManifest) | ConvertFrom-Json
    if ($selection.schemaVersion -ne 'focused-webforms-selected-source.v1' -or !$selection.rawSource -or $selection.fileCount -ne 2) {
        throw 'Selected-source manifest did not retain its explicit raw-source contract.'
    }
    if ((@($selection.files.alias) -join ',') -ne 'source-001.aspx,source-002.vb') {
        throw 'Selected-source aliases were not deterministic.'
    }
    $assessment = Join-Path $review 'agent-reviews/claude-selected-source-review.md'
    $assessmentRetained = (Test-Path -LiteralPath $assessment -PathType Leaf) -and
        [IO.File]::ReadAllText($assessment, [Text.UTF8Encoding]::new($false, $true)).Contains('# Selected source assessment — café', [StringComparison]::Ordinal)
    if (!$assessmentRetained) { throw 'Selected-source assessment was not retained under the review root.' }

    $failure = $null
    try {
        & $entry -ReviewRoot $review -SourceRoot $source -SourceRelativePath '../outside.vb' -TraceMapRoot $repo -ClaudeLauncherPath $launcherPath | Out-Null
    } catch { $failure = $_.Exception.Message }
    if (!$failure.StartsWith('WEBFORMS_CLAUDE_SOURCE_SELECTION_INVALID', [StringComparison]::Ordinal)) {
        throw 'Selected-source handoff accepted a path outside the source root.'
    }
    Write-Host 'PASS focused Web Forms selected-source Claude handoff'
}
finally {
    Remove-Item Env:\TRACEMAP_TEST_SOURCE_MANIFEST -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
