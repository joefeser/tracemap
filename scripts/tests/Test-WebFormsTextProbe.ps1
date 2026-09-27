[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$cli = Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
if (!(Test-Path -LiteralPath $cli -PathType Leaf)) { throw 'WEBFORMS_TEXT_PROBE_TEST_CLI_UNAVAILABLE' }
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-text-probe-test-' + [guid]::NewGuid().ToString('N'))
try {
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'bin'))
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'probe'))
    $target = Join-Path $root 'bin/PublicFixture.dll'
    [IO.File]::Copy($cli, $target)
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest = @{ compiledInputProvenance = @{
        effectiveLimits = @{ maxTextLength = 4096 }
        outcomes = @(@{ outcome = 'limit-exhausted'; rawFileSha256 = $hash;
            gapKinds = @('ManagedInputTextLimitExceeded') })
    } }
    $receipt = @{ assemblyInventory = @(@{ path = 'bin/PublicFixture.dll'; sha256 = $hash;
        disposition = 'artifact-context-no-source-commit' }) }
    [IO.File]::WriteAllText((Join-Path $root 'probe/scan-manifest.json'), ($manifest | ConvertTo-Json -Depth 10))
    [IO.File]::WriteAllText((Join-Path $root 'publish-receipt.local.json'), ($receipt | ConvertTo-Json -Depth 10))
    $lines = @(& (Join-Path $TraceMapRoot 'scripts/wtext.ps1') -OutputRoot $root -TraceMapRoot $TraceMapRoot)
    if ($lines -cnotcontains 'textProbeInputs=1' -or
        $lines -cnotcontains 'textProbeAdmittedCeiling=8192') {
        throw "WEBFORMS_TEXT_PROBE_TEST_FAILED:$($lines -join ',')"
    }
    Write-Output 'webFormsTextProbeTest=pass'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
