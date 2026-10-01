param(
    [string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),
    [string]$OutputRoot,
    [switch]$Updatable,
    [switch]$CrossAssembly,
    [switch]$DeepChain
)

$ErrorActionPreference = 'Stop'
$TraceMapRoot = [System.IO.Path]::GetFullPath($TraceMapRoot)
if ($DeepChain) { $CrossAssembly = $true }
$fixture = if ($DeepChain) { 'vb-deep-projectless' }
    elseif ($CrossAssembly) { 'vb-publish-crossdll' }
    elseif ($Updatable) { 'vb-publish-mapless' } else { 'vb-publish-projectless' }
$site = [System.IO.Path]::GetFullPath((Join-Path $TraceMapRoot "samples/messy-dotnet-workspace/$fixture"))
$pageSourcePath = if ($DeepChain) { 'Lookup.aspx' } else { 'Pages/Lookup.aspx' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/aspnet_compiler.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'ASP.NET_FRAMEWORK_COMPILER_UNAVAILABLE'
}
if (-not (Test-Path -LiteralPath $site -PathType Container)) {
    throw 'PUBLIC_WEBFORMS_FIXTURE_UNAVAILABLE'
}
if ($CrossAssembly -and !$Updatable -and !$DeepChain) { throw 'PUBLIC_WEBFORMS_CROSS_ASSEMBLY_REQUIRES_UPDATABLE' }

