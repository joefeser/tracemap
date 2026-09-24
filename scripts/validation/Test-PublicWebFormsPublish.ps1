param(
    [string]$TraceMapRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)

$ErrorActionPreference = 'Stop'
$site = Join-Path $TraceMapRoot 'samples/messy-dotnet-workspace/vb-publish-projectless'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/aspnet_compiler.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'ASP.NET_FRAMEWORK_COMPILER_UNAVAILABLE'
}
if (-not (Test-Path -LiteralPath $site -PathType Container)) {
    throw 'PUBLIC_WEBFORMS_FIXTURE_UNAVAILABLE'
}

# This is deliberately a fresh public-only publish, with no -u (updatable)
# switch. A normal Web Site build does not persist the page assembly.
$output = Join-Path ([System.IO.Path]::GetTempPath()) ('tracemap-public-webforms-publish-' + [guid]::NewGuid().ToString('N'))
$inputs = @(Get-ChildItem -LiteralPath $site -Recurse -File |
    Where-Object { $_.Extension -in @('.vb', '.aspx', '.config') } |
    Sort-Object FullName)
$digestLines = @($inputs | ForEach-Object {
    $relative = $_.FullName.Substring($site.Length).TrimStart('\', '/').Replace('\', '/')
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
$generatorSha256 = (Get-FileHash -LiteralPath $compiler -Algorithm SHA256).Hash.ToLowerInvariant()

& $compiler -p $site -v / $output
if ($LASTEXITCODE -ne 0) {
    throw "PUBLIC_WEBFORMS_PUBLISH_FAILED:exit=$LASTEXITCODE"
}

$dlls = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.dll')
$pdbs = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.pdb')
$maps = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter '*.compiled')
$pageMaps = @($maps | Where-Object {
    try {
        [xml]$xml = Get-Content -LiteralPath $_.FullName -Raw
        $xml.preserve.virtualPath -match '(^|/)Pages/Lookup\.aspx$'
    } catch {
        $false
    }
})
if ($pageMaps.Count -ne 1) {
    throw "PUBLIC_WEBFORMS_PAGE_MAPPING_NOT_UNIQUE:count=$($pageMaps.Count)"
}
[xml]$pageMap = Get-Content -LiteralPath $pageMaps[0].FullName -Raw
$assemblyName = [string]$pageMap.preserve.assembly
if ([string]::IsNullOrWhiteSpace($assemblyName) -or
    -not (Test-Path -LiteralPath (Join-Path $output "bin/$assemblyName.dll") -PathType Leaf)) {
    throw 'PUBLIC_WEBFORMS_MAPPED_ASSEMBLY_UNAVAILABLE'
}
if ($pdbs.Count -ne 0) {
    throw "PUBLIC_WEBFORMS_EXPECTED_NO_PDB:count=$($pdbs.Count)"
}

Write-Output 'publicWebFormsPublishStatus=valid'
Write-Output "generatorSha256=$generatorSha256"
Write-Output "boundedInputSha256=$inputSha256"
Write-Output "inputFiles=$($inputs.Count)"
Write-Output "dllCount=$($dlls.Count)"
Write-Output "pdbCount=$($pdbs.Count)"
Write-Output "compiledMapCount=$($maps.Count)"
Write-Output "pageMapCount=$($pageMaps.Count)"
Write-Output "mappedAssemblyPresent=$([bool](Test-Path -LiteralPath (Join-Path $output "bin/$assemblyName.dll")))"
Write-Output 'claim=page-to-assembly-only;no-source-method-or-runtime-claim'
Write-Output "outputRoot=$output"
