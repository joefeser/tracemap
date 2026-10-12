# Read-only console classification of receipt-bound retained evidence.
# Associations are exact-node co-occurrences, not proven causes or reachability.
. (Join-Path $PSScriptRoot 'WebFormsRequeryDiagnostics.ps1')
function Read-WebFormsClassificationHandoff([string]$Directory) {
    function Read-BoundedBytes([string]$Path, [int]$Limit) {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            if ($stream.Length -gt $Limit) { throw 'WEBFORMS_GAP_CLASSIFICATION_INPUT_LIMIT' }
            $bytes = [byte[]]::new([int]$stream.Length)
            $offset = 0
            while ($offset -lt $bytes.Length) {
                $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
                if ($read -eq 0) { throw 'WEBFORMS_GAP_CLASSIFICATION_INPUT_CHANGED' }
                $offset += $read
            }
            return ,$bytes
        } finally { $stream.Dispose() }
    }
    $receipt = [Text.Encoding]::UTF8.GetString((Read-BoundedBytes (Join-Path $Directory 'handler-requery.local.json') 4194304)).TrimStart([char]0xFEFF) | ConvertFrom-Json
    $variant = Assert-WebFormsReceipt $receipt 'WEBFORMS_GAP_CLASSIFICATION_RECEIPT_INVALID'
    if ($variant -ceq 'method-graph') { throw 'WEBFORMS_GAP_CLASSIFICATION_REQUIRES_GROUPED_PATHS' }
    $bytes = Read-BoundedBytes (Join-Path $Directory 'compiled-paths.handoff.local.json') 67108864
    $artifacts = @($receipt.artifacts | Where-Object { $_.relativePath -ceq 'compiled-paths.handoff.local.json' })
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $digest = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    if ($artifacts.Count -ne 1 -or $artifacts[0].bytes -ne $bytes.Length -or $artifacts[0].sha256 -cne $digest) {
        throw 'WEBFORMS_GAP_CLASSIFICATION_HANDOFF_CHANGED'
    }
    $handoff = [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF) | ConvertFrom-Json
    if ($handoff.schemaVersion -cne 'webforms-compiled-grouped-handoff.v1') { throw 'WEBFORMS_GAP_CLASSIFICATION_SCHEMA_INVALID' }
    return $handoff
}

function Get-WebFormsGapClassification($Handoff) {
    $gaps = @($Handoff.header.gaps)
    if ($gaps.Count -gt 200000) { throw 'WEBFORMS_GAP_CLASSIFICATION_RECORD_LIMIT' }
    $targets = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $roundtrips = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $depthRecords = 0; $identityRecords = 0; $missingNodeRecords = 0
    foreach ($gap in $gaps) {
        if ($gap.gapKind -cne 'TruncatedByLimit') { continue }
        $cause = if ($gap.PSObject.Properties['cutoffCause']) { $gap.cutoffCause } else { $null }
        if ($cause -ceq 'terminal-route-not-found-within-depth-bound') {
            $depthRecords++
            if (!$gap.PSObject.Properties['nodeId'] -or [string]::IsNullOrWhiteSpace($gap.nodeId)) { $missingNodeRecords++ }
            else { [void]$targets.Add([string]$gap.nodeId) }
        } elseif ($cause -ceq 'candidate-identity-roundtrip') {
            $identityRecords++
            if ($gap.PSObject.Properties['nodeId'] -and ![string]::IsNullOrWhiteSpace($gap.nodeId)) { [void]$roundtrips.Add([string]$gap.nodeId) }
        }
    }
    # Closed categories: unknown/new gap names must not become assertions by regex.
    $kinds = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    $kinds.Add('CompiledIlTargetUnavailable', 'compiled-target-unavailable')
    $kinds.Add('CompiledIlTargetAmbiguous', 'compiled-target-ambiguous')
    $kinds.Add('CompiledIlFrameworkVersionAmbiguous', 'framework-signature-ambiguous')
    $kinds.Add('CompiledIlFrameworkVersionCandidate', 'framework-signature-candidate')
    $kinds.Add('CompiledIlVirtualDispatchUnproven', 'virtual-dispatch-unproven')
    $kinds.Add('CompiledIlArtifactContext', 'compiled-artifact-context')
    $associated = [Collections.Generic.Dictionary[string,Collections.Generic.HashSet[string]]]::new([StringComparer]::Ordinal)
    $withEvidence = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($gap in $gaps) {
        if (!$gap.PSObject.Properties['nodeId'] -or !$targets.Contains([string]$gap.nodeId) -or
            $gap.gapKind -ceq 'TruncatedByLimit') { continue }
        # An association requires a retained rule and tier, not only a label.
        if (!$gap.PSObject.Properties['ruleId'] -or [string]::IsNullOrWhiteSpace($gap.ruleId) -or
            !$gap.PSObject.Properties['evidenceTier'] -or [string]::IsNullOrWhiteSpace($gap.evidenceTier)) { continue }
        $category = if ($kinds.ContainsKey([string]$gap.gapKind)) { $kinds[[string]$gap.gapKind] } else { 'other-retained-gap' }
        if (!$associated.ContainsKey($category)) { $associated.Add($category, [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)) }
        [void]$associated[$category].Add([string]$gap.nodeId)
        [void]$withEvidence.Add([string]$gap.nodeId)
    }
    [pscustomobject]@{
        RuleId = 'workflow.webforms.gap-classification.v1'
        EvidenceTier = 'Tier4Unknown'
        DepthGapRecords = $depthRecords
        UniqueDepthNodes = $targets.Count
        DepthRecordsWithoutNode = $missingNodeRecords
        IdentityGapRecords = $identityRecords
        UniqueIdentityNodes = $roundtrips.Count
        UnclassifiedDepthNodes = $targets.Count - $withEvidence.Count
        Categories = @($associated.Keys | Sort-Object | ForEach-Object { [pscustomobject]@{ Category = $_; UniqueNodes = $associated[$_].Count } })
    }
}

function Write-WebFormsGapClassification([string]$Directory) {
    $result = Get-WebFormsGapClassification (Read-WebFormsClassificationHandoff $Directory)
    Write-Output "rule=$($result.RuleId);tier=$($result.EvidenceTier);scope=retained-cutoff-node-associations"
    Write-Output "boundedDepthGapRecords=$($result.DepthGapRecords);uniqueNodes=$($result.UniqueDepthNodes);recordsWithoutNode=$($result.DepthRecordsWithoutNode)"
    Write-Output "identityRoundtripRecords=$($result.IdentityGapRecords);uniqueNodes=$($result.UniqueIdentityNodes)"
    foreach ($category in $result.Categories) { Write-Output "associatedEvidence=$($category.Category);uniqueNodes=$($category.UniqueNodes)" }
    Write-Output "unclassifiedDepthNodes=$($result.UnclassifiedDepthNodes)"
    Write-Output 'Categories overlap and identify same-node retained evidence, not proven causes. Unclassified means insufficient associated evidence, never no database dependency.'
    Write-Output 'Receipt artifact hash verified; this is not receipt authenticity or runtime proof. No source identities, SQL, private paths, or input hashes are printed. No artifact is written.'
}
