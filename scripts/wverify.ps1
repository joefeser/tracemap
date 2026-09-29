[CmdletBinding()]
param(
    [string]$ReviewRoot,
    [string]$ProofRoot,
    [string]$PublishedRoot,
    [string]$SourceBase,
    [string]$OutputRoot,
    [switch]$Run,
    [switch]$Diagnose,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Run -and $Diagnose) { throw 'WEBFORMS_VERIFY_DIAGNOSE_CANNOT_RUN' }
function Read-Required([string]$Value, [string]$Prompt) {
    if ([string]::IsNullOrWhiteSpace($Value)) { $Value = Microsoft.PowerShell.Utility\Read-Host $Prompt }
    if ([string]::IsNullOrWhiteSpace($Value)) { throw 'WEBFORMS_VERIFY_SELECTION_REQUIRED' }
    return [IO.Path]::GetFullPath($Value.Trim().Trim('"'))
}
Write-Host 'Verify the retained Web Forms proof. No rebuild of your website, new attestation, cleanup or two-repository merge.'
$ReviewRoot = Read-Required $ReviewRoot 'Copied Web Forms review folder (contains native-config)'
$draft = Join-Path $ReviewRoot 'native-config/review.draft.json'
if (!(Test-Path -LiteralPath $draft -PathType Leaf)) { throw 'WEBFORMS_VERIFY_MIGRATION_DRAFT_UNAVAILABLE' }
if ([string]::IsNullOrWhiteSpace($ProofRoot)) {
    # Offer bounded immediate TEMP children; the owner must explicitly select one.
    # Neither modification time nor a newest-folder heuristic selects authority.
    $candidates = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'tracemap-existing-publish-*' |
        Where-Object { (Test-Path -LiteralPath (Join-Path $_.FullName 'publish-receipt.local.json') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'compiled-binding.local.json') -PathType Leaf) } | Sort-Object Name | Select-Object -First 101)
    if ($candidates.Count -gt 100) { throw 'WEBFORMS_VERIFY_TOO_MANY_PROOFS;pass-explicit-ProofRoot' }
    for ($i = 0; $i -lt $candidates.Count; $i++) { Write-Host "[$($i + 1)] $($candidates[$i].Name)" }
    $selection = Microsoft.PowerShell.Utility\Read-Host 'Retained proof folder: enter its number above, or its full path'
    $number = 0
    if ([int]::TryParse($selection, [ref]$number)) {
        if ($number -lt 1 -or $number -gt $candidates.Count) { throw 'WEBFORMS_VERIFY_PROOF_SELECTION_INVALID' }
        $ProofRoot = $candidates[$number - 1].FullName
    } else { $ProofRoot = $selection }
}
$ProofRoot = Read-Required $ProofRoot 'Retained proof folder (contains publish-receipt and compiled-binding)'
$PublishedRoot = Read-Required $PublishedRoot 'Original compiled website folder (parent of bin)'
if ([string]::IsNullOrWhiteSpace($SourceBase)) {
    $SourceBase = Microsoft.PowerShell.Utility\Read-Host 'Website folder relative to repository root (for example WebSite; enter . only if website is at repository root)'
}
if ([string]::IsNullOrWhiteSpace($SourceBase)) { throw 'WEBFORMS_VERIFY_SOURCE_BASE_REQUIRED' }
$SourceBase = $SourceBase.Trim().Trim('"')
foreach ($name in @('publish-receipt.local.json', 'compiled-binding.local.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $ProofRoot $name) -PathType Leaf)) { throw 'WEBFORMS_VERIFY_RETAINED_RECEIPTS_UNAVAILABLE' }
}
if (!(Test-Path -LiteralPath (Join-Path $PublishedRoot 'bin') -PathType Container)) { throw 'WEBFORMS_VERIFY_PUBLISHED_BIN_UNAVAILABLE' }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $ReviewRoot 'native-proof-verification' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (!$Diagnose -and (Test-Path -LiteralPath $OutputRoot)) { throw 'WEBFORMS_VERIFY_OUTPUT_EXISTS;preserved-unchanged-select-a-new-OutputRoot' }
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj'
if (!$NoBuild) {
    dotnet build $project --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_VERIFY_TOOL_BUILD_FAILED' }
}
$cli = Join-Path $repo 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
$configuration = Join-Path $OutputRoot 'configuration'
$diagnosticArgs = @()
if ($Diagnose) { $diagnosticArgs = @('--diagnose') }
dotnet $cli webforms-review import-proof --config $draft --proof-root $ProofRoot --published-root $PublishedRoot --out $configuration --source-base $SourceBase @diagnosticArgs
if ($Diagnose) {
    Write-Host 'Diagnostics only: no review/proof inputs or output folders were written; no scan started.'
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_VERIFY_DIAGNOSIS_NOT_ADMITTED;originals-preserved' }
    return
}
if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_VERIFY_IMPORT_FAILED;original-inputs-preserved;no-scan-started' }
$config = Join-Path $configuration 'review-config.local.json'
Write-Host "verifiedConfiguration=$config"
Write-Host "verificationReceipt=$(Join-Path $configuration 'proof-import.local.json')"
if ($Run) {
    $review = Join-Path $OutputRoot 'review'
    dotnet $cli webforms-review start --config $config --out $review
    if ($LASTEXITCODE -ne 0) { throw 'WEBFORMS_VERIFY_RUN_FAILED;preserve-output-for-diagnostics' }
    Write-Host 'Native documents generated separately. Compare against the retained baseline; completion is not a parity verdict.'
} else {
    Write-Host 'Verified inputs only; no scan or reports ran. Start with the printed config and a new output folder when ready.'
}
