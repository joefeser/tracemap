[CmdletBinding()]
param([string]$ProofRoot = '', [string]$OutputRoot = '', [switch]$NoOpen)

# Temporary, local-only before/after reporting check. Never scans or cleans up.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_COMPARE_POWERSHELL_7_REQUIRED' }
$beforeCommit = 'e8121dc1c147923c39adcc1cb3cdbff5fd349d7e'
$afterCommit = 'f7891d980c2fd8080b70c14d0ea07a834598d35a'
$toolRoot = Split-Path $PSScriptRoot -Parent
$required = @('publish-receipt.local.json', 'handler-paths.json', 'combined.sqlite',
    'scan/scan-manifest.json', 'probe/scan-manifest.json',
    'combined-ilwork-30000000.sqlite', 'scan-ilwork-30000000/scan-manifest.json',
    'scan-ilwork-30000000/facts.ndjson')

function Complete-Proof([string]$Root) {
    foreach ($name in $required) {
        if (!(Test-Path -LiteralPath (Join-Path $Root $name) -PathType Leaf)) { return $false }
    }
    return $true
}
function Git-Checked([string[]]$Arguments) {
    $result = @(& git @Arguments)
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_COMPARE_GIT_FAILED' }
    return $result
}
function Text-Hash([string]$Value) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($Value))).ToLowerInvariant()
}
function Optional-Text([object]$Value, [string]$Name) {
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return '' }
    return [string]$property.Value
}
function Input-Inventory {
    $names = @($required) + @('probe/facts.ndjson', 'scan/facts.ndjson' | Where-Object {
        Test-Path -LiteralPath (Join-Path $ProofRoot $_) -PathType Leaf
    })
    foreach ($name in $names) {
        $file = Get-Item -LiteralPath (Join-Path $ProofRoot $name)
        if ($file.Length -le 0 -or $file.Length -gt 4GB) { throw 'WEBFORMS_COMPARE_INPUT_LIMIT' }
        [ordered]@{ path = $name; bytes = [long]$file.Length;
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
}
function One-Line([object[]]$Lines, [string]$Prefix) {
    $matches = @($Lines | Where-Object { ([string]$_).StartsWith($Prefix, [StringComparison]::Ordinal) })
    if ($matches.Count -ne 1) { throw "WEBFORMS_COMPARE_OUTPUT_MISSING:$Prefix" }
    return ([string]$matches[0]).Substring($Prefix.Length)
}
function Read-Handoff([string]$Root) {
    $path = Join-Path $Root 'workbench/compiled-paths.local.json'
    $file = Get-Item -LiteralPath $path
    if ($file.Length -le 0 -or $file.Length -gt 16MB) { throw 'WEBFORMS_COMPARE_HANDOFF_LIMIT' }
    $value = [IO.File]::ReadAllText($path) | ConvertFrom-Json -Depth 50
    if ($value.schemaVersion -cne 'webforms-compiled-path-handoff.v1' -or
        $value.ruleId -cne 'diagnostic.webforms.compiled-path-handoff.v1' -or
        $value.claimLevel -cne 'review-only-static-evidence' -or
        @($value.paths).Count -lt 1 -or @($value.paths).Count -gt 256) {
        throw 'WEBFORMS_COMPARE_NO_VALID_COMPILED_PATHS'
    }
    return $value
}
function Path-Projection([object]$Handoff) {
    # Compare exact method transitions/evidence, not labels, generated IDs or HTML bytes.
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($path in @($Handoff.paths)) {
        if ($path.claim -cne 'review-only-static-path' -or @($path.hops).Count -lt 1 -or @($path.hops).Count -gt 20) {
            throw 'WEBFORMS_COMPARE_PATH_INVALID'
        }
        $hops = @($path.hops | ForEach-Object {
            if ([string]::IsNullOrWhiteSpace([string]$_.ruleId) -or
                $_.evidenceTier -cnotin @('Tier1Semantic','Tier2Structural','Tier3SyntaxOrTextual','Tier4Unknown')) {
                throw 'WEBFORMS_COMPARE_PATH_INVALID'
            }
            [ordered]@{ ordinal = $_.ordinal; from = $_.from.name; to = $_.to.name;
                fromScan = $_.from.scanId; fromCommit = $_.from.commitSha;
                toScan = $_.to.scanId; toCommit = $_.to.commitSha;
                edgeKind = $_.edgeKind; ruleId = $_.ruleId; tier = $_.evidenceTier;
                filePath = $_.filePath; startLine = $_.startLine; endLine = $_.endLine;
                supportingFactIds = @($_.supportingFactIds) }
        })
        $paths.Add(([ordered]@{ classification = $path.classification; claim = $path.claim;
            terminalKind = $path.terminalKind; hops = $hops;
            supportingFactIds = @($path.supportingFactIds) } | ConvertTo-Json -Depth 20 -Compress))
    }
    $sorted = $paths.ToArray()
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    # Retain the pre-review coverage/provenance fields; additive gap fields are allowed.
    $coverage = $Handoff.coverage
    $common = [ordered]@{
        paths = $sorted; query = $Handoff.query;
        sourceCommit = $Handoff.provenance.sourceCommitSha; scanId = $Handoff.provenance.scanId;
        scanFolder = $Handoff.provenance.scanFolder; combinedIndex = $Handoff.provenance.combinedIndex;
        assemblies = @($Handoff.assemblies);
        reportCoverage = $coverage.reportCoverage; warnings = @($coverage.warnings);
        gapCounts = @($coverage.gapCounts); omittedGapGroupCount = $coverage.omittedGapGroupCount;
        omittedGapDetailCount = $coverage.omittedGapDetailCount;
        gaps = @($coverage.gaps | ForEach-Object {
            [ordered]@{ gapKind = Optional-Text $_ 'gapKind'; ruleId = Optional-Text $_ 'ruleId';
                tier = Optional-Text $_ 'evidenceTier'; message = Optional-Text $_ 'message';
                reason = Optional-Text $_ 'reason'; commitSha = Optional-Text $_ 'commitSha' }
        })
    }
    return ($common | ConvertTo-Json -Depth 25 -Compress)
}

if (!$ProofRoot) {
    $candidates = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'tracemap-existing-publish-*' |
        Where-Object { Complete-Proof $_.FullName } | Sort-Object Name)
    if ($candidates.Count -eq 0) { throw 'WEBFORMS_COMPARE_PROOF_UNAVAILABLE' }
    for ($i = 0; $i -lt $candidates.Count; $i++) { Write-Host "[$($i + 1)] $($candidates[$i].Name)" }
    $choice = if ($candidates.Count -eq 1) { '1' } else { Read-Host 'Choose the working proof number (the folder you verified)' }
    $number = 0
    if (![int]::TryParse($choice, [ref]$number) -or $number -lt 1 -or $number -gt $candidates.Count) {
        throw 'WEBFORMS_COMPARE_SELECTION_INVALID'
    }
    $ProofRoot = $candidates[$number - 1].FullName
}
$ProofRoot = [IO.Path]::GetFullPath($ProofRoot)
if (!(Complete-Proof $ProofRoot)) { throw 'WEBFORMS_COMPARE_PROOF_INCOMPLETE' }
foreach ($commit in @($beforeCommit, $afterCommit)) {
    $resolved = ([string](Git-Checked -Arguments @('-C', $toolRoot, 'rev-parse', "$commit^{commit}"))).Trim()
    if ($resolved -cne $commit) { throw 'WEBFORMS_COMPARE_COMMIT_UNAVAILABLE' }
}
if (!$OutputRoot) { $OutputRoot = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-report-compare-' + [Guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw 'WEBFORMS_COMPARE_OUTPUT_EXISTS' }
[void][IO.Directory]::CreateDirectory($OutputRoot)
$originalInputs = @(Input-Inventory)
$inputJson = $originalInputs | ConvertTo-Json -Depth 5 -Compress
Write-Output "comparisonProofRoot=$ProofRoot"
Write-Output "comparisonRoot=$OutputRoot"
$reviews = @{}
foreach ($side in @('before','after')) {
    $commit = if ($side -ceq 'before') { $beforeCommit } else { $afterCommit }
    $checkout = Join-Path $OutputRoot "code/$side"
    [void][IO.Directory]::CreateDirectory((Split-Path $checkout -Parent))
    Git-Checked -Arguments @('-C', $toolRoot, 'worktree', 'add', '--detach', $checkout, $commit) | Out-Host
    $actual = ([string](Git-Checked -Arguments @('-C', $checkout, 'rev-parse', 'HEAD'))).Trim()
    if ($actual -cne $commit) { throw 'WEBFORMS_COMPARE_CHECKOUT_MISMATCH' }
    $docs = Join-Path $OutputRoot $side
    [void][IO.Directory]::CreateDirectory($docs)
    Write-Host "Generating $side documents at $commit"
    # Both sides recompute graph reporting, rather than just rendering an old report.
    if ($side -ceq 'before') {
        & (Join-Path $checkout 'scripts/wp.ps1') -OutputRoot $ProofRoot -RecheckCompiledApi -IlMaxWork 30000000 -FillOnly |
            Tee-Object -FilePath (Join-Path $docs 'graph.local.log') | Out-Host
    }
    $viewArgs = @{ ProofRoot = $ProofRoot; FromSavedProof = $true; OutputRoot = $docs }
    if ($side -ceq 'after') { $viewArgs.RecheckApi = $true }
    $lines = @(& (Join-Path $checkout 'scripts/wview.ps1') @viewArgs |
        Tee-Object -FilePath (Join-Path $docs 'view.local.log'))
    $root = One-Line $lines 'standaloneReviewRoot='
    $handoff = Read-Handoff $root
    $reviews[$side] = [ordered]@{ commit = $commit; root = $root; pathCount = @($handoff.paths).Count;
        pathSha256 = Text-Hash (Path-Projection $handoff); gapCount = $handoff.coverage.gapCount;
        truncated = $handoff.coverage.truncated;
        handoffSha256 = (Get-FileHash -LiteralPath (Join-Path $root 'workbench/compiled-paths.local.json') -Algorithm SHA256).Hash.ToLowerInvariant() }
    if ((@(Input-Inventory) | ConvertTo-Json -Depth 5 -Compress) -cne $inputJson) {
        throw 'WEBFORMS_COMPARE_INPUTS_CHANGED'
    }
    Write-Output "comparison.$side.paths=$($reviews[$side].pathCount)"
}
$same = $reviews.before.pathSha256 -ceq $reviews.after.pathSha256 -and
    $reviews.before.gapCount -eq $reviews.after.gapCount -and
    $reviews.before.truncated -eq $reviews.after.truncated
$status = if ($same) { 'matching-retained-static-paths' } else { 'different-retained-static-paths' }
$boundedHash = Text-Hash "$beforeCommit`n$afterCommit`n$inputJson`n$($reviews.before.handoffSha256)`n$($reviews.after.handoffSha256)"
$receipt = [ordered]@{ schemaVersion = 'webforms-report-comparison.v1'; ruleId = 'diagnostic.webforms.report-comparison.v1';
    visibility = 'local-only'; status = $status;
    generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant();
    boundedInputSha256 = $boundedHash; canonicalization = 'pinned-commits-input-inventory-json-handoff-hashes-lf-v1';
    proofRoot = $ProofRoot; inputs = $originalInputs; reviews = $reviews;
    limitations = @('Selected saved proof only; no source scan, publish, combine, all-pages parity or runtime SQL claim.',
        'Compares retained method transitions, fact support, locations, rules, tiers, terminals, source identity, DLL provenance and pre-existing coverage/gap fields. New gap-detail fields and HTML formatting may differ.',
        'Derived reports are added beneath the proof root; inventoried inputs are checked unchanged after each run. Worktrees and outputs are retained; no cleanup is performed.') }
[IO.File]::WriteAllText((Join-Path $OutputRoot 'comparison.receipt.local.json'), (($receipt | ConvertTo-Json -Depth 12) + "`n"), [Text.UTF8Encoding]::new($false))
function Html([string]$Value) { [Net.WebUtility]::HtmlEncode($Value) }
$rows = foreach ($side in @('before','after')) {
    $root = $reviews[$side].root
    $index = ([Uri](Join-Path $root 'workbench/index.html')).AbsoluteUri
    $paths = ([Uri](Join-Path $root 'workbench/compiled-paths.local.html')).AbsoluteUri
    "<tr><td>$side</td><td>$(Html $reviews[$side].commit)</td><td>$($reviews[$side].pathCount)</td><td><a href='$(Html $index)'>Application docs</a> | <a href='$(Html $paths)'>Compiled method paths</a></td></tr>"
}
$landing = Join-Path $OutputRoot 'index.html'
[IO.File]::WriteAllText($landing, "<!doctype html><meta charset='utf-8'><title>Local report comparison</title><h1>Local Web Forms report comparison</h1><p>$(Html $status)</p><p>Selected saved proof only. Review-only static evidence; not all-pages or runtime execution validation.</p><table border='1'><tr><th>Version</th><th>Commit</th><th>Paths</th><th>Documents</th></tr>$($rows -join '')</table><p><a href='comparison.receipt.local.json'>Exact inputs and comparison receipt</a></p><p>Keep this folder and its code worktrees until the comparison has been reviewed. No cleanup was run.</p>", [Text.UTF8Encoding]::new($false))
Write-Output "comparisonStatus=$status"
Write-Output "comparisonIndex=$landing"
if (!$NoOpen) { Start-Process $landing }
if (!$same) { throw 'WEBFORMS_COMPARE_REPORTS_DIFFER' }
