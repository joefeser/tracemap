# Read-only helpers. No checkpoint edits or relaxed native verification.
function Get-WebFormsVerifiedStatus([string]$Cli, [string]$Run) {
    $candidates = @($Run)
    if ($Run -cmatch '\A[A-Za-z]:[\\/]') {
        $alternate = if ([char]::IsUpper($Run[0])) { $Run.Substring(0,1).ToLowerInvariant() } else { $Run.Substring(0,1).ToUpperInvariant() }
        $candidates += $alternate + $Run.Substring(1)
    }
    foreach ($candidate in $candidates) {
        $text = @(& dotnet $Cli webforms-review status --run $candidate --json 2>&1)
        if ($LASTEXITCODE -eq 0) {
            return @{ Run=$candidate; Status=(($text -join "`n") | ConvertFrom-Json) }
        }
        # Only the known casing-sensitive checkpoint failure permits one
        # read-only retry of the same drive path. All native checks still run.
        if (($text -join "`n").Trim() -ne 'error: WEBFORMS_EXECUTION_CHECKPOINT_INVALID') { break }
    }
    throw 'WEBFORMS_HANDLER_STATUS_FAILED; native verification failed; originals preserved'
}

function Write-WebFormsTruncationSummary([string]$Directory) {
    $receiptPath = Join-Path $Directory 'handler-requery.local.json'
    $handoffPath = Join-Path $Directory 'compiled-paths.handoff.local.json'
    # This is a bounded console projection, not a new evidence artifact.
    if ((Get-Item -LiteralPath $receiptPath).Length -gt 4194304 -or
        (Get-Item -LiteralPath $handoffPath).Length -gt 67108864) {
        Write-Output 'Truncation details unavailable: summary input exceeds its 64 MiB handoff / 4 MiB receipt bound. Reports preserved.'
        return
    }
    function Read-SummaryBytes([string]$Path, [int]$Maximum) {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            if ($stream.Length -gt $Maximum) { throw 'WEBFORMS_HANDLER_SUMMARY_INPUT_LIMIT' }
            $buffer = [byte[]]::new([int]$stream.Length)
            $offset = 0
            while ($offset -lt $buffer.Length) {
                $read = $stream.Read($buffer, $offset, $buffer.Length - $offset)
                if ($read -eq 0) { throw 'WEBFORMS_HANDLER_SUMMARY_INPUT_CHANGED' }
                $offset += $read
            }
            return ,$buffer
        } finally { $stream.Dispose() }
    }
    $receipt = [Text.Encoding]::UTF8.GetString((Read-SummaryBytes $receiptPath 4194304)).TrimStart([char]0xFEFF) | ConvertFrom-Json
    if ($receipt.schemaVersion -ne 'webforms-handler-requery.v1') { throw 'WEBFORMS_HANDLER_SUMMARY_RECEIPT_INVALID' }
    $artifact = @($receipt.artifacts | Where-Object { $_.relativePath -ceq 'compiled-paths.handoff.local.json' })
    $bytes = Read-SummaryBytes $handoffPath 67108864
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    if ($artifact.Count -ne 1 -or $artifact[0].bytes -ne $bytes.Length -or $artifact[0].sha256 -cne $hash) {
        throw 'WEBFORMS_HANDLER_SUMMARY_HANDOFF_CHANGED'
    }
    $handoff = [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF) | ConvertFrom-Json
    if ($handoff.schemaVersion -ne 'webforms-compiled-grouped-handoff.v1') { throw 'WEBFORMS_HANDLER_SUMMARY_SCHEMA_INVALID' }
    $gaps = @($handoff.header.gaps | Where-Object { $_.gapKind -ceq 'TruncatedByLimit' })
    Write-Output "Truncation: $($receipt.truncated); retained limit gaps: $($gaps.Count). Counts are retained gap records, not missing paths."
    function Short-Text($Value) {
        if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return 'unavailable' }
        $valueText = [regex]::Replace([string]$Value, '[\p{Cc}\p{Cf}]', ' ')
        if ($valueText.Length -gt 256) { return $valueText.Substring(0,256) + '…' }
        return $valueText
    }
    $groups = @($gaps | Group-Object reason | Sort-Object Name)
    foreach ($group in @($groups | Select-Object -First 10)) {
        Write-Output "  $(Short-Text $group.Name): $($group.Count) retained gaps"
        $causes = @($group.Group | Group-Object { if ($_.PSObject.Properties['cutoffCause']) { $_.cutoffCause } else { 'unavailable-legacy-report' } } | Sort-Object Name)
        foreach ($cause in @($causes | Select-Object -First 10)) {
            Write-Output "    cause=$(Short-Text $cause.Name); retainedGaps=$($cause.Count)"
        }
        foreach ($gap in @($group.Group | Select-Object -First 3)) {
            Write-Output "    location=$(Short-Text $gap.filePath); line=$(Short-Text $gap.startLine); node=$(Short-Text $gap.nodeId)"
            Write-Output "    rule=$(Short-Text $gap.ruleId); tier=$(Short-Text $gap.evidenceTier); detail=$(Short-Text $gap.message)"
        }
        foreach ($gap in @($group.Group | Where-Object { $_.PSObject.Properties['cutoffWitness'] -and $null -ne $_.cutoffWitness } | Select-Object -First 3)) {
            $witness = $gap.cutoffWitness
            Write-Output "    Root-to-cutoff sample; prefixTruncated=$($witness.prefixTruncated); lastEdgeNotTraversed=$($witness.lastEdgeNotTraversed)"
            foreach ($edge in @($witness.edges | Select-Object -First 64)) {
                Write-Output "      $(Short-Text $edge.fromNodeId) -> $(Short-Text $edge.toNodeId)"
                Write-Output "      kind=$(Short-Text $edge.edgeKind); rule=$(Short-Text $edge.ruleId); tier=$(Short-Text $edge.evidenceTier); location=$(Short-Text $edge.filePath):$(Short-Text $edge.startLine)"
            }
        }
    }
    Write-Output 'Display bounded to 10 reasons, 3 examples each, 256 characters per field. Unavailable means not retained; no stopped branch is inferred.'
}
