Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script = Join-Path (Split-Path $PSScriptRoot -Parent) 'wgroups.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-group-test-' + [Guid]::NewGuid().ToString('N'))
function Node([string]$Name, [string]$Display) { @{ name = $Name; displayLabel = $Display; scanId = 'public-scan'; commitSha = ('a' * 40) } }
function Path([string]$Id, [string]$Method, [string]$Fact, [string]$Tier = 'Tier3SyntaxOrTextual') {
    @{ pathId = $Id; classification = 'NeedsReviewPath'; claim = 'review-only-static-path'; terminalKind = 'database-api';
        supportingFactIds = @($Fact); hops = @(@{ ordinal = 1; from = (Node 'Public.Init(Object)' 'Public.Init()');
            to = (Node $Method 'Public.Fill()'); edgeKind = 'compiled-database-api-candidate'; ruleId = 'public.rule.v1';
            evidenceTier = $Tier; filePath = 'compiled:public'; startLine = 1; endLine = 1; supportingFactIds = @($Fact) }) }
}
try {
    $root = Join-Path $temp 'tracemap-report-compare-public'
    $review = Join-Path $root 'after/review'
    $file = Join-Path $review 'workbench/compiled-paths.local.json'
    [void][IO.Directory]::CreateDirectory((Split-Path $file -Parent))
    $value = @{ schemaVersion = 'webforms-compiled-path-handoff.v1'; ruleId = 'diagnostic.webforms.compiled-path-handoff.v1';
        claimLevel = 'review-only-static-evidence'; paths = @(
            (Path 'path:0002' 'Public.Fill(DataSet)' 'fact-a'),
            (Path 'path:0003' 'Public.Fill(DataSet)' 'fact-a'),
            (Path 'path:0004' 'Public.Fill(DataSet)' 'fact-b'),
            (Path 'path:0008' 'Public.Fill(DataTable)' 'fact-c'),
            (Path 'path:0011' 'Public.Fill(DataSet)' 'fact-a' 'Tier2Structural')) }
    [IO.File]::WriteAllText($file, ($value | ConvertTo-Json -Depth 15))
    [IO.File]::WriteAllText((Join-Path $root 'comparison.receipt.local.json'), (@{
        schemaVersion = 'webforms-report-comparison.v1'; reviews = @{ after = @{ root = $review } } } | ConvertTo-Json -Depth 5))
    $original = (Get-FileHash -LiteralPath $file).Hash
    $result = @(& $script -SearchRoot $temp)
    if ($result -cnotcontains 'Paths=5; exact method chains=2; displayed chains=1' -or
        $result -cnotcontains '  path:0002, path:0003, path:0004, path:0011: 4 paths; evidence variants=3' -or
        $result -cnotcontains '  path:0002, path:0003, path:0004, path:0008, path:0011: 5 paths; exact chain variants=2') {
        throw 'Grouping conflated overloads, lost evidence variants or did not discover the saved comparison'
    }
    if (($result -join "`n") -match 'Public\.|public-scan|fact-a|compiled:public') { throw 'Console leaked method or evidence details' }
    if ((Get-FileHash -LiteralPath $file).Hash -cne $original) { throw 'Input was modified' }
    foreach ($mutation in @('empty','duplicate','disconnected','claim')) {
        $changed = ($value | ConvertTo-Json -Depth 15) | ConvertFrom-Json -Depth 15
        switch ($mutation) {
            'empty' { $changed.paths = @() }
            'duplicate' { $changed.paths[1].pathId = 'path:0002' }
            'disconnected' { $changed.paths[0].hops[0].to.name = '' }
            'claim' { $changed.claimLevel = 'runtime' }
        }
        [IO.File]::WriteAllText($file, ($changed | ConvertTo-Json -Depth 15))
        $failure = $null
        try { & $script -HandoffPath $file | Out-Null } catch { $failure = $_.Exception.Message }
        if ($failure -notmatch '^WEBFORMS_GROUP_(HANDOFF|PATH)_INVALID$') { throw "Unsafe $mutation input accepted: $failure" }
    }
    Write-Output 'webFormsPathGroupingPublicTests=passed'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
