[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$first = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-pdb-build/bin/Debug/net10.0/CompiledProjectless.VB.dll'
$second = Join-Path $TraceMapRoot 'src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll'
if (!(Test-Path -LiteralPath $first -PathType Leaf) -or !(Test-Path -LiteralPath $second -PathType Leaf)) {
    throw 'WEBFORMS_IL_PROBE_TEST_ASSEMBLIES_UNAVAILABLE'
}
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-il-probe-test-' + [guid]::NewGuid().ToString('N'))
try {
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'bin'))
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'scan'))
    $one = Join-Path $root 'bin/PublicOne.dll'
    $two = Join-Path $root 'bin/PublicTwo.dll'
    [IO.File]::Copy($first, $one)
    [IO.File]::Copy($second, $two)
    $oneHash = (Get-FileHash -LiteralPath $one -Algorithm SHA256).Hash.ToLowerInvariant()
    $twoHash = (Get-FileHash -LiteralPath $two -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifest = @{ ilBodyProvenance = @{
        effectiveLimits = @{ maxTextLength = 4096 }
        outcomes = @(
            @{ provenanceState = 'bound'; rawFileSha256 = $oneHash; gapKinds = @('IlReaderDisagreement') },
            @{ provenanceState = 'bound'; rawFileSha256 = $twoHash; gapKinds = @('IlTextLimitExceeded') })
    } }
    $receipt = @{ sourceCommitSha = ('a' * 40); assemblyInventory = @(
        @{ path = 'bin/PublicOne.dll'; sha256 = $oneHash; disposition = 'selected' },
        @{ path = 'bin/PublicTwo.dll'; sha256 = $twoHash; disposition = 'selected' }) }
    [IO.File]::WriteAllText((Join-Path $root 'scan/scan-manifest.json'), ($manifest | ConvertTo-Json -Depth 10))
    [IO.File]::WriteAllText((Join-Path $root 'publish-receipt.local.json'), ($receipt | ConvertTo-Json -Depth 10))
    $lines = @(& (Join-Path $TraceMapRoot 'scripts/wil.ps1') -OutputRoot $root -TraceMapRoot $TraceMapRoot)
    if ($lines -cnotcontains 'disputed.ilReaderProbeStatus=agreed' -or
        $lines -cnotcontains 'disputed.ilReaderProbeFirstDifference=none' -or
        $lines -cnotcontains 'ilProbeResult=disputed-input-not-reproduced') {
        throw 'WEBFORMS_IL_PROBE_TEST_RESULT_INVALID'
    }
    if (($lines -join "`n") -match 'PublicOne|PublicTwo|[a-f0-9]{64}') {
        throw 'WEBFORMS_IL_PROBE_TEST_PRIVACY_FAILED'
    }
    $project = Join-Path $TraceMapRoot 'scripts/diagnostics/TraceMap.IlReaderProbe/TraceMap.IlReaderProbe.csproj'
    $limited = @(& dotnet run --no-build --project $project -- $one 71)
    if ($LASTEXITCODE -ne 0 -or $limited -cnotcontains 'ilReaderProbeStatus=IlTextLimitExceeded') {
        throw 'WEBFORMS_IL_PROBE_TEST_TEXT_LIMIT_INVALID'
    }
    Write-Output 'webFormsIlProbeTest=pass'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
