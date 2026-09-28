Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script = Join-Path (Split-Path $PSScriptRoot -Parent) 'wcompare.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-compare-test-' + [Guid]::NewGuid().ToString('N'))
$proof = Join-Path $temp 'proof'
$required = @('publish-receipt.local.json','handler-paths.json','combined.sqlite','scan/scan-manifest.json',
    'probe/scan-manifest.json','combined-ilwork-30000000.sqlite','scan-ilwork-30000000/scan-manifest.json',
    'scan-ilwork-30000000/facts.ndjson')
$global:CompareHeads = @{}
$global:CompareMutation = ''
$global:CompareGitCalls = [Collections.Generic.List[string]]::new()
function global:git {
    $arguments = @($args)
    $global:CompareGitCalls.Add(($arguments -join ' '))
    $global:LASTEXITCODE = 0
    if ($arguments[2] -ceq 'rev-parse') {
        if ($global:CompareMutation -ceq 'git-failure') { $global:LASTEXITCODE = 1; return }
        if ($arguments[3] -ceq 'HEAD') { return $global:CompareHeads[[string]$arguments[1]] }
        return ([string]$arguments[3]).Replace('^{commit}', '')
    }
    if ($arguments[2] -cne 'worktree' -or $arguments[3] -cne 'add') { throw 'Unexpected Git operation' }
    $checkout = [string]$arguments[5]
    $global:CompareHeads[$checkout] = [string]$arguments[6]
    [void][IO.Directory]::CreateDirectory((Join-Path $checkout 'scripts'))
    [IO.File]::WriteAllText((Join-Path $checkout 'scripts/wp.ps1'), @'
param($OutputRoot, [switch]$RecheckCompiledApi, $IlMaxWork, [switch]$FillOnly)
if (!$RecheckCompiledApi -or $IlMaxWork -ne 30000000 -or !$FillOnly) { throw 'Graph was not recomputed at the correct budget' }
Write-Output 'compiledApiPaths=1'
'@)
    [IO.File]::WriteAllText((Join-Path $checkout 'scripts/wview.ps1'), @'
param($ProofRoot, [switch]$FromSavedProof, $OutputRoot, [switch]$RecheckApi)
$side = Split-Path (Split-Path $PSScriptRoot -Parent) -Leaf
if (!$FromSavedProof -or ($side -ceq 'after' -and !$RecheckApi) -or ($side -ceq 'before' -and $RecheckApi)) { throw 'Wrong view routing' }
$root = Join-Path $OutputRoot 'fresh-review'
[void][IO.Directory]::CreateDirectory((Join-Path $root 'workbench'))
$hop = [ordered]@{ ordinal = 1; from = @{ name = 'Public.Lookup.Init'; scanId = 'public-scan'; commitSha = ('a' * 40) }; to = @{ name = 'compiled:DbDataAdapter.Fill'; scanId = 'public-scan'; commitSha = ('a' * 40) };
    edgeKind = 'compiled-database-api-candidate'; ruleId = 'public.rule.v1'; evidenceTier = 'Tier3SyntaxOrTextual';
    filePath = 'compiled:public'; startLine = 1; endLine = 1; supportingFactIds = @('public-fact') }
$path = @{ classification = 'NeedsReview'; claim = 'review-only-static-path'; terminalKind = 'database-api'; hops = @($hop); supportingFactIds = @('public-fact') }
$value = @{ schemaVersion = 'webforms-compiled-path-handoff.v1'; ruleId = 'diagnostic.webforms.compiled-path-handoff.v1'; claimLevel = 'review-only-static-evidence';
    provenance = @{ sourceCommitSha = ('a' * 40); scanId = 'public-scan'; scanFolder = 'scan-ilwork-30000000'; combinedIndex = 'combined-ilwork-30000000.sqlite' };
    query = @{ fromSymbol = 'Public.Lookup.Init'; toSurface = 'database-api'; maxDepth = 20; maxPaths = 256 };
    assemblies = @(@{ rawFileSha256 = ('b' * 64); safeLocator = 'compiled:public'; provenanceState = 'bound' });
    paths = @($path); coverage = @{ gapCount = 3; truncated = $true; reportCoverage = 'ReducedCoverage'; warnings = @();
        gapCounts = @(); omittedGapGroupCount = 0; omittedGapDetailCount = 0; gaps = @() } }
if ($side -ceq 'after') {
    $value.coverage.newGapDetail = 'New provenance detail intentionally not part of parity'
    switch ($global:CompareMutation) {
        'method' { $hop.to.name = 'Other.Fill' }
        'tier' { $hop.evidenceTier = 'Tier2Structural' }
        'dll' { $value.assemblies[0].rawFileSha256 = ('c' * 64) }
        'gaps' { $value.coverage.gapCount = 2 }
        'empty' { $value.paths = @() }
        'input' { [IO.File]::WriteAllText((Join-Path $ProofRoot 'handler-paths.json'), 'changed') }
    }
}
[IO.File]::WriteAllText((Join-Path $root 'workbench/compiled-paths.local.json'), ($value | ConvertTo-Json -Depth 20))
[IO.File]::WriteAllText((Join-Path $root 'workbench/index.html'), '<p>Public application docs</p>')
[IO.File]::WriteAllText((Join-Path $root 'workbench/compiled-paths.local.html'), '<p>Public method paths</p>')
Write-Output "standaloneReviewRoot=$root"
'@)
}
try {
    foreach ($name in $required) {
        $file = Join-Path $proof $name
        [void][IO.Directory]::CreateDirectory((Split-Path $file -Parent))
        [IO.File]::WriteAllText($file, 'public synthetic input')
    }
    $output = Join-Path $temp 'match'
    $result = @(& $script -ProofRoot $proof -OutputRoot $output -NoOpen)
    if ($result -cnotcontains 'comparisonStatus=matching-retained-static-paths') { throw 'Valid parity did not pass' }
    $receipt = [IO.File]::ReadAllText((Join-Path $output 'comparison.receipt.local.json')) | ConvertFrom-Json -Depth 20
    if ($receipt.generatorSha256 -cne (Get-FileHash $script -Algorithm SHA256).Hash.ToLowerInvariant() -or
        $receipt.boundedInputSha256 -cnotmatch '^[0-9a-f]{64}$' -or $receipt.inputs.Count -ne 8 -or
        $receipt.reviews.before.commit -cne 'e8121dc1c147923c39adcc1cb3cdbff5fd349d7e' -or
        $receipt.reviews.after.commit -cne 'f7891d980c2fd8080b70c14d0ea07a834598d35a' -or
        !(Test-Path (Join-Path $output 'index.html'))) { throw 'Comparison omitted source authority or landing page' }
    $expectedText = "$($receipt.reviews.before.commit)`n$($receipt.reviews.after.commit)`n$($receipt.inputs | ConvertTo-Json -Depth 5 -Compress)`n$($receipt.reviews.before.handoffSha256)`n$($receipt.reviews.after.handoffSha256)"
    $expectedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($expectedText))).ToLowerInvariant()
    if ($receipt.boundedInputSha256 -cne $expectedHash) { throw 'Comparison input digest omitted actual handoffs' }
    foreach ($mutation in @('method','tier','dll','gaps','empty','input','git-failure')) {
        $global:CompareMutation = $mutation
        $failure = $null
        try { & $script -ProofRoot $proof -OutputRoot (Join-Path $temp $mutation) -NoOpen | Out-Null }
        catch { $failure = $_.Exception.Message }
        $expected = switch ($mutation) {
            'empty' { 'WEBFORMS_COMPARE_NO_VALID_COMPILED_PATHS' }
            'input' { 'WEBFORMS_COMPARE_INPUTS_CHANGED' }
            'git-failure' { 'WEBFORMS_COMPARE_GIT_FAILED' }
            default { 'WEBFORMS_COMPARE_REPORTS_DIFFER' }
        }
        if ($failure -cne $expected) { throw "Mutation $mutation not rejected: $failure" }
        [IO.File]::WriteAllText((Join-Path $proof 'handler-paths.json'), 'public synthetic input')
    }
    $global:CompareMutation = ''
    $existingFailure = $null
    try { & $script -ProofRoot $proof -OutputRoot $output -NoOpen | Out-Null } catch { $existingFailure = $_.Exception.Message }
    if ($existingFailure -cne 'WEBFORMS_COMPARE_OUTPUT_EXISTS') { throw 'Existing comparison could be overwritten' }
    if (@($global:CompareGitCalls | Where-Object { $_ -match '\b(fetch|switch|reset|clean|remove|prune)\b' }).Count -gt 0) { throw 'Comparison touched current checkout or cleaned inputs' }
    Write-Output 'webFormsReportComparisonPublicTests=passed'
}
finally {
    Remove-Item Function:global:git -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $temp -Recurse -Force
    Remove-Variable CompareHeads,CompareMutation,CompareGitCalls -Scope Global -ErrorAction SilentlyContinue
}
