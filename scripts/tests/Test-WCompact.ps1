Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script = Join-Path (Split-Path $PSScriptRoot -Parent) 'wcompact.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-compact-test-' + [Guid]::NewGuid().ToString('N'))
function Node([string]$Name) { @{ name = $Name; displayLabel = 'Public.Fill()'; scanId = 'public-scan'; commitSha = ('a' * 40) } }
function Path([string]$Id, [string]$Method, [string]$Fact, [string]$Tier) {
    @{ pathId = $Id; classification = 'NeedsReviewPath'; claim = 'review-only-static-path'; terminalKind = 'database-api';
        supportingFactIds = @($Fact); hops = @(@{ ordinal = 1; from = (Node 'Public.Init(Object)'); to = (Node $Method);
            edgeKind = 'compiled-database-api-candidate'; ruleId = 'public.rule.v1'; evidenceTier = $Tier;
            filePath = '<private>&file'; startLine = 1; endLine = 2; supportingFactIds = @($Fact) }) }
}
try {
    $root = Join-Path $temp 'tracemap-report-compare-public'
    $review = Join-Path $root 'after/review'
    $file = Join-Path $review 'workbench/compiled-paths.local.json'
    [void][IO.Directory]::CreateDirectory((Split-Path $file -Parent))
    $value = @{ schemaVersion = 'webforms-compiled-path-handoff.v1'; ruleId = 'diagnostic.webforms.compiled-path-handoff.v1';
        claimLevel = 'review-only-static-evidence'; coverage = @{ truncated = $true; gapCount = 7 };
        paths = @((Path 'path:0002' 'Public.Fill(DataSet)' 'fact-a' 'Tier3SyntaxOrTextual'),
            (Path 'path:0003' 'Public.Fill(DataSet)' 'fact-b' 'Tier2Structural'),
            (Path 'path:0008' 'Public.Fill(DataTable)' 'fact-c' 'Tier3SyntaxOrTextual')) }
    [IO.File]::WriteAllText($file, ($value | ConvertTo-Json -Depth 15))
    [IO.File]::WriteAllText((Join-Path $root 'comparison.receipt.local.json'), (@{
        schemaVersion = 'webforms-report-comparison.v1'; reviews = @{ after = @{ root = $review } } } | ConvertTo-Json -Depth 5))
    $original = (Get-FileHash -LiteralPath $file).Hash
    $result = @(& $script -SearchRoot $temp -NoOpen)
    if ($result -cnotcontains 'compactChains=2' -or $result -cnotcontains 'compactEvidencePaths=3') { throw 'Overloads conflated or paths lost' }
    $output = ([string]($result | Where-Object { $_.StartsWith('compactHtml=') })).Substring(12)
    $html = [IO.File]::ReadAllText($output)
    foreach ($text in @('path:0002','path:0003','path:0008','fact-a','fact-b','fact-c','Tier2Structural','Tier3SyntaxOrTextual',
        'Public.Fill(DataSet)','Public.Fill(DataTable)','public.rule.v1','&lt;private&gt;&amp;file',$original.ToLowerInvariant(),
        (Get-FileHash -LiteralPath $script).Hash.ToLowerInvariant())) {
        if (!$html.Contains($text, [StringComparison]::Ordinal)) { throw "Grouped HTML lost retained data: $text" }
    }
    if ([regex]::Matches($html, '<section>').Count -ne 2 -or [regex]::Matches($html, 'data-path-id=').Count -ne 3) { throw 'Wrong group or variant cardinality' }
    if ((Get-FileHash -LiteralPath $file).Hash -cne $original) { throw 'Original handoff changed' }
    $again = @(& $script -HandoffPath $file -NoOpen)
    if ($again -ccontains "compactHtml=$output" -or !(Test-Path -LiteralPath $output)) { throw 'Prior grouped view overwritten' }
    $value.paths = @()
    [IO.File]::WriteAllText($file, ($value | ConvertTo-Json -Depth 15))
    $failure = $null
    try { & $script -HandoffPath $file -NoOpen | Out-Null } catch { $failure = $_.Exception.Message }
    if ($failure -cne 'WEBFORMS_COMPACT_HANDOFF_INVALID') { throw 'Empty handoff accepted' }
    Write-Output 'webFormsCompactPathReviewPublicTests=passed'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
