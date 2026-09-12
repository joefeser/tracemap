$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Invoke-WitsApplicationReview.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-wits-application-review-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
$packetPath = Join-Path $temp 'webforms-modernization.json'
$packet = [ordered]@{
    schemaVersion = 'webforms-modernization-packet.v1'; packetId = 'packet-one'; ruleId = 'legacy.webforms.modernization-packet.v1'
    sources = @(@{ scanId = 'scan-one'; commitSha = ('a' * 40) })
    surfaces = @(
        @{ surfaceId = 'surface-two'; evidence = @{ factId = 'fact-two'; filePath = 'Pages/Second.aspx' } },
        @{ surfaceId = 'surface-one'; evidence = @{ factId = 'fact-one'; filePath = 'Pages/First.aspx' } }
    )
}
[IO.File]::WriteAllText($packetPath, (($packet | ConvertTo-Json -Depth 12) + "`n"), [Text.UTF8Encoding]::new($false))
$packetHash = (Get-FileHash $packetPath -Algorithm SHA256).Hash
function dotnet { $global:LASTEXITCODE = 0 }
function Expect-Failure([string]$Code, [scriptblock]$Action) { try { & $Action; throw "Expected $Code" } catch { if (!$_.Exception.Message.Contains($Code, [StringComparison]::Ordinal)) { throw } } }
try {
    $first = Join-Path $temp 'review-one.json'
    $second = Join-Path $temp 'review-two.json'
    & $scriptPath -Mode Export -PacketPath $packetPath -ReviewPath $first | Out-Null
    & $scriptPath -Mode Export -PacketPath $packetPath -ReviewPath $second | Out-Null
    if ((Get-FileHash $first).Hash -ne (Get-FileHash $second).Hash) { throw 'Application review export was not deterministic.' }
    & $scriptPath -Mode Validate -PacketPath $packetPath -ReviewPath $first | Out-Null
    if ([IO.File]::ReadAllText($first).Contains('Pages/First.aspx', [StringComparison]::Ordinal)) { throw 'Application review overlay leaked a retained private path.' }
    $review = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 30
    if ($review.decisions[0].pageId -ne 'page-001' -or $review.decisions[0].surfaceId -ne 'surface-one') { throw 'Application review decisions were not ordered by retained page path.' }
    $review.reviewState = 'completed'; $review.reviewer = 'reviewer-001'; $review.reviewedAtUtc = '2026-09-11T18:00:00Z'
    foreach ($decision in $review.decisions) { $decision.verdict = 'needs-review'; $decision.migrationDisposition = 'defer' }
    $completed = Join-Path $temp 'completed.json'
    [IO.File]::WriteAllText($completed, (($review | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
    & $scriptPath -Mode Validate -PacketPath $packetPath -ReviewPath $completed | Out-Null
    $invalidDraftReviewer = [IO.File]::ReadAllText($first) | ConvertFrom-Json -Depth 30
    $invalidDraftReviewer.reviewer = 42
    $invalidDraftReviewer.reviewedAtUtc = '2026-09-11T18:00:00Z'
    $invalidDraftReviewerPath = Join-Path $temp 'invalid-draft-reviewer.json'
    [IO.File]::WriteAllText($invalidDraftReviewerPath, (($invalidDraftReviewer | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
    Expect-Failure 'WITS_APPLICATION_REVIEW_COMPLETION_INVALID' { & $scriptPath -Mode Validate -PacketPath $packetPath -ReviewPath $invalidDraftReviewerPath }
    $invalidDraftReviewer.reviewer = 'r' * 129
    [IO.File]::WriteAllText($invalidDraftReviewerPath, (($invalidDraftReviewer | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
    Expect-Failure 'WITS_APPLICATION_REVIEW_COMPLETION_INVALID' { & $scriptPath -Mode Validate -PacketPath $packetPath -ReviewPath $invalidDraftReviewerPath }
    $bad = [IO.File]::ReadAllText($completed) | ConvertFrom-Json -Depth 30
    $bad.decisions[1].surfaceId = 'surface-one'
    $badPath = Join-Path $temp 'bad.json'
    [IO.File]::WriteAllText($badPath, (($bad | ConvertTo-Json -Depth 20) + "`n"), [Text.UTF8Encoding]::new($false))
    Expect-Failure 'WITS_APPLICATION_REVIEW_REFERENCE_MISMATCH' { & $scriptPath -Mode Validate -PacketPath $packetPath -ReviewPath $badPath }
    if ((Get-FileHash $packetPath -Algorithm SHA256).Hash -ne $packetHash) { throw 'Application review workflow modified its packet.' }
    Write-Host 'PASS WITS Web Forms application review overlay'
}
finally { Remove-Item Function:\dotnet -ErrorAction SilentlyContinue; if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force } }
