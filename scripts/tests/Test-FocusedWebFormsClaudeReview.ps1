$ErrorActionPreference = 'Stop'
$scripts = Split-Path -Parent $PSScriptRoot
$entry = Join-Path $scripts 'Start-FocusedWebFormsClaudeReview.ps1'
$repo = Split-Path -Parent $scripts
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-claude-' + [Guid]::NewGuid().ToString('N'))

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
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    $packet = New-Artifact $temp 'packet/webforms-modernization.json' '{}'
    $manifest = New-Artifact $temp 'evidence-docs/manifest.json' '{}'
    $recipes = New-Artifact $temp 'evidence-docs/query-recipes.json' '{}'
    $chunks = New-Artifact $temp 'evidence-docs/chunks.jsonl' "{}\n"
    $handoff = New-Artifact $temp 'workbench/application-handoff.json' '{}'
    $receipt = [ordered]@{
        schemaVersion = 'focused-webforms-review-run-receipt.v1'
        run = [ordered]@{ state = 'completed' }
        stages = [ordered]@{
            packet = [ordered]@{ state = 'completed'; artifacts = @($packet) }
            evidenceDocs = [ordered]@{ state = 'completed'; artifacts = @($manifest, $recipes, $chunks) }
            workbench = [ordered]@{ state = 'completed'; artifacts = @($handoff) }
        }
    }
    [IO.File]::WriteAllText((Join-Path $temp 'run-receipt.json'), ($receipt | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))

    $global:capturedClaudeArguments = @()
    function global:claude { $global:capturedClaudeArguments = @($args); $global:LASTEXITCODE = 0 }
    $output = @(& $entry -ReviewRoot $temp -TraceMapRoot $repo)
    if ($output -notcontains 'webformsClaudeHandoff=validated' -or $output -notcontains 'sourceAccess=not-granted') {
        throw 'Claude handoff did not report its validated access boundary.'
    }
    if ($global:capturedClaudeArguments -notcontains '--permission-mode' -or
        $global:capturedClaudeArguments -notcontains 'plan' -or
        @($global:capturedClaudeArguments | Where-Object { $_ -eq '--add-dir' }).Count -ne 3) {
        throw 'Claude handoff did not retain plan mode and three bounded evidence directories.'
    }

    [IO.File]::AppendAllText((Join-Path $temp 'workbench/application-handoff.json'), 'changed')
    $failure = $null
    try { & $entry -ReviewRoot $temp -TraceMapRoot $repo | Out-Null } catch { $failure = $_.Exception.Message }
    if (!$failure.StartsWith('WEBFORMS_CLAUDE_ARTIFACT_MISMATCH', [StringComparison]::Ordinal)) {
        throw 'Claude handoff accepted an artifact that no longer matched the receipt.'
    }
    Write-Host 'PASS focused Web Forms Claude handoff'
}
finally {
    Remove-Item Function:\global:claude -ErrorAction SilentlyContinue
    Remove-Variable capturedClaudeArguments -Scope Global -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
