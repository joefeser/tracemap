#Requires -Version 7.0
[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$wrapper = Join-Path $TraceMapRoot 'scripts/validation/Test-DeepProjectlessCorpus.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-deep-guard-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$before = $env:TRACEMAP_DEEP_CORPUS_ROOT
$env:TRACEMAP_DEEP_CORPUS_ROOT = 'synthetic-preexisting-value'
$global:deepGuardCalls = 0
function global:dotnet {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $global:deepGuardCalls++
    [IO.File]::WriteAllText((Join-Path $env:TRACEMAP_DEEP_CORPUS_ROOT 'failure.local.log'), 'synthetic failure')
    $global:LASTEXITCODE = 1
}
try {
    try { & $wrapper -TraceMapRoot $TraceMapRoot -OutputRoot $root; throw 'Existing output accepted' }
    catch { if ($_.Exception.Message -cne 'DEEP_CORPUS_OUTPUT_NOT_FRESH') { throw } }
    if ($global:deepGuardCalls -ne 0) { throw 'Existing output launched tests' }
    if (!$IsWindows) {
        $refused = Join-Path $root 'windows-refused'
        try { & $wrapper -TraceMapRoot $TraceMapRoot -OutputRoot $refused -RequireWindowsPublish; throw 'Non-Windows accepted' }
        catch { if ($_.Exception.Message -cne 'DEEP_CORPUS_WINDOWS_REQUIRED') { throw } }
        if (Test-Path -LiteralPath $refused) { throw 'Windows refusal wrote output' }
    }
    $failed = Join-Path $root 'failed'
    try { & $wrapper -TraceMapRoot $TraceMapRoot -OutputRoot $failed; throw 'Failed tests accepted' }
    catch { if ($_.Exception.Message -cne 'DEEP_CORPUS_TEST_FAILED;outputs-preserved') { throw } }
    if (!(Test-Path -LiteralPath (Join-Path $failed 'failure.local.log')) -or
        (Test-Path -LiteralPath (Join-Path $failed 'validation.local.json')) -or $global:deepGuardCalls -ne 1) {
        throw 'Failure preservation/status contract broken'
    }
    if ($env:TRACEMAP_DEEP_CORPUS_ROOT -cne 'synthetic-preexisting-value') { throw 'Environment not restored' }
    Write-Output 'deepCorpusGuardTests=passed;orchestration-only;not-native-acceptance'
} finally {
    $env:TRACEMAP_DEEP_CORPUS_ROOT = $before
    Remove-Item Function:\dotnet
    [IO.Directory]::Delete($root, $true)
}