# A fresh public-only publish. -Updatable reproduces a page without a
# .compiled map while retaining the code-behind in App_Web_*.dll.
$output = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path ([System.IO.Path]::GetTempPath()) ('tracemap-public-webforms-publish-' + [guid]::NewGuid().ToString('N'))
} else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}
if (Test-Path -LiteralPath $output) {
    throw 'PUBLIC_WEBFORMS_OUTPUT_NOT_FRESH'
}
$publishSite = $site
$externalInput = $null
$frameworkPdb = $null
$frameworkCompilerSha256 = $null
$frameworkBoundedInputSha256 = $null
if ($CrossAssembly) {
    $frameworkRoot = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-publish-crossdll-framework'
    $frameworkSource = Join-Path $frameworkRoot 'PublicSqlDataAccess.vb'
    $frameworkProject = Join-Path $frameworkRoot 'PublicProof.Framework.vbproj'
    $sdkVersion = (& dotnet --version).Trim()
    $sdkCompiler = Join-Path (Split-Path (Get-Command dotnet).Source -Parent) "sdk/$sdkVersion/Roslyn/bincore/vbc.dll"
    if (!(Test-Path -LiteralPath $frameworkSource -PathType Leaf) -or
        !(Test-Path -LiteralPath $frameworkProject -PathType Leaf) -or
        !(Test-Path -LiteralPath $sdkCompiler -PathType Leaf)) {
        throw 'PUBLIC_WEBFORMS_FRAMEWORK_COMPILER_INPUT_UNAVAILABLE'
    }
    $frameworkCompilerSha256 = (Get-FileHash -LiteralPath $sdkCompiler -Algorithm SHA256).Hash.ToLowerInvariant()
    # Every compiled provider source is part of the bounded build input, including
    # the legacy ref-array/control-flow fixture used by -DeepChain.
    $frameworkInputs = @($frameworkProject) + @(Get-ChildItem -LiteralPath $frameworkRoot -File -Filter '*.vb' |
        Sort-Object Name | ForEach-Object { $_.FullName })
    $frameworkLines = @($frameworkInputs | ForEach-Object {
        (Split-Path $_ -Leaf) + ':' + (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    })
    $frameworkBoundedInputSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes(($frameworkLines -join "`n") + "`n"))).ToLowerInvariant()
    $publishSite = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-public-crossdll-site-' + [guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($publishSite)
    Copy-Item -Path (Join-Path $site '*') -Destination $publishSite -Recurse
    $bin = Join-Path $publishSite 'bin'
    [void][IO.Directory]::CreateDirectory($bin)
    $externalInput = Join-Path $bin 'PublicProof.Framework.dll'
    $buildRoot = Join-Path ([IO.Path]::GetTempPath()) ('tracemap-public-crossdll-build-' + [guid]::NewGuid().ToString('N'))
    $baseOutput = (Join-Path $buildRoot 'bin') + [IO.Path]::DirectorySeparatorChar
    $baseIntermediate = (Join-Path $buildRoot 'obj') + [IO.Path]::DirectorySeparatorChar
    & dotnet build $frameworkProject -p:Configuration=Debug "-p:BaseOutputPath=$baseOutput" `
        "-p:BaseIntermediateOutputPath=$baseIntermediate" -v:q --ignore-failed-sources
    $builtDll = Join-Path $baseOutput 'Debug/net48/PublicProof.Framework.dll'
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $builtDll -PathType Leaf)) {
        throw 'PUBLIC_WEBFORMS_FRAMEWORK_BUILD_FAILED'
    }
    Copy-Item -LiteralPath $builtDll -Destination $externalInput
    $builtPdb = Join-Path $baseOutput 'Debug/net48/PublicProof.Framework.pdb'
    if (!(Test-Path -LiteralPath $builtPdb -PathType Leaf)) {
        throw 'PUBLIC_WEBFORMS_FRAMEWORK_PDB_UNAVAILABLE'
    }
    $frameworkPdb = $output + '.framework.pdb'
    Copy-Item -LiteralPath $builtPdb -Destination $frameworkPdb
}
$inputs = @(Get-ChildItem -LiteralPath $site -Recurse -File |
    Where-Object { $_.Extension -in @('.vb', '.aspx', '.config', '.asax', '.wsdl', '.discomap') } |
    Sort-Object FullName)
$digestLines = @($inputs | ForEach-Object {
    $relative = [System.IO.Path]::GetRelativePath($site, $_.FullName).Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$relative`:$hash"
})
$digestBytes = [System.Text.Encoding]::UTF8.GetBytes(($digestLines -join "`n") + "`n")
$hasher = [System.Security.Cryptography.SHA256]::Create()
try {
    $inputSha256 = ([BitConverter]::ToString($hasher.ComputeHash($digestBytes))).Replace('-', '').ToLowerInvariant()
} finally {
    $hasher.Dispose()
}
$publishInputSha256 = $null
if ($CrossAssembly) {
    $publishInputLines = @($digestLines) + @('external:bin/PublicProof.Framework.dll:' +
        (Get-FileHash -LiteralPath $externalInput -Algorithm SHA256).Hash.ToLowerInvariant())
    $publishInputSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes(($publishInputLines -join "`n") + "`n"))).ToLowerInvariant()
}
$generatorSha256 = (Get-FileHash -LiteralPath $compiler -Algorithm SHA256).Hash.ToLowerInvariant()
$receiptGeneratorSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$sourceCommitSha = (& git -C $TraceMapRoot rev-parse HEAD).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $sourceCommitSha -notmatch '^[0-9a-f]{40}$') {
    throw 'PUBLIC_WEBFORMS_SOURCE_COMMIT_UNAVAILABLE'
}

$compilerArguments = @('-p', $publishSite, '-v', $(if ($Updatable) { '/UBid' } else { '/' }))
if ($Updatable) { $compilerArguments += '-u' }
$compilerArguments += $output
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "PUBLIC_WEBFORMS_PUBLISH_FAILED:exit=$LASTEXITCODE"
}

$dlls = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.dll')
$pdbs = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.pdb')
$maps = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.compiled')
$pageMaps = @($maps | Where-Object {
    try {
        [xml]$xml = Get-Content -LiteralPath $_.FullName -Raw
        ([string]$xml.preserve.virtualPath).EndsWith('/' + $pageSourcePath, [StringComparison]::OrdinalIgnoreCase)
    } catch {
        $false
    }
})
if ($pageMaps.Count -ne $(if ($Updatable) { 0 } else { 1 })) {
    throw "PUBLIC_WEBFORMS_PAGE_MAPPING_NOT_UNIQUE:count=$($pageMaps.Count)"
}
$assemblyName = ''
if (!$Updatable) {
    [xml]$pageMap = Get-Content -LiteralPath $pageMaps[0].FullName -Raw
    $assemblyName = [string]$pageMap.preserve.assembly
    if ([string]::IsNullOrWhiteSpace($assemblyName) -or
        -not (Test-Path -LiteralPath (Join-Path $output "bin/$assemblyName.dll") -PathType Leaf)) {
        throw 'PUBLIC_WEBFORMS_MAPPED_ASSEMBLY_UNAVAILABLE'
    }
} elseif (@($dlls | Where-Object { $_.Name -like 'App_Web_*.dll' }).Count -lt 1) {
    throw 'PUBLIC_WEBFORMS_MAPLESS_WEB_ASSEMBLY_UNAVAILABLE'
}
if ($pdbs.Count -ne 0) {
    throw "PUBLIC_WEBFORMS_EXPECTED_NO_PDB:count=$($pdbs.Count)"
}

