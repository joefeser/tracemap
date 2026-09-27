[CmdletBinding()]
param(
    [string]$SourceSiteRoot,
    [string]$PublishedRoot,
    [string]$PagePath,
    [string]$HandlerName,
    [string]$SnapshotRoot,
    [string]$OutputRoot,
    [string]$TraceMapRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$PrepareOnly
)

# Local-only source snapshot for a Web Site whose Web.config was changed for
# compilation. The source checkout, index, branch, and publish stay untouched.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'WEBFORMS_SNAPSHOT_POWERSHELL_7_REQUIRED' }

function Read-Required([string]$Value, [string]$Prompt) {
    if (![string]::IsNullOrWhiteSpace($Value)) { return $Value.Trim() }
    $answer = Read-Host $Prompt
    if ([string]::IsNullOrWhiteSpace($answer)) { throw 'WEBFORMS_SNAPSHOT_VALUE_REQUIRED' }
    return $answer.Trim()
}

$SourceSiteRoot = [IO.Path]::GetFullPath((Read-Required $SourceSiteRoot 'Source Web Site folder')).TrimEnd('\', '/')
$PublishedRoot = [IO.Path]::GetFullPath((Read-Required $PublishedRoot 'Existing published Web Site folder')).TrimEnd('\', '/')
$PagePath = (Read-Required $PagePath 'Page path relative to the Web Site').Replace('\', '/')
$HandlerName = Read-Required $HandlerName 'Handler method name'
$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot).TrimEnd('\', '/')
$proof = Join-Path $TraceMapRoot 'scripts/Invoke-ExistingWebFormsPublishProof.ps1'
if (!(Test-Path -LiteralPath $proof -PathType Leaf) -or
    !(Test-Path -LiteralPath (Join-Path $SourceSiteRoot $PagePath) -PathType Leaf) -or
    !(Test-Path -LiteralPath (Join-Path $SourceSiteRoot 'Web.config') -PathType Leaf)) {
    throw 'WEBFORMS_SNAPSHOT_INPUT_UNAVAILABLE'
}
# Pin the public proof implementation so a local deletion of its provenance
# check cannot be used through this wrapper. Accept exact bytes or Git's
# checkout-filtered representation of that one public script.
$expectedProofBlob = 'a9167ed3f24bc779084a8c7036f324435b12421c'
$proofRawBlob = ([string](& git -C $TraceMapRoot hash-object --no-filters $proof)).Trim()
$rawValid = $LASTEXITCODE -eq 0 -and $proofRawBlob -cmatch '^[0-9a-f]{40}$'
$proofFilteredBlob = ([string](& git -C $TraceMapRoot hash-object `
    '--path=scripts/Invoke-ExistingWebFormsPublishProof.ps1' $proof)).Trim()
$filteredValid = $LASTEXITCODE -eq 0 -and $proofFilteredBlob -cmatch '^[0-9a-f]{40}$'
if (!$rawValid -or !$filteredValid -or
    ($proofRawBlob -cne $expectedProofBlob -and $proofFilteredBlob -cne $expectedProofBlob)) {
    throw 'WEBFORMS_SNAPSHOT_PROOF_MODIFIED'
}

$repositoryRoot = ([string](& git -C $SourceSiteRoot rev-parse --show-toplevel)).Trim()
$sourcePrefix = ([string](& git -C $SourceSiteRoot rev-parse --show-prefix)).Trim()
$head = ([string](& git -C $SourceSiteRoot rev-parse HEAD)).Trim()
if ($LASTEXITCODE -ne 0 -or $head -cnotmatch '^[0-9a-f]{40}$') {
    throw 'WEBFORMS_SNAPSHOT_COMMIT_UNAVAILABLE'
}
$dirty = @(& git -C $SourceSiteRoot status --porcelain --untracked-files=all -- .)
if ($LASTEXITCODE -ne 0 -or $dirty.Count -ne 0) { throw 'WEBFORMS_SNAPSHOT_SOURCE_DIRTY' }

$trackedRaw = [string](& git -C $SourceSiteRoot ls-tree -r -z --name-only HEAD -- .)
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_SNAPSHOT_TREE_UNAVAILABLE' }
$paths = @($trackedRaw.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
if ($paths.Count -gt 4096) { throw 'WEBFORMS_SNAPSHOT_FILE_LIMIT' }
$configPaths = @($paths | Where-Object { $_.Equals('Web.config', [StringComparison]::OrdinalIgnoreCase) })
if ($configPaths.Count -ne 1) { throw 'WEBFORMS_SNAPSHOT_CONFIG_NOT_UNIQUE' }
$configPath = [string]$configPaths[0]
$changedConfig = 0
$changedOther = 0
$hashErrors = 0
$totalBytes = [long]0
$inputLines = [Collections.Generic.List[string]]::new()
foreach ($path in $paths) {
    $physical = Join-Path $SourceSiteRoot $path
    if (!(Test-Path -LiteralPath $physical -PathType Leaf) -or
        (Get-Item -LiteralPath $physical).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
        $hashErrors++
        continue
    }
    $length = (Get-Item -LiteralPath $physical).Length
    if ($length -gt 67108864 -or $totalBytes + $length -gt 536870912) {
        throw 'WEBFORMS_SNAPSHOT_BYTE_LIMIT'
    }
    $totalBytes += $length
    $inputLines.Add("$($path):$((Get-FileHash -LiteralPath $physical -Algorithm SHA256).Hash.ToLowerInvariant())")
    $expected = ([string](& git -C $SourceSiteRoot rev-parse "HEAD:$sourcePrefix$path")).Trim()
    $expectedValid = $LASTEXITCODE -eq 0 -and $expected -cmatch '^[0-9a-f]{40}$'
    $raw = ([string](& git -C $SourceSiteRoot hash-object --no-filters $physical)).Trim()
    $rawValid = $LASTEXITCODE -eq 0 -and $raw -cmatch '^[0-9a-f]{40}$'
    $filtered = ''
    $filteredValid = $false
    if ($rawValid -and $raw -cne $expected) {
        $filtered = ([string](& git -C $SourceSiteRoot hash-object "--path=$sourcePrefix$path" $physical)).Trim()
        $filteredValid = $LASTEXITCODE -eq 0 -and $filtered -cmatch '^[0-9a-f]{40}$'
    }
    if (!$expectedValid -or !$rawValid -or ($raw -cne $expected -and !$filteredValid)) {
        $hashErrors++
        continue
    }
    if ($expected -cne $raw -and $expected -cne $filtered) {
        if ($path.Equals($configPath, [StringComparison]::Ordinal)) { $changedConfig++ }
        else { $changedOther++ }
    }
}
Write-Output "sourceSnapshotConfigDifferences=$changedConfig"
Write-Output "sourceSnapshotOtherDifferences=$changedOther"
Write-Output "sourceSnapshotHashErrors=$hashErrors"
if ($changedOther -ne 0 -or $hashErrors -ne 0 -or $changedConfig -gt 1) {
    throw 'WEBFORMS_SNAPSHOT_SOURCE_NOT_CONFIG_ONLY'
}
$boundedInputSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes("parent:$head`n" + ($inputLines -join "`n") + "`n"))).ToLowerInvariant()
if ($changedConfig -eq 0) {
    Write-Output 'sourceSnapshotKind=existing-commit'
    & $proof -SourceSiteRoot $SourceSiteRoot -PublishedRoot $PublishedRoot -PagePath $PagePath `
        -HandlerName $HandlerName -IncludeAllPublishedAssembliesAsContext -FailOnUnclassifiedAssemblies `
        -TraceMapRoot $TraceMapRoot -OutputRoot $OutputRoot -PrepareOnly:$PrepareOnly
    return
}

# Require the existing bounded source admission to identify exactly the one
# config mismatch before constructing any local Git objects.
$preflightLines = [Collections.Generic.List[string]]::new()
$preflightError = ''
try {
    & $proof -SourceSiteRoot $SourceSiteRoot -PublishedRoot $PublishedRoot -PagePath $PagePath `
        -HandlerName $HandlerName -IncludeAllPublishedAssembliesAsContext -FailOnUnclassifiedAssemblies `
        -TraceMapRoot $TraceMapRoot -PrepareOnly |
        ForEach-Object { $preflightLines.Add([string]$_) }
} catch { $preflightError = $_.Exception.Message }
if ($preflightError -ne 'WEBFORMS_EXISTING_PUBLISH_SOURCE_MISMATCH' -or
    !$preflightLines.Contains('sourceMismatchCount=1') -or
    !$preflightLines.Contains('sourceMismatchConfigCount=1') -or
    !$preflightLines.Contains('sourceMismatchHashErrorCount=0')) {
    throw 'WEBFORMS_SNAPSHOT_PREFLIGHT_NOT_CONFIG_ONLY'
}

$snapshotRoot = if ($SnapshotRoot) { [IO.Path]::GetFullPath($SnapshotRoot) } else {
    Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-source-' + [guid]::NewGuid().ToString('N'))
}
if (Test-Path -LiteralPath $snapshotRoot) { throw 'WEBFORMS_SNAPSHOT_OUTPUT_NOT_FRESH' }
foreach ($inputRoot in @($SourceSiteRoot, $PublishedRoot, $TraceMapRoot)) {
    if ($snapshotRoot.StartsWith($inputRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'WEBFORMS_SNAPSHOT_OUTPUT_INSIDE_INPUT'
    }
}
$repoConfigPath = $sourcePrefix + $configPath
$configFile = Join-Path $SourceSiteRoot $configPath
$blob = ([string](& git -C $repositoryRoot hash-object -w "--path=$repoConfigPath" $configFile)).Trim()
if ($LASTEXITCODE -ne 0 -or $blob -cnotmatch '^[0-9a-f]{40}$') {
    throw 'WEBFORMS_SNAPSHOT_BLOB_FAILED'
}
$entry = ([string](& git -C $repositoryRoot ls-tree HEAD -- $repoConfigPath)).Trim()
if ($LASTEXITCODE -ne 0 -or $entry -cnotmatch '^(100644|100755) blob [0-9a-f]{40}\s') {
    throw 'WEBFORMS_SNAPSHOT_CONFIG_ENTRY_INVALID'
}
$mode = $Matches[1]
$indexFile = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-webforms-index-' + [guid]::NewGuid().ToString('N'))
$priorIndex = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $indexFile
    & git -C $repositoryRoot read-tree HEAD
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_SNAPSHOT_INDEX_FAILED' }
    & git -C $repositoryRoot update-index --add --cacheinfo "$mode,$blob,$repoConfigPath"
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_SNAPSHOT_INDEX_FAILED' }
    $tree = ([string](& git -C $repositoryRoot write-tree)).Trim()
    if ($LASTEXITCODE -ne 0 -or $tree -cnotmatch '^[0-9a-f]{40}$') {
        throw 'WEBFORMS_SNAPSHOT_TREE_FAILED'
    }
} finally {
    if ($null -eq $priorIndex) { Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue }
    else { $env:GIT_INDEX_FILE = $priorIndex }
    if (Test-Path -LiteralPath $indexFile) { Remove-Item -LiteralPath $indexFile -Force }
}
$snapshotCommit = ([string](& git -C $repositoryRoot -c user.name=TraceMapLocalSnapshot `
    -c user.email=local-snapshot@example.invalid commit-tree $tree -p $head `
    -m 'Local Web Forms publish source snapshot')).Trim()
if ($LASTEXITCODE -ne 0 -or $snapshotCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'WEBFORMS_SNAPSHOT_COMMIT_FAILED'
}
& git -C $repositoryRoot worktree add --detach -- $snapshotRoot $snapshotCommit *> $null
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_SNAPSHOT_WORKTREE_FAILED' }
$snapshotSite = Join-Path $snapshotRoot $sourcePrefix
if (@(& git -C $snapshotSite status --porcelain --untracked-files=all -- .).Count -ne 0) {
    throw 'WEBFORMS_SNAPSHOT_WORKTREE_DIRTY'
}
$snapshotReceipt = [ordered]@{
    schemaVersion = 'webforms-local-config-snapshot.v1'
    visibility = 'local-only'
    generatorSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
    boundedInputSha256 = $boundedInputSha256
    boundedFileCount = $paths.Count
    boundedInputBytes = $totalBytes
    parentCommitSha = $head
    snapshotCommitSha = $snapshotCommit
    configOnlyTrackedDifference = $true
}
[IO.File]::WriteAllText(($snapshotRoot + '.receipt.local.json'),
    (($snapshotReceipt | ConvertTo-Json -Depth 5) + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output 'sourceSnapshotKind=local-config-commit'
Write-Output "sourceSnapshotParent=$head"
Write-Output "sourceSnapshotCommit=$snapshotCommit"
Write-Output 'sourceSnapshotLocalOnly=true'
& $proof -SourceSiteRoot $snapshotSite -PublishedRoot $PublishedRoot -PagePath $PagePath `
    -HandlerName $HandlerName -IncludeAllPublishedAssembliesAsContext -FailOnUnclassifiedAssemblies `
    -TraceMapRoot $TraceMapRoot -OutputRoot $OutputRoot -PrepareOnly:$PrepareOnly
