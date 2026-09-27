[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-method-probe-test-' + [guid]::NewGuid().ToString('N'))
try {
    $scan = Join-Path $root 'scan'
    [void][IO.Directory]::CreateDirectory($scan)
    $facts = @(
        @{ factType='ManagedMethodDeclared'; factId='method-1'; targetSymbol='assembly:name:4:Test|type:namespace:4:Demo|names:8:Provider|arity:0|method:3:Run|signature:x'; properties=@{ rawFileSha256='sha-1'; metadataToken='0x06000001' } },
        @{ factType='ManagedIlBodyDeclared'; factId='body-1'; properties=@{ compiledFactId='method-1'; rawFileSha256='sha-1'; metadataToken='0x06000001' } },
        @{ factType='ManagedIlBodyDeclared'; factId='body-unlinked'; properties=@{ rawFileSha256='sha-1'; metadataToken='0x06000099' } },
        @{ factType='ManagedIlCallObserved'; factId='call-1'; properties=@{ ilBodyFactId='body-1'; targetIdentity='type:x|method:4:Fill|signature:y' } },
        @{ factType='ManagedMethodDeclared'; factId='method-2'; targetSymbol='assembly:name:4:Test|type:namespace:4:Demo|names:8:Provider|arity:0|method:5:Other|signature:x'; properties=@{ rawFileSha256='sha-1'; metadataToken='0x06000002' } }
    )
    [IO.File]::WriteAllLines((Join-Path $scan 'facts.ndjson'), [string[]]@($facts | ForEach-Object { $_ | ConvertTo-Json -Depth 10 -Compress }))
    $manifest = @{ ilBodyProvenance=@{ outcomes=@(
        @{ rawFileSha256='sha-1'; outcome='admitted'; gapKinds=@() },
        @{ outcome='gap'; gapKinds=@('InputUnavailable') }
    ) } }
    [IO.File]::WriteAllText((Join-Path $scan 'scan-manifest.json'), ($manifest | ConvertTo-Json -Depth 10 -Compress))
    $lines = @(& (Join-Path $PSScriptRoot '../wm.ps1') -OutputRoot $root -TypeName Provider -MethodName Run)
    foreach ($expected in @('methodAssemblyCount=1', 'methodCount=1', 'methodBodyCount=1', 'methodLinkedBodyCount=1',
            'methodCallCount=1', 'methodFillCallCount=1', 'methodIlOutcome.admitted=1')) {
        if ($expected -cnotin $lines) { throw "WEBFORMS_METHOD_PROBE_ASSERTION_FAILED:$expected" }
    }
    Write-Output 'Web Forms method probe passed'
} finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolved = [IO.Path]::GetFullPath($root)
    if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolved) -cmatch '^tracemap-method-probe-test-[0-9a-f]{32}$' -and
        (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
