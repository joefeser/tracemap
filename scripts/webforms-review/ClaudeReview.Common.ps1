Set-StrictMode -Version Latest

function Read-FocusedWebFormsBoundedJson([string]$Path, [long]$MaximumBytes, [string]$Failure) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $Failure }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes) { throw $Failure }
    try { return [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -Depth 30 }
    catch { throw $Failure }
}

function Assert-FocusedWebFormsReceiptedArtifact([object]$Receipt, [string]$StageName, [string]$Root, [string]$RelativePath) {
    $normalized = $RelativePath.Replace('\', '/')
    $matches = @($Receipt.stages.PSObject.Properties[$StageName].Value.artifacts | Where-Object {
        ([string]$_.path).Replace('\', '/').Equals($normalized, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($matches.Count -ne 1) { throw "WEBFORMS_CLAUDE_ARTIFACT_NOT_RECEIPTED;path=$normalized" }
    $path = Join-Path $Root $RelativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "WEBFORMS_CLAUDE_INPUT_UNAVAILABLE;path=$path" }
    $file = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($file.Length -ne [long]$matches[0].bytes -or $hash -ne [string]$matches[0].sha256) {
        throw "WEBFORMS_CLAUDE_ARTIFACT_MISMATCH;path=$normalized"
    }
}

function Get-FocusedWebFormsClaudeEvidenceContext([string]$ReviewRoot, [string]$TraceMapRoot) {
    $root = [IO.Path]::GetFullPath($ReviewRoot).TrimEnd('\', '/')
    $traceRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
    $receiptPath = Join-Path $root 'run-receipt.json'
    $receipt = Read-FocusedWebFormsBoundedJson $receiptPath 16MB 'WEBFORMS_CLAUDE_RECEIPT_UNAVAILABLE'
    if ($receipt.schemaVersion -ne 'focused-webforms-review-run-receipt.v1' -or $receipt.run.state -ne 'completed') {
        throw 'WEBFORMS_CLAUDE_RUN_INCOMPLETE'
    }
    foreach ($stageName in @('packet','evidenceDocs','workbench')) {
        if ($receipt.stages.PSObject.Properties[$stageName].Value.state -ne 'completed') {
            throw "WEBFORMS_CLAUDE_STAGE_INCOMPLETE;stage=$stageName"
        }
    }
    Assert-FocusedWebFormsReceiptedArtifact $receipt 'packet' $root 'packet/webforms-modernization.json'
    Assert-FocusedWebFormsReceiptedArtifact $receipt 'evidenceDocs' $root 'evidence-docs/manifest.json'
    Assert-FocusedWebFormsReceiptedArtifact $receipt 'evidenceDocs' $root 'evidence-docs/query-recipes.json'
    Assert-FocusedWebFormsReceiptedArtifact $receipt 'evidenceDocs' $root 'evidence-docs/chunks.jsonl'
    Assert-FocusedWebFormsReceiptedArtifact $receipt 'workbench' $root 'workbench/application-handoff.json'
    return [pscustomobject]@{
        Root = $root
        TraceRoot = $traceRoot
        PacketPath = Join-Path $root 'packet/webforms-modernization.json'
        EvidenceDocsRoot = Join-Path $root 'evidence-docs'
        WorkbenchRoot = Join-Path $root 'workbench'
        AgentReviewRoot = Join-Path $root 'agent-reviews'
    }
}

function Resolve-FocusedWebFormsClaudeLauncher([string]$ClaudeLauncherPath) {
    $launcherInput = $ClaudeLauncherPath.Trim()
    if ($IsWindows -and $launcherInput -match '^[A-Za-z]:/') {
        $launcherInput = $launcherInput.Replace('/', '\')
    }
    if (!(Test-Path -LiteralPath $launcherInput -PathType Leaf)) {
        throw 'WEBFORMS_CLAUDE_LAUNCHER_UNAVAILABLE;use-resolve-path-with-a-native-windows-path'
    }
    return (Resolve-Path -LiteralPath $launcherInput).Path
}