# Local-only build provenance. Paths are relative to the explicit source and
# publish roots; source hashes must not be copied into a shareable artifact.
$published = @($dlls + $maps | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = [System.IO.Path]::GetRelativePath($output, $_.FullName).Replace('\', '/')
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        kind = if ($_.Extension -eq '.dll') { 'assembly' } else { 'compiled-map' }
    }
})
$mapLines = [string[]]@($published | Where-Object { $_.kind -eq 'compiled-map' } |
    ForEach-Object { "$($_.path):$($_.sha256)" })
[Array]::Sort($mapLines, [StringComparer]::Ordinal)
$mapInventorySha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes(($mapLines -join "`n") + "`n"))).ToLowerInvariant()
$receipt = [ordered]@{
    schemaVersion = 'webforms-publish-binding.v1'
    visibility = 'local-only'
    receiptGeneratorSha256 = $receiptGeneratorSha256
    compilerSha256 = $generatorSha256
    frameworkCompilerSha256 = $frameworkCompilerSha256
    frameworkBoundedInputSha256 = $frameworkBoundedInputSha256
    frameworkPdbSha256 = if ($CrossAssembly) {
        (Get-FileHash -LiteralPath $frameworkPdb -Algorithm SHA256).Hash.ToLowerInvariant()
    } else { $null }
    sourceCommitSha = $sourceCommitSha
    boundedInputSha256 = $inputSha256
    publishInputSha256 = $publishInputSha256
    sourceFiles = @($inputs | ForEach-Object {
        [ordered]@{
            path = [System.IO.Path]::GetRelativePath($site, $_.FullName).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    publishedFiles = $published
    publishedMapCount = $maps.Count
    mapInventorySha256 = $mapInventorySha256
    pages = @([ordered]@{
        virtualPath = if ($Updatable) { '/UBid/' + $pageSourcePath } else { [string]$pageMap.preserve.virtualPath }
        sourcePath = $pageSourcePath
        assembly = if ($Updatable) { $null } else { $assemblyName }
        generatedType = if ($Updatable) { $null } else { [string]$pageMap.preserve.type }
        mapPath = if ($Updatable) { $null } else { [System.IO.Path]::GetRelativePath($output, $pageMaps[0].FullName).Replace('\', '/') }
        bindingKind = if ($Updatable) { 'mapless-source-type-candidate' } else { $null }
    })
}
$receiptPath = Join-Path $output 'publish-receipt.local.json'
[System.IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 8) + "`n", [System.Text.UTF8Encoding]::new($false))

Write-Output 'publicWebFormsPublishStatus=valid'
Write-Output "generatorSha256=$generatorSha256"
Write-Output "boundedInputSha256=$inputSha256"
Write-Output "inputFiles=$($inputs.Count)"
Write-Output "dllCount=$($dlls.Count)"
Write-Output "pdbCount=$($pdbs.Count)"
Write-Output "compiledMapCount=$($maps.Count)"
Write-Output "pageMapCount=$($pageMaps.Count)"
Write-Output "mappedAssemblyPresent=$([bool](!$Updatable -and (Test-Path -LiteralPath (Join-Path $output "bin/$assemblyName.dll"))))"
Write-Output "receiptGeneratorSha256=$receiptGeneratorSha256"
Write-Output "sourceCommitSha=$sourceCommitSha"
Write-Output 'receiptVisibility=local-only'
Write-Output 'claim=page-to-assembly-only;no-source-method-or-runtime-claim'
Write-Output "outputRoot=$output"
