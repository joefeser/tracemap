#Requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../WebFormsRequeryDiagnostics.ps1')
$temp = Join-Path ([IO.Path]::GetTempPath()) ('diagnostic-types-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
try {
    $valid = @{schemaVersion='webforms-compiled-grouped-handoff.v1';header=@{gaps=@(
        @{gapKind='TruncatedByLimit';reason='depth';filePath='sample.vb';startLine=1;nodeId='n';ruleId='test';
            evidenceTier='Tier4Unknown';message='bounded';cutoffWitness=@{prefixTruncated=$false;lastEdgeNotTraversed=$true;edges=@()}})};
        nodes=@{};variants=@()} | ConvertTo-Json -Depth 12
    foreach ($mutation in @(
        { param($h) $h.header.gaps = $h.header.gaps[0] },
        { param($h) $h.header.gaps = @('scalar') },
        { param($h) $h.header.gaps[0].nodeId = 17 },
        { param($h) $h.header.gaps[0].startLine = '1' },
        { param($h) $h.header.gaps[0].cutoffWitness.edges = @('scalar') },
        { param($h) $h.header.gaps[0].cutoffWitness.prefixTruncated = 'false' },
        { param($h) $h.variants = @([pscustomobject]@{nodeReferences=@(42)}) }
    )) {
        $handoff = $valid | ConvertFrom-Json -Depth 16
        & $mutation $handoff
        $file = Join-Path $temp 'compiled-paths.handoff.local.json'
        $handoff | ConvertTo-Json -Depth 16 | Set-Content $file
        @{schemaVersion='webforms-handler-requery.v1';truncated=$true;artifacts=@(@{
            relativePath='compiled-paths.handoff.local.json';bytes=(Get-Item $file).Length;
            sha256=(Get-FileHash $file).Hash.ToLowerInvariant()})} | ConvertTo-Json -Depth 8 |
            Set-Content (Join-Path $temp 'handler-requery.local.json')
        $rejected = $false
        try { Write-WebFormsTruncationSummary $temp | Out-Null }
        catch {
            if ($_.Exception.Message -cne 'WEBFORMS_HANDLER_SUMMARY_METADATA_INVALID') { throw }
            $rejected = $true
        }
        if (!$rejected) { throw 'Malformed metadata was accepted.' }
    }
    Write-Output 'Web Forms diagnostic input type tests passed.'
} finally { [IO.Directory]::Delete($temp, $true) }
