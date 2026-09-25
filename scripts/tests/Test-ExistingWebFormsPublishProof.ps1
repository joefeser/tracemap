[CmdletBinding()]
param([string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'EXISTING_PUBLISH_TEST_POWERSHELL_7_REQUIRED' }

$TraceMapRoot = [IO.Path]::GetFullPath($TraceMapRoot)
$source = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-pdb-projectless'
$project = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-pdb-build/CompiledProjectless.VB.vbproj'
$script = Join-Path $TraceMapRoot 'scripts/Invoke-ExistingWebFormsPublishProof.ps1'
& dotnet build $project --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'EXISTING_PUBLISH_TEST_BUILD_FAILED' }
$assembly = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-pdb-build/bin/Debug/net10.0/CompiledProjectless.VB.dll'
if (!(Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'EXISTING_PUBLISH_TEST_ASSEMBLY_UNAVAILABLE' }
$temp = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-existing-publish-test-' + [guid]::NewGuid().ToString('N'))
try {
    $publish = Join-Path $temp 'publish'
    [void][IO.Directory]::CreateDirectory((Join-Path $publish 'bin'))
    [void][IO.Directory]::CreateDirectory((Join-Path $publish 'Pages'))
    [IO.File]::Copy($assembly, (Join-Path $publish 'bin/CompiledProjectless.VB.dll'))
    $map = Join-Path $publish 'Pages/Lookup.aspx.compiled'
    [IO.File]::WriteAllText($map,
        '<preserve virtualPath="/Pages/Lookup.aspx" assembly="CompiledProjectless.VB" type="PublicProof.LookupPage" />',
        [Text.UTF8Encoding]::new($false))

    $output = Join-Path $temp 'proof'
    $lines = @(& $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath 'Pages/Lookup.aspx' `
        -HandlerName 'Lookup_Init' -OutputRoot $output -TraceMapRoot $TraceMapRoot `
        -OperatorAttestsExactSourceCommit)
    if ($lines -notcontains 'existingPublishPreparation=valid' -or $lines -notcontains 'existingPublishScan=bound') {
        $probe = [IO.File]::ReadAllText((Join-Path $output 'probe/scan-manifest.json')) | ConvertFrom-Json -Depth 30
        $outcome = @($probe.compiledInputProvenance.outcomes | ForEach-Object {
            "$($_.outcome):$($_.provenanceState):$(@($_.gapKinds) -join ',')"
        }) -join ';'
        throw "EXISTING_PUBLISH_TEST_POSITIVE_FAILED:$($lines -join ',');outcomes=$outcome"
    }
    $receipt = [IO.File]::ReadAllText((Join-Path $output 'publish-receipt.local.json')) | ConvertFrom-Json -Depth 20
    if ($receipt.schemaVersion -ne 'webforms-publish-binding.v1' -or
        $receipt.compilerProvenance -ne 'unavailable-existing-output' -or
        @($receipt.pages).Count -ne 1 -or @($receipt.publishedFiles).Count -ne 2 -or
        @($receipt.sourceFiles).Count -ne 2) {
        throw 'EXISTING_PUBLISH_TEST_RECEIPT_INVALID'
    }
    $manifest = [IO.File]::ReadAllText((Join-Path $output 'scan/scan-manifest.json')) | ConvertFrom-Json -Depth 20
    if ($manifest.webFormsPublishProvenance.status -ne 'bound' -or
        $manifest.webFormsPublishProvenance.pageCount -ne 1) {
        throw 'EXISTING_PUBLISH_TEST_MANIFEST_INVALID'
    }
    if (!(Test-Path -LiteralPath (Join-Path $output 'handler-paths.json') -PathType Leaf)) {
        throw 'EXISTING_PUBLISH_TEST_PATH_REPORT_UNAVAILABLE'
    }
    if ($IsWindows) {
        $publicSite = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-publish-projectless'
        $publicPublish = Join-Path $temp 'public-publish'
        & (Join-Path $TraceMapRoot 'scripts/validation/Test-PublicWebFormsPublish.ps1') `
            -TraceMapRoot $TraceMapRoot -OutputRoot $publicPublish | Out-Null
        $publicProof = @(& $script -SourceSiteRoot $publicSite -PublishedRoot $publicPublish `
            -PagePath 'Pages/Lookup.aspx' -HandlerName 'Names_Init' `
            -OutputRoot (Join-Path $temp 'public-proof') -TraceMapRoot $TraceMapRoot `
            -OperatorAttestsExactSourceCommit)
        if ($publicProof -notcontains 'existingPublishScan=bound' -or
            @($publicProof | Where-Object { $_ -match '^sqlQueryPaths=[1-9][0-9]*$' }).Count -ne 1) {
            throw "EXISTING_PUBLISH_TEST_WINDOWS_CHAIN_FAILED:$($publicProof -join ',')"
        }
    }
    $captured = $null
    try {
        & $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath '../Lookup.aspx' `
            -OutputRoot (Join-Path $temp 'unsafe') -PrepareOnly *> $null
    } catch { $captured = $_.Exception.Message }
    if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_UNSAFE_PATH') {
        throw 'EXISTING_PUBLISH_TEST_UNSAFE_PATH_NOT_REJECTED'
    }
    [IO.File]::WriteAllText($map,
        '<preserve virtualPath="/Pages/Other.aspx" assembly="CompiledProjectless.VB" type="PublicProof.LookupPage" />',
        [Text.UTF8Encoding]::new($false))
    $captured = $null
    try {
        & $script -SourceSiteRoot $source -PublishedRoot $publish -PagePath 'Pages/Lookup.aspx' `
            -OutputRoot (Join-Path $temp 'mismatch') -PrepareOnly *> $null
    } catch { $captured = $_.Exception.Message }
    if ($captured -ne 'WEBFORMS_EXISTING_PUBLISH_PAGE_MAP_NOT_UNIQUE') {
        throw 'EXISTING_PUBLISH_TEST_MAP_MISMATCH_NOT_REJECTED'
    }
    Write-Output 'existingPublishPublicTests=passed'
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
